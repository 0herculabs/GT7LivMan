using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/japan.json from japan_plate.svg (right bolt hole moved from x=380 to 412.5,
/// mirroring the left one: real plates' holes are symmetric, and at 380 it sat under the
/// classification number): a private (自家用) medium plate, 330x165 mm,
/// green characters on white. Four fields, as on the real plate: the registration office (e.g.
/// 品川) and 3-character classification number (e.g. 330) on top; the hiragana and the 4-digit
/// serial below. The serial is right-aligned with leading positions shown as "・" and the hyphen
/// only when all 4 digits are there: "・・12", "・123", "12-34".
/// </summary>
public class JapanTemplateGeneratorTests
{
    private const string RegionFieldId = "region";
    private const string ClassFieldId = "classNumber";
    private const string HiraganaFieldId = "hiragana";
    private const string SerialFieldId = "serialNumber";

    /// <summary>
    /// Real 2-kanji registration offices (運輸支局 / 自動車検査登録事務所). Offices with 1, 3 or
    /// more characters (堺, 名古屋, 宇都宮, なにわ, ...) are left out: the top line's layout is
    /// sized for two.
    /// </summary>
    private static readonly string[] Regions =
    [
        "品川", "練馬", "足立", "多摩", "杉並", "板橋", "江東", "葛飾", "横浜", "川崎", "湘南", "相模",
        "大宮", "所沢", "熊谷", "川越", "川口", "千葉", "成田", "野田", "水戸", "土浦", "群馬", "高崎",
        "山梨", "長野", "松本", "新潟", "長岡", "富山", "石川", "金沢", "福井", "岐阜", "飛騨", "静岡",
        "浜松", "沼津", "豊橋", "岡崎", "豊田", "三重", "鈴鹿", "滋賀", "京都", "大阪", "和泉", "神戸",
        "姫路", "奈良", "鳥取", "島根", "岡山", "倉敷", "広島", "福山", "山口", "下関", "徳島", "香川",
        "愛媛", "高知", "福岡", "筑豊", "佐賀", "長崎", "熊本", "大分", "宮崎", "沖縄", "札幌", "函館",
        "旭川", "室蘭", "釧路", "帯広", "北見", "青森", "八戸", "岩手", "宮城", "仙台", "秋田", "山形",
        "庄内", "福島", "郡山",
    ];

    /// <summary>
    /// The hiragana of private cars (自家用): お, し, へ and ん are never used (お is too close to
    /// あ, し and へ read as 死 "death" and 屁 "fart", ん is hard to say clearly), and あ-こ/を are
    /// for commercial plates, わ/れ for rentals.
    /// </summary>
    private const string PrivateHiragana = "さすせそたちつてとなにぬねのはひふほまみむめもやゆらりる";

    /// <summary>First digit 3 (over 2000 cc) or 5 (up to 2000 cc); since 2018 the other two can be letters too.</summary>
    private static readonly string[] ClassNumbers = ["300", "330", "331", "332", "500", "501", "502", "530", "531", "533", "30A", "50C"];

    private const string ClassLetters = "ACFHKLMPXY";

    // Characters: Yu Gothic Bold, which ships with every Windows 10/11, so nothing to download.
    // The plate typeface has no public release; the one free replica, FZナンバープレートゴシック,
    // was tried and dropped — its outlines are crudely traced (visibly faceted curves), its kanji
    // and せ are empty glyphs, and it's personal-use only. Yu Gothic Bold has the same monoline
    // gothic look with clean curves; its digits are condensed below to the plate's proportions.
    private const string PlateFont = "Yu Gothic Bold";
    private static readonly string[] PlateFallbacks = ["MS Gothic", "Roboto Condensed", "Overpass"];

    private static readonly RgbColor Ink = RgbColor.Parse("#4c8663");

    private static PlateDocument BuildJapanPlate()
    {
        string svgPath = Path.Combine(RepoPaths.Root, "assets", "base", "japan_plate.svg");
        var elements = SvgReader.ReadStaticShapes(File.ReadAllText(svgPath))
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // All positions are measured off the ja.wikipedia reference plate (Japanese green on white
        // license plate.png, "多摩 500 さ 46-49", public domain), drawn on exactly this 500x250
        // canvas — its bolt holes match this SVG's — and cross-checked against a photo of a real
        // plate ("横浜 77 や 62-46", CC BY-SA 2.0): serial digits about half as wide as tall.

        // Office: two kanji, ink 21..84 tall, centers 145 and 218.
        elements.Add(new Element.TextField(RegionFieldId, Ink, Anchor: TextAnchor.Center) { Transform = Affine2.Translate(181.5, 78) });

        // Classification: three digits, ink 23..82, centers 291/330.5/370.5 (pitch 39.75).
        elements.Add(new Element.TextField(ClassFieldId, Ink, Anchor: TextAnchor.Center) { Transform = Affine2.Translate(330.5, 81) });

        // Hiragana: ink 137..198, centered on x=55.
        elements.Add(new Element.TextField(HiraganaFieldId, Ink, Anchor: TextAnchor.Center) { Transform = Affine2.Translate(55, 195) });

        // Serial: five fixed slots — digit, digit, hyphen, digit, digit — each drawn on its own so
        // a "・" or the blank hyphen slot of a shorter number stays exactly where a real plate
        // puts it. Digit ink 107..228, centers 144/228.5 and 349/433.5; hyphen centered on 289.
        double[] slotCenters = [144, 228.5, 289, 349, 433.5];
        for (int slot = 0; slot < slotCenters.Length; slot++)
        {
            elements.Add(new Element.TextField(SerialFieldId, Ink, CharStart: slot, CharCount: 1, Anchor: TextAnchor.Center)
            {
                Transform = Affine2.Translate(slotCenters[slot], SerialBaseline),
            });
        }

        var region = new FieldDef(
            Id: RegionFieldId,
            Label: "Oficina de registro",
            Mask: "AA",
            AllowedLetters: new string([.. Regions.SelectMany(r => r).Distinct()]),
            CharHeight: 52.4, // Yu Gothic Bold kanji: 0.93 em of ink, scaled so it spans 63
            Tracking: 73,
            SpaceTracking: 73, // unused
            PreferredFontFamily: PlateFont,
            FallbackFontFamilies: PlateFallbacks,
            DropdownOptions: Regions,
            RequireDropdownSelection: true);

        var classNumber = new FieldDef(
            Id: ClassFieldId,
            Label: "Número de clasificación",
            Mask: "9XX",
            AllowedLetters: ClassLetters,
            CharHeight: 60.5, // Yu Gothic Bold digits: 0.99x the cap height in ink, so 60
            Tracking: 39.75, // pitch; also caps the ink at 34 wide, as measured
            SpaceTracking: 39.75, // unused
            PreferredFontFamily: PlateFont,
            FallbackFontFamilies: PlateFallbacks,
            DropdownOptions: ClassNumbers);

        var hiragana = new FieldDef(
            Id: HiraganaFieldId,
            Label: "Hiragana",
            Mask: "A",
            AllowedLetters: PrivateHiragana,
            CharHeight: 56.5, // Yu Gothic Bold's さ is 0.849 em of ink, scaled to span 62
            Tracking: 45.3, // caps the ink at 39 wide
            SpaceTracking: 45.3, // unused
            PreferredFontFamily: PlateFont,
            FallbackFontFamilies: PlateFallbacks,
            DropdownOptions: [.. PrivateHiragana.Select(c => c.ToString())],
            RequireDropdownSelection: true);

        var serial = new FieldDef(
            Id: SerialFieldId,
            Label: "Número de serie",
            Mask: "99-99",
            AllowedLetters: string.Empty,
            CharHeight: 122, // 121 of ink
            // Each slot is placed on its own, so this is only the cell a digit is fitted into: it
            // caps the ink at 61 wide (0.86 of the cell). Yu Gothic Bold draws its digits ~0.65
            // as wide as tall, the real plate ~0.5, so they're condensed to the measured width.
            Tracking: 71,
            SpaceTracking: 36, // the hyphen's cell
            PreferredFontFamily: PlateFont,
            FallbackFontFamilies: PlateFallbacks,
            DigitPadChar: '・',
            // No available font draws these at the plate's proportions (Yu Gothic's hyphen is
            // thinner, its ・ smaller), so they're drawn here: the hyphen 29x16 as measured, the
            // dot a disc of about the same weight, both at the digits' mid-height (168.5).
            GlyphShapes: new Dictionary<string, PathData>
            {
                ["-"] = Shapes.RoundedRect(-14.5, 161 - SerialBaseline, 29, 16, 2),
                ["・"] = Shapes.Circle(0, 168.5 - SerialBaseline, 8.5),
            });

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "JP",
            DisplayName: "Japan",
            Size: new SizeMm(500, 250),
            Elements: elements,
            Fields: [region, classNumber, hiragana, serial]);
    }

    /// <summary>Yu Gothic Bold's digits bottom out ~0.01 em below the baseline: 228 - 0.01 * (122 / 0.774).</summary>
    private const double SerialBaseline = 226.5;

    private static FieldDef Field(string id) => BuildJapanPlate().Fields.Single(f => f.Id == id);

    [Fact]
    public void GenerateAndWrite_JapanTemplate()
    {
        string json = PlateDocumentSerializer.Serialize(BuildJapanPlate());
        string outputPath = Path.Combine(RepoPaths.Root, "templates", "japan.json");
        File.WriteAllText(outputPath, json);

        Assert.True(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("1234", "12-34")]
    [InlineData("123", "・1 23")]
    [InlineData("12", "・・ 12")]
    [InlineData("1", "・・ ・1")]
    [InlineData("0012", "・・ 12")] // a leading zero is padding, not a digit
    [InlineData("12345", "12-34")] // extra digits dropped
    [InlineData("12-34", "12-34")]
    [InlineData("", "")]
    public void Serial_IsRightAlignedWithDots_AndTheHyphenOnlyWhenFull(string rawInput, string expected)
    {
        Assert.Equal(expected, MaskFormatter.AutoFormat(Field(SerialFieldId), rawInput));
    }

    [Theory]
    [InlineData("12-34", true)]
    [InlineData("・1 23", true)]
    [InlineData("・・ ・1", true)]
    [InlineData("01-23", false)]
    [InlineData("12 34", false)]
    [InlineData("・・ 00", false)]
    [InlineData("", false)]
    public void Serial_OnlyAcceptsTheCanonicalForm(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(Field(SerialFieldId), value));
    }

    [Fact]
    public void Serial_Random_IsAlwaysValid()
    {
        FieldDef serial = Field(SerialFieldId);
        var random = new Random(3);
        for (int i = 0; i < 200; i++)
        {
            string value = MaskFormatter.GenerateRandom(serial, random);
            Assert.True(MaskValidator.IsValid(serial, value), value);
        }
    }

    [Fact]
    public void Serial_IsDrawnInFiveFixedSlots()
    {
        var slots = BuildJapanPlate().Elements.OfType<Element.TextField>()
            .Where(t => t.FieldId == SerialFieldId)
            .Select(t => (t.CharStart, t.CharCount));

        Assert.Equal([(0, 1), (1, 1), (2, 1), (3, 1), (4, 1)], slots);
    }

    [Theory]
    [InlineData("さ", true)]
    [InlineData("る", true)]
    [InlineData("し", false)] // never used: reads as 死
    [InlineData("お", false)]
    [InlineData("あ", false)] // commercial plates only
    [InlineData("わ", false)] // rentals
    public void Hiragana_OnlyThePrivateCarSet(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(Field(HiraganaFieldId), value));
    }

    [Theory]
    [InlineData("品川", true)]
    [InlineData("横浜", true)]
    [InlineData("東京", false)] // not a registration office name
    public void Region_IsAClosedListOfOffices(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(Field(RegionFieldId), value));
    }

    [Theory]
    [InlineData("330", true)]
    [InlineData("50C", true)]
    [InlineData("5CC", true)]
    [InlineData("C30", false)] // the first character is always a digit
    [InlineData("30B", false)] // B isn't one of the letters allowed
    public void ClassNumber_DigitThenDigitsOrAllowedLetters(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(Field(ClassFieldId), value));
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        PlateDocument document = BuildJapanPlate();
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
