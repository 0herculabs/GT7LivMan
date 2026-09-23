using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// Imports an arbitrary SVG a user uploaded (SVG Generator) — unlike <see cref="SvgReader"/>,
/// which is deliberately strict for our own curated plate art and throws on anything unexpected,
/// this one is best-effort: whatever it can't represent as a flat-fill shape is skipped (or
/// approximated) and reported in <see cref="Result.Warnings"/> instead of aborting the whole
/// import. A real exported SVG (Illustrator/Inkscape/Figma) routinely uses things GT7 can't render
/// at all (text, gradients, embedded bitmaps, opacity) — the goal here is "get as much of it onto
/// the decal as possible", not "reject anything imperfect".
/// </summary>
public static class GenericSvgImporter
{
    /// <param name="TransparentRegions">
    /// Geometry of the shapes that were left out of <paramref name="Elements"/> for being fully
    /// transparent, in paint order. Nothing to draw, but a raster trace encodes a letter's counter
    /// (the hole in an "e") as exactly this — a transparent region sitting on top of the solid blob
    /// the glyph was traced as — so that pipeline feeds these to <see cref="HoleCutter"/> rather
    /// than discarding them. A hand-authored SVG can simply ignore them.
    /// </param>
    public sealed record Result(
        SizeMm Size,
        IReadOnlyList<Element> Elements,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<PathData> TransparentRegions);

