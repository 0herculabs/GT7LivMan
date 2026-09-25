using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Svg;
using WpfGeometry = System.Windows.Media.Geometry;

namespace GT7LivMan.Typography.Wpf.Tests;

/// <summary>
/// Design-time only: turns a base plate SVG that uses a few features the strict
/// <see cref="SvgReader"/> rejects into flat (Path, Fill) shapes a template can hold. GT7 accepts
/// none of these features either, so each one is resolved into plain filled shapes:
/// <list type="bullet">
/// <item><c>&lt;text&gt;</c> is outlined with the font it names (weight, italic, letter-spacing).</item>
/// <item>A <c>clip-path</c> (a rect in <c>&lt;defs&gt;</c>) is intersected into the shapes it clips.</item>
/// <item><c>opacity</c> is blended into a solid color against the shape painted underneath it.</item>
/// </list>
/// <c>&lt;use&gt;</c> is expanded into a copy of what it references. A <c>filter</c> (drop
/// shadow) is dropped, like everything else in <c>&lt;defs&gt;</c>.
/// Everything else goes straight through <see cref="SvgReader"/>. Needs WPF (outlining, geometry
/// intersection), which is why it lives here rather than in Core.
/// </summary>
internal static class DesignSvgFlattener
{
    public static List<(PathData Path, RgbColor Fill)> Flatten(string svgContent)
    {
        XElement root = XDocument.Parse(svgContent).Root!;
        XNamespace ns = root.Name.Namespace;
        ExpandUses(root, ns);
        Dictionary<string, WpfGeometry> clips = root.Descendants(ns + "clipPath")
            .ToDictionary(c => (string)c.Attribute("id")!, ClipGeometry);

        var state = new State(ns, clips);
        Walk(root, clip: null, opacity: 1, inheritedFill: null, state);
        return state.Sink;
    }

    private sealed record State(XNamespace Ns, Dictionary<string, WpfGeometry> Clips)
    {
        public List<(PathData Path, RgbColor Fill)> Sink { get; } = [];

        /// <summary>Shapes built here in WPF, kept so a translucent shape can find what it's painted over.</summary>
        public List<(WpfGeometry Geometry, RgbColor Fill)> Painted { get; } = [];
    }

    private static void Walk(XElement parent, WpfGeometry? clip, double opacity, string? inheritedFill, State state)
    {
        foreach (XElement el in parent.Elements())
        {
            string name = el.Name.LocalName;
            if (name == "defs")
            {
                continue;
            }

            string? fill = (string?)el.Attribute("fill") ?? inheritedFill;
            double elOpacity = opacity * ReadNum(el, "opacity", 1);
            WpfGeometry? elClip = ResolveClip(el, clip, state);

            if (name == "text")
            {
                OutlineText(el, fill, state);
                continue;
            }

            bool needsWpf = elClip is not null || elOpacity < 1;
            bool hasText = el.Descendants(state.Ns + "text").Any();

            if (name == "g" && (needsWpf || hasText))
            {
                if (el.Attribute("transform") is not null)
                {
                    throw new NotSupportedException("A transformed <g> with clip-path/opacity/text isn't supported — extend DesignSvgFlattener if a template needs it.");
                }

                Walk(el, elClip, elOpacity, fill, state);
                continue;
            }

            if (!needsWpf)
            {
                // Wrapped so the element keeps any fill inherited from an ancestor this walk unpacked.
                var wrapper = new XElement(state.Ns + "svg",
                    new XElement(state.Ns + "g", fill is null ? null : new XAttribute("fill", fill), new XElement(el)));
                state.Sink.AddRange(SvgReader.ReadStaticShapes(wrapper.ToString()));

                // A plain filled rect (the plate's own face) is also what a translucent shape
                // further up may be painted over, so it has to be findable for blending.
                if (name == "rect" && fill is not (null or "none"))
                {
                    state.Painted.Add((RectGeometry(el), RgbColor.Parse(fill)));
                }

                continue;
            }

            WpfGeometry geometry = name switch
            {
                "rect" => RectGeometry(el),
                "polygon" => PolygonGeometry(el),
                // WPF's path mini-language reads SVG path data as-is; "F1" is SVG's default nonzero fill.
                "path" => WpfGeometry.Parse("F1 " + (string)el.Attribute("d")!),
                _ => throw new NotSupportedException($"<{name}> under clip-path/opacity isn't supported — extend DesignSvgFlattener if a template needs it."),
            };
            if (elClip is not null)
            {
                geometry = WpfGeometry.Combine(geometry, elClip, GeometryCombineMode.Intersect, null);
            }

            RgbColor color = RgbColor.Parse(fill ?? throw new FormatException($"<{name}> has no fill."));
            if (elOpacity < 1)
            {
                color = Blend(color, ColorUnderneath(geometry, state), elOpacity);
            }

            state.Painted.Add((geometry, color));
            state.Sink.Add((WpfGeometryConverter.Convert(geometry), color));
        }
    }

