using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/nl-front.json and nl-back.json from Nederlands_front/back.svg — identical
/// layout, differing only in background color. The plate number comes in two formats the user
/// picks between, "XX-XX-XX" (e.g. "NL-01-AB") and "XX-XXX-X" (e.g. "29-KTV-7") — both six
/// characters, so which is meant can't be inferred from what's typed.
/// </summary>
public class NetherlandsTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string[] PlateFontFallbacks = ["Roboto Condensed", "Overpass"];

    private static PlateDocument BuildNetherlandsPlate(string svgFileName, string displayName)
    {
        string svgPath = Path.Combine(RepoPaths.Root, "assets", "base", svgFileName);
        var imported = SvgReader.ReadStaticShapes(File.ReadAllText(svgPath));

        var elements = imported
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // Measured off two real plate photos (neder1/neder2.png, "29-KTV-7" and "NL-01-AB"), both
        // agreeing to within 1%, as fractions of the plate's outer height — this SVG's plate spans
        // y:[8,102], 94 units:
        //   character height 0.56   -> 53
        //   baseline         0.782  -> 8 + 0.782*94 = 81.5
        // and as multiples of the character height (horizontal pitch, center to center):
        //   letter to letter 0.887  -> Tracking 47
        //   letter to dash   0.755  -> Tracking/2 + SpaceTracking/2, so SpaceTracking = 1.51*53 - 47 = 33
        // Centered on the yellow face between the EU band (ends x=60) and the inner border (x=509).
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#111111"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(284.5, 81.5),
        });

        var field = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            Mask: "XX-XX-XX",
            AllowedLetters: FullAlphabet,
            CharHeight: 53,
            Tracking: 47,
            SpaceTracking: 33,
            PreferredFontFamily: "Kenteken",
            // Condensed first: these cell widths are calibrated for the real plate font's narrow
            // proportions, and a wide fallback overflows its cell into the next character.
            FallbackFontFamilies: PlateFontFallbacks,
            AlternativeMasks: ["XX-XXX-X"],
            RandomizeAsAlternatingBlocks: true);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "NL",
            DisplayName: displayName,
            Size: new SizeMm(520, 110),
            Elements: elements,
            Fields: [field]);
    }

    private static PlateDocument BuildFront() => BuildNetherlandsPlate("Nederlands_front.svg", "Netherlands - Front");

    private static PlateDocument BuildBack() => BuildNetherlandsPlate("Nederlands_back.svg", "Netherlands - Back");

    [Theory]
    [InlineData("Nederlands_front.svg", "Netherlands - Front", "nl-front.json")]
    [InlineData("Nederlands_back.svg", "Netherlands - Back", "nl-back.json")]
    public void GenerateAndWrite_NetherlandsTemplate(string svgFileName, string displayName, string outputFileName)
    {
        PlateDocument document = BuildNetherlandsPlate(svgFileName, displayName);
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
        Assert.Contains(front.Elements.OfType<Element.Shape>(), s => s.Fill == RgbColor.Parse("#f8f8f8"));
        Assert.Contains(back.Elements.OfType<Element.Shape>(), s => s.Fill == RgbColor.Parse("#f0c400"));
    }

    [Fact]
    public void Serialize_ThenDeserialize_KeepsAlternativeMasks()
    {
        string json = PlateDocumentSerializer.Serialize(BuildFront());
        FieldDef reloaded = PlateDocumentSerializer.Deserialize(json).Fields.Single();

        Assert.Equal(["XX-XX-XX", "XX-XXX-X"], reloaded.AllMasks());
        Assert.True(reloaded.RandomizeAsAlternatingBlocks);
    }

    private static FieldDef InFormat(string mask) => BuildFront().Fields[0].WithMask(mask);

    [Theory]
    [InlineData("XX-XX-XX", "nl01ab", "NL-01-AB")]
    [InlineData("XX-XXX-X", "29ktv7", "29-KTV-7")]
    [InlineData("XX-XXX-X", "29-ktv-7", "29-KTV-7")] // typed dashes are fine too
    [InlineData("XX-XXX-X", "29kt", "29-KT")] // still typing
    [InlineData("XX-XX-XX", "29ktv7", "29-KT-V7")] // same keys, other format: dashes where that one puts them
    public void AutoFormat_PlacesTheDashesForTheChosenFormat(string mask, string rawInput, string expected)
    {
        Assert.Equal(expected, MaskFormatter.AutoFormat(InFormat(mask), rawInput));
    }

    [Theory]
    [InlineData("XX-XX-XX", "NL-01-AB", true)]
    [InlineData("XX-XX-XX", "29-KTV-7", false)] // the other format's shape
    [InlineData("XX-XXX-X", "29-KTV-7", true)]
    [InlineData("XX-XXX-X", "NL-01-AB", false)]
    public void MaskValidator_AcceptsOnlyTheChosenFormat(string mask, string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(InFormat(mask), value));
    }

    [Theory]
    [InlineData("XX-XX-XX", 1)]
    [InlineData("XX-XX-XX", 2)]
    [InlineData("XX-XX-XX", 3)]
    [InlineData("XX-XXX-X", 1)]
    [InlineData("XX-XXX-X", 2)]
    [InlineData("XX-XXX-X", 3)]
    public void GenerateRandom_StaysInTheChosenFormat_WithGroupsAlternatingLettersAndDigits(string mask, int seed)
    {
        FieldDef field = InFormat(mask);
        string value = MaskFormatter.GenerateRandom(field, new Random(seed));

        Assert.True(MaskValidator.IsValid(field, value), value);
        string[] groups = value.Split('-');
        for (int i = 0; i < groups.Length; i++)
        {
            bool letters = groups[i].All(char.IsAsciiLetter);
            Assert.True(letters || groups[i].All(char.IsAsciiDigit), value);
            if (i > 0)
            {
                Assert.NotEqual(groups[i - 1].All(char.IsAsciiLetter), letters);
            }
        }
    }

    [Fact]
    public void Plate_FitsInsideTheYellowFace()
    {
        FieldDef field = BuildFront().Fields[0];
        double width = TextMetrics.AdvanceWidth("29-KTV-7", field.Tracking, field.SpaceTracking);

        Assert.InRange(284.5 - (width / 2), 60, 509);
        Assert.InRange(284.5 + (width / 2), 60, 509);
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        PlateDocument document = BuildFront();
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
