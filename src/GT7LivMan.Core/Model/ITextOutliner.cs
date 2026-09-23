using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Model;

/// <summary>
/// Turns a field's text value into positioned glyph outlines. Implemented outside Core (against
/// WPF's GlyphTypeface) so Core stays free of a UI framework dependency; <see cref="SceneCompiler"/>
/// only depends on this interface.
/// </summary>
public interface ITextOutliner
{
    /// <summary>
    /// Outlines <paramref name="text"/> left to right starting at the origin with the baseline on
    /// the X axis, one <see cref="PathData"/> per glyph already positioned by a fixed per-character
    /// pitch: <paramref name="tracking"/> after a drawn glyph, <paramref name="spaceTracking"/>
    /// after a literal whitespace character. A character the font (and its fallbacks) can't render
    /// is simply omitted from the result — rejecting that character up front is
    /// <see cref="MaskValidator"/>'s job, not this one's.
    /// </summary>
    IReadOnlyList<PathData> Outline(
        string text,
        string preferredFontFamily,
        IReadOnlyList<string> fallbackFontFamilies,
        double charHeight,
        double tracking,
        double spaceTracking);
}
