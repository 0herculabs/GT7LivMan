using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/es-long.json from the real base SVG plus a hand-placed field for the plate
/// number (the base art has no sample characters — those are user input, rendered from
/// fonts/MESPREG.otf once GT7LivMan.Typography.Wpf exists). Regenerates the file on every run, which
/// is intentional: the template stays in sync with the source SVG automatically.
/// </summary>
public class SpainTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";

    // The official DGT plate alphabet excludes vowels, Ñ, and Q, but this is a decal creator for a
    // game, not a DGT registration tool — deliberately permissive: the full A-Z alphabet, so users
    // can make up any combination they like. Ñ is left out only because it falls outside the plain
    // A-Z typing flow the mask/keyboard assume, not for any plate-realism reason.
    private const string SpainAllowedLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static string BaseSvgPath => Path.Combine(RepoPaths.Root, "assets", "base", "spain_licenseplate_v1.svg");
    private static string TemplateOutputPath => Path.Combine(RepoPaths.Root, "templates", "es-long.json");

    private static PlateDocument BuildSpainLongPlate()
    {
        var imported = SvgReader.ReadStaticShapes(File.ReadAllText(BaseSvgPath));

        var elements = imported
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // Position and size calibrated against a real Spanish plate photo (68d4ee8a20b5e.jpg,
        // "4980 LPZ"), measured in pixels and mapped onto this template's inner-rect coordinate
        // space (x:[8,1583], y:[8,346], i.e. scale = 1575/670 horizontally, 338/142 vertically
        // against the photo's measured interior). See the Tracking/SpaceTracking/CharHeight
        // comments below for the numbers that measurement produced.
        // Center-anchored so the block stays put whichever characters are typed (a plate full of
        // narrow "1"s would otherwise sit differently than one full of wide "8"s). X is the
        // measured block center: the reference photo's ink ran 226.6..1479.7 in these units, and
        // allowing for the glyphs' side bearings that puts the cell block's center at ~853.
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#171717"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(853, 294),
        });

        var field = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            Mask: "9999 AAA",
            AllowedLetters: SpainAllowedLetters,
            // Ink height measured 99-100px across all 7 characters out of a 142px-tall interior
            // (~70%) — notably taller than our earlier guess of 190 (56%).
            CharHeight: 237,
            // Fixed absolute pitch per character slot (not the font's own AdvanceWidths — see
            // FieldDef.Tracking): measured as the average start-to-start distance between
            // same-group characters in the reference photo (171.6-176.3px raw, ~172.5px average),
            // mapped to this coordinate space.
            Tracking: 173,
            // The literal mask space between digit and letter groups: measured as the '0'-to-'L'
            // pitch (258.6, mapped) minus Tracking. Smaller than Tracking because Tracking already
            // carries a full glyph's width plus its own trailing gap — the invisible space only
            // needs to add the extra gap on top of that.
            SpaceTracking: 86,
            PreferredFontFamily: "MESPREG",
            // Condensed first: cell widths are calibrated for a narrow plate font, and a wide
            // fallback overflows its cell into the next character.
            FallbackFontFamilies: ["Roboto Condensed", "Overpass"]);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "ES",
            DisplayName: "Spain - License v1",
            Size: new SizeMm(1591, 354), // native template units; mapped to real mm by the app at export time
            Elements: elements,
            Fields: [field]);
    }

    [Fact]
    public void GenerateAndWrite_EsLongTemplate()
    {
        PlateDocument document = BuildSpainLongPlate();
        string json = PlateDocumentSerializer.Serialize(document);

        Directory.CreateDirectory(Path.GetDirectoryName(TemplateOutputPath)!);
        File.WriteAllText(TemplateOutputPath, json);

        Assert.True(File.Exists(TemplateOutputPath));
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsShapeAndFieldCounts()
    {
        PlateDocument original = BuildSpainLongPlate();
        string json = PlateDocumentSerializer.Serialize(original);
        PlateDocument reloaded = PlateDocumentSerializer.Deserialize(json);

        Assert.Equal(original.SchemaVersion, reloaded.SchemaVersion);
        Assert.Equal(original.CountryCode, reloaded.CountryCode);
        Assert.Equal(original.Elements.Count, reloaded.Elements.Count);
        Assert.Equal(original.Fields.Count, reloaded.Fields.Count);
        Assert.Equal(original.Fields[0].Mask, reloaded.Fields[0].Mask);
        Assert.IsType<Element.TextField>(reloaded.Elements[^1]);
    }

    [Fact]
    public void MaskValidator_AcceptsVowelsAndAnyLetter_ButStillEnforcesShape()
    {
        FieldDef field = BuildSpainLongPlate().Fields[0];

        Assert.True(MaskValidator.IsValid(field, "1234 BBC"));
        Assert.True(MaskValidator.IsValid(field, "1234 AAA")); // vowels are allowed — this is a decal tool, not the DGT
        Assert.True(MaskValidator.TryNormalize(field, "1234 bbc", out string normalized));
        Assert.Equal("1234 BBC", normalized);

        Assert.False(MaskValidator.IsValid(field, "1234 BBÑ")); // Ñ still excluded (outside plain A-Z typing)
        Assert.False(MaskValidator.IsValid(field, "123 BBCX")); // wrong shape
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        // No font project exists yet, so this compiles only the imported static art (no text
        // field) — full end-to-end compilation including the plate number is covered once
        // GT7LivMan.Typography.Wpf provides a real ITextOutliner.
        PlateDocument document = BuildSpainLongPlate();
        PlateDocument staticOnly = document with { Elements = document.Elements.Where(e => e is not Element.TextField).ToList() };

        Scene scene = SceneCompiler.Compile(staticOnly, new Dictionary<string, string>(), new ThrowingTextOutliner());
        string svg = SvgWriter.Write(scene);

        Assert.True(SizeBudget.IsWithinTarget(svg), $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
    }

    private sealed class ThrowingTextOutliner : ITextOutliner
    {
        public IReadOnlyList<PathData> Outline(
            string text, string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies, double charHeight, double tracking, double spaceTracking) =>
            throw new InvalidOperationException("No text fields should be compiled in this static-only test.");
    }
}
