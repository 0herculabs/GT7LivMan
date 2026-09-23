using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Svg;

public class SvgFormatTests
{
    [Theory]
    [InlineData(0.5, 2, ".5")]
    [InlineData(-0.5, 2, "-.5")]
    [InlineData(1.5, 2, "1.5")]
    [InlineData(2.0, 2, "2")]
    [InlineData(0.0, 2, "0")]
    [InlineData(1.505, 2, "1.51")]
    [InlineData(1.504, 2, "1.5")]
    [InlineData(10, 0, "10")]
    [InlineData(-3.14159, 4, "-3.1416")]
    public void Number_StripsLeadingAndTrailingZeros(double value, int decimals, string expected)
    {
        Assert.Equal(expected, SvgFormat.Number(value, decimals));
    }

    [Fact]
    public void Number_NormalizesNegativeZero()
    {
        // Rounding a tiny negative value can land on IEEE -0.0; must format as "0", not "-0".
        Assert.Equal("0", SvgFormat.Number(-0.001, 2));
    }

    [Fact]
    public void Number_IsCultureInvariant_UnderSpanishLocale()
    {
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
            Assert.Equal("12.5", SvgFormat.Number(12.5, 2));
            Assert.DoesNotContain(',', SvgFormat.Number(12.5, 2));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