    /// <summary>
    /// Replaces each <c>&lt;use href="#id"&gt;</c> with a copy of what it references, wrapped in a
    /// <c>&lt;g&gt;</c> carrying the use's transform — France's 12 EU stars are one star used 12 times.
    /// </summary>
    private static void ExpandUses(XElement root, XNamespace ns)
    {
        XNamespace xlink = "http://www.w3.org/1999/xlink";
        var byId = root.Descendants().Where(e => e.Attribute("id") is not null).ToDictionary(e => (string)e.Attribute("id")!);

        foreach (XElement use in root.Descendants(ns + "use").ToList())
        {
            string href = ((string?)use.Attribute("href") ?? (string?)use.Attribute(xlink + "href")
                ?? throw new FormatException("<use> without href.")).TrimStart('#');
            var copy = new XElement(byId[href]);
            copy.Attribute("id")?.Remove();

            // x/y on a <use> are an extra translation applied after its transform.
            string transform = string.Join(' ', new[]
            {
                (string?)use.Attribute("transform"),
                use.Attribute("x") is not null || use.Attribute("y") is not null
                    ? $"translate({ReadNum(use, "x", 0).ToString(CultureInfo.InvariantCulture)},{ReadNum(use, "y", 0).ToString(CultureInfo.InvariantCulture)})"
                    : null,
            }.Where(t => t is not null));

            var group = new XElement(ns + "g", transform.Length > 0 ? new XAttribute("transform", transform) : null, copy);
            foreach (string inherited in new[] { "fill", "opacity" })
            {
                if (use.Attribute(inherited) is { } attr)
                {
                    group.SetAttributeValue(inherited, attr.Value);
                }
            }

            use.ReplaceWith(group);
        }
    }

    private static WpfGeometry? ResolveClip(XElement el, WpfGeometry? inherited, State state)
    {
        string? reference = (string?)el.Attribute("clip-path");
        if (reference is null)
        {
            return inherited;
        }

        string id = Regex.Match(reference, @"url\(\s*#([^)\s]+)\s*\)").Groups[1].Value;
        WpfGeometry own = state.Clips[id];
        return inherited is null ? own : WpfGeometry.Combine(inherited, own, GeometryCombineMode.Intersect, null);
    }

    private static WpfGeometry ClipGeometry(XElement clipPath)
    {
        XElement rect = clipPath.Elements().Single(e => e.Name.LocalName == "rect");
        return RectGeometry(rect);
    }

    private static RectangleGeometry RectGeometry(XElement el)
    {
        double rx = ReadNum(el, "rx", 0);
        return new RectangleGeometry(
            new Rect(ReadNum(el, "x", 0), ReadNum(el, "y", 0), ReadNum(el, "width", 0), ReadNum(el, "height", 0)),
            rx,
            ReadNum(el, "ry", rx));
    }

