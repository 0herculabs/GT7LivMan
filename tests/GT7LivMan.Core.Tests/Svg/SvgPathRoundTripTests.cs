using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Svg;

public class SvgPathRoundTripTests
{
    private static readonly SvgWriterOptions HighPrecision = SvgWriterOptions.Default with { Decimals = 6 };

    [Fact]
    public void ParseThenSerialize_PreservesLineAndCurveGeometry()
    {
        var original = new PathData(
        [
            new SubPath(new Pt(0, 0),
            [
                new Seg.Line(new Pt(10, 0)),
                new Seg.Cubic(new Pt(15, 5), new Pt(20, 5), new Pt(25, 0)),
                new Seg.Quad(new Pt(30, -5), new Pt(35, 0)),
                new Seg.Close(),
            ]),
        ],
        FillRule.NonZero);

        string d = SerializePublic(original);
        PathData reparsed = SvgPathParser.Parse(d);

        AssertApproximatelyEqual(original, reparsed);
    }

    [Theory]
    [InlineData("M0,0 L10,0 L10,10 Z")] // explicit absolute
    [InlineData("m0,0 l10,0 l0,10 z")] // explicit relative
    [InlineData("M0,0 L10,0 10,10 0,10 Z")] // implicit repeated lineto
    [InlineData("M0,0 C1,1 2,1 3,0 S4,-1 5,0")] // smooth cubic reflection
    [InlineData("M0,0 Q1,1 2,0 T4,0")] // smooth quad reflection
    [InlineData("M0,0 H10 V10 H0 Z")] // horizontal/vertical shorthand
    public void Parse_AcceptsCommonAuthoringForms_WithoutThrowing(string d)
    {
        PathData parsed = SvgPathParser.Parse(d);
        Assert.NotEmpty(parsed.SubPaths);
    }

    [Fact]
    public void Parse_HandlesConcatenatedNumbersWithoutSeparators()
    {
        // "1.5.5" must tokenize as two numbers: 1.5 and .5 (classic minified-SVG ambiguity).
        PathData parsed = SvgPathParser.Parse("M0,0L1.5.5");
        Seg.Line line = Assert.IsType<Seg.Line>(parsed.SubPaths[0].Segments[0]);
        Assert.Equal(1.5, line.To.X, precision: 6);
        Assert.Equal(0.5, line.To.Y, precision: 6);
    }

    [Fact]
    public void Parse_HandlesArcFlagsAbuttingCoordinates()
    {
        // Arc flags are single digits and may run directly into the next number: "...115,5".
        PathData parsed = SvgPathParser.Parse("M0,0A5,5,0,115,5");
        var arc = Assert.IsType<Seg.Arc>(parsed.SubPaths[0].Segments[0]);
        Assert.True(arc.LargeArc);
        Assert.True(arc.SweepClockwise);
        Assert.Equal(5, arc.To.X, precision: 6);
        Assert.Equal(5, arc.To.Y, precision: 6);
    }

    private static string SerializePublic(PathData path)
    {
        // Route through SvgWriter's single-element scene so the test exercises the real path,
        // then strip the surrounding markup back down to the "d" value.
        var scene = new Scene(new SizeMm(10, 10),
            [new SceneElement(path, RgbColor.Parse("#000000"))]);
        string svg = SvgWriter.Write(scene, HighPrecision);
        int start = svg.IndexOf("d=\"", StringComparison.Ordinal) + 3;
        int end = svg.IndexOf('"', start);
        return svg[start..end];
    }

    private static void AssertApproximatelyEqual(PathData expected, PathData actual)
    {
        Assert.Equal(expected.SubPaths.Count, actual.SubPaths.Count);
        for (int i = 0; i < expected.SubPaths.Count; i++)
        {
            SubPath e = expected.SubPaths[i];
            SubPath a = actual.SubPaths[i];
            AssertApproximatelyEqual(e.Start, a.Start);
            Assert.Equal(e.Segments.Count, a.Segments.Count);
            for (int j = 0; j < e.Segments.Count; j++)
            {
                AssertSegApproximatelyEqual(e.Segments[j], a.Segments[j]);
            }
        }
    }

    private static void AssertSegApproximatelyEqual(Seg expected, Seg actual)
    {
        switch (expected, actual)
        {
            case (Seg.Line el, Seg.Line al):
                AssertApproximatelyEqual(el.To, al.To);
                break;
            case (Seg.Cubic ec, Seg.Cubic ac):
                AssertApproximatelyEqual(ec.C1, ac.C1);
                AssertApproximatelyEqual(ec.C2, ac.C2);
                AssertApproximatelyEqual(ec.To, ac.To);
                break;
            case (Seg.Quad eq, Seg.Quad aq):
                AssertApproximatelyEqual(eq.C, aq.C);
                AssertApproximatelyEqual(eq.To, aq.To);
                break;
            case (Seg.Close, Seg.Close):
                break;
            default:
                Assert.Fail($"Segment type mismatch: expected {expected.GetType().Name}, got {actual.GetType().Name}.");
                break;
        }
    }

    private static void AssertApproximatelyEqual(Pt expected, Pt actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 5);
        Assert.Equal(expected.Y, actual.Y, precision: 5);
    }
}
