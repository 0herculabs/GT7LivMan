using System.Windows;
using System.Windows.Media;
using CoreFillRule = GT7LivMan.Core.Geometry.FillRule;
using CoreGeometry = GT7LivMan.Core.Geometry;

namespace GT7LivMan.Typography.Wpf;

/// <summary>
/// Walks a WPF <see cref="Geometry"/> (glyph outlines, in practice) into our own
/// <see cref="CoreGeometry.PathData"/>. Written by hand rather than trusting Geometry.ToString():
/// that emits WPF's own path mini-language with full double precision and no rounding hook, and we
/// need our own segment tree to round coordinates and control the 15 KB export budget later.
/// </summary>
internal static class WpfGeometryConverter
{
    public static CoreGeometry.PathData Convert(Geometry geometry)
    {
        PathGeometry pathGeometry = PathGeometry.CreateFromGeometry(geometry);
        var subPaths = new List<CoreGeometry.SubPath>();

        foreach (PathFigure figure in pathGeometry.Figures)
        {
            var segs = new List<CoreGeometry.Seg>();

            foreach (PathSegment segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment line:
                        segs.Add(new CoreGeometry.Seg.Line(ToPt(line.Point)));
                        break;

                    case PolyLineSegment polyLine:
                        foreach (Point pt in polyLine.Points)
                        {
                            segs.Add(new CoreGeometry.Seg.Line(ToPt(pt)));
                        }

                        break;

                    case BezierSegment bezier:
                        segs.Add(new CoreGeometry.Seg.Cubic(ToPt(bezier.Point1), ToPt(bezier.Point2), ToPt(bezier.Point3)));
                        break;

                    case PolyBezierSegment polyBezier:
                        for (int i = 0; i + 2 < polyBezier.Points.Count; i += 3)
                        {
                            segs.Add(new CoreGeometry.Seg.Cubic(
                                ToPt(polyBezier.Points[i]), ToPt(polyBezier.Points[i + 1]), ToPt(polyBezier.Points[i + 2])));
                        }

                        break;

                    case QuadraticBezierSegment quad:
                        segs.Add(new CoreGeometry.Seg.Quad(ToPt(quad.Point1), ToPt(quad.Point2)));
                        break;

                    case PolyQuadraticBezierSegment polyQuad:
                        for (int i = 0; i + 1 < polyQuad.Points.Count; i += 2)
                        {
                            segs.Add(new CoreGeometry.Seg.Quad(ToPt(polyQuad.Points[i]), ToPt(polyQuad.Points[i + 1])));
                        }

                        break;

                    case ArcSegment arc:
                        segs.Add(new CoreGeometry.Seg.Arc(
                            arc.Size.Width,
                            arc.Size.Height,
                            arc.RotationAngle,
                            arc.IsLargeArc,
                            arc.SweepDirection == SweepDirection.Clockwise,
                            ToPt(arc.Point)));
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported WPF path segment type {segment.GetType()}.");
                }
            }

            if (figure.IsClosed)
            {
                segs.Add(new CoreGeometry.Seg.Close());
            }

            subPaths.Add(new CoreGeometry.SubPath(ToPt(figure.StartPoint), segs));
        }

        // TrueType/OpenType glyph contours wind NonZero; forward whatever WPF actually reports so
        // SvgWriter's throw-on-EvenOdd guard can catch a pathological font instead of us masking it.
        CoreFillRule rule = pathGeometry.FillRule == System.Windows.Media.FillRule.EvenOdd
            ? CoreFillRule.EvenOdd
            : CoreFillRule.NonZero;

        return new CoreGeometry.PathData(subPaths, rule);
    }

    private static CoreGeometry.Pt ToPt(Point p) => new(p.X, p.Y);
}
