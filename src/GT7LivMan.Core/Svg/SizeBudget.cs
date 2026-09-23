using System.Text;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// GT7's Decal Uploader hard-rejects any SVG at or above 15 KB. Community practice reports the
/// limit as decimal kilobytes (15000 bytes, not the binary 15360), so we measure the same way and
/// keep a safety margin below it.
/// </summary>
public static class SizeBudget
{
    public const int MaxBytes = 15_000;
    public const int TargetBytes = 14_000;

    public static int MeasureUtf8Bytes(string svg) => Encoding.UTF8.GetByteCount(svg);

    public static bool IsWithinGt7Limit(string svg) => MeasureUtf8Bytes(svg) < MaxBytes;

    public static bool IsWithinTarget(string svg) => MeasureUtf8Bytes(svg) <= TargetBytes;
}
