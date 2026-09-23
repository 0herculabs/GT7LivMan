using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Svg;

public class ExportEscalationTests
{
    [Fact]
    public void Write_MatchesTheNaiveWriter_WhenAlreadyWithinBudget()
    {
        var scene = new Scene(
            new SizeMm(10, 10),
            [new SceneElement(PathData.Single(new SubPath(new Pt(0, 0), [new Seg.Line(new Pt(5, 5)), new Seg.Close()])), RgbColor.Parse("#000000"))]);

        Assert.Equal(SvgWriter.Write(scene), ExportEscalation.Write(scene));
    }

    [Fact]
    public void Write_FallsBackToSimplification_WhenEvenZeroDecimalsIsNotEnough()
    {
        // One subpath with thousands of tiny sub-pixel wiggles: too complex for GT7's budget at any
        // decimal precision (each wiggle is a real, distinct Cubic segment), but easily reducible by
        // Douglas-Peucker since consecutive wiggles barely deviate from the overall straight path —
        // a synthetic stand-in for what a real font's overly-detailed glyph (see the California
        // template's "8", 84 segments across 3 subpaths) does to the export.
        var segs = new List<Seg>();
        double x = 0;
        for (int i = 0; i < 2000; i++)
        {
            double nx = x + 1;
            double wiggle = i % 2 == 0 ? 0.3 : -0.3;
            segs.Add(new Seg.Cubic(new Pt(x + 0.3, wiggle), new Pt(x + 0.7, wiggle), new Pt(nx, 0)));
            x = nx;
        }

        segs.Add(new Seg.Close());
        var path = PathData.Single(new SubPath(new Pt(0, 0), segs));
        var scene = new Scene(new SizeMm(2100, 100), [new SceneElement(path, RgbColor.Parse("#000000"))]);

        string naive = SvgWriter.Write(scene);
        Assert.False(SizeBudget.IsWithinGt7Limit(naive), "Test setup problem: this path should already overflow the naive writer.");

        string escalated = ExportEscalation.Write(scene);
        Assert.True(
            SizeBudget.IsWithinGt7Limit(escalated),
            $"Expected < {SizeBudget.MaxBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(escalated)}.");
    }

    [Fact]
    public void Write_NeverThrows_OnEvenOddPaths()
    {
        // Simplification flattens to Line/Close only, discarding the original curve types, but must
        // still carry the source PathData's fill rule through untouched.
        var path = new PathData(
            [new SubPath(new Pt(0, 0), [new Seg.Cubic(new Pt(1, 1), new Pt(2, 1), new Pt(3, 0)), new Seg.Close()])],
            FillRule.EvenOdd);
        var scene = new Scene(new SizeMm(10, 10), [new SceneElement(path, RgbColor.Parse("#000000"))]);

        string svg = ExportEscalation.Write(scene);
        Assert.False(string.IsNullOrEmpty(svg));
    }
}
