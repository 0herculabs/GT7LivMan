namespace GT7LivMan.Core.Geometry;

/// <summary>
/// Last-resort size reduction for a path that's still too complex after decimal rounding — flattens
/// every curve to a dense polyline, then runs Douglas-Peucker to drop points that don't meaningfully
/// change the shape. Exists because some real fonts draw glyphs with far more curve detail than a
/// minimal plate face (one observed digit used 84 segments across 3 subpaths — not a bug, just a
/// more elaborate design), and no amount of decimal-place trimming fixes a genuine segment-count problem.
/// </summary>
public static class PathDataSimplifier
{
    public static PathData Simplify(PathData path, double tolerance)
    {
        var subPaths = new List<SubPath>(path.SubPaths.Count);
        foreach (SubPath sub in path.SubPaths)
        {
            List<Pt> flattened = Flatten(sub);
            List<Pt> simplified = flattened.Count > 2 ? DouglasPeucker(flattened, tolerance) : flattened;
            subPaths.Add(Rebuild(simplified, sub.IsClosed));
        }

        return new PathData(subPaths, path.Rule);
    }

    /// <summary>Samples every curve segment down to a dense polyline (also reused by <see cref="Shapes.StrokeToFill"/>, which needs the same flat point list to build an offset ribbon).</summary>
    internal static List<Pt> Flatten(SubPath sub)
    {
        var points = new List<Pt> { sub.Start };
        Pt current = sub.Start;

        foreach (Seg seg in sub.Segments)
        {
            switch (seg)
            {
                case Seg.Line l:
                    points.Add(l.To);
                    current = l.To;
                    break;

                case Seg.Cubic c:
                    FlattenCubic(current, c.C1, c.C2, c.To, points);
                    current = c.To;
                    break;

                case Seg.Quad q:
                    FlattenQuad(current, q.C, q.To, points);
                    current = q.To;
                    break;

                case Seg.Arc a:
                    // Arcs don't occur in glyph outlines (fonts are cubic/quad only) or in our own
                    // generated shapes at the point this runs on font-sourced text — keep the
                    // endpoint rather than approximate, so a future caller on other content degrades
                    // gracefully instead of silently losing the arc's curvature.
                    points.Add(a.To);
                    current = a.To;
                    break;

                case Seg.Close:
                    points.Add(sub.Start);
                    current = sub.Start;
                    break;
            }
        }

        return points;
    }

    private static void FlattenCubic(Pt p0, Pt p1, Pt p2, Pt p3, List<Pt> sink)
    {
        const int Steps = 12;
        for (int i = 1; i <= Steps; i++)
        {
            double t = (double)i / Steps;
            double mt = 1 - t;
            double x = (mt * mt * mt * p0.X) + (3 * mt * mt * t * p1.X) + (3 * mt * t * t * p2.X) + (t * t * t * p3.X);
            double y = (mt * mt * mt * p0.Y) + (3 * mt * mt * t * p1.Y) + (3 * mt * t * t * p2.Y) + (t * t * t * p3.Y);
            sink.Add(new Pt(x, y));
        }
    }

    private static void FlattenQuad(Pt p0, Pt p1, Pt p2, List<Pt> sink)
    {
        const int Steps = 10;
        for (int i = 1; i <= Steps; i++)
        {
            double t = (double)i / Steps;
            double mt = 1 - t;
            double x = (mt * mt * p0.X) + (2 * mt * t * p1.X) + (t * t * p2.X);
            double y = (mt * mt * p0.Y) + (2 * mt * t * p1.Y) + (t * t * p2.Y);
            sink.Add(new Pt(x, y));
        }
    }

    private static List<Pt> DouglasPeucker(List<Pt> points, double tolerance)
    {
        int farthestIndex = -1;
        double farthestDist = 0;

        for (int i = 1; i < points.Count - 1; i++)
        {
            double dist = PerpendicularDistance(points[i], points[0], points[^1]);
            if (dist > farthestDist)
            {
                farthestDist = dist;
                farthestIndex = i;
            }
        }

        if (farthestDist <= tolerance || farthestIndex < 0)
        {
            return [points[0], points[^1]];
        }

        List<Pt> left = DouglasPeucker(points.GetRange(0, farthestIndex + 1), tolerance);
        List<Pt> right = DouglasPeucker(points.GetRange(farthestIndex, points.Count - farthestIndex), tolerance);

        var merged = new List<Pt>(left.Count + right.Count - 1);
        merged.AddRange(left);
        merged.AddRange(right.Skip(1));
        return merged;
    }

    private static double PerpendicularDistance(Pt p, Pt lineStart, Pt lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double lenSq = (dx * dx) + (dy * dy);

        if (lenSq < 1e-9)
        {
            double ddx = p.X - lineStart.X;
            double ddy = p.Y - lineStart.Y;
            return Math.Sqrt((ddx * ddx) + (ddy * ddy));
        }

        double t = (((p.X - lineStart.X) * dx) + ((p.Y - lineStart.Y) * dy)) / lenSq;
        double projX = lineStart.X + (t * dx);
        double projY = lineStart.Y + (t * dy);
        double ex = p.X - projX;
        double ey = p.Y - projY;
        return Math.Sqrt((ex * ex) + (ey * ey));
    }

    private static SubPath Rebuild(List<Pt> points, bool isClosed)
    {
        var segs = new List<Seg>(points.Count);
        for (int i = 1; i < points.Count; i++)
        {
            segs.Add(new Seg.Line(points[i]));
        }

        if (isClosed)
        {
            segs.Add(new Seg.Close());
        }

        return new SubPath(points[0], segs);
    }
}
