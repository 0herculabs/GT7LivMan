using System.IO;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Typography.Wpf.Tests;

/// <summary>
/// The one test that exercises the whole MVP pipeline against real assets: a generated template,
/// its real font, WPF glyph outlining, and <see cref="ExportEscalation"/> — the same path the app
/// itself runs, including the automatic decimal/simplification fallback if a detailed font pushes
/// a plate over GT7's budget. Also drops the resulting SVG under out/ so it can be opened in a
/// browser or, ultimately, tried against the real GT7 Decal Uploader.
/// </summary>
public class EndToEndPlateExportTests
{
    private static string RepoRoot => RepoPaths.Root;

    [Fact]
    public void CompileAndExport_RealSpainTemplate_WithRealFont_ProducesConformantSvgWithinBudget()
    {
        string templateJson = File.ReadAllText(Path.Combine(RepoRoot, "templates", "es-long.json"));
        PlateDocument document = PlateDocumentSerializer.Deserialize(templateJson);

        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoRoot, "fonts")));
        var fieldValues = new Dictionary<string, string> { ["plateNumber"] = "1234 BBC" };

        Core.Rendering.Scene scene = SceneCompiler.Compile(document, fieldValues, outliner);
        string svg = ExportEscalation.Write(scene);

        Assert.True(
            SizeBudget.IsWithinTarget(svg),
            $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<image", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gradient", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version=\"1.1\"", svg, StringComparison.Ordinal);

        string outDir = Path.Combine(RepoRoot, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "es-long-sample.svg"), svg);
    }

    [Fact]
    public void CompileAndExport_RealPortugalTemplate_ProducesConformantSvgWithinBudget()
    {
        string templateJson = File.ReadAllText(Path.Combine(RepoRoot, "templates", "pt-long.json"));
        PlateDocument document = PlateDocumentSerializer.Deserialize(templateJson);

        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoRoot, "fonts"), Path.Combine(RepoRoot, "fonts-bundled")));
        var fieldValues = new Dictionary<string, string>
        {
            ["plateNumber"] = "12 AB 34",
            ["inspectionDate"] = "0212",
        };

        Core.Rendering.Scene scene = SceneCompiler.Compile(document, fieldValues, outliner);
        string svg = ExportEscalation.Write(scene);

        Assert.True(
            SizeBudget.IsWithinTarget(svg),
            $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stroke", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version=\"1.1\"", svg, StringComparison.Ordinal);

        string outDir = Path.Combine(RepoRoot, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "pt-long-sample.svg"), svg);
    }

    [Fact]
    public void CompileAndExport_RealPortugalV1Template_ProducesConformantSvgWithinBudget()
    {
        string templateJson = File.ReadAllText(Path.Combine(RepoRoot, "templates", "pt-v1.json"));
        PlateDocument document = PlateDocumentSerializer.Deserialize(templateJson);

        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoRoot, "fonts"), Path.Combine(RepoRoot, "fonts-bundled")));
        var fieldValues = new Dictionary<string, string> { ["plateNumber"] = "AA 00 AA" };

        Core.Rendering.Scene scene = SceneCompiler.Compile(document, fieldValues, outliner);
        string svg = ExportEscalation.Write(scene);

        Assert.True(
            SizeBudget.IsWithinTarget(svg),
            $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stroke", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version=\"1.1\"", svg, StringComparison.Ordinal);

        string outDir = Path.Combine(RepoRoot, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "pt-v1-sample.svg"), svg);
    }

    [Theory]
    [InlineData("uk-front.json", "uk-front-sample.svg")]
    [InlineData("uk-back.json", "uk-back-sample.svg")]
    public void CompileAndExport_RealUkTemplate_WithRealFont_ProducesConformantSvgWithinBudget(string templateFile, string outputFile)
    {
        string templateJson = File.ReadAllText(Path.Combine(RepoRoot, "templates", templateFile));
        PlateDocument document = PlateDocumentSerializer.Deserialize(templateJson);

        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoRoot, "fonts"), Path.Combine(RepoRoot, "fonts-bundled")));
        var fieldValues = new Dictionary<string, string> { ["plateNumber"] = "LC67 DKF" };

        Core.Rendering.Scene scene = SceneCompiler.Compile(document, fieldValues, outliner);
        string svg = ExportEscalation.Write(scene);

        Assert.True(
            SizeBudget.IsWithinTarget(svg),
            $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stroke", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version=\"1.1\"", svg, StringComparison.Ordinal);

        string outDir = Path.Combine(RepoRoot, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, outputFile), svg);
    }

    [Fact]
    public void CompileAndExport_RealNewYorkTemplate_WithRealFont_ProducesConformantSvgWithinBudget()
    {
        string templateJson = File.ReadAllText(Path.Combine(RepoRoot, "templates", "newyork.json"));
        PlateDocument document = PlateDocumentSerializer.Deserialize(templateJson);

        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoRoot, "fonts"), Path.Combine(RepoRoot, "fonts-bundled")));
        var fieldValues = new Dictionary<string, string> { ["plateNumber"] = "GXB 5332" };

        Core.Rendering.Scene scene = SceneCompiler.Compile(document, fieldValues, outliner);
        string svg = ExportEscalation.Write(scene);

        Assert.True(
            SizeBudget.IsWithinTarget(svg),
            $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stroke", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version=\"1.1\"", svg, StringComparison.Ordinal);

        string outDir = Path.Combine(RepoRoot, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "newyork-sample.svg"), svg);
    }

    [Fact]
    public void CompileAndExport_RealCaliforniaTemplate_WithRealFont_ProducesConformantSvgWithinBudget()
    {
        string templateJson = File.ReadAllText(Path.Combine(RepoRoot, "templates", "california.json"));
        PlateDocument document = PlateDocumentSerializer.Deserialize(templateJson);

        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoRoot, "fonts"), Path.Combine(RepoRoot, "fonts-bundled")));
        var fieldValues = new Dictionary<string, string>
        {
            ["plateNumber"] = "7YJL967",
            ["month"] = "JUN",
            ["year"] = "2020",
        };

        Core.Rendering.Scene scene = SceneCompiler.Compile(document, fieldValues, outliner);
        string svg = ExportEscalation.Write(scene);

        // The naive writer alone lands this one at ~14.1 KB (the cursive "California" wordmark and
        // LICENSE PLATE USA are both far more curve-detailed than this app's other templates) —
        // over the soft target on its own, but ExportEscalation's decimals/simplification tiers
        // bring every template to the same real margin rather than carving out an exception here.
        Assert.True(
            SizeBudget.IsWithinTarget(svg),
            $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");

        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stroke", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version=\"1.1\"", svg, StringComparison.Ordinal);

        string outDir = Path.Combine(RepoRoot, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "california-sample.svg"), svg);
    }

    [Fact]
    public void CompileAndExport_CaliforniaWorstCaseGlyph_StaysWithinBudget_ViaExportEscalation()
    {
        // LICENSE PLATE USA's '8' measured 84 segments across 3 subpaths (vs. '7''s 22/1) — a real,
        // legitimately-detailed glyph, not a bug. Seven of them plus a heavy month/year blows past
        // GT7's 15 KB limit at any decimal precision (21 KB even at 0 decimals) — only
        // ExportEscalation's Douglas-Peucker simplification step actually recovers it.
        string templateJson = File.ReadAllText(Path.Combine(RepoRoot, "templates", "california.json"));
        PlateDocument document = PlateDocumentSerializer.Deserialize(templateJson);

        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoRoot, "fonts"), Path.Combine(RepoRoot, "fonts-bundled")));
        var fieldValues = new Dictionary<string, string>
        {
            ["plateNumber"] = "8888888",
            ["month"] = "SSS",
            ["year"] = "8888",
        };

        Core.Rendering.Scene scene = SceneCompiler.Compile(document, fieldValues, outliner);

        string naive = SvgWriter.Write(scene);
        Assert.False(SizeBudget.IsWithinGt7Limit(naive), "This case is expected to overflow the naive writer — otherwise the escalation path below isn't actually being exercised.");

        string escalated = ExportEscalation.Write(scene);
        Assert.True(
            SizeBudget.IsWithinGt7Limit(escalated),
            $"Expected < {SizeBudget.MaxBytes} bytes after escalation, got {SizeBudget.MeasureUtf8Bytes(escalated)}.");
        Assert.DoesNotContain("<text", escalated, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version=\"1.1\"", escalated, StringComparison.Ordinal);
    }

    [Fact]
    public void FontResolver_ResolvesTheRealLicensePlateUsaFont_FromTheFontsFolder()
    {
        var resolver = new FontResolver(Path.Combine(RepoRoot, "fonts"));
        FontResolution resolution = resolver.Resolve("LICENSE PLATE USA", ["Roboto Condensed", "Overpass"]);

        Assert.Equal(FontSource.UserFontsFolder, resolution.Source);
        Assert.False(resolution.IsFallback);
    }

    [Fact]
    public void FontResolver_ResolvesTheRealCharlesWrightFont_FromTheFontsFolder()
    {
        var resolver = new FontResolver(Path.Combine(RepoRoot, "fonts"));
        FontResolution resolution = resolver.Resolve("CharlesWright-Bold", ["Roboto Condensed", "Overpass"]);

        Assert.Equal(FontSource.UserFontsFolder, resolution.Source);
        Assert.False(resolution.IsFallback);
    }

    [Fact]
    public void FontResolver_ResolvesTheRealMespregFont_FromTheFontsFolder()
    {
        var resolver = new FontResolver(Path.Combine(RepoRoot, "fonts"));
        FontResolution resolution = resolver.Resolve("MESPREG", ["Overpass", "Roboto Condensed"]);

        Assert.Equal(FontSource.UserFontsFolder, resolution.Source);
        Assert.False(resolution.IsFallback);
    }

    [Fact]
    public void FontResolver_FallsBackWhenThePreferredFamilyIsMissingEverywhere()
    {
        var resolver = new FontResolver(Path.Combine(RepoRoot, "fonts"));
        FontResolution resolution = resolver.Resolve("ThisFontDoesNotExist12345", ["Arial"]);

        Assert.True(resolution.IsFallback);
    }

    [Fact]
    public void WpfGlyphOutliner_OutlinesARealPlateNumber_WithNonEmptyGlyphGeometry()
    {
        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoRoot, "fonts")));
        var glyphs = outliner.Outline("1234 BBC", "MESPREG", ["Overpass", "Roboto Condensed"], charHeight: 237, tracking: 173, spaceTracking: 86);

        Assert.True(glyphs.Count >= 7, $"Expected at least 7 glyphs (space may be skipped by the font), got {glyphs.Count}.");
        foreach (var glyph in glyphs.Where(g => g.SubPaths.Count > 0))
        {
            Assert.NotEmpty(glyph.SubPaths[0].Segments);
        }
    }
}
