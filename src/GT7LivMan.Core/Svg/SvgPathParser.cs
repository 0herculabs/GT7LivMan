using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// Parses an SVG "d" attribute into our own <see cref="PathData"/> geometry model. Needed both to
/// import the hand-authored base plate SVG and, combined with <see cref="PathSerializer"/>, for the
/// round-trip tests that keep the writer honest.
/// </summary>
public static class SvgPathParser
{
    public static PathData Parse(string d)
    {
        var cursor = new PathCursor(d);
        var subPaths = new List<SubPath>();

        Pt current = default;
        Pt subPathStart = default;
        List<Seg>? segs = null;
        char? lastCommand = null;

        Pt lastCubicControl = default;
        bool hasLastCubicControl = false;
        Pt lastQuadControl = default;
        bool hasLastQuadControl = false;

        while (!cursor.AtEnd)
        {
            char? explicitCommand = cursor.PeekCommand();
            char cmd;
            if (explicitCommand.HasValue)
            {
                cmd = cursor.ReadCommand();
            }
            else
            {
                if (lastCommand is null or 'Z' or 'z')
                {
                    throw new FormatException($"Expected a path command in '{d}'.");
                }

                // Implicit repeat: extra coordinate pairs after a moveto are implicit linetos;
                // any other command simply repeats itself.
                cmd = lastCommand switch
                {
                    'M' => 'L',
                    'm' => 'l',
                    _ => lastCommand.Value,
                };
            }

            bool isRelative = char.IsLower(cmd);

            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                {
                    double x = cursor.ReadNumber();
                    double y = cursor.ReadNumber();
                    Pt p = isRelative && lastCommand is not null ? current.Translate(x, y) : new Pt(x, y);
                    if (segs is not null)
                    {
                        subPaths.Add(new SubPath(subPathStart, segs));
                    }

                    subPathStart = p;
                    current = p;
                    segs = [];
                    hasLastCubicControl = false;
                    hasLastQuadControl = false;
                    break;
                }

                case 'L':
                {
                    double x = cursor.ReadNumber();
                    double y = cursor.ReadNumber();
                    Pt p = isRelative ? current.Translate(x, y) : new Pt(x, y);
                    RequireOpenSubPath(segs).Add(new Seg.Line(p));
                    current = p;
                    hasLastCubicControl = false;
                    hasLastQuadControl = false;
                    break;
                }

                case 'H':
                {
                    double x = cursor.ReadNumber();
                    Pt p = isRelative ? current.Translate(x, 0) : new Pt(x, current.Y);
                    RequireOpenSubPath(segs).Add(new Seg.Line(p));
                    current = p;
                    hasLastCubicControl = false;
                    hasLastQuadControl = false;
                    break;
                }

                case 'V':
                {
                    double y = cursor.ReadNumber();
                    Pt p = isRelative ? current.Translate(0, y) : new Pt(current.X, y);
                    RequireOpenSubPath(segs).Add(new Seg.Line(p));
                    current = p;
                    hasLastCubicControl = false;
                    hasLastQuadControl = false;
                    break;
                }

                case 'C':
                {
                    double x1 = cursor.ReadNumber(), y1 = cursor.ReadNumber();
                    double x2 = cursor.ReadNumber(), y2 = cursor.ReadNumber();
                    double x = cursor.ReadNumber(), y = cursor.ReadNumber();
                    Pt c1 = isRelative ? current.Translate(x1, y1) : new Pt(x1, y1);
                    Pt c2 = isRelative ? current.Translate(x2, y2) : new Pt(x2, y2);
                    Pt p = isRelative ? current.Translate(x, y) : new Pt(x, y);
                    RequireOpenSubPath(segs).Add(new Seg.Cubic(c1, c2, p));
                    current = p;
                    lastCubicControl = c2;
                    hasLastCubicControl = true;
                    hasLastQuadControl = false;
                    break;
                }

                case 'S':
                {
                    double x2 = cursor.ReadNumber(), y2 = cursor.ReadNumber();
                    double x = cursor.ReadNumber(), y = cursor.ReadNumber();
                    Pt c1 = hasLastCubicControl ? Reflect(current, lastCubicControl) : current;
                    Pt c2 = isRelative ? current.Translate(x2, y2) : new Pt(x2, y2);
                    Pt p = isRelative ? current.Translate(x, y) : new Pt(x, y);
                    RequireOpenSubPath(segs).Add(new Seg.Cubic(c1, c2, p));
                    current = p;
                    lastCubicControl = c2;
                    hasLastCubicControl = true;
                    hasLastQuadControl = false;
                    break;
                }

                case 'Q':
                {
                    double x1 = cursor.ReadNumber(), y1 = cursor.ReadNumber();
                    double x = cursor.ReadNumber(), y = cursor.ReadNumber();
                    Pt c = isRelative ? current.Translate(x1, y1) : new Pt(x1, y1);
                    Pt p = isRelative ? current.Translate(x, y) : new Pt(x, y);
                    RequireOpenSubPath(segs).Add(new Seg.Quad(c, p));
                    current = p;
                    lastQuadControl = c;
                    hasLastQuadControl = true;
                    hasLastCubicControl = false;
                    break;
                }

                case 'T':
                {
                    double x = cursor.ReadNumber(), y = cursor.ReadNumber();
                    Pt c = hasLastQuadControl ? Reflect(current, lastQuadControl) : current;
                    Pt p = isRelative ? current.Translate(x, y) : new Pt(x, y);
                    RequireOpenSubPath(segs).Add(new Seg.Quad(c, p));
                    current = p;
                    lastQuadControl = c;
                    hasLastQuadControl = true;
                    hasLastCubicControl = false;
                    break;
                }

                case 'A':
                {
                    double rx = cursor.ReadNumber(), ry = cursor.ReadNumber();
                    double rot = cursor.ReadNumber();
                    bool largeArc = cursor.ReadFlag();
                    bool sweep = cursor.ReadFlag();
                    double x = cursor.ReadNumber(), y = cursor.ReadNumber();
                    Pt p = isRelative ? current.Translate(x, y) : new Pt(x, y);
                    RequireOpenSubPath(segs).Add(new Seg.Arc(rx, ry, rot, largeArc, sweep, p));
                    current = p;
                    hasLastCubicControl = false;
                    hasLastQuadControl = false;
                    break;
                }

                case 'Z':
                {
                    RequireOpenSubPath(segs).Add(new Seg.Close());
                    current = subPathStart;
                    hasLastCubicControl = false;
                    hasLastQuadControl = false;
                    break;
                }

                default:
                    throw new FormatException($"Unsupported path command '{cmd}' in '{d}'.");
            }

            lastCommand = cmd;
        }

        if (segs is not null)
        {
            subPaths.Add(new SubPath(subPathStart, segs));
        }

        return new PathData(subPaths, FillRule.NonZero);
    }

    private static List<Seg> RequireOpenSubPath(List<Seg>? segs) =>
        segs ?? throw new FormatException("Path data must start with a moveto command.");

    private static Pt Reflect(Pt current, Pt control) => new((2 * current.X) - control.X, (2 * current.Y) - control.Y);
}
