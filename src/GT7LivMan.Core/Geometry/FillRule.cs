namespace GT7LivMan.Core.Geometry;

public enum FillRule
{
    /// <summary>SVG's default; the only rule GT7LivMan emits. Glyph outlines and our own shapes both wind this way.</summary>
    NonZero,

    /// <summary>
    /// Not supported by <see cref="Svg.SvgWriter"/> — it throws rather than silently mis-rendering
    /// holes (e.g. the counters in "O", "A", "8"). Kept as an enum member so callers/parsers can
    /// represent and reject it explicitly instead of defaulting incorrectly.
    /// </summary>
    EvenOdd,
}
