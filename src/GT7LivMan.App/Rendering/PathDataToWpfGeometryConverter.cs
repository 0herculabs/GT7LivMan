using System.Windows;
using System.Windows.Media;
using CoreGeometry = GT7LivMan.Core.Geometry;

namespace GT7LivMan.App.Rendering;

/// <summary>The inverse of Typography.Wpf's WpfGeometryConverter: turns our own <see cref="CoreGeometry.PathData"/> into a WPF <see cref="Geometry"/> for the live preview.</summary>
internal static class PathDataToWpfGeometryConverter
{
    public static Geometry Convert(CoreGeometry.PathData path)
    {
        var streamGeometry = new StreamGeometry
        {
            FillRule = path.Rule == CoreGeometry.FillRule.EvenOdd ? FillRule.EvenOdd : FillRule.Nonzero,
        };

        using (StreamGeometryContext ctx = streamGeometry.Open())
        {
            foreach (CoreGeometry.SubPath sub in path.SubPaths)
            {
                ctx.BeginFigure(ToPoint(sub.Start), isFilled: true, isClosed: sub.IsClosed);
                foreach (CoreGeometry.Seg seg in sub.Segments)
                {
                    switch (seg)
                    {
                        case CoreGeometry.Seg.Line l:
                            ctx.LineTo(ToPoint(l.To), isStroked: true, isSmoothJoin: false);
                            break;

                        case CoreGeometry.Seg.Cubic c:
                            ctx.BezierTo(ToPoint(c.C1), ToPoint(c.C2), ToPoint(c.To), isStroked: true, isSmoothJoin: false);
                            break;

                        case CoreGeometry.Seg.Quad q:
                            ctx.QuadraticBezierTo(ToPoint(q.C), ToPoint(q.To), isStroked: true, isSmoothJoin: false);
                            break;

                        case CoreGeometry.Seg.Arc a:
                            ctx.ArcTo(
                                ToPoint(a.To),
                                new Size(a.Rx, a.Ry),
                                a.XAxisRotationDeg,
                                a.LargeArc,
                                a.SweepClockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                                isStroked: true,
                                isSmoothJoin: false);
                            break;

                        case CoreGeometry.Seg.Close:
                            // No-op: closedness is already conveyed by BeginFigure's isClosed argument above.
                            break;
                    }
                }
            }
        }

        streamGeometry.Freeze();
        return streamGeometry;
    }

    private static Point ToPoint(CoreGeometry.Pt p) => new(p.X, p.Y);
}
