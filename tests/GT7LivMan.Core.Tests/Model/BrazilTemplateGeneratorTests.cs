using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/brazil.json from brasil_plate.svg: the Mercosur plate Brazil has issued since
/// 2018 — "BRASIL" on the blue band, black characters in the Mercosur format LLLNLNN (e.g.
/// "BCA9G35": 3 letters, a digit, a letter, 2 digits) in FE-Schrift.
/// </summary>
public class BrazilTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static PlateDocument BuildBrazilPlate()
    {
        string svgPath = Path.Combine(RepoPaths.Root, "assets", "base", "brasil_plate.svg");
        var elements = SvgReader.ReadStaticShapes(File.ReadAllText(svgPath))
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // Measured off a photo of a real plate ("BCA9G35", brasil plate.png), as fractions of the
        // white area under the blue band — the photo is taken at an angle, so fractions rather than
        // a pixel mapping — then applied to this SVG's white area (x 24..576, y 64..187):
        //   character ink 73% of the white area's height -> 90; its top 8% down -> 74
        //   fixed pitch 11.8% of the width -> 65 (FE-Schrift is proportional; the plate isn't)
        //   block centered 51.6% across -> 308.8
        // FE-Font's ink is 0.893 em tall against a 0.736 em cap height, and reaches 0.16 em below
        // its baseline: CharHeight 74.2 gives 90 of ink, the baseline at 147.9 puts its top at 74.
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#111111"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(308.8, 147.9),
        });

        var field = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            Mask: "AAA9A99",
            AllowedLetters: FullAlphabet,
            CharHeight: 74.2,
            Tracking: 65,
            SpaceTracking: 65, // unused: no literal in the mask
            PreferredFontFamily: "FE-Font",
            FallbackFontFamilies: ["Roboto Condensed", "Overpass"]);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "BR",
            DisplayName: "Brazil",
            Size: new SizeMm(604, 196),
            Elements: elements,
            Fields: [field]);
    }

    [Fact]
    public void GenerateAndWrite_BrazilTemplate()
    {
        string outputPath = Path.Combine(RepoPaths.Root, "templates", "brazil.json");
        File.WriteAllText(outputPath, PlateDocumentSerializer.Serialize(BuildBrazilPlate()));

        Assert.True(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("BCA9G35", true)]
    [InlineData("ABC1D23", true)]
    [InlineData("ABC1234", false)] // the old grey plate's LLL-NNNN, not Mercosur
    [InlineData("BCA9G3", false)]
    public void MaskValidator_AcceptsTheMercosurFormat(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(BuildBrazilPlate().Fields[0], value));
    }

    [Fact]
    public void GenerateRandom_AlwaysProducesAMaskValidValue()
    {
        FieldDef field = BuildBrazilPlate().Fields[0];
        var random = new Random(5);
        for (int i = 0; i < 50; i++)
        {
            Assert.True(MaskValidator.IsValid(field, MaskFormatter.GenerateRandom(field, random)));
        }
    }

    [Fact]
    public void PlateNumber_FitsInsideTheWhiteArea_ClearOfTheBrMark()
    {
        double width = TextMetrics.AdvanceWidth("BCA9G35", 65, 65);
        double left = 308.8 - (width / 2);

        Assert.True(left > 60, $"Starts at {left}, over the BR mark (x 28..60)."); // BR mark's right edge
        Assert.True(308.8 + (width / 2) < 576);
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        PlateDocument document = BuildBrazilPlate();
        PlateDocument staticOnly = document with { Elements = document.Elements.Where(e => e is not Element.TextField).ToList() };

        Scene scene = SceneCompiler.Compile(staticOnly, new Dictionary<string, string>(), new ThrowingTextOutliner());
        Assert.True(SizeBudget.IsWithinTarget(SvgWriter.Write(scene)));
    }

    private sealed class ThrowingTextOutliner : ITextOutliner
    {
        public IReadOnlyList<PathData> Outline(
            string text, string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies, double charHeight, double tracking, double spaceTracking) =>
            throw new InvalidOperationException("No text fields should be compiled in this static-only test.");
    }
}
