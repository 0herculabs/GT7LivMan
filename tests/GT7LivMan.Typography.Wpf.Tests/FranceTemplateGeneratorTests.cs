using System.IO;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Typography.Wpf.Tests;

/// <summary>
/// Builds templates/france.json from france_plate.svg. Lives here, next to Mexico, because the SVG
/// uses &lt;use&gt;, a clip-path and a text "F" (<see cref="DesignSvgFlattener"/>). Two fields: the
/// SIV number ("AV-456-CE": 2 letters, 3 digits, 2 letters — never I, O or U) and the department
/// number in the right-hand band, which the owner picks freely (e.g. 75 for Paris).
/// </summary>
public class FranceTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string DepartmentFieldId = "department";

    /// <summary>I, O and U are never used on SIV plates (too easily read as 1, 0 and V).</summary>
    private const string SivLetters = "ABCDEFGHJKLMNPQRSTVWXYZ";

    /// <summary>Metropolitan departments: 01-95, Corsica as 2A/2B instead of 20. The overseas ones (971-976) are left out: three digits don't fit the band at this size.</summary>
    private static readonly string[] Departments =
    [
        .. Enumerable.Range(1, 19).Select(n => n.ToString("D2")),
        "2A", "2B",
        .. Enumerable.Range(21, 75).Select(n => n.ToString("D2")),
    ];

    private const string PlateFont = "Alte DIN 1451 Mittelschrift"; // same as Portugal
    private static readonly string[] PlateFallbacks = ["Roboto Condensed", "Overpass"];

    // Measured off a screenshot of a real plate ("AV-456-CE"). That plate is proportionally longer
    // than this SVG's, so its number is scaled uniformly (keeping the letters' shape) to span the
    // same 91% of the white area between the bands (x 65.5..454.5): scale 0.666, giving character
    // height 66.6, pitch 41.7 (the characters nearly touch), and a 32.9 cell for each dash.
    // Centered horizontally between the bands and vertically on the plate, as in the screenshot.
    private const double CharHeight = 66.6;
    private const double Baseline = 95.3;

    private static PlateDocument BuildFrancePlate()
    {
        string svgPath = Path.Combine(RepoPaths.Root, "assets", "base", "france_plate.svg");
        var elements = DesignSvgFlattener.Flatten(File.ReadAllText(svgPath))
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // The outline is drawn last in the SVG so the bands meet it cleanly; the characters go
        // under it anyway (they never reach it), so they're simply added after.
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#000000"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(260, Baseline),
        });

        // Department: white, centered in the right band, in its lower part (screenshot: 34% of
        // the band's height, from 58% to 91% down).
        elements.Add(new Element.TextField(DepartmentFieldId, RgbColor.Parse("#ffffff"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(484.75, 108.3),
        });

        var plateNumber = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            Mask: "AA-999-AA",
            AllowedLetters: SivLetters,
            CharHeight: CharHeight,
            Tracking: 41.7,
            SpaceTracking: 32.9,
            PreferredFontFamily: PlateFont,
            FallbackFontFamilies: PlateFallbacks,
            // DIN's own dash is wider than the plate's and sits below mid-height; the screenshot's
            // is 17.3 x 10.7 (scaled), centered on the characters' mid-height.
            GlyphShapes: new Dictionary<string, PathData>
            {
                ["-"] = Shapes.RoundedRect(-8.65, -(CharHeight / 2) - 5.35, 17.3, 10.7, 1),
            });

        var department = new FieldDef(
            Id: DepartmentFieldId,
            Label: "Departamento",
            Mask: "XX",
            AllowedLetters: "AB",
            CharHeight: 37.7,
            Tracking: 25.5, // the pair spans 51 of the 60-wide band, as in the screenshot
            SpaceTracking: 25.5, // unused
            PreferredFontFamily: PlateFont,
            FallbackFontFamilies: PlateFallbacks,
            DropdownOptions: Departments,
            RequireDropdownSelection: true);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "FR",
            DisplayName: "France",
            Size: new SizeMm(520, 124),
            Elements: elements,
            Fields: [plateNumber, department]);
    }

    private static FieldDef Field(string id) => BuildFrancePlate().Fields.Single(f => f.Id == id);

    [Fact]
    public void GenerateAndWrite_FranceTemplate()
    {
        string outputPath = Path.Combine(RepoPaths.Root, "templates", "france.json");
        File.WriteAllText(outputPath, PlateDocumentSerializer.Serialize(BuildFrancePlate()));

        Assert.True(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("av456ce", "AV-456-CE")]
    [InlineData("AV-456-CE", "AV-456-CE")]
    public void AutoFormat_InsertsTheDashes(string rawInput, string expected)
    {
        Assert.Equal(expected, MaskFormatter.AutoFormat(Field(PlateNumberFieldId), rawInput));
    }

    [Theory]
    [InlineData("AV-456-CE", true)]
    [InlineData("AI-456-CE", false)] // no I
    [InlineData("AV-456-CO", false)] // no O
    [InlineData("UV-456-CE", false)] // no U
    [InlineData("AV-45-CE", false)]
    public void PlateNumber_IsTheSivFormat(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(Field(PlateNumberFieldId), value));
    }

    [Theory]
    [InlineData("75", true)]
    [InlineData("2A", true)]
    [InlineData("01", true)]
    [InlineData("20", false)] // Corsica is 2A/2B
    [InlineData("96", false)]
    public void Department_IsAClosedListOfRealDepartments(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(Field(DepartmentFieldId), value));
    }

    [Fact]
    public void Stars_AreAllExpandedFromTheirUse()
    {
        var starFill = RgbColor.Parse("#ffd11a");
        Assert.Equal(12, BuildFrancePlate().Elements.OfType<Element.Shape>().Count(s => s.Fill == starFill));
    }

    [Fact]
    public void PlateNumber_FitsBetweenTheBands()
    {
        FieldDef field = Field(PlateNumberFieldId);
        double width = TextMetrics.AdvanceWidth("AV-456-CE", field.Tracking, field.SpaceTracking);

        Assert.InRange(260 - (width / 2), 65.5, 454.5);
        Assert.InRange(260 + (width / 2), 65.5, 454.5);
    }

    [Theory]
    [InlineData("AV-456-CE", "75")]
    [InlineData("WM-888-WM", "2B")]
    public void CompileAndExport_WithRealFont_StaysWithinTarget(string plate, string department)
    {
        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoPaths.Root, "fonts"), Path.Combine(RepoPaths.Root, "fonts-bundled")));
        Scene scene = SceneCompiler.Compile(
            BuildFrancePlate(),
            new Dictionary<string, string> { [PlateNumberFieldId] = plate, [DepartmentFieldId] = department },
            outliner);
        string svg = ExportEscalation.Write(scene);

        Assert.True(SizeBudget.IsWithinTarget(svg), $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<use", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clip", svg, StringComparison.OrdinalIgnoreCase);

        string outDir = Path.Combine(RepoPaths.Root, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, $"france-{plate}.svg"), svg);
    }
}
