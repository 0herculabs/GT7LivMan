using System.Globalization;
using System.Xml.Linq;
using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// Imports a static, hand-authored SVG template into flat, fully-transformed (Path, Fill) shapes
/// in paint order. Understands only the subset of SVG our base plate art actually uses —
/// g/path/rect/circle/ellipse/polygon with fill, fill-rule on path, stroke(+width) on rect, and
/// transform composition — and throws on anything else so an unsupported feature in a future
/// template fails loudly at import time instead of silently dropping content.
/// </summary>
public static class SvgReader
{
    public static IReadOnlyList<(PathData Path, RgbColor Fill)> ReadStaticShapes(string svgContent)
    {
        XDocument doc = XDocument.Parse(svgContent);
        XElement root = doc.Root ?? throw new FormatException("SVG document has no root element.");

        var sink = new List<(PathData, RgbColor)>();
        Walk(root, Affine2.Identity, null, sink);
        return sink;
    }

    private static void Walk(XElement parent, Affine2 ambient, RgbColor? inheritedFill, List<(PathData, RgbColor)> sink)
    {
        foreach (XElement el in parent.Elements())
        {
            Affine2 own = SvgTransformParser.Parse(el.Attribute("transform")?.Value);
            Affine2 effective = ambient * own;
            RgbColor? fill = ParseFillOrNull(el.Attribute("fill")?.Value) ?? inheritedFill;

            switch (el.Name.LocalName)
            {
                case "g":
                    Walk(el, effective, fill, sink);
                    break;

                case "path":
                {
                    string? d = el.Attribute("d")?.Value;
                    if (string.IsNullOrEmpty(d))
                    {
                        break;
                    }

                    PathData local = SvgPathParser.Parse(d);
                    double pathStrokeWidth = ReadNum(el, "stroke-width", 0);
                    RgbColor? pathStroke = ParseFillOrNull(el.Attribute("stroke")?.Value);

                    if (fill is null && pathStroke is { } strokeOnlyColor && pathStrokeWidth > 0)
                    {
                        // A thin decorative line (e.g. the curved seam under a plate's color band):
                        // one filled ribbon per subpath, same reasoning as a stroke-only <rect>.
                        foreach (SubPath sub in local.SubPaths)
                        {
                            sink.Add((PathDataTransform.Apply(Shapes.StrokeToFill(sub, pathStrokeWidth), effective), strokeOnlyColor));
                        }

                        break;
                    }

                    RgbColor pathFill = fill ?? throw new FormatException("<path> has no fill (and none inherited).");
                    if (el.Attribute("fill-rule")?.Value == "evenodd")
                    {
                        // Hand-authored artwork with self-intersecting curves (e.g. a cursive
                        // wordmark) needs this to render its counters correctly — SvgWriter emits
                        // the same evenodd back out, it isn't limited to NonZero.
                        local = local with { Rule = FillRule.EvenOdd };
                    }

                    sink.Add((PathDataTransform.Apply(local, effective), pathFill));
                    break;
                }

                case "rect":
                {
                    double x = ReadNum(el, "x", 0);
                    double y = ReadNum(el, "y", 0);
                    double w = ReadNum(el, "width", 0);
                    double h = ReadNum(el, "height", 0);
                    double rx = ReadNum(el, "rx", 0);
                    double strokeWidth = ReadNum(el, "stroke-width", 0);
                    RgbColor? stroke = ParseFillOrNull(el.Attribute("stroke")?.Value);

                    // SvgWriter never emits an SVG "stroke" — only flat fills — so every case here
                    // gets rebuilt as one or two solid shapes instead.
                    if (stroke is { } strokeColor && strokeWidth > 0)
                    {
                        if (fill is { } fillColor)
                        {
                            // Both fill and stroke: two concentric solid rounded rects (a background
                            // with a colored border, e.g. the plate's own outer frame).
                            double half = strokeWidth / 2;
                            PathData outer = Shapes.RoundedRect(x - half, y - half, w + strokeWidth, h + strokeWidth, rx + half);
                            sink.Add((PathDataTransform.Apply(outer, effective), strokeColor));
                            PathData inner = Shapes.RoundedRect(x + half, y + half, w - strokeWidth, h - strokeWidth, Math.Max(0, rx - half));
                            sink.Add((PathDataTransform.Apply(inner, effective), fillColor));
                        }
                        else
                        {
                            // Stroke only (fill="none"): a true outline — an annulus built from an
                            // outer contour and an inner counter-wound contour under NonZero, not a
                            // solid block (e.g. Portugal's decorative inner border line).
                            PathData ring = Shapes.RoundedRectRing(x, y, w, h, rx, strokeWidth);
                            sink.Add((PathDataTransform.Apply(ring, effective), strokeColor));
                        }
                    }
                    else if (fill is { } fillOnlyColor)
                    {
                        PathData plain = Shapes.RoundedRect(x, y, w, h, rx);
                        sink.Add((PathDataTransform.Apply(plain, effective), fillOnlyColor));
                    }

                    break;
                }

                case "circle":
                {
                    double cx = ReadNum(el, "cx", 0);
                    double cy = ReadNum(el, "cy", 0);
                    double r = ReadNum(el, "r", 0);
                    RgbColor circleFill = fill ?? throw new FormatException("<circle> has no fill (and none inherited).");
                    PathData circle = Shapes.Circle(cx, cy, r);
                    sink.Add((PathDataTransform.Apply(circle, effective), circleFill));
                    break;
                }

                case "ellipse":
                {
                    double cx = ReadNum(el, "cx", 0);
                    double cy = ReadNum(el, "cy", 0);
                    double rx = ReadNum(el, "rx", 0);
                    double ry = ReadNum(el, "ry", 0);
                    RgbColor ellipseFill = fill ?? throw new FormatException("<ellipse> has no fill (and none inherited).");
                    PathData ellipse = Shapes.Ellipse(cx, cy, rx, ry);
                    sink.Add((PathDataTransform.Apply(ellipse, effective), ellipseFill));
                    break;
                }

                case "polygon":
                {
                    string? pointsAttr = el.Attribute("points")?.Value;
                    if (string.IsNullOrEmpty(pointsAttr))
                    {
                        break;
                    }

                    RgbColor polygonFill = fill ?? throw new FormatException("<polygon> has no fill (and none inherited).");
                    PathData polygon = Shapes.Polygon(ParsePoints(pointsAttr));
                    sink.Add((PathDataTransform.Apply(polygon, effective), polygonFill));
                    break;
                }

                case "line":
                {
                    // Always stroke-only by definition (a <line> has no interior to fill), so this
                    // becomes a filled ribbon the same way a stroke-only <path> does — e.g. New
                    // York's two thin accent lines along the plate's bottom edge.
                    double x1 = ReadNum(el, "x1", 0);
                    double y1 = ReadNum(el, "y1", 0);
                    double x2 = ReadNum(el, "x2", 0);
                    double y2 = ReadNum(el, "y2", 0);
                    double lineStrokeWidth = ReadNum(el, "stroke-width", 0);
                    RgbColor lineStroke = ParseFillOrNull(el.Attribute("stroke")?.Value)
                        ?? throw new FormatException("<line> has no stroke.");

                    if (lineStrokeWidth <= 0)
                    {
                        break;
                    }

                    var segment = new SubPath(new Pt(x1, y1), [new Seg.Line(new Pt(x2, y2))]);
                    sink.Add((PathDataTransform.Apply(Shapes.StrokeToFill(segment, lineStrokeWidth), effective), lineStroke));
                    break;
                }

                default:
                    throw new NotSupportedException(
                        $"Unsupported SVG element <{el.Name.LocalName}> — extend SvgReader when a template actually needs it.");
            }
        }
    }

    private static double ReadNum(XElement el, string attr, double defaultValue)
    {
        string? v = el.Attribute(attr)?.Value;
        return v is null ? defaultValue : double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static List<Pt> ParsePoints(string pointsAttr)
    {
        string[] tokens = pointsAttr.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var points = new List<Pt>(tokens.Length / 2);
        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            double x = double.Parse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture);
            double y = double.Parse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture);
            points.Add(new Pt(x, y));
        }

        return points;
    }

    private static RgbColor? ParseFillOrNull(string? value) =>
        string.IsNullOrEmpty(value) || value == "none" ? null : RgbColor.Parse(value);
}
