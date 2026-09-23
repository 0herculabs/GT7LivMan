using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Rendering;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// Writes a <see cref="Scene"/> to SVG, retrying with progressively more aggressive size reduction
/// if the result doesn't fit GT7's 15 KB limit: fewer decimal places first (cheap, no shape change),
/// then Douglas-Peucker path simplification as a last resort (a visible-if-you-look-closely
/// trade-off, but the alternative is an export GT7 flatly rejects).
/// </summary>
public static class ExportEscalation
{
    /// <summary>
    /// Simplification tolerances to try in order, in the scene's own units. Each retry roughly
    /// doubles the previous one — a single fixed tolerance left as little as 32 bytes of margin
    /// under GT7's 15 KB limit for one real combination (California's "AAAAAAA"), too close for
    /// comfort against any future change in the exact byte count, so this keeps escalating until
    /// there's real headroom rather than stopping at "technically under."
    /// </summary>
    private static readonly double[] SimplifyTolerances = [2.0, 4.0, 8.0, 16.0];

    public static string Write(Scene scene)
    {
        // Every tier below targets the soft 14 KB budget, not just the hard 15 KB GT7 limit —
        // "technically under" with only a few dozen bytes of margin isn't good enough; escalating
        // further whenever there's room to is what actually gives every export real headroom.
        string svg = SvgWriter.Write(scene, SvgWriterOptions.Default);
        if (SizeBudget.IsWithinTarget(svg))
        {
            return svg;
        }

        var noDecimals = SvgWriterOptions.Default with { Decimals = 0 };
        svg = SvgWriter.Write(scene, noDecimals);
        if (SizeBudget.IsWithinTarget(svg))
        {
            return svg;
        }

        string bestEffort = svg;
        foreach (double tolerance in SimplifyTolerances)
        {
            Scene simplified = scene with
            {
                Elements = [.. scene.Elements.Select(e => e with { Path = PathDataSimplifier.Simplify(e.Path, tolerance) })],
            };
            bestEffort = SvgWriter.Write(simplified, noDecimals);
            if (SizeBudget.IsWithinTarget(bestEffort))
            {
                return bestEffort;
            }
        }

        // Every tolerance tried and still over the soft target: return the most-simplified attempt
        // — by far the smallest of everything computed above — even if it didn't clear the target.
        return bestEffort;
    }
}