    private static PathGeometry PolygonGeometry(XElement el)
    {
        double[] n = ((string)el.Attribute("points")!)
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => double.Parse(t, CultureInfo.InvariantCulture))
            .ToArray();
        var points = Enumerable.Range(0, n.Length / 2).Select(i => new Point(n[2 * i], n[(2 * i) + 1])).ToList();
        var figure = new PathFigure(points[0], [new PolyLineSegment(points.Skip(1), isStroked: true)], closed: true);
        return new PathGeometry([figure]);
    }

    /// <summary>The fill of the topmost already-painted shape under <paramref name="geometry"/>'s center.</summary>
    private static RgbColor ColorUnderneath(WpfGeometry geometry, State state)
    {
        Rect bounds = geometry.Bounds;
        var center = new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        for (int i = state.Painted.Count - 1; i >= 0; i--)
        {
            if (state.Painted[i].Geometry.FillContains(center))
            {
                return state.Painted[i].Fill;
            }
        }

        throw new NotSupportedException("A translucent shape isn't painted over another clipped/translucent shape — can't tell what color to blend it with.");
    }

    private static RgbColor Blend(RgbColor top, RgbColor under, double alpha) => new(
        (byte)Math.Round((alpha * top.R) + ((1 - alpha) * under.R)),
        (byte)Math.Round((alpha * top.G) + ((1 - alpha) * under.G)),
        (byte)Math.Round((alpha * top.B) + ((1 - alpha) * under.B)));

    private static void OutlineText(XElement el, string? fill, State state)
    {
        string weightAttr = (string?)el.Attribute("font-weight") ?? "normal";
        FontWeight weight = weightAttr is "bold" || (int.TryParse(weightAttr, out int w) && w >= 600) ? FontWeights.Bold : FontWeights.Normal;
        FontStyle style = (string?)el.Attribute("font-style") is "italic" or "oblique" ? FontStyles.Italic : FontStyles.Normal;
        var typeface = new Typeface(
            new FontFamily((string?)el.Attribute("font-family") ?? "Arial"), style, weight, FontStretches.Normal);
        double size = ReadNum(el, "font-size", 16);
        double letterSpacing = ReadNum(el, "letter-spacing", 0);

        FormattedText Format(string s) =>
            new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, Brushes.Black, pixelsPerDip: 1);

        if ((string?)el.Attribute("stroke") is not (null or "none"))
        {
            throw new NotSupportedException("Stroked <text> isn't supported — a widened outline costs several KB of GT7's 15 KB budget.");
        }

        // With letter-spacing each character is laid out on its own (FormattedText has no such
        // setting), which gives up kerning between them — invisible at the few units this adds.
        string content = el.Value;
        var runs = letterSpacing == 0 ? [content] : content.Select(c => c.ToString()).ToList();
        var formatted = runs.Select(Format).ToList();
        double width = formatted.Sum(f => f.WidthIncludingTrailingWhitespace + letterSpacing);

        double x = ReadNum(el, "x", 0);
        double pen = (string?)el.Attribute("text-anchor") switch
        {
            "middle" => x - (width / 2),
            "end" => x - width,
            _ => x,
        };

        double baseline = ReadNum(el, "y", 0);
        var group = new GeometryGroup { FillRule = System.Windows.Media.FillRule.Nonzero };
        foreach (FormattedText run in formatted)
        {
            group.Children.Add(run.BuildGeometry(new Point(pen, baseline - run.Baseline)));
            pen += run.WidthIncludingTrailingWhitespace + letterSpacing;
        }

        state.Sink.Add((WpfGeometryConverter.Convert(group), RgbColor.Parse(fill ?? throw new FormatException("<text> has no fill."))));
    }

    /// <summary>Curved or many-segment art (outlined text, a logo, a waving flag) — not a plain rect or polygon, which simplification can only damage.</summary>
    public static bool IsDetailed(PathData path) =>
        path.SubPaths.Any(sub => sub.Segments.Any(seg => seg is Seg.Cubic or Seg.Quad))
        || path.SubPaths.Sum(sub => sub.Segments.Count) > 16;

    private static double ReadNum(XElement el, string attr, double defaultValue) =>
        el.Attribute(attr) is { } a ? double.Parse(a.Value, CultureInfo.InvariantCulture) : defaultValue;
}
