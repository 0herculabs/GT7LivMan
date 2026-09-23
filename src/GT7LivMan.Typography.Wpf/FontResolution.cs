using System.Windows.Media;

namespace GT7LivMan.Typography.Wpf;

public enum FontSource
{
    /// <summary>Found in the user-managed fonts/ folder next to the exe — typically the real plate font, which we can't redistribute.</summary>
    UserFontsFolder,

    /// <summary>Found among the fonts already installed on this machine.</summary>
    System,

    /// <summary>Neither the preferred family nor its installed fallbacks were found; resolved to a bundled SIL-OFL substitute instead.</summary>
    BundledFallback,
}

public sealed record FontResolution(GlyphTypeface Typeface, FontSource Source, bool IsFallback);
