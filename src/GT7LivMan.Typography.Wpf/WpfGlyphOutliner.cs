using System.Windows;
using System.Windows.Media;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;

namespace GT7LivMan.Typography.Wpf;

/// <summary>
/// Renders a field's text as positioned glyph outlines using WPF's <see cref="GlyphTypeface"/>.
/// Advances the pen by the template's own fixed <c>tracking</c> pitch rather than the font's
/// AdvanceWidths — plate character spacing is set by regulation, not by font metrics.
/// </summary>
public sealed class WpfGlyphOutliner(FontResolver fontResolver) : ITextOutliner
{
    /// <summary>How much of a cell's width a glyph's ink may occupy before it gets condensed to fit — measured off real plates, characters sit at roughly 0.76-0.85 of their cell.</summary>
    private const double MaxInkWidthFraction = 0.86;

    private readonly FontResolver _fontResolver = fontResolver;
    private readonly Dictionary<string, IReadOnlyList<GlyphTypeface>> _chainCache = new(StringComparer.Ordinal);

    public IReadOnlyList<PathData> Outline(
        string text, string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies, double charHeight, double tracking, double spaceTracking)
    {
        IReadOnlyList<GlyphTypeface> chain = ResolveChainCached(preferredFontFamily, fallbackFontFamilies);

        var result = new List<PathData>(text.Length);
        double penX = 0;

        foreach (char ch in text)
        {
            double cellWidth = TextMetrics.Advance(ch, tracking, spaceTracking);

            if (!char.IsWhiteSpace(ch) && TryFindGlyph(chain, ch, out GlyphTypeface typeface, out Geometry glyphGeometry))
            {
                // A font reporting CapsHeight <= 0 (malformed metrics) falls back to a plausible
                // default rather than dividing by zero and producing an infinitely large glyph.
                // Scaled per font, so a character taken from a fallback lines up with the rest.
                double capsHeight = typeface.CapsHeight > 0 ? typeface.CapsHeight : 0.7;
                double scale = charHeight / capsHeight;

                // Outline at unit em size: coordinates come out as a fraction of em, so scaling by
                // (charHeight / capsHeight) maps the font's own cap height exactly onto charHeight.
                PathData local = WpfGeometryConverter.Convert(glyphGeometry);
                result.Add(PathDataTransform.Apply(local, PlaceInCell(glyphGeometry.Bounds, penX, cellWidth, scale)));
            }

            // A literal mask separator (an invisible space, or a visible dash) gets its own,
            // narrower pitch — see FieldDef.SpaceTracking.
            penX += cellWidth;
        }

        return result;
    }

    private static double CondenseFactor(Rect ink, double cellWidth, double scale)
    {
        if (ink.IsEmpty)
        {
            return 1.0;
        }

        double inkWidth = ink.Width * scale;
        double maxInkWidth = cellWidth * MaxInkWidthFraction;
        return inkWidth > maxInkWidth ? maxInkWidth / inkWidth : 1.0;
    }

    /// <summary>
    /// Fits one glyph into its fixed-pitch cell: centers the ink, and condenses it horizontally if
    /// it would otherwise overflow.
    /// <para>
    /// A real plate font (DIN 1451, MESPREG) is designed so every A-Z0-9 fits a constant cell — that
    /// uniform grid is what makes a plate read as a plate, and Portugal's separator dots are
    /// positioned against it. A general-purpose fallback font is proportional instead, so a "W" or
    /// "M" runs far wider than the cell and would collide with its neighbours and throw the row's
    /// alignment off. Squeezing only the offenders keeps the grid intact; for a real plate font this
    /// never triggers.
    /// </para>
    /// </summary>
    private static Affine2 PlaceInCell(Rect ink, double penX, double cellWidth, double scale)
    {
        if (ink.IsEmpty)
        {
            return Affine2.Translate(penX, 0) * Affine2.Scale(scale, scale);
        }

        double condense = CondenseFactor(ink, cellWidth, scale);
        double finalInkWidth = ink.Width * scale * condense;
        double finalInkLeft = ink.X * scale * condense;
        double offset = ((cellWidth - finalInkWidth) / 2) - finalInkLeft;

        return Affine2.Translate(penX + offset, 0) * Affine2.Scale(scale * condense, scale);
    }

    /// <summary>
    /// The first font in <paramref name="chain"/> that has actual ink for <paramref name="ch"/>. A
    /// font can map a character to an empty glyph rather than leave it out (FZ's plate font does
    /// that for せ, the hyphen and the dot), which should fall through just like a missing one.
    /// </summary>
    private static bool TryFindGlyph(IReadOnlyList<GlyphTypeface> chain, char ch, out GlyphTypeface typeface, out Geometry glyph)
    {
        foreach (GlyphTypeface candidate in chain)
        {
            if (candidate.CharacterToGlyphMap.TryGetValue(ch, out ushort glyphIndex))
            {
                Geometry geometry = candidate.GetGlyphOutline(glyphIndex, 1.0, 1.0);
                if (!geometry.Bounds.IsEmpty)
                {
                    typeface = candidate;
                    glyph = geometry;
                    return true;
                }
            }
        }

        typeface = null!;
        glyph = null!;
        return false;
    }

    private IReadOnlyList<GlyphTypeface> ResolveChainCached(string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies)
    {
        string key = preferredFontFamily + "|" + string.Join(',', fallbackFontFamilies);
        if (_chainCache.TryGetValue(key, out IReadOnlyList<GlyphTypeface>? cached))
        {
            return cached;
        }

        IReadOnlyList<GlyphTypeface> resolved = [.. _fontResolver.ResolveAll(preferredFontFamily, fallbackFontFamilies).Select(r => r.Typeface)];
        _chainCache[key] = resolved;
        return resolved;
    }
}
