using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Tests.Geometry;

public class ShapesTests
{
    [Fact]
    public void StrokeToFill_StraightLine_ProducesARectangleOfTheRightWidthAndLength()
    {
        var line = new SubPath(new Pt(0, 0), [new Seg.Line(new Pt(100, 0))]);
        PathData ribbon = Shapes.StrokeToFill(line, strokeWidth: 10);

        SubPath sub = Assert.Single(ribbon.SubPaths);
        Assert.True(sub.IsClosed);

        var points = new List<Pt> { sub.Start };
        points.AddRange(sub.Segments.OfType<Seg.Line>().Select(l => l.To));

        // A horizontal line's normal is vertical, so every point should land exactly 5 units above
        // or below the centerline (y=0), and the ribbon should span the full [0,100] length.
        Assert.All(points, p => Assert.Equal(5, Math.Abs(p.Y), precision: 6));
        Assert.Contains(points, p => Math.Abs(p.X) < 1e-6);
        Assert.Contains(points, p => Math.Abs(p.X - 100) < 1e-6);
    }

    [Fact]
    public void StrokeToFill_ThrowsOnASinglePointSubPath()
    {
        var degenerate = new SubPath(new Pt(0, 0), []);
        Assert.Throws<ArgumentException>(() => Shapes.StrokeToFill(degenerate, strokeWidth: 10));
    }
}
