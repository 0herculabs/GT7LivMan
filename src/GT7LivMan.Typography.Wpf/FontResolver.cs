using System.IO;
using System.Windows.Media;

namespace GT7LivMan.Typography.Wpf;

/// <summary>
/// Resolves a font family in order: the user-managed fonts/ folder next to the exe, then fonts
/// already installed on the system, then a bundled SIL-OFL fallback. Never bundles a real plate
/// font (FE-Schrift, Charles Wright, MESPREG, ...) — those aren't ours to redistribute.
/// </summary>
public sealed class FontResolver(string? fontsFolder = null, string? bundledFontsFolder = null)
{
    private readonly string _fontsFolder = fontsFolder ?? Path.Combine(AppContext.BaseDirectory, "fonts");

    // Ships with the app (tracked in git), unlike fonts/ which is user-managed and gitignored —
    // the genuine last-resort fallback so a template never fails to render just because its real
    // plate font isn't installed.
    private readonly string _bundledFontsFolder = bundledFontsFolder ?? Path.Combine(AppContext.BaseDirectory, "fonts-bundled");

    /// <summary>
    /// Tries the preferred family first, then each fallback in turn; within each, the user's
    /// fonts/ folder, then installed system fonts, then the bundled folder. <see cref="FontResolution.Source"/>
    /// reports where it actually came from and <see cref="FontResolution.IsFallback"/> whether the
    /// preferred family had to be given up on — those are independent answers.
    /// </summary>
    public FontResolution Resolve(string preferredFamily, IReadOnlyList<string> fallbackFamilies)
    {
        if (TryResolveFamily(preferredFamily, out FontResolution preferred, isFallback: false))
        {
            return preferred;
        }

        foreach (string fallback in fallbackFamilies)
        {
            if (TryResolveFamily(fallback, out FontResolution resolved, isFallback: true))
            {
                return resolved;
            }
        }

        throw new InvalidOperationException(
            $"Could not resolve font '{preferredFamily}' or any fallback ({string.Join(", ", fallbackFamilies)}) " +
            $"from '{_fontsFolder}', installed system fonts, or '{_bundledFontsFolder}'.");
    }

    private bool TryResolveFamily(string familyName, out FontResolution resolution, bool isFallback)
    {
        if (TryResolveFromFolder(_fontsFolder, familyName, out GlyphTypeface? typeface))
        {
            resolution = new FontResolution(typeface, FontSource.UserFontsFolder, isFallback);
            return true;
        }

        if (TryResolveFromSystem(familyName, out typeface))
        {
            resolution = new FontResolution(typeface, FontSource.System, isFallback);
            return true;
        }

        if (TryResolveFromFolder(_bundledFontsFolder, familyName, out typeface))
        {
            resolution = new FontResolution(typeface, FontSource.BundledFallback, isFallback);
            return true;
        }

        resolution = null!;
        return false;
    }

    /// <summary>
    /// Looks for a font file in <paramref name="folder"/>, most specific match first: an exact
    /// filename, then an exact internal family name, then a normalized "contains" match on either.
    /// That last pass is what lets a template asking for "DIN1451" find a file the user dropped in
    /// as "DIN1451-36breit.ttf" (whose family name is actually "DIN 1451 fette Breitschrift 1936")
    /// — nobody should have to rename a font to make it work.
    /// </summary>
    private static bool TryResolveFromFolder(string folder, string familyName, out GlyphTypeface typeface)
    {
        typeface = null!;
        if (!Directory.Exists(folder))
        {
            return false;
        }

        string[] files = [.. Directory.EnumerateFiles(folder)
            .Where(f => f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];

        foreach (string file in files)
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(file), familyName, StringComparison.OrdinalIgnoreCase))
            {
                typeface = Load(file);
                return true;
            }
        }

        foreach (string file in files)
        {
            if (FamilyNamesOf(file).Any(n => string.Equals(n, familyName, StringComparison.OrdinalIgnoreCase)))
            {
                typeface = Load(file);
                return true;
            }
        }

        string wanted = Normalize(familyName);
        foreach (string file in files)
        {
            bool matches = Normalize(Path.GetFileNameWithoutExtension(file)).Contains(wanted, StringComparison.Ordinal)
                || FamilyNamesOf(file).Any(n => Normalize(n).Contains(wanted, StringComparison.Ordinal));
            if (matches)
            {
                typeface = Load(file);
                return true;
            }
        }

        return false;
    }

    // GlyphTypeface memory-maps the file for the app's lifetime, so replacing a font on disk while
    // the app is running won't be picked up without a restart.
    private static GlyphTypeface Load(string file) => new(new Uri(Path.GetFullPath(file), UriKind.Absolute));

    private static IEnumerable<string> FamilyNamesOf(string file)
    {
        try
        {
            return Load(file).FamilyNames.Values;
        }
        catch (Exception ex) when (ex is FileFormatException or NotSupportedException or IOException)
        {
            // A stray or unreadable file in a user-managed folder shouldn't break resolution.
            return [];
        }
    }

    private static string Normalize(string name) =>
        new([.. name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    private static bool TryResolveFromSystem(string familyName, out GlyphTypeface typeface)
    {
        typeface = null!;
        var family = new FontFamily(familyName);
        foreach (Typeface candidate in family.GetTypefaces())
        {
            // TryGetGlyphTypeface returns false for a synthesized bold/italic (no real glyph data) — skip those.
            if (candidate.TryGetGlyphTypeface(out GlyphTypeface? gt))
            {
                typeface = gt;
                return true;
            }
        }

        return false;
    }

}
