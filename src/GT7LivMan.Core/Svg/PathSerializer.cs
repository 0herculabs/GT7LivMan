using System.Text;
using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// Walks our own <see cref="PathData"/> segment tree into an SVG "d" attribute string. Written by
/// hand rather than relying on any framework's Geometry.ToString(): those emit full round-trip
/// double precision with no rounding hook, and coordinate precision is our main lever against the
/// 15 KB GT7 upload limit.
/// </summary>
internal static class PathSerializer
{
    public static string Serialize(PathData path, SvgWriterOptions options)
    {
        var sb = new StringBuilder();
        foreach (SubPath sub in path.SubPaths)
        {
            AppendSubPath(sb, sub, options);
        }

        return sb.ToString();
    }

    private static void AppendSubPath(StringBuilder sb, SubPath sub, SvgWriterOptions options)
    {
        int decimals = options.Decimals;
        sb.Append('M').Append(SvgFormat.Point(sub.Start, decimals));

        char? lastCommand = null;
        foreach (Seg seg in sub.Segments)
        {
            switch (seg)
            {
                case Seg.Line l:
                    AppendCommand(sb, 'L', ref lastCommand, options);
                    sb.Append(SvgFormat.Point(l.To, decimals));
                    break;

                case Seg.Cubic c:
                    AppendCommand(sb, 'C', ref lastCommand, options);
                    sb.Append(SvgFormat.Point(c.C1, decimals)).Append(' ')
                      .Append(SvgFormat.Point(c.C2, decimals)).Append(' ')
                      .Append(SvgFormat.Point(c.To, decimals));
                    break;

                case Seg.Quad q:
                    AppendCommand(sb, 'Q', ref lastCommand, options);
                    sb.Append(SvgFormat.Point(q.C, decimals)).Append(' ')
                      .Append(SvgFormat.Point(q.To, decimals));
                    break;

                case Seg.Arc a:
                    // Always an explicit "A": concatenated flag digits ("...11...") ahead of a
                    // coordinate is a real parser-ambiguity risk not worth the few bytes saved.
                    sb.Append('A')
                      .Append(SvgFormat.Number(a.Rx, decimals)).Append(',')
                      .Append(SvgFormat.Number(a.Ry, decimals)).Append(',')
                      .Append(SvgFormat.Number(a.XAxisRotationDeg, decimals)).Append(',')
                      .Append(a.LargeArc ? '1' : '0').Append(',')
                      .Append(a.SweepClockwise ? '1' : '0').Append(',')
                      .Append(SvgFormat.Point(a.To, decimals));
                    lastCommand = null;
                    break;

                case Seg.Close:
                    sb.Append('Z');
                    lastCommand = null;
                    break;

                default:
                    throw new NotSupportedException($"Unknown segment type {seg.GetType()}.");
            }
        }
    }

    private static void AppendCommand(StringBuilder sb, char command, ref char? lastCommand, SvgWriterOptions options)
    {
        bool omit = options.ImplicitRepeatedCommands && lastCommand == command;
        if (omit)
        {
            // The letter itself normally acts as the separator; when we drop it, a space still has
            // to separate this point from the previous one (e.g. "L1,2 3,4", never "L1,23,4").
            sb.Append(' ');
        }
        else
        {
            sb.Append(command);
        }

        lastCommand = command;
    }
}
