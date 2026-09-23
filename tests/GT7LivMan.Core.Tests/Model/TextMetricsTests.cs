using GT7LivMan.Core.Model;

namespace GT7LivMan.Core.Tests.Model;

public class TextMetricsTests
{
    [Fact]
    public void AdvanceWidth_UsesTheNarrowerPitchForLiteralSpaces()
    {
        // "99 AA 99": 6 character cells + 2 space cells.
        Assert.Equal((6 * 95) + (2 * 67), TextMetrics.AdvanceWidth("12 AB 34", tracking: 95, spaceTracking: 67));
    }

    [Theory]
    [InlineData(TextAnchor.Left, 0)]
    [InlineData(TextAnchor.Center, -350)]
    [InlineData(TextAnchor.Right, -700)]
    public void AnchorOffset_ShiftsTheBlockRelativeToTheOrigin(TextAnchor anchor, double expected)
    {
        Assert.Equal(expected, TextMetrics.AnchorOffset(anchor, advanceWidth: 700));
    }

    [Fact]
    public void PortugalTracking_PutsTheMaskSpaceCellsExactlyOnTheSvgSeparatorDots()
    {
        // The base SVG draws its two separator dots at cx=368 and cx=625 as static art, so the
        // mask's space cells have to land centered on them. This is the constraint that fixes
        // Tracking/SpaceTracking for that template; if someone retunes them, this test says how.
        const double tracking = 95;
        const double spaceTracking = 67;
        const double blockCenter = 496.5;

        double advance = TextMetrics.AdvanceWidth("99 AA 99", tracking, spaceTracking);
        double left = blockCenter + TextMetrics.AnchorOffset(TextAnchor.Center, advance);

        // Cell layout: 2 chars, space, 2 chars, space, 2 chars.
        double firstSpaceCellStart = left + (2 * tracking);
        double secondSpaceCellStart = firstSpaceCellStart + spaceTracking + (2 * tracking);

        Assert.Equal(368, firstSpaceCellStart + (spaceTracking / 2), precision: 6);
        Assert.Equal(625, secondSpaceCellStart + (spaceTracking / 2), precision: 6);
    }
}
