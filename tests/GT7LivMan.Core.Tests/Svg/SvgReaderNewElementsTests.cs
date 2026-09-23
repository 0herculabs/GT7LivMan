using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Svg;

public class SvgReaderNewElementsTests
{
    [Fact]
    public void ReadStaticShapes_ImportsCircleAsTwoSemicircularArcs()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"10\" cy=\"20\" r=\"5\" fill=\"#112233\"/></svg>";
        var shapes = SvgReader.ReadStaticShapes(svg);

        var shape = Assert.Single(shapes);
        Assert.Equal(RgbColor.Parse("#112233"), shape.Fill);
        SubPath sub = Assert.Single(shape.Path.SubPaths);
        Assert.Equal(new Pt(15, 20), sub.Start); // cx+r, cy
        Assert.Equal(3, sub.Segments.Count); // arc, arc, close
        Assert.IsType<Seg.Arc>(sub.Segments[0]);
        Assert.IsType<Seg.Arc>(sub.Segments[1]);
        Assert.IsType<Seg.Close>(sub.Segments[2]);
    }

    [Fact]
    public void ReadStaticShapes_ImportsPolygonAsClosedLineLoop()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\">" +
                     "<polygon points=\"0,0 10,0 5,8\" fill=\"#ffd200\"/></svg>";
        var shapes = SvgReader.ReadStaticShapes(svg);

        var shape = Assert.Single(shapes);
        Assert.Equal(RgbColor.Parse("#ffd200"), shape.Fill);
        SubPath sub = Assert.Single(shape.Path.SubPaths);
        Assert.Equal(new Pt(0, 0), sub.Start);
        Assert.Equal(3, sub.Segments.Count); // L, L, Z
        Assert.Equal(new Pt(10, 0), Assert.IsType<Seg.Line>(sub.Segments[0]).To);
        Assert.Equal(new Pt(5, 8), Assert.IsType<Seg.Line>(sub.Segments[1]).To);
        Assert.IsType<Seg.Close>(sub.Segments[2]);
    }

    [Fact]
    public void ReadStaticShapes_StrokeOnlyRect_ProducesARing_NotASolidBlock()
    {
        // fill="none": must not become a nearly-solid rounded rect covering the interior.
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\">" +
                     "<rect x=\"10\" y=\"10\" width=\"100\" height=\"50\" rx=\"5\" fill=\"none\" stroke=\"#111\" stroke-width=\"2\"/></svg>";
        var shapes = SvgReader.ReadStaticShapes(svg);

        var shape = Assert.Single(shapes);
        Assert.Equal(RgbColor.Parse("#111111"), shape.Fill);
        Assert.Equal(2, shape.Path.SubPaths.Count); // outer (CW) + inner (CCW) — an annulus, not a filled block
    }

    [Fact]
    public void ReadStaticShapes_FillAndStrokeRect_StillProducesTwoSolidConcentricShapes()
    {
        // Regression guard: the ring-vs-solid branch split must not disturb Spain's border case.
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\">" +
                     "<rect x=\"4\" y=\"4\" width=\"100\" height=\"50\" rx=\"10\" fill=\"#f7f7f7\" stroke=\"#171717\" stroke-width=\"8\"/></svg>";
        var shapes = SvgReader.ReadStaticShapes(svg);

        Assert.Equal(2, shapes.Count);
        Assert.Equal(RgbColor.Parse("#171717"), shapes[0].Fill);
        Assert.Single(shapes[0].Path.SubPaths);
        Assert.Equal(RgbColor.Parse("#f7f7f7"), shapes[1].Fill);
        Assert.Single(shapes[1].Path.SubPaths);
    }

    [Fact]
    public void ReadStaticShapes_StrokeOnlyPath_ProducesAFilledRibbon_NotAnUnfillableOpenPath()
    {
        // New York's curved seam under its top band: fill="none" + stroke, an open (not closed)
        // path — a genuinely different shape from a stroke-only <rect>, so it needs its own branch
        // rather than falling into the "<path> has no fill" exception.
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\">" +
                     "<path d=\"M 0,0 L 100,0\" fill=\"none\" stroke=\"#26345b\" stroke-width=\"10\"/></svg>";
        var shapes = SvgReader.ReadStaticShapes(svg);

        var shape = Assert.Single(shapes);
        Assert.Equal(RgbColor.Parse("#26345b"), shape.Fill);
        SubPath sub = Assert.Single(shape.Path.SubPaths);
        Assert.True(sub.IsClosed); // a filled ribbon, not the original open centerline
    }

    [Fact]
    public void ReadStaticShapes_PathWithNeitherFillNorStroke_StillThrows()
    {
        // Regression guard: the new stroke-only branch must not swallow the genuine "no paint at
        // all" error case it sits next to.
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M 0,0 L 100,0\" fill=\"none\"/></svg>";
        Assert.Throws<FormatException>(() => SvgReader.ReadStaticShapes(svg));
    }

    [Fact]
    public void ReadStaticShapes_ImportsLineAsAFilledRibbon()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\">" +
                     "<line x1=\"10\" y1=\"20\" x2=\"110\" y2=\"20\" stroke=\"#26345b\" stroke-width=\"4\"/></svg>";
        var shapes = SvgReader.ReadStaticShapes(svg);

        var shape = Assert.Single(shapes);
        Assert.Equal(RgbColor.Parse("#26345b"), shape.Fill);
        SubPath sub = Assert.Single(shape.Path.SubPaths);
        Assert.True(sub.IsClosed);
    }

    [Fact]
    public void ReadStaticShapes_LineWithoutStroke_Throws()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><line x1=\"10\" y1=\"20\" x2=\"110\" y2=\"20\"/></svg>";
        Assert.Throws<FormatException>(() => SvgReader.ReadStaticShapes(svg));
    }
}
