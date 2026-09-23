using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Svg;

/// <summary>
/// Imports the real, user-authored Spain plate template (assets/base/spain_licenseplate.svg) —
/// the only end-to-end test exercising SvgReader against non-synthetic data.
/// </summary>
public class SvgReaderImportTests
{
    private static string BaseSvgPath => Path.Combine(RepoPaths.Root, "assets", "base", "spain_licenseplate_v1.svg");

    private static string ReadBaseSvg() => File.ReadAllText(BaseSvgPath);

    [Fact]
    public void ReadStaticShapes_ImportsTheRealSpainTemplate_WithExpectedShapeCount()
    {
        var shapes = SvgReader.ReadStaticShapes(ReadBaseSvg());

        // Stroked border rect -> 2 (outer border color + inner background), blue band -> 1,
        // 12 stars -> 12, "E" country-code glyph -> 1.
        Assert.Equal(2 + 1 + 12 + 1, shapes.Count);
    }

    [Fact]
    public void ReadStaticShapes_FirstStarFirstVertex_MatchesHandComputedTransform()
    {
        var shapes = SvgReader.ReadStaticShapes(ReadBaseSvg());
        var stars = shapes.Where(s => s.Fill == RgbColor.Parse("#ffd500")).ToList();
        Assert.Equal(12, stars.Count);

        // Star 1's own transform is "translate(0 -48)", nested in the group's "translate(88 100)":
        // local (0,-9) -> translate(0,-48) -> (0,-57) -> translate(88,100) -> (88,43).
        Pt firstVertex = stars[0].Path.SubPaths[0].Start;
        Assert.Equal(88, firstVertex.X, precision: 6);
        Assert.Equal(43, firstVertex.Y, precision: 6);
    }

    [Fact]
    public void ReadStaticShapes_BorderRect_SplitsIntoConcentricOuterAndInnerFills()
    {
        var shapes = SvgReader.ReadStaticShapes(ReadBaseSvg());

        // rect x=4 y=4 w=1583 h=346 rx=32 stroke-width=8 -> outer covers the full 0..1591 x 0..354
        // canvas (stroke color), inner is inset by the 4-unit half-stroke (background color).
        var outer = shapes[0];
        var inner = shapes[1];

        Assert.Equal(RgbColor.Parse("#171717"), outer.Fill);
        Assert.Equal(new Pt(36, 0), outer.Path.SubPaths[0].Start); // top-left corner start, after the rx=36 arc

        Assert.Equal(RgbColor.Parse("#f7f7f7"), inner.Fill);
        Assert.Equal(new Pt(36, 8), inner.Path.SubPaths[0].Start); // rx=28 inset by the 8-unit border
    }

    [Fact]
    public void ReadStaticShapes_ThenWrite_ProducesConformantSvgWithinBudget()
    {
        var shapes = SvgReader.ReadStaticShapes(ReadBaseSvg());
        var scene = new Scene(
            new SizeMm(1591, 354),
            shapes.Select(s => new SceneElement(s.Path, s.Fill)).ToList());

        string exported = SvgWriter.Write(scene);

        Assert.True(
            SizeBudget.IsWithinTarget(exported),
            $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(exported)}.");
        Assert.DoesNotContain("<text", exported, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<rect", exported, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stroke", exported, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version=\"1.1\"", exported, StringComparison.Ordinal);
    }
}
