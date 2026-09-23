using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/pt-v1.json from portugal_licenseplate_v1.svg: the same "AA 00 AA"-shaped
/// Portuguese plate as pt-long (v2), but without the printed separator dots — just a plain gap
/// between the three 2-character blocks — and no yellow inspection-date patch, so this template
/// carries a single field.
/// </summary>
public class PortugalV1TemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static string BaseSvgPath => Path.Combine(RepoPaths.Root, "assets", "base", "portugal_licenseplate_v1.svg");
    private static string TemplateOutputPath => Path.Combine(RepoPaths.Root, "templates", "pt-v1.json");

    private static PlateDocument BuildPortugalV1Plate()
    {
        var imported = SvgReader.ReadStaticShapes(File.ReadAllText(BaseSvgPath));

        var elements = imported
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // Calibrated against a real reference image ("portugal v1.png", "AA 00 AA"), measured the
        // same way as the other templates: character-height and baseline-position ratios against
        // the plate's own outer-border height (77.8% and 86.9% — a bit taller/lower than Spain's
        // 70%/84.5%, but this plate is proportionally much shorter and wider, so it has more room),
        // then applied to this SVG's own bounds (y:[29,262], height 233; band ends x=121, outer
        // right edge x=1037).
        //
        // Unlike pt-long (v2), there's no static dot art to align against here, so Tracking/
        // SpaceTracking come directly from the measured pitch, scaled by the ratio between the
        // photo's band-to-edge content width (468px) and this SVG's own (916 units): a within-block
        // pitch of 65px -> 127, and a between-block gap of 26px -> 51.
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#111111"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(579, 231),
        });

        var plateNumberField = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            // 'X': each block is freely a digit or a letter, same as pt-long — see its comment.
            Mask: "XX XX XX",
            AllowedLetters: FullAlphabet,
            CharHeight: 181,
            Tracking: 127,
            SpaceTracking: 51,
            PreferredFontFamily: "DIN1451",
            // Condensed first: these cell widths are calibrated for a narrow plate font, and a wide
            // fallback overflows its cell into the next character.
            FallbackFontFamilies: ["Roboto Condensed", "Overpass"],
            // Same reasoning as pt-long: Random License Plate should pick one letter block and two
            // digit blocks, never a block mixing both.
            RandomizeAsLetterAndDigitBlocks: true);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "PT",
            DisplayName: "Portugal - License v1",
            Size: new SizeMm(1042, 290), // native template units; mapped to real mm by the app at export time
            Elements: elements,
            Fields: [plateNumberField]);
    }

    [Fact]
    public void GenerateAndWrite_PtV1Template()
    {
        PlateDocument document = BuildPortugalV1Plate();
        string json = PlateDocumentSerializer.Serialize(document);

        Directory.CreateDirectory(Path.GetDirectoryName(TemplateOutputPath)!);
        File.WriteAllText(TemplateOutputPath, json);

        Assert.True(File.Exists(TemplateOutputPath));
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsShapeAndFieldCounts()
    {
        PlateDocument original = BuildPortugalV1Plate();
        string json = PlateDocumentSerializer.Serialize(original);
        PlateDocument reloaded = PlateDocumentSerializer.Deserialize(json);

        Assert.Equal(original.Elements.Count, reloaded.Elements.Count);
        Assert.Single(reloaded.Fields);
        Assert.Single(reloaded.Elements.OfType<Element.TextField>());
    }

    [Fact]
    public void MaskValidator_AcceptsAnyMixOfDigitsAndLettersPerBlock()
    {
        FieldDef field = BuildPortugalV1Plate().Fields[0];

        Assert.True(MaskValidator.IsValid(field, "AA 00 AA"));
        Assert.True(MaskValidator.IsValid(field, "12 AB 34"));
        Assert.True(MaskValidator.TryNormalize(field, "aa 00 aa", out string normalized));
        Assert.Equal("AA 00 AA", normalized);
        Assert.False(MaskValidator.IsValid(field, "AA00AA")); // wrong shape (no space slots filled)
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        PlateDocument document = BuildPortugalV1Plate();
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
