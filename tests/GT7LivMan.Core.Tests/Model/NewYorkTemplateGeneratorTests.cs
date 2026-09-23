using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/newyork.json from newyork_licenseplate.svg. The plate number is one field
/// ("AAA 9999") split visually by the state-outline icon the source SVG already draws between the
/// letter and digit groups — the mask's literal space is sized and positioned so its gap lands
/// exactly on the icon instead of the two groups running together or the icon sitting mid-character.
/// </summary>
public class NewYorkTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static string BaseSvgPath => Path.Combine(RepoPaths.Root, "assets", "base", "newyork_licenseplate.svg");
    private static string TemplateOutputPath => Path.Combine(RepoPaths.Root, "templates", "newyork.json");

    private static PlateDocument BuildNewYorkPlate()
    {
        var imported = SvgReader.ReadStaticShapes(File.ReadAllText(BaseSvgPath));

        var elements = imported
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // Icon (the NY state outline already drawn in the source SVG, between where the letter and
        // digit groups sit) measures x:[522,596] y:[300,364] — width 74, center (559,332). The mask's
        // literal space must land its gap exactly there: with CharHeight 230 / Tracking 100 /
        // SpaceTracking 114 (icon width + ~20 padding each side), centering the whole "AAA 9999"
        // block via TextAnchor.Center at Transform X=609 puts the space cell's own center at
        // (609 - 407) + 3*100 + 57 = 559, matching the icon exactly — 100 (not a rounder 112) is
        // deliberately smaller than the widest available Tracking so the 4-digit block (one
        // character longer than the 3-letter block) doesn't crowd the plate's right-side rounded
        // corner once everything is centered on the icon rather than the plate's own midpoint.
        // Baseline Y=435 centers the digit line in the gold area between the top band (bottom
        // ~y160) and "EMPIRE STATE" (top y488).
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#1e2746"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(609, 435),
        });

        var plateNumberField = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            // Fixed shape (unlike Portugal/UK's free 'X' blocks): a real New York plate is always
            // 3 letters then 4 digits, and the literal space is what the icon-gap calibration above
            // depends on being in a fixed position.
            Mask: "AAA 9999",
            AllowedLetters: FullAlphabet,
            CharHeight: 230,
            Tracking: 100,
            SpaceTracking: 114,
            PreferredFontFamily: "Zurich Extra Condensed Regular",
            FallbackFontFamilies: ["Roboto Condensed", "Overpass"]);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "US-NY",
            DisplayName: "USA - New York",
            Size: new SizeMm(1160, 604), // native template units; mapped to real mm by the app at export time
            Elements: elements,
            Fields: [plateNumberField]);
    }

    [Fact]
    public void GenerateAndWrite_NewYorkTemplate()
    {
        PlateDocument document = BuildNewYorkPlate();
        string json = PlateDocumentSerializer.Serialize(document);

        Directory.CreateDirectory(Path.GetDirectoryName(TemplateOutputPath)!);
        File.WriteAllText(TemplateOutputPath, json);

        Assert.True(File.Exists(TemplateOutputPath));
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsShapeAndFieldCounts()
    {
        PlateDocument original = BuildNewYorkPlate();
        string json = PlateDocumentSerializer.Serialize(original);
        PlateDocument reloaded = PlateDocumentSerializer.Deserialize(json);

        Assert.Equal(original.Elements.Count, reloaded.Elements.Count);
        Assert.Single(reloaded.Fields);
        Assert.Single(reloaded.Elements.OfType<Element.TextField>());
    }

    [Fact]
    public void MaskValidator_PlateNumber_RequiresThreeLettersThenFourDigits()
    {
        FieldDef field = BuildNewYorkPlate().Fields.Single(f => f.Id == PlateNumberFieldId);

        Assert.True(MaskValidator.IsValid(field, "GXB 5332"));
        Assert.True(MaskValidator.TryNormalize(field, "gxb 5332", out string normalized));
        Assert.Equal("GXB 5332", normalized);
        Assert.Equal("GXB 5332", MaskFormatter.AutoFormat(field, "gxb5332")); // the UI auto-inserts the literal space
        Assert.False(MaskValidator.IsValid(field, "5332 GXB")); // wrong shape: digits/letters swapped
        Assert.False(MaskValidator.IsValid(field, "GXB 533")); // wrong length
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        PlateDocument document = BuildNewYorkPlate();
        PlateDocument staticOnly = document with { Elements = document.Elements.Where(e => e is not Element.TextField).ToList() };

        Scene scene = SceneCompiler.Compile(staticOnly, new Dictionary<string, string>(), new ThrowingTextOutliner());
        string svg = SvgWriter.Write(scene);

        Assert.True(SizeBudget.IsWithinTarget(svg), $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<rect", svg, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThrowingTextOutliner : ITextOutliner
    {
        public IReadOnlyList<PathData> Outline(
            string text, string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies, double charHeight, double tracking, double spaceTracking) =>
            throw new InvalidOperationException("No text fields should be compiled in this static-only test.");
    }
}
