using System.IO;

namespace GT7LivMan.Typography.Wpf.Tests;

/// <summary>
/// Verifies the actual bundled fallback font (fonts-bundled/Overpass.ttf) resolves and produces
/// real glyph outlines — this is what every template's font resolution ultimately falls back to
/// when neither the real plate font nor a system-installed one is available, so a silent failure
/// here (e.g. WPF mishandling a variable font's named instances) would break every template at once.
/// </summary>
public class BundledFallbackFontTests
{
    private static string BundledFontsFolder => Path.Combine(RepoPaths.Root, "fonts-bundled");

    [Fact]
    public void FontResolver_ResolvesOverpass_FromTheBundledFolder_WhenNothingElseIsAvailable()
    {
        // No user fonts/ folder and a preferred family that doesn't exist anywhere -> must fall
        // through to the bundled folder rather than throwing.
        var resolver = new FontResolver(fontsFolder: Path.Combine(RepoPaths.Root, "does-not-exist"), bundledFontsFolder: BundledFontsFolder);

        FontResolution resolution = resolver.Resolve("ThisFontDoesNotExist12345", ["Overpass"]);

        Assert.Equal(FontSource.BundledFallback, resolution.Source);
        Assert.True(resolution.IsFallback);
    }

    [Fact]
    public void WpfGlyphOutliner_OutlinesText_UsingTheBundledFallbackFont()
    {
        var resolver = new FontResolver(fontsFolder: Path.Combine(RepoPaths.Root, "does-not-exist"), bundledFontsFolder: BundledFontsFolder);
        var outliner = new WpfGlyphOutliner(resolver);

        var glyphs = outliner.Outline("AB12", "ThisFontDoesNotExist12345", ["Overpass"], charHeight: 100, tracking: 80, spaceTracking: 80);

        Assert.Equal(4, glyphs.Count);
        Assert.All(glyphs, g => Assert.NotEmpty(g.SubPaths));
        Assert.All(glyphs, g => Assert.NotEmpty(g.SubPaths[0].Segments));
    }
}
