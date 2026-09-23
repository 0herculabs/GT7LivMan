using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/uk-front.json and uk-back.json from uk_licenseplate_front/back.svg — identical
/// layout (same 520x111 canvas, same band/stars/"GB" art), differing only in background color
/// (white front, yellow back) and, for back, occasionally an "XXXX XXX" reading as e.g. "LC67 DKF"
/// (2 letters + 2 digits, then 3 letters) — but a real plate's blocks aren't fixed to one shape, so
/// per our permissive philosophy this mask just frees each character rather than encoding that.
/// </summary>
public class UkTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string[] PlateFontFallbacks = ["Roboto Condensed", "Overpass"];

    private static PlateDocument BuildUkPlate(string svgFileName, string displayName)
    {
        string svgPath = Path.Combine(RepoPaths.Root, "assets", "base", svgFileName);
        var imported = SvgReader.ReadStaticShapes(File.ReadAllText(svgPath));

        var elements = imported
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // This SVG's 520x111 canvas is (unlike Spain's or Portugal's) drawn at real-world mm
        // scale, so rather than measuring a photo — this one's at a hard angle anyway — the DVLA's
        // own published "outline specification for registration plates" gives exact figures
        // directly: character height 79mm, character width 50mm, stroke width 14mm, space between
        // characters 11mm, space between groups 33mm, symmetric top/bottom margins.
        //
        // Tracking (a character's own advance) = width + normal inter-character space = 50+11=61.
        // SpaceTracking (the literal mask space's own advance) tops that up to the group gap:
        // Tracking + SpaceTracking must equal width + inter-group space = 50+33=83, so
        // SpaceTracking = 83-61 = 22.
        //
        // Baseline: top/bottom margin around the 79mm character band on a 111mm-tall (here y:[2,109]
        // outer-rect) plate is (111-79)/2 ~ 16mm, so baseline = 2 + 14 + 79 = 95 (14, not 16, to
        // land the margins symmetric against this SVG's actual y:[2,109] bounds rather than a
        // nominal 111).
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#111111"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(291, 95),
        });

        var field = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            // 'X': free mix of digits/letters per character, same reasoning as Portugal — this is
            // a decal tool, not the DVLA, and doesn't encode which positions "should" be letters.
            Mask: "XXXX XXX",
            AllowedLetters: FullAlphabet,
            CharHeight: 79,
            Tracking: 61,
            SpaceTracking: 22,
            PreferredFontFamily: "CharlesWright-Bold",
            // Condensed first: these cell widths are calibrated for the real plate font's narrow
            // proportions, and a wide fallback overflows its cell into the next character.
            FallbackFontFamilies: PlateFontFallbacks);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "GB",
            DisplayName: displayName,
            Size: new SizeMm(520, 111),
            Elements: elements,
            Fields: [field]);
    }

    private static PlateDocument BuildFront() => BuildUkPlate("uk_licenseplate_front.svg", "UK - Front");

    private static PlateDocument BuildBack() => BuildUkPlate("uk_licenseplate_back.svg", "UK - Back");

    [Theory]
    [InlineData("uk_licenseplate_front.svg", "UK - Front", "uk-front.json")]
    [InlineData("uk_licenseplate_back.svg", "UK - Back", "uk-back.json")]
    public void GenerateAndWrite_UkTemplate(string svgFileName, string displayName, string outputFileName)
    {
        PlateDocument document = BuildUkPlate(svgFileName, displayName);
        string json = PlateDocumentSerializer.Serialize(document);

        string outputPath = Path.Combine(RepoPaths.Root, "templates", outputFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, json);

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void FrontAndBack_ShareIdenticalLayout_DifferingOnlyInBackgroundColor()
    {
        PlateDocument front = BuildFront();
        PlateDocument back = BuildBack();

        Assert.Equal(front.Elements.Count, back.Elements.Count);

        var frontOuter = (Element.Shape)front.Elements[0];
        var backOuter = (Element.Shape)back.Elements[0];
        Assert.Equal(RgbColor.Parse("#111111"), frontOuter.Fill); // outer border/stroke shape, same both
        Assert.Equal(frontOuter.Fill, backOuter.Fill);

        var frontFace = (Element.Shape)front.Elements[1];
        var backFace = (Element.Shape)back.Elements[1];
        Assert.Equal(RgbColor.Parse("#f7f7f7"), frontFace.Fill); // white face
        Assert.Equal(RgbColor.Parse("#f5c400"), backFace.Fill); // yellow face
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsShapeAndFieldCounts()
    {
        PlateDocument original = BuildFront();
        string json = PlateDocumentSerializer.Serialize(original);
        PlateDocument reloaded = PlateDocumentSerializer.Deserialize(json);

        Assert.Equal(original.Elements.Count, reloaded.Elements.Count);
        Assert.Single(reloaded.Fields);
        Assert.Single(reloaded.Elements.OfType<Element.TextField>());
    }

    [Fact]
    public void MaskValidator_AcceptsARealPlateNumber_AndAnyMixOfDigitsAndLettersPerCharacter()
    {
        FieldDef field = BuildFront().Fields[0];

        Assert.True(MaskValidator.IsValid(field, "LC67 DKF"));
        Assert.True(MaskValidator.TryNormalize(field, "lc67 dkf", out string normalized));
        Assert.Equal("LC67 DKF", normalized);

        Assert.True(MaskValidator.IsValid(field, "AAAA AAA")); // free mix, not fixed letters/digits
        Assert.True(MaskValidator.IsValid(field, "1234 567"));
        Assert.False(MaskValidator.IsValid(field, "LC67DKF")); // wrong shape (no space)
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        PlateDocument document = BuildFront();
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
