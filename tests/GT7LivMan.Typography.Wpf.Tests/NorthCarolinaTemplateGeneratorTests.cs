using System.IO;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Typography.Wpf.Tests;

/// <summary>
/// Builds templates/northcarolina.json from northcarolina_plate.svg. Lives here, next to Mexico,
/// because the SVG has live text, a clip-path and translucent layers (<see cref="DesignSvgFlattener"/>).
/// The plate number is free vanity text: up to 8 of any character — letters, digits, symbols,
/// spaces — in LICENSE PLATE USA, blue.
/// </summary>
public class NorthCarolinaTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // Measured off a screenshot of the real plate (same typeface), then scaled to this SVG's
    // 1729x849 plate: characters 47% of the plate's height (398; LICENSE PLATE USA's ink is its
    // cap height), pitch 0.5 of that (199, the font's own proportions) for every character,
    // spaces and symbols included, so any 8 fit the same 8 cells. Centered between "IN GOD WE
    // TRUST" (baseline 195) and the motto (top ~684), as on the real plate.
    private const double CharHeight = 398;
    private const double Pitch = 199;
    private const double Baseline = 646;

    /// <summary>
    /// Generation-time simplification of the detailed art (outlined text, flag curves), in canvas
    /// units — about the same share of the plate as Mexico's 1 unit on its 760-wide canvas.
    /// </summary>
    private const double ArtTolerance = 2;

    private static readonly RgbColor Ink = RgbColor.Parse("#0238a0"); // sampled from the screenshot

    private static PlateDocument BuildNorthCarolinaPlate()
    {
        string svgPath = Path.Combine(RepoPaths.Root, "assets", "base", "northcarolina_plate.svg");
        var elements = DesignSvgFlattener.Flatten(File.ReadAllText(svgPath))
            .Select(s => (Element)new Element.Shape(DesignSvgFlattener.IsDetailed(s.Path) ? PathDataSimplifier.Simplify(s.Path, ArtTolerance) : s.Path, s.Fill))
            .ToList();

        elements.Add(new Element.TextField(PlateNumberFieldId, Ink, Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(879, Baseline),
        });

        var field = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            Mask: "********",
            AllowedLetters: FullAlphabet,
            CharHeight: CharHeight,
            Tracking: Pitch,
            SpaceTracking: Pitch,
            PreferredFontFamily: "LICENSE PLATE USA",
            // Also where the symbols LICENSE PLATE USA doesn't have ('?', '(', ...) come from.
            FallbackFontFamilies: ["Roboto Condensed", "Overpass"],
            // LICENSE PLATE USA's '-' is a "DO NOT ENTER" sign; a plate's dash is just a bar, at
            // mid-height. Its other pictograms (# eagle, & paw, + airplane...) are kept as they are.
            GlyphShapes: new Dictionary<string, PathData>
            {
                ["-"] = Shapes.RoundedRect(-50, -(CharHeight / 2) - 19, 100, 38, 6),
            });

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "US-NC",
            DisplayName: "USA - North Carolina",
            Size: new SizeMm(1757, 877),
            Elements: elements,
            Fields: [field]);
    }

    [Fact]
    public void GenerateAndWrite_NorthCarolinaTemplate()
    {
        string outputPath = Path.Combine(RepoPaths.Root, "templates", "northcarolina.json");
        File.WriteAllText(outputPath, PlateDocumentSerializer.Serialize(BuildNorthCarolinaPlate()));

        Assert.True(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("12345678", true)]
    [InlineData("GO HEELS", true)] // spaces are characters too
    [InlineData("NC-2026!", true)] // and symbols
    [InlineData("A", true)] // shorter than 8 is fine
    [InlineData("123456789", false)] // more than 8 isn't
    [InlineData("", false)]
    public void PlateNumber_IsUpTo8OfAnyCharacter(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(BuildNorthCarolinaPlate().Fields[0], value));
    }

    [Theory]
    [InlineData("go heels", "GO HEELS")]
    [InlineData("nc-2026!", "NC-2026!")]
    [InlineData("123456789", "12345678")]
    public void AutoFormat_KeepsSpacesAndSymbols_AndCapsAt8(string rawInput, string expected)
    {
        Assert.Equal(expected, MaskFormatter.AutoFormat(BuildNorthCarolinaPlate().Fields[0], rawInput));
    }

    [Fact]
    public void FlattenedArt_HasNoTextClipOrTranslucency_AndKeepsTheSvgColors()
    {
        var fills = BuildNorthCarolinaPlate().Elements.OfType<Element.Shape>().Select(s => s.Fill).ToHashSet();

        Assert.Contains(RgbColor.Parse("#182a66"), fills); // IN GOD WE TRUST
        Assert.Contains(RgbColor.Parse("#e41d2d"), fills); // NORTH CAROLINA
        Assert.Contains(RgbColor.Parse("#9aadc3"), fills); // flag's blue field
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("GO HEELS")]
    [InlineData("NC-2026!")]
    [InlineData("88888888")] // LICENSE PLATE USA's 8 is its most detailed glyph
    [InlineData("#&+*@`$/")] // its pictograms
    public void CompileAndExport_WithRealFont_StaysWithinGt7Limit(string plate)
    {
        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoPaths.Root, "fonts"), Path.Combine(RepoPaths.Root, "fonts-bundled")));
        Scene scene = SceneCompiler.Compile(BuildNorthCarolinaPlate(), new Dictionary<string, string> { [PlateNumberFieldId] = plate }, outliner);
        string svg = ExportEscalation.Write(scene);

        Assert.True(SizeBudget.IsWithinGt7Limit(svg), $"Expected < {SizeBudget.MaxBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clip", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("opacity", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("filter", svg, StringComparison.OrdinalIgnoreCase);

        string outDir = Path.Combine(RepoPaths.Root, "out");
        Directory.CreateDirectory(outDir);
        string safeName = string.Concat(plate.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        File.WriteAllText(Path.Combine(outDir, $"northcarolina-{safeName}.svg"), svg);
    }
}
