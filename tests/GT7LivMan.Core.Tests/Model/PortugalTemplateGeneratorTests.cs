using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/pt-long.json from portugal_licenseplate_v2.svg plus two hand-placed fields:
/// the plate number ("12 AB 34" — the dots between groups are static art already in the SVG,
/// so the mask uses a plain space there) and the inspection-date sticker in the yellow patch
/// (one 4-digit field split into month/year via Element.TextField's CharStart/CharCount).
/// </summary>
public class PortugalTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string InspectionDateFieldId = "inspectionDate";

    // Same permissive philosophy as Spain: this is a decal tool, not a registration authority.
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static readonly string[] PlateFontFallbacks = ["Roboto Condensed", "Overpass"];

    private static string BaseSvgPath => Path.Combine(RepoPaths.Root, "assets", "base", "portugal_licenseplate_v2.svg");
    private static string TemplateOutputPath => Path.Combine(RepoPaths.Root, "templates", "pt-long.json");

    private static PlateDocument BuildPortugalLongPlate()
    {
        var imported = SvgReader.ReadStaticShapes(File.ReadAllText(BaseSvgPath));

        var elements = imported
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // Calibrated against a real Portuguese plate reference image
        // (placa-del-coche-de-portugal-...127253128.jpg, "12·AB·34" / "02 12"), measured the same
        // way as Spain: character-height and baseline-position ratios against the plate's interior
        // height (71.9% and 85.2% — essentially identical to Spain's 70%/84.5%, a useful
        // cross-check), then applied to this SVG's own interior (y:[157,355], height 198).
        //
        // Horizontally, this SVG's own art pins the layout exactly, which beats any ratio measured
        // off a stock photo: the two separator dots (cx=368, cx=625) have to fall in the middle of
        // the mask's two space cells. With the block centered on the dots' midpoint (496.5), that
        // works out to the single constraint 2*Tracking + SpaceTracking = 625-368 = 257, and the
        // chosen 95/67 satisfies it while leaving sensible margins to the band (~25) and the
        // yellow patch (~48). Verified by PortugalDotAlignment tests.
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#111111"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(496.5, 326),
        });

        // The inspection-date sticker in the yellow patch (x:[897,987]): one 4-digit field,
        // rendered as two stacked 2-digit slices straddling the SVG's own divider rect
        // (x=905 y=259 width=74 height=5) — month above, year below.
        //
        // In the reference image the digit block measures exactly the same width as that divider
        // line (both x 700..759 there), so the divider gives the block's width and center for
        // free: 74 wide centered on x=942, i.e. Tracking = 37 per cell. Baselines come from the
        // measured ink rows (y 153..190 and 209..246 within a 130..270 interior -> 42.9% and 82.9%).
        elements.Add(new Element.TextField(InspectionDateFieldId, RgbColor.Parse("#111111"), CharStart: 0, CharCount: 2, Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(942, 242),
        });
        elements.Add(new Element.TextField(InspectionDateFieldId, RgbColor.Parse("#111111"), CharStart: 2, CharCount: 2, Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(942, 321),
        });

        var plateNumberField = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            // 'X' (not '9'/'A'): each block is freely a digit or a letter — a real Portuguese
            // plate's blocks aren't fixed to one or the other (unlike Spain's digits-then-letters),
            // so we don't force that either. Space is a plain gap, not a printed literal — the two
            // separator dots are already static art in the base SVG, positioned independently of
            // this text.
            Mask: "XX XX XX",
            AllowedLetters: FullAlphabet,
            CharHeight: 142,
            // 2*Tracking + SpaceTracking must equal 257 so the mask's space cells center on the
            // SVG's fixed separator dots — see the placement comment above.
            Tracking: 95,
            SpaceTracking: 67,
            PreferredFontFamily: "Alte DIN 1451 Mittelschrift",
            // Condensed first: these cell widths are calibrated for DIN 1451's narrow proportions,
            // and a wide fallback (Overpass runs ~20% wider) overflows its cell and collides with
            // the next character.
            FallbackFontFamilies: PlateFontFallbacks,
            // Random License Plate should land on a shape a real Portuguese series actually uses —
            // one block of 2 letters, two blocks of 2 digits, never a block that mixes both (plain
            // per-'X' randomization could otherwise produce e.g. "A5").
            RandomizeAsLetterAndDigitBlocks: true);

        var inspectionDateField = new FieldDef(
            Id: InspectionDateFieldId,
            Label: "Fecha ITV (mes/año)",
            Mask: "9999",
            AllowedLetters: string.Empty,
            // Measured ink height 38px of a 140px interior (27%) -> 54 here; trimmed slightly so
            // the digits keep visible margin inside the patch.
            CharHeight: 50,
            Tracking: 37, // half the divider's 74-unit width — see the placement comment above
            SpaceTracking: 37, // unused — "9999" has no literal mask position
            PreferredFontFamily: "Alte DIN 1451 Mittelschrift",
            FallbackFontFamilies: PlateFontFallbacks,
            // The first 2 digits are the inspection month (01-12), the last 2 the two-digit year —
            // Random License Plate should pick a real month and a plausible year (1990 up to the
            // current one), not any 4 random digits. Typing something outside that range by hand
            // (e.g. a future "28") still works; this only caps what Random picks.
            RandomMonthDigitsStart: 0,
            RandomTwoDigitYearDigits: new RandomTwoDigitYearSpec(Start: 2, MinTwoDigitYear: 90));

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "PT",
            DisplayName: "Portugal - License v2",
            Size: new SizeMm(1024, 500), // native template units; mapped to real mm by the app at export time
            Elements: elements,
            Fields: [plateNumberField, inspectionDateField]);
    }

    [Fact]
    public void GenerateAndWrite_PtLongTemplate()
    {
        PlateDocument document = BuildPortugalLongPlate();
        string json = PlateDocumentSerializer.Serialize(document);

        Directory.CreateDirectory(Path.GetDirectoryName(TemplateOutputPath)!);
        File.WriteAllText(TemplateOutputPath, json);

        Assert.True(File.Exists(TemplateOutputPath));
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsShapeAndFieldCounts()
    {
        PlateDocument original = BuildPortugalLongPlate();
        string json = PlateDocumentSerializer.Serialize(original);
        PlateDocument reloaded = PlateDocumentSerializer.Deserialize(json);

        Assert.Equal(original.Elements.Count, reloaded.Elements.Count);
        Assert.Equal(2, reloaded.Fields.Count);
        Assert.Equal(3, reloaded.Elements.OfType<Element.TextField>().Count());
    }

    [Fact]
    public void MaskValidator_AcceptsARealPlateNumber_AndAnyMixOfDigitsAndLettersPerBlock()
    {
        FieldDef field = BuildPortugalLongPlate().Fields.Single(f => f.Id == PlateNumberFieldId);

        Assert.True(MaskValidator.IsValid(field, "12 AB 34"));
        Assert.True(MaskValidator.TryNormalize(field, "12 ab 34", out string normalized));
        Assert.Equal("12 AB 34", normalized);

        // Each block is a free mix of digits/letters — no block is pinned to one or the other.
        Assert.True(MaskValidator.IsValid(field, "AA 00 AA"));
        Assert.True(MaskValidator.IsValid(field, "A1 2B 34"));

        Assert.False(MaskValidator.IsValid(field, "1AB2 34")); // wrong shape
    }

    [Fact]
    public void MaskValidator_AcceptsTheInspectionDateAsFourPlainDigits()
    {
        FieldDef field = BuildPortugalLongPlate().Fields.Single(f => f.Id == InspectionDateFieldId);

        Assert.True(MaskValidator.IsValid(field, "0212"));
        Assert.False(MaskValidator.IsValid(field, "02 12")); // no literal space in this mask
        Assert.False(MaskValidator.IsValid(field, "02A2")); // digits only
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        PlateDocument document = BuildPortugalLongPlate();
        PlateDocument staticOnly = document with { Elements = document.Elements.Where(e => e is not Element.TextField).ToList() };

        Scene scene = SceneCompiler.Compile(staticOnly, new Dictionary<string, string>(), new ThrowingTextOutliner());
        string svg = SvgWriter.Write(scene);

        Assert.True(SizeBudget.IsWithinTarget(svg), $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<rect", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<circle", svg, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingTextOutliner : ITextOutliner
    {
        public IReadOnlyList<PathData> Outline(
            string text, string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies, double charHeight, double tracking, double spaceTracking) =>
            throw new InvalidOperationException("No text fields should be compiled in this static-only test.");
    }
}
