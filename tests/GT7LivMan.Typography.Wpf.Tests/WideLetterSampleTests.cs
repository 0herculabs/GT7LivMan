using System.IO;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Typography.Wpf.Tests;

/// <summary>
/// Exports deliberately worst-case samples (W and M are the widest glyphs in any font) under out/
/// for visual checking, and asserts they still respect the GT7 budget. These are the cases where a
/// fixed-pitch cell grid plus a proportional fallback font goes wrong, so they're worth keeping an
/// eye on whenever the typography changes.
/// </summary>
public class WideLetterSampleTests
{
    private static string RepoRoot => RepoPaths.Root;

    private static WpfGlyphOutliner Outliner => new(new FontResolver(
        Path.Combine(RepoRoot, "fonts"),
        Path.Combine(RepoRoot, "fonts-bundled")));

    [Theory]
    [InlineData("es-long.json", "es-wide-letters.svg", "plateNumber", "1234 WWW", null, null)]
    [InlineData("pt-long.json", "pt-wide-letters.svg", "plateNumber", "12 WM 34", "inspectionDate", "0212")]
    [InlineData("pt-v1.json", "pt-v1-wide-letters.svg", "plateNumber", "WM 00 WM", null, null)]
    [InlineData("uk-front.json", "uk-wide-letters.svg", "plateNumber", "WWWW WWW", null, null)]
    [InlineData("california.json", "california-wide-letters.svg", "plateNumber", "WWWWWWW", "month", "SEP")]
    public void Export_WideLetterSample_StaysWithinBudget(
        string templateFile, string outputFile, string field1, string value1, string? field2, string? value2)
    {
        PlateDocument document = PlateDocumentSerializer.Deserialize(
            File.ReadAllText(Path.Combine(RepoRoot, "templates", templateFile)));

        var fieldValues = new Dictionary<string, string> { [field1] = value1 };
        if (field2 is not null && value2 is not null)
        {
            fieldValues[field2] = value2;
        }

        Core.Rendering.Scene scene = SceneCompiler.Compile(document, fieldValues, Outliner);
        string svg = SvgWriter.Write(scene);

        Assert.True(
            SizeBudget.IsWithinTarget(svg),
            $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");

        string outDir = Path.Combine(RepoRoot, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, outputFile), svg);
    }

    [Fact]
    public void Outline_WideGlyph_IsCondensedToFitItsCell()
    {
        // "W" in a proportional fallback font is wider than a plate cell; it must be squeezed
        // rather than allowed to overflow into the neighbouring character.
        const double tracking = 95;
        var glyphs = Outliner.Outline("W", "DIN1451", ["Roboto Condensed", "Overpass"], charHeight: 142, tracking: tracking, spaceTracking: tracking);

        var glyph = Assert.Single(glyphs);
        double min = double.MaxValue;
        double max = double.MinValue;
        foreach (var sub in glyph.SubPaths)
        {
            foreach (var pt in new[] { sub.Start })
            {
                min = Math.Min(min, pt.X);
                max = Math.Max(max, pt.X);
            }

            foreach (var seg in sub.Segments)
            {
                if (seg is Core.Geometry.Seg.Line l)
                {
                    min = Math.Min(min, l.To.X);
                    max = Math.Max(max, l.To.X);
                }
            }
        }

        Assert.True(max - min <= tracking, $"W ink width {max - min:F1} must fit within the {tracking} cell.");
        // And it must sit centered in the cell, not hard against one edge.
        Assert.Equal(tracking / 2, (min + max) / 2, precision: 0);
    }
}
