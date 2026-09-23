using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Svg;

public class SizeBudgetTests
{
    [Fact]
    public void MeasureUtf8Bytes_CountsMultiByteCharactersCorrectly()
    {
        // "ñ" is 2 bytes in UTF-8, not 1 — String.Length (UTF-16 code units) would under-measure this.
        Assert.Equal(3, SizeBudget.MeasureUtf8Bytes("añ"));
    }

    [Fact]
    public void IsWithinGt7Limit_RejectsAtExactly15000Bytes()
    {
        string exactly15000 = new('a', 15_000);
        Assert.False(SizeBudget.IsWithinGt7Limit(exactly15000));
    }

    [Fact]
    public void IsWithinGt7Limit_AcceptsOneByteUnderLimit()
    {
        string under = new('a', 14_999);
        Assert.True(SizeBudget.IsWithinGt7Limit(under));
    }

    [Fact]
    public void IsWithinTarget_AcceptsExactlyAtTarget()
    {
        string atTarget = new('a', 14_000);
        Assert.True(SizeBudget.IsWithinTarget(atTarget));
    }

    [Fact]
    public void IsWithinTarget_RejectsOneByteOverTarget()
    {
        string overTarget = new('a', 14_001);
        Assert.False(SizeBudget.IsWithinTarget(overTarget));
    }
}
