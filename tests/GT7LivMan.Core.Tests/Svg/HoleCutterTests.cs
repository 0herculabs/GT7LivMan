using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Svg;

public class HoleCutterTests
{
    private static PathData Square(double x, double y, double size) =>
        Shapes.RoundedRect(x, y, size, size, 0);

    private static Element.Shape ShapeAt(double x, double y, double size) =>
        new(Square(x, y, size), RgbColor.Parse("#ff0000"));

    [Fact]
    public void CutHoles_TransparentRegionInsideAShape_BecomesAnEvenOddHole()
    {
        // The shape a letter is traced as, plus the transparent counter sitting on top of it.
        IReadOnlyList<Element> shapes = [ShapeAt(0, 0, 100)];
        IReadOnlyList<PathData> transparent = [Square(40, 40, 20)];

        IReadOnlyList<Element> result = HoleCutter.CutHoles(shapes, transparent);

        var cut = Assert.IsType<Element.Shape>(Assert.Single(result));
        Assert.Equal(FillRule.EvenOdd, cut.Path.Rule);
        Assert.Equal(2, cut.Path.SubPaths.Count); // outer contour + the hole
    }

    [Fact]
    public void CutHoles_TransparentBackgroundSurroundingEverything_IsLeftAlone()
    {
        // Regression guard: only the background's outer contour gets traced, and taken on its own
        // that rectangle encloses the artwork — so a containment test alone would cut the background
        // into the artwork and invert the colors. A hole has to be smaller than what it punches.
        IReadOnlyList<Element> shapes = [ShapeAt(20, 20, 60)];
        IReadOnlyList<PathData> transparent = [Square(0, 0, 100)];

        IReadOnlyList<Element> result = HoleCutter.CutHoles(shapes, transparent);

        var untouched = Assert.IsType<Element.Shape>(Assert.Single(result));
        Assert.Equal(FillRule.NonZero, untouched.Path.Rule);
        Assert.Single(untouched.Path.SubPaths);
    }

    [Fact]
    public void CutHoles_PicksTheInnermostContainingShape_NotTheOutermost()
    {
        // A hole inside a small shape that itself sits on a large one belongs to the small shape:
        // whichever containing shape was painted last is the one actually showing at that spot.
        var large = ShapeAt(0, 0, 100);
        var small = ShapeAt(20, 20, 50);
        IReadOnlyList<Element> shapes = [large, small];
        IReadOnlyList<PathData> transparent = [Square(35, 35, 10)];

        IReadOnlyList<Element> result = HoleCutter.CutHoles(shapes, transparent);

        Assert.Equal(FillRule.NonZero, ((Element.Shape)result[0]).Path.Rule);
        Assert.Equal(FillRule.EvenOdd, ((Element.Shape)result[1]).Path.Rule);
    }

    [Fact]
    public void CutHoles_RegionOutsideEveryShape_IsIgnored()
    {
        IReadOnlyList<Element> shapes = [ShapeAt(0, 0, 40)];
        IReadOnlyList<PathData> transparent = [Square(200, 200, 10)];

        IReadOnlyList<Element> result = HoleCutter.CutHoles(shapes, transparent);

        Assert.Equal(FillRule.NonZero, ((Element.Shape)Assert.Single(result)).Path.Rule);
    }

    [Fact]
    public void CutHoles_NoTransparentRegions_ReturnsTheInputUntouched()
    {
        IReadOnlyList<Element> shapes = [ShapeAt(0, 0, 40)];

        Assert.Same(shapes, HoleCutter.CutHoles(shapes, []));
    }
}
