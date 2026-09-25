using System.IO;
using System.Windows;
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

        throw NotFound(preferredFamily, fallbackFamilies);
    }

    /// <summary>
    /// Every family in the list that resolves, preferred first then each fallback in order — for
    /// per-character fallback, where a glyph the first font lacks (FZ's empty せ, hyphen and dot)
    /// is taken from the next one that has it.
    /// </summary>
    public IReadOnlyList<FontResolution> ResolveAll(string preferredFamily, IReadOnlyList<string> fallbackFamilies)
    {
        var chain = new List<FontResolution>();
        if (TryResolveFamily(preferredFamily, out FontResolution preferred, isFallback: false))
        {
            chain.Add(preferred);
        }

        foreach (string fallback in fallbackFamilies)
        {
            if (TryResolveFamily(fallback, out FontResolution resolved, isFallback: true))
            {
                chain.Add(resolved);
            }
        }

        return chain.Count > 0 ? chain : throw NotFound(preferredFamily, fallbackFamilies);
    }

    private InvalidOperationException NotFound(string preferredFamily, IReadOnlyList<string> fallbackFamilies) => new(
        $"Could not resolve font '{preferredFamily}' or any fallback ({string.Join(", ", fallbackFamilies)}) " +
        $"from '{_fontsFolder}', installed system fonts, or '{_bundledFontsFolder}'.");

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

    /// <summary>
    /// An installed family, upright. A weight named in <paramref name="familyName"/> ("Yu Gothic
    /// Bold", which WPF resolves to the Yu Gothic family) picks that face; otherwise the regular
    /// one — not just whichever face the family lists first, which for Yu Gothic is Light.
    /// </summary>
    private static bool TryResolveFromSystem(string familyName, out GlyphTypeface typeface)
    {
        typeface = null!;

        // TryGetGlyphTypeface returns false for a synthesized bold/italic (no real glyph data) — skip those.
        var faces = new List<(Typeface Face, GlyphTypeface Glyphs)>();
        foreach (Typeface candidate in new FontFamily(familyName).GetTypefaces())
        {
            if (candidate.Style == FontStyles.Normal && candidate.TryGetGlyphTypeface(out GlyphTypeface? gt))
            {
                faces.Add((candidate, gt));
            }
        }

        if (faces.Count == 0)
        {
            return false;
        }

        FontWeight wanted = RequestedWeight(familyName);
        typeface = (faces.FirstOrDefault(f => f.Face.Weight == wanted).Glyphs
            ?? faces.FirstOrDefault(f => f.Face.Weight == FontWeights.Normal).Glyphs
            ?? faces[0].Glyphs);
        return true;
    }

    private static FontWeight RequestedWeight(string familyName)
    {
        string name = familyName.ToLowerInvariant();
        return name.EndsWith(" bold", StringComparison.Ordinal) ? FontWeights.Bold
            : name.EndsWith(" semibold", StringComparison.Ordinal) ? FontWeights.SemiBold
            : name.EndsWith(" medium", StringComparison.Ordinal) ? FontWeights.Medium
            : name.EndsWith(" light", StringComparison.Ordinal) ? FontWeights.Light
            : FontWeights.Normal;
    }

}
