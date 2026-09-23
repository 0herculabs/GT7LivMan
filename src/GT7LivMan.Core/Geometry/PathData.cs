namespace GT7LivMan.Core.Geometry;

/// <summary>The geometry behind a single SVG &lt;path&gt; (or, pre-compilation, a document element).</summary>
public sealed record PathData(IReadOnlyList<SubPath> SubPaths, FillRule Rule)
{
    public static PathData Single(SubPath subPath, FillRule rule = FillRule.NonZero) => new([subPath], rule);
}