    private static readonly Dictionary<string, RgbColor> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = new RgbColor(0, 0, 0),
        ["white"] = new RgbColor(255, 255, 255),
        ["red"] = new RgbColor(255, 0, 0),
        ["green"] = new RgbColor(0, 128, 0),
        ["blue"] = new RgbColor(0, 0, 255),
        ["yellow"] = new RgbColor(255, 255, 0),
        ["orange"] = new RgbColor(255, 165, 0),
        ["purple"] = new RgbColor(128, 0, 128),
        ["pink"] = new RgbColor(255, 192, 203),
        ["brown"] = new RgbColor(165, 42, 42),
        ["gray"] = new RgbColor(128, 128, 128),
        ["grey"] = new RgbColor(128, 128, 128),
        ["silver"] = new RgbColor(192, 192, 192),
        ["gold"] = new RgbColor(255, 215, 0),
        ["navy"] = new RgbColor(0, 0, 128),
        ["teal"] = new RgbColor(0, 128, 128),
        ["cyan"] = new RgbColor(0, 255, 255),
        ["magenta"] = new RgbColor(255, 0, 255),
        ["lime"] = new RgbColor(0, 255, 0),
        ["maroon"] = new RgbColor(128, 0, 0),
        ["olive"] = new RgbColor(128, 128, 0),
        ["indigo"] = new RgbColor(75, 0, 130),
        ["violet"] = new RgbColor(238, 130, 238),
        ["beige"] = new RgbColor(245, 245, 220),
        ["turquoise"] = new RgbColor(64, 224, 208),
        ["darkgray"] = new RgbColor(169, 169, 169),
        ["darkgrey"] = new RgbColor(169, 169, 169),
        ["lightgray"] = new RgbColor(211, 211, 211),
        ["lightgrey"] = new RgbColor(211, 211, 211),
    };

    private static readonly Regex RgbFunctionPattern = new(
        @"^rgba?\(\s*(?<r>\d+)\s*,\s*(?<g>\d+)\s*,\s*(?<b>\d+)\s*(,\s*[\d.]+\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Result Import(string svgContent)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(svgContent);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException)
        {
            throw new FormatException($"El archivo no es un SVG válido: {ex.Message}", ex);
        }

        XElement root = doc.Root ?? throw new FormatException("El SVG no tiene elemento raíz.");

        var warnings = new WarningCollector();
        (SizeMm size, Affine2 rootOffset) = ReadSize(root, warnings);
        Dictionary<string, RgbColor> gradientColors = CollectGradientColors(root, warnings);

        var elements = new List<Element>();
        var transparentRegions = new List<Element>();
        Walk(root, rootOffset, null, elements, transparentRegions, warnings, gradientColors);

        return new Result(
            size,
            elements,
            warnings.Build(),
            [.. transparentRegions.OfType<Element.Shape>().Select(s => s.Path)]);
    }

    private static (SizeMm Size, Affine2 Offset) ReadSize(XElement root, WarningCollector warnings)
    {
        string? viewBox = root.Attribute("viewBox")?.Value;
        if (!string.IsNullOrWhiteSpace(viewBox))
        {
            string[] parts = viewBox.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4
                && TryParseLength(parts[0], out double minX)
                && TryParseLength(parts[1], out double minY)
                && TryParseLength(parts[2], out double vbWidth)
                && TryParseLength(parts[3], out double vbHeight)
                && vbWidth > 0 && vbHeight > 0)
            {
                return (new SizeMm(vbWidth, vbHeight), Affine2.Translate(-minX, -minY));
            }
        }

        string? widthAttr = root.Attribute("width")?.Value;
        string? heightAttr = root.Attribute("height")?.Value;
        if (widthAttr is not null && heightAttr is not null
            && TryParseLength(widthAttr, out double width) && TryParseLength(heightAttr, out double height)
            && width > 0 && height > 0)
        {
            return (new SizeMm(width, height), Affine2.Identity);
        }

        warnings.Add("El SVG no declara viewBox ni width/height válidos; se ha usado un lienzo de 200x200 por defecto.");
        return (new SizeMm(200, 200), Affine2.Identity);
    }

    /// <summary>Strips a trailing CSS unit ("px", "mm", "pt"...) before parsing — SVG width/height/viewBox numbers are otherwise unitless, but real-world files often add one anyway.</summary>
    private static bool TryParseLength(string raw, out double value)
    {
        ReadOnlySpan<char> s = raw.AsSpan().Trim();
        int end = s.Length;
        while (end > 0 && !char.IsAsciiDigit(s[end - 1]) && s[end - 1] != '.')
        {
            end--;
        }

        return double.TryParse(s[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static Dictionary<string, RgbColor> CollectGradientColors(XElement root, WarningCollector warnings)
    {
        var result = new Dictionary<string, RgbColor>();

        foreach (XElement gradient in root.Descendants().Where(e => e.Name.LocalName is "linearGradient" or "radialGradient"))
        {
            string? id = gradient.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            List<RgbColor> stopColors = [];
            foreach (XElement stop in gradient.Elements().Where(e => e.Name.LocalName == "stop"))
            {
                string? colorText = stop.Attribute("stop-color")?.Value ?? ReadStyleProperty(stop.Attribute("style")?.Value, "stop-color");
                if (colorText is not null && TryParseColor(colorText, out RgbColor stopColor))
                {
                    stopColors.Add(stopColor);
                }
            }

            if (stopColors.Count > 0)
            {
                int r = (int)Math.Round(stopColors.Average(c => c.R));
                int g = (int)Math.Round(stopColors.Average(c => c.G));
                int b = (int)Math.Round(stopColors.Average(c => c.B));
                result[id] = new RgbColor((byte)r, (byte)g, (byte)b);
                warnings.AddGradientFlattened();
            }
        }

        return result;
    }

    private static void Walk(
        XElement parent,
        Affine2 ambient,
        RgbColor? inheritedFill,
        List<Element> sink,
        List<Element> transparentSink,
        WarningCollector warnings,
        Dictionary<string, RgbColor> gradientColors)
    {
        foreach (XElement el in parent.Elements())
        {
            switch (el.Name.LocalName)
            {
                // Definitions only — never visible on their own, and their gradients were already
                // collected up front. Recursing into these as if they were siblings would draw
                // shapes that aren't actually part of the picture.
                case "defs" or "linearGradient" or "radialGradient" or "clipPath" or "mask" or "symbol" or "style" or "title" or "desc" or "metadata":
                    continue;

                case "text" or "tspan":
                    warnings.AddSkippedText();
                    continue;

                case "image":
                    warnings.AddSkippedImage();
                    continue;

                case "use":
                    warnings.AddSkippedUse();
                    continue;
            }

            // A fully transparent shape draws nothing, so it never joins the visible elements —
            // importing it as opaque (what ignoring opacity would do) would paint a solid block
            // over the artwork. Its geometry is still kept: in a traced raster that shape is either
            // the transparent background or the hole in a letter, and the latter has to be cut out
            // of the glyph beneath it (see Result.TransparentRegions).
            bool isTransparent = IsFullyTransparent(el);
            List<Element> target = isTransparent ? transparentSink : sink;

            Affine2 own = SvgTransformParser.Parse(el.Attribute("transform")?.Value);
            Affine2 effective = ambient * own;
            RgbColor? fill = ResolveFill(el, gradientColors, warnings) ?? inheritedFill;
            if (!isTransparent)
            {
                // Only partial opacity is "ignored" and worth warning about; a fully transparent
                // shape is honored exactly — it just isn't drawn.
                WarnAboutOpacityIfAny(el, warnings);
            }

            switch (el.Name.LocalName)
            {
                case "g":
                    Walk(el, effective, fill, target, transparentSink, warnings, gradientColors);
                    break;

                case "path":
                    ImportPath(el, effective, fill, target, warnings);
                    break;

                case "rect":
                    ImportRect(el, effective, fill, target, warnings);
                    break;

                case "circle":
                    if (fill is { } circleFill)
                    {
                        double cx = ReadNum(el, "cx", 0);
                        double cy = ReadNum(el, "cy", 0);
                        double r = ReadNum(el, "r", 0);
                        if (r > 0)
                        {
                            target.Add(ToShape(Shapes.Circle(cx, cy, r), effective, circleFill));
                        }
                    }
                    else
                    {
                        warnings.AddSkippedUnfilled("circle");
                    }

                    break;

                case "ellipse":
                    if (fill is { } ellipseFill)
                    {
                        double cx = ReadNum(el, "cx", 0);
                        double cy = ReadNum(el, "cy", 0);
                        double rx = ReadNum(el, "rx", 0);
                        double ry = ReadNum(el, "ry", 0);
                        if (rx > 0 && ry > 0)
                        {
                            target.Add(ToShape(Shapes.Ellipse(cx, cy, rx, ry), effective, ellipseFill));
                        }
                    }
                    else
                    {
                        warnings.AddSkippedUnfilled("ellipse");
                    }

                    break;

                case "polygon" or "polyline":
                    ImportPolyLike(el, effective, fill, target, warnings);
                    break;

                case "line":
                    ImportLine(el, effective, target, warnings);
                    break;

                default:
                    warnings.AddSkippedUnsupportedElement(el.Name.LocalName);
                    break;
            }
        }
    }

    private static void ImportPath(XElement el, Affine2 effective, RgbColor? fill, List<Element> sink, WarningCollector warnings)
    {
        string? d = el.Attribute("d")?.Value;
        if (string.IsNullOrEmpty(d))
        {
            return;
        }

        PathData local;
        try
        {
            local = SvgPathParser.Parse(d);
        }
        catch (FormatException)
        {
            warnings.AddUnparseablePath();
            return;
        }

        double strokeWidth = ReadNum(el, "stroke-width", 0);
        RgbColor? stroke = ResolveStroke(el);

        if (fill is null && stroke is { } strokeOnlyColor && strokeWidth > 0)
        {
            foreach (SubPath sub in local.SubPaths)
            {
                sink.Add(ToShape(Shapes.StrokeToFill(sub, strokeWidth), effective, strokeOnlyColor));
            }

            return;
        }

        if (fill is not { } pathFill)
        {
            warnings.AddSkippedUnfilled("path");
            return;
        }

        if (el.Attribute("fill-rule")?.Value == "evenodd")
        {
            local = local with { Rule = FillRule.EvenOdd };
        }

        sink.Add(ToShape(local, effective, pathFill));
    }

    private static void ImportRect(XElement el, Affine2 effective, RgbColor? fill, List<Element> sink, WarningCollector warnings)
    {
        double x = ReadNum(el, "x", 0);
        double y = ReadNum(el, "y", 0);
        double w = ReadNum(el, "width", 0);
        double h = ReadNum(el, "height", 0);
        double rx = ReadNum(el, "rx", 0);
        double strokeWidth = ReadNum(el, "stroke-width", 0);
        RgbColor? stroke = ResolveStroke(el);

        if (w <= 0 || h <= 0)
        {
            return;
        }

        if (stroke is { } strokeColor && strokeWidth > 0)
        {
            if (fill is { } fillColor)
            {
                double half = strokeWidth / 2;
                sink.Add(ToShape(Shapes.RoundedRect(x - half, y - half, w + strokeWidth, h + strokeWidth, rx + half), effective, strokeColor));
                sink.Add(ToShape(Shapes.RoundedRect(x + half, y + half, w - strokeWidth, h - strokeWidth, Math.Max(0, rx - half)), effective, fillColor));
            }
            else
            {
                sink.Add(ToShape(Shapes.RoundedRectRing(x, y, w, h, rx, strokeWidth), effective, strokeColor));
            }
        }
        else if (fill is { } fillOnlyColor)
        {
            sink.Add(ToShape(Shapes.RoundedRect(x, y, w, h, rx), effective, fillOnlyColor));
        }
        else
        {
            warnings.AddSkippedUnfilled("rect");
        }
    }

    private static void ImportPolyLike(XElement el, Affine2 effective, RgbColor? fill, List<Element> sink, WarningCollector warnings)
    {
        string? pointsAttr = el.Attribute("points")?.Value;
        if (string.IsNullOrEmpty(pointsAttr))
        {
            return;
        }

        List<Pt> points = ParsePoints(pointsAttr);
        if (points.Count < 3)
        {
            return;
        }

        // SVG fills a polyline as if it were closed even though its stroke wouldn't be — a filled
        // <polyline> and a filled <polygon> are the same shape.
        if (fill is { } fillColor)
        {
            sink.Add(ToShape(Shapes.Polygon(points), effective, fillColor));
        }
        else
        {
            warnings.AddSkippedUnfilled(el.Name.LocalName);
        }
    }

    private static void ImportLine(XElement el, Affine2 effective, List<Element> sink, WarningCollector warnings)
    {
        double strokeWidth = ReadNum(el, "stroke-width", 0);
        RgbColor? stroke = ResolveStroke(el);

        if (stroke is not { } lineStroke || strokeWidth <= 0)
        {
            warnings.AddSkippedUnfilled("line");
            return;
        }

        double x1 = ReadNum(el, "x1", 0);
        double y1 = ReadNum(el, "y1", 0);
        double x2 = ReadNum(el, "x2", 0);
        double y2 = ReadNum(el, "y2", 0);
        var segment = new SubPath(new Pt(x1, y1), [new Seg.Line(new Pt(x2, y2))]);
        sink.Add(ToShape(Shapes.StrokeToFill(segment, strokeWidth), effective, lineStroke));
    }

    private static Element ToShape(PathData path, Affine2 transform, RgbColor fill) =>
        new Element.Shape(PathDataTransform.Apply(path, transform), fill);

    private static RgbColor? ResolveFill(XElement el, Dictionary<string, RgbColor> gradientColors, WarningCollector warnings)
    {
        string? fillText = el.Attribute("fill")?.Value ?? ReadStyleProperty(el.Attribute("style")?.Value, "fill");
        if (fillText is null)
        {
            return null;
        }

        return ResolvePaint(fillText, gradientColors, warnings);
    }

    private static RgbColor? ResolveStroke(XElement el)
    {
        string? strokeText = el.Attribute("stroke")?.Value ?? ReadStyleProperty(el.Attribute("style")?.Value, "stroke");
        if (strokeText is null || strokeText == "none")
        {
            return null;
        }

        return TryParseColor(strokeText, out RgbColor color) ? color : null;
    }

    private static RgbColor? ResolvePaint(string paintText, Dictionary<string, RgbColor> gradientColors, WarningCollector warnings)
    {
        paintText = paintText.Trim();
        if (paintText is "none" or "transparent" or "")
        {
            return null;
        }

        if (paintText.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            string id = paintText[4..].TrimEnd(')').Trim().TrimStart('#');
            if (gradientColors.TryGetValue(id, out RgbColor gradientColor))
            {
                return gradientColor;
            }

            warnings.AddUnresolvedPaintReference();
            return new RgbColor(128, 128, 128);
        }

        if (TryParseColor(paintText, out RgbColor color))
        {
            return color;
        }

        warnings.AddUnrecognizedColor(paintText);
        return null;
    }

    private static bool TryParseColor(string text, out RgbColor color)
    {
        text = text.Trim();

        if (text.StartsWith('#'))
        {
            try
            {
                color = RgbColor.Parse(text);
                return true;
            }
            catch (FormatException)
            {
                color = default;
                return false;
            }
        }

        Match rgbMatch = RgbFunctionPattern.Match(text);
        if (rgbMatch.Success)
        {
            color = new RgbColor(
                byte.Parse(rgbMatch.Groups["r"].Value, CultureInfo.InvariantCulture),
                byte.Parse(rgbMatch.Groups["g"].Value, CultureInfo.InvariantCulture),
                byte.Parse(rgbMatch.Groups["b"].Value, CultureInfo.InvariantCulture));
            return true;
        }

        return NamedColors.TryGetValue(text, out color);
    }

    private static bool IsFullyTransparent(XElement el)
    {
        string? opacityText = el.Attribute("opacity")?.Value
            ?? el.Attribute("fill-opacity")?.Value
            ?? ReadStyleProperty(el.Attribute("style")?.Value, "opacity")
            ?? ReadStyleProperty(el.Attribute("style")?.Value, "fill-opacity");

        return opacityText is not null
            && double.TryParse(opacityText, NumberStyles.Float, CultureInfo.InvariantCulture, out double opacity)
            && opacity <= 0.001;
    }

    private static void WarnAboutOpacityIfAny(XElement el, WarningCollector warnings)
    {
        string? opacityText = el.Attribute("opacity")?.Value
            ?? el.Attribute("fill-opacity")?.Value
            ?? ReadStyleProperty(el.Attribute("style")?.Value, "opacity")
            ?? ReadStyleProperty(el.Attribute("style")?.Value, "fill-opacity");

        if (opacityText is not null
            && double.TryParse(opacityText, NumberStyles.Float, CultureInfo.InvariantCulture, out double opacity)
            && opacity < 0.999)
        {
            warnings.AddIgnoredOpacity();
        }
    }

    /// <summary>Reads one "name:value" pair out of a style="..." attribute — many real-world SVGs (Illustrator/Figma exports) set fill/opacity/stroke via CSS instead of presentation attributes.</summary>
    private static string? ReadStyleProperty(string? style, string property)
    {
        if (string.IsNullOrEmpty(style))
        {
            return null;
        }

        foreach (string declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = declaration.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }

            if (declaration[..colon].Trim().Equals(property, StringComparison.OrdinalIgnoreCase))
            {
                return declaration[(colon + 1)..].Trim();
            }
        }

        return null;
    }

    private static double ReadNum(XElement el, string attr, double defaultValue)
    {
        string? v = el.Attribute(attr)?.Value;
        return v is not null && TryParseLength(v, out double parsed) ? parsed : defaultValue;
    }

    private static List<Pt> ParsePoints(string pointsAttr)
    {
        string[] tokens = pointsAttr.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var points = new List<Pt>(tokens.Length / 2);
        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                && double.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                points.Add(new Pt(x, y));
            }
        }

        return points;
    }

    /// <summary>Counts skipped/approximated content by category during a <see cref="Walk"/> and renders one summarized Spanish message per category — a complex real-world SVG can skip dozens of individual elements, and a wall of one-line-each warnings would bury the ones that matter.</summary>
    private sealed class WarningCollector
    {
        private readonly List<string> _freeform = [];
        private int _skippedText;
        private int _skippedImage;
        private int _skippedUse;
        private int _gradientsFlattened;
        private int _ignoredOpacity;
        private int _unparseablePaths;
        private int _unresolvedPaintReferences;
        private readonly Dictionary<string, int> _skippedUnfilledByTag = new();
        private readonly Dictionary<string, int> _skippedUnsupportedByTag = new();
        private readonly HashSet<string> _unrecognizedColors = [];

        public void Add(string message) => _freeform.Add(message);

        public void AddSkippedText() => _skippedText++;

        public void AddSkippedImage() => _skippedImage++;

        public void AddSkippedUse() => _skippedUse++;

        public void AddGradientFlattened() => _gradientsFlattened++;

        public void AddIgnoredOpacity() => _ignoredOpacity++;

        public void AddUnparseablePath() => _unparseablePaths++;

        public void AddUnresolvedPaintReference() => _unresolvedPaintReferences++;

        public void AddUnrecognizedColor(string text) => _unrecognizedColors.Add(text);

        public void AddSkippedUnfilled(string tag) =>
            _skippedUnfilledByTag[tag] = _skippedUnfilledByTag.GetValueOrDefault(tag) + 1;

        public void AddSkippedUnsupportedElement(string tag) =>
            _skippedUnsupportedByTag[tag] = _skippedUnsupportedByTag.GetValueOrDefault(tag) + 1;

        public IReadOnlyList<string> Build()
        {
            var messages = new List<string>(_freeform);

            if (_skippedText > 0)
            {
                messages.Add($"Se ignoraron {_skippedText} elemento(s) <text> — conviértelos a contornos antes de subir el SVG.");
            }

            if (_skippedImage > 0)
            {
                messages.Add($"Se ignoraron {_skippedImage} imagen(es) incrustada(s) — GT7 no admite bitmaps embebidos.");
            }

            if (_skippedUse > 0)
            {
                messages.Add($"Se ignoraron {_skippedUse} elemento(s) <use> — no se resuelven referencias a símbolos.");
            }

            if (_gradientsFlattened > 0)
            {
                messages.Add($"{_gradientsFlattened} degradado(s) aproximados a su color medio — GT7 solo admite rellenos planos.");
            }

            if (_ignoredOpacity > 0)
            {
                messages.Add($"Se ignoró la opacidad de {_ignoredOpacity} forma(s) (se dibujan opacas) — GT7 no fía el alpha en los rellenos.");
            }

            if (_unparseablePaths > 0)
            {
                messages.Add($"No se pudieron interpretar {_unparseablePaths} trazado(s) <path>.");
            }

            if (_unresolvedPaintReferences > 0)
            {
                messages.Add($"{_unresolvedPaintReferences} referencia(s) de color (url(#...)) no se pudieron resolver — se sustituyeron por gris.");
            }

            foreach ((string tag, int count) in _skippedUnfilledByTag)
            {
                messages.Add($"Se ignoraron {count} elemento(s) <{tag}> sin relleno (solo trazo decorativo no soportado).");
            }

            foreach ((string tag, int count) in _skippedUnsupportedByTag)
            {
                messages.Add($"Se ignoraron {count} elemento(s) <{tag}> no soportados.");
            }

            if (_unrecognizedColors.Count > 0)
            {
                messages.Add($"No se reconocieron {_unrecognizedColors.Count} valor(es) de color: {string.Join(", ", _unrecognizedColors.Take(5))}{(_unrecognizedColors.Count > 5 ? "…" : "")}.");
            }

            return messages;
        }
    }
}
