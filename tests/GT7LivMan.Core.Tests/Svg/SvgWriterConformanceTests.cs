using System.Globalization;
using System.Threading;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Svg;

/// <summary>
/// Asserts the writer's output obeys the GT7 Decal Uploader's documented rules. These are product
/// requirements, not style preferences — a violation here means the exported file gets rejected or
/// mis-renders in-game.
/// </summary>
public class SvgWriterConformanceTests
{
    private static Scene SimplePlateScene() => new(
        new SizeMm(520, 110),
        [
            new SceneElement(
                PathData.Single(new SubPath(new Pt(0, 0),
                [
                    new Seg.Line(new Pt(520, 0)),
                    new Seg.Line(new Pt(520, 110)),
                    new Seg.Line(new Pt(0, 110)),
                    new Seg.Close(),
                ])),
                RgbColor.Parse("#FFFFFF")),
            new SceneElement(
                PathData.Single(new SubPath(new Pt(10, 10),
                [
                    new Seg.Line(new Pt(60, 10)),
                    new Seg.Line(new Pt(60, 100)),
                    new Seg.Close(),
                ])),
                RgbColor.Parse("#003399")),
        ]);

    [Fact]
    public void Write_NeverEmitsTextOrImageElements()
    {
        string svg = SvgWriter.Write(SimplePlateScene());
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<image", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_NeverEmitsGradientsFiltersOrBlendModes()
    {
        string svg = SvgWriter.Write(SimplePlateScene());
        Assert.DoesNotContain("gradient", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<filter", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mix-blend-mode", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_NeverEmbedsRasterData()
    {
        string svg = SvgWriter.Write(SimplePlateScene());
        Assert.DoesNotContain("data:image", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_DeclaresSvgVersion11()
    {
        string svg = SvgWriter.Write(SimplePlateScene());
        Assert.Contains("version=\"1.1\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_OmitsXmlDeclarationCommentsIdsAndMetadata()
    {
        string svg = SvgWriter.Write(SimplePlateScene());
        Assert.DoesNotContain("<?xml", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<!--", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<metadata", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" id=\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_NeverPaintsAFullCanvasBackgroundRect()
    {
        // No <rect> at all: a transparent background comes from never drawing a canvas-covering
        // shape, not from any special "background" flag.
        string svg = SvgWriter.Write(SimplePlateScene());
        Assert.DoesNotContain("<rect", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_EmitsExplicitFillRule_ForEvenOddPaths_ButOmitsItForNonZero()
    {
        var evenOddPath = new PathData(
            [new SubPath(new Pt(0, 0), [new Seg.Line(new Pt(1, 1)), new Seg.Close()])],
            FillRule.EvenOdd);
        var nonZeroPath = new PathData(
            [new SubPath(new Pt(2, 2), [new Seg.Line(new Pt(3, 3)), new Seg.Close()])],
            FillRule.NonZero);
        var scene = new Scene(
            new SizeMm(10, 10),
            [
                new SceneElement(evenOddPath, RgbColor.Parse("#000000")),
                new SceneElement(nonZeroPath, RgbColor.Parse("#111111")),
            ]);

        string svg = SvgWriter.Write(scene);

        Assert.Contains("fill-rule=\"evenodd\"", svg, StringComparison.Ordinal);
        // The NonZero path's own tag must not pick up the other element's fill-rule attribute.
        int nonZeroPathIndex = svg.IndexOf("#111111", StringComparison.Ordinal);
        string aroundNonZeroPath = svg[Math.Max(0, nonZeroPathIndex - 40)..nonZeroPathIndex];
        Assert.DoesNotContain("fill-rule", aroundNonZeroPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_DoesNotMergeEvenOddAndNonZeroElements_EvenWithTheSameFill()
    {
        var evenOdd = new SceneElement(
            new PathData([new SubPath(new Pt(0, 0), [new Seg.Line(new Pt(1, 1)), new Seg.Close()])], FillRule.EvenOdd),
            RgbColor.Parse("#222222"));
        var nonZero = new SceneElement(
            new PathData([new SubPath(new Pt(2, 2), [new Seg.Line(new Pt(3, 3)), new Seg.Close()])], FillRule.NonZero),
            RgbColor.Parse("#222222"));
        var scene = new Scene(new SizeMm(10, 10), [evenOdd, nonZero]);

        string svg = SvgWriter.Write(scene, SvgWriterOptions.Default with { GroupByFill = true });

        // Same fill would normally merge into one <g>, but the differing rule must keep them apart.
        Assert.DoesNotContain("<g fill=\"#222222\">", svg, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(svg, "fill=\"#222222\"").Count);
    }

    [Fact]
    public void Write_GroupsConsecutiveSameFillElementsUnderOneGroup()
    {
        var el = new SceneElement(
            PathData.Single(new SubPath(new Pt(0, 0), [new Seg.Line(new Pt(1, 0)), new Seg.Close()])),
            RgbColor.Parse("#112233"));
        var scene = new Scene(new SizeMm(10, 10), [el, el]);

        string svg = SvgWriter.Write(scene, SvgWriterOptions.Default with { GroupByFill = true });

        Assert.Contains("<g fill=\"#112233\">", svg, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(svg, "<path").Count);
    }

    [Fact]
    public void Write_IsCultureInvariant_UnderSpanishLocale()
    {
        // A scene with a fractional coordinate is the case that actually exercises the bug: under
        // es-ES, a naive ToString() turns 12.5 into "12,5" and corrupts the path grammar.
        var scene = new Scene(
            new SizeMm(52.5, 11.25),
            [
                new SceneElement(
                    PathData.Single(new SubPath(new Pt(0, 0), [new Seg.Line(new Pt(12.5, 0.75)), new Seg.Close()])),
                    RgbColor.Parse("#003399")),
            ]);

        CultureInfo original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
            string underSpanish = SvgWriter.Write(scene);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            string underInvariant = SvgWriter.Write(scene);

            Assert.Equal(underInvariant, underSpanish);
            Assert.Contains("12.5", underSpanish, StringComparison.Ordinal);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void Write_TypicalPlateScene_StaysWellUnderGt7Limit()
    {
        string svg = SvgWriter.Write(SimplePlateScene());
        Assert.True(SizeBudget.IsWithinTarget(svg), $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
    }
}
