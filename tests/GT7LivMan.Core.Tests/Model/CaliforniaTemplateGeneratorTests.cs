using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/california.json from california_licenseplate.svg plus three fields: the main
/// registration ("7YJL967"), the registration month (a blue sticker on the left — not present in
/// the source SVG, so drawn here as a plain rounded rect mirroring the year sticker's own style),
/// and the registration year (embedded in the SVG's own orange sticker on the right).
/// </summary>
public class CaliforniaTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string MonthFieldId = "month";
    private const string YearFieldId = "year";
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static readonly string[] MonthAbbreviations =
        ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

    private static string BaseSvgPath => Path.Combine(RepoPaths.Root, "assets", "base", "california_licenseplate.svg");
    private static string TemplateOutputPath => Path.Combine(RepoPaths.Root, "templates", "california.json");

    private static PlateDocument BuildCaliforniaPlate()
    {
        var imported = SvgReader.ReadStaticShapes(File.ReadAllText(BaseSvgPath));

        var elements = imported
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // The source SVG only draws the year's orange sticker (x=1385 y=128 w=230 h=120) — no
        // month sticker. The reference photo shows one on the left, so we add a matching rounded
        // rect mirrored across the plate's horizontal center (canvas width 1728):
        // mirrored_x = 1728 - 1385 - 230 = 113.
        elements.Add((Element)new Element.Shape(Shapes.RoundedRect(113, 128, 230, 120, 8), RgbColor.Parse("#1C4E9C")));

        // Main registration: canvas outer rect is y:[54,834] (height 780). The cursive "California"
        // wordmark's own path data occupies roughly y:[82,345] after its translate+scale transform,
        // so the registration sits below that. CharHeight/Tracking come from LICENSE PLATE USA's
        // measured natural ink ratio (~0.43 of cap height — a tall, narrow block font): fitting 7
        // characters into the ~1471-unit available width (inner width 1571 minus margins) allows up
        // to Tracking=210 (CharHeight up to ~420 before condensing). Sized down from that ceiling to
        // 340/188 instead — LICENSE PLATE USA's glyphs measured ~800 bytes each (a detailed font,
        // unlike a minimal plate face), and combined with the wordmark's own ~6 KB this plate has
        // little byte headroom left for the full 15 KB GT7 budget.
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#171717"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(864, 780),
        });

        // Month (blue sticker) and year (the SVG's own orange sticker, x:[1385,1615] y:[128,248],
        // center 1500,188): both use CharlesWright-Bold rather than the plate font or a fallback
        // sans. Not realistic (real CA stickers use a plain sans for this small print), but a
        // deliberate byte-budget call: LICENSE PLATE USA's/Roboto Condensed's glyphs measured
        // 800-1100 bytes each (a "realistic" font has enough curve detail that it adds up fast),
        // while CharlesWright-Bold's came out at ~210 — already loaded for no extra cost, and this
        // plate's cursive "California" wordmark alone eats ~6 KB of the 15 KB GT7 budget before any
        // text is added, so the two small fields don't have room to be expensive too.
        elements.Add(new Element.TextField(MonthFieldId, RgbColor.Parse("#ffffff"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(228, 221), // blue box center (228,188) + half CharHeight
        });
        elements.Add(new Element.TextField(YearFieldId, RgbColor.Parse("#171717"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(1500, 213), // orange box center (1500,188) + half CharHeight
        });

        var plateNumberField = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            // 'X': free mix of digits/letters, same reasoning as UK/Portugal — a real California
            // plate does follow a fixed 9-AAA-999 shape, but this is a decal tool, not the DMV.
            Mask: "XXXXXXX",
            AllowedLetters: FullAlphabet,
            CharHeight: 340,
            Tracking: 188,
            SpaceTracking: 188, // unused — no literal space in this mask
            PreferredFontFamily: "LICENSE PLATE USA",
            FallbackFontFamilies: ["Roboto Condensed", "Overpass"]);

        var monthField = new FieldDef(
            Id: MonthFieldId,
            Label: "Mes",
            Mask: "AAA",
            AllowedLetters: FullAlphabet,
            CharHeight: 65,
            Tracking: 63,
            SpaceTracking: 63, // unused
            PreferredFontFamily: "CharlesWright-Bold",
            FallbackFontFamilies: ["Roboto Condensed", "Overpass"],
            // Unlike everywhere else in this app, this picker is exclusive: a mask of "AAA" can't
            // tell "a real month abbreviation" from "three random letters", so the closed list is
            // the only thing enforcing a plausible value here.
            DropdownOptions: MonthAbbreviations,
            RequireDropdownSelection: true);

        var yearField = new FieldDef(
            Id: YearFieldId,
            Label: "Año",
            Mask: "9999",
            AllowedLetters: string.Empty,
            CharHeight: 50,
            Tracking: 48,
            SpaceTracking: 48, // unused
            PreferredFontFamily: "CharlesWright-Bold",
            FallbackFontFamilies: ["Roboto Condensed", "Overpass"],
            // Random License Plate should produce a plausible inspection year, not any 4 random
            // digits (a real sticker is never from the future, and "9999" reads as obviously fake).
            RandomYearMin: 1990);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "US-CA",
            DisplayName: "USA - California",
            Size: new SizeMm(1728, 892), // native template units; mapped to real mm by the app at export time
            Elements: elements,
            Fields: [plateNumberField, monthField, yearField]);
    }

    [Fact]
    public void GenerateAndWrite_CaliforniaTemplate()
    {
        PlateDocument document = BuildCaliforniaPlate();
        string json = PlateDocumentSerializer.Serialize(document);

        Directory.CreateDirectory(Path.GetDirectoryName(TemplateOutputPath)!);
        File.WriteAllText(TemplateOutputPath, json);

        Assert.True(File.Exists(TemplateOutputPath));
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsShapeAndFieldCounts_AndDropdownOptions()
    {
        PlateDocument original = BuildCaliforniaPlate();
        string json = PlateDocumentSerializer.Serialize(original);
        PlateDocument reloaded = PlateDocumentSerializer.Deserialize(json);

        Assert.Equal(original.Elements.Count, reloaded.Elements.Count);
        Assert.Equal(3, reloaded.Fields.Count);
        Assert.Equal(3, reloaded.Elements.OfType<Element.TextField>().Count());

        FieldDef reloadedMonth = reloaded.Fields.Single(f => f.Id == MonthFieldId);
        Assert.NotNull(reloadedMonth.DropdownOptions);
        Assert.Equal(MonthAbbreviations, reloadedMonth.DropdownOptions);

        FieldDef reloadedYear = reloaded.Fields.Single(f => f.Id == YearFieldId);
        Assert.Null(reloadedYear.DropdownOptions);
    }

    [Fact]
    public void MaskValidator_PlateNumber_AcceptsAnyMixOfDigitsAndLetters()
    {
        FieldDef field = BuildCaliforniaPlate().Fields.Single(f => f.Id == PlateNumberFieldId);

        Assert.True(MaskValidator.IsValid(field, "7YJL967"));
        Assert.True(MaskValidator.TryNormalize(field, "7yjl967", out string normalized));
        Assert.Equal("7YJL967", normalized);
        Assert.False(MaskValidator.IsValid(field, "7YJL96")); // wrong shape (6 chars)
    }

    [Fact]
    public void MaskValidator_Month_OnlyAcceptsOneOfTheTwelveDropdownOptions()
    {
        // Unlike other picker fields in this app, the month is a closed choice: a mask of "AAA"
        // can't tell "a real month abbreviation" from "three random letters" on its own, so
        // RequireDropdownSelection is what actually enforces a plausible value.
        FieldDef field = BuildCaliforniaPlate().Fields.Single(f => f.Id == MonthFieldId);

        Assert.True(MaskValidator.IsValid(field, "JUN")); // a real option
        Assert.False(MaskValidator.IsValid(field, "XYZ")); // right shape, not one of the 12
        Assert.False(MaskValidator.IsValid(field, "JU")); // wrong length
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        PlateDocument document = BuildCaliforniaPlate();
        PlateDocument staticOnly = document with { Elements = document.Elements.Where(e => e is not Element.TextField).ToList() };

        Scene scene = SceneCompiler.Compile(staticOnly, new Dictionary<string, string>(), new ThrowingTextOutliner());
        string svg = SvgWriter.Write(scene);

        Assert.True(SizeBudget.IsWithinTarget(svg), $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<rect", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<ellipse", svg, StringComparison.OrdinalIgnoreCase);
        // The cursive wordmark's self-intersecting curves need this to render its counters correctly.
        Assert.Contains("fill-rule=\"evenodd\"", svg, StringComparison.Ordinal);
    }

    private sealed class ThrowingTextOutliner : ITextOutliner
    {
        public IReadOnlyList<PathData> Outline(
            string text, string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies, double charHeight, double tracking, double spaceTracking) =>
            throw new InvalidOperationException("No text fields should be compiled in this static-only test.");
    }
}
