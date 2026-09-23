using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Tests.Geometry;

public class PathDataSimplifierTests
{
    [Fact]
    public void Simplify_CollapsesManyCollinearPointsOnAStraightLine()
    {
        // A "curve" that's actually dead straight should flatten down to just its two endpoints.
        var segs = new List<Seg>();
        for (int i = 1; i <= 20; i++)
        {
            segs.Add(new Seg.Line(new Pt(i * 5, 0)));
        }

        var path = PathData.Single(new SubPath(new Pt(0, 0), segs));
        PathData simplified = PathDataSimplifier.Simplify(path, tolerance: 0.5);

        SubPath sub = Assert.Single(simplified.SubPaths);
        Assert.True(sub.Segments.Count < segs.Count, $"Expected fewer than {segs.Count} segments, got {sub.Segments.Count}.");
        Assert.Equal(new Pt(100, 0), Assert.IsType<Seg.Line>(sub.Segments[^1]).To);
    }

    [Fact]
    public void Simplify_PreservesEndpointsAndClosedness()
    {
        var path = PathData.Single(new SubPath(new Pt(0, 0),
        [
            new Seg.Cubic(new Pt(10, 20), new Pt(30, 20), new Pt(40, 0)),
            new Seg.Line(new Pt(40, 40)),
            new Seg.Line(new Pt(0, 40)),
            new Seg.Close(),
        ]));

        PathData simplified = PathDataSimplifier.Simplify(path, tolerance: 1.0);

        SubPath sub = Assert.Single(simplified.SubPaths);
        Assert.Equal(new Pt(0, 0), sub.Start);
        Assert.True(sub.IsClosed);
        Assert.All(sub.Segments, s => Assert.True(s is Seg.Line or Seg.Close));
    }

    [Fact]
    public void Simplify_StaysWithinToleranceOfTheOriginalCurve()
    {
        // A near-semicircular cubic approximation: simplifying it shouldn't move any retained point
        // more than `tolerance` away from where the original smooth curve actually passed.
        var original = new SubPath(new Pt(0, 0), [new Seg.Cubic(new Pt(0, 55), new Pt(100, 55), new Pt(100, 0))]);
        var path = PathData.Single(original);

        const double tolerance = 1.5;
        PathData simplified = PathDataSimplifier.Simplify(path, tolerance);

        // The simplified polyline must still pass near the curve's true midpoint (t=0.5 on the cubic).
        Pt trueMid = CubicAt(original.Start, ((Seg.Cubic)original.Segments[0]).C1, ((Seg.Cubic)original.Segments[0]).C2, ((Seg.Cubic)original.Segments[0]).To, 0.5);

        SubPath simplifiedSub = simplified.SubPaths[0];
        var points = new List<Pt> { simplifiedSub.Start };
        points.AddRange(simplifiedSub.Segments.OfType<Seg.Line>().Select(l => l.To));

        double minDistToMid = points.Min(p => Math.Sqrt(Math.Pow(p.X - trueMid.X, 2) + Math.Pow(p.Y - trueMid.Y, 2)));
        Assert.True(minDistToMid < tolerance * 3, $"Nearest simplified point to the curve's midpoint was {minDistToMid:F2} units away.");
    }

    private static Pt CubicAt(Pt p0, Pt p1, Pt p2, Pt p3, double t)
    {
        double mt = 1 - t;
        double x = (mt * mt * mt * p0.X) + (3 * mt * mt * t * p1.X) + (3 * mt * t * t * p2.X) + (t * t * t * p3.X);
        double y = (mt * mt * mt * p0.Y) + (3 * mt * mt * t * p1.Y) + (3 * mt * t * t * p2.Y) + (t * t * t * p3.Y);
        return new Pt(x, y);
    }
}
