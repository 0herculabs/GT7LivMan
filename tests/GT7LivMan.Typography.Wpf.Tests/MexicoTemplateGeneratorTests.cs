using System.Globalization;
using System.IO;
using System.Xml.Linq;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Typography.Wpf.Tests;

/// <summary>
/// Builds templates/mexico.json from mexico_plate.svg. Lives here rather than with the other
/// generators in Core.Tests because this SVG has live text, a clip-path and a translucent layer
/// that need WPF to flatten (<see cref="DesignSvgFlattener"/>).
/// </summary>
public class MexicoTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // Measured off a real plate photo (estmex2024.jpg, "LNX-224-B", 1200x600 for a 744x372 plate
    // here, so 0.62 units per pixel): character height 228px -> 141; pitch 124px (0.544 of the
    // height) -> 77, the same center to center whether the neighbour is a character or a dash, so
    // the dashes take a full cell. The 9 cells, 693 wide, are centered on the plate (x 8..752).
    private const double CharHeight = 141;
    private const double Tracking = 77;
    private const double FirstCellLeft = 380 - (9 * Tracking / 2);

    // Vertically centered in the free band between the "Estado de México" heading and the
    // footer art, rather than copying the photo's position: this SVG's layout differs below.
    private const double Baseline = 300;

    private static readonly string[] PlateFontFallbacks = ["Roboto Condensed", "Overpass"];

    /// <summary>
    /// Text smaller than this (in canvas units, on a 453-tall canvas) is dropped: the SVG's size-8
    /// "GOBIERNO DEL / ESTADO DE MÉXICO" is ~6 units tall, unreadable on a car and ~2.5 KB of
    /// outlines that export simplification mangles anyway.
    /// </summary>
    private const double MinTextSize = 10;

    private const double ArtTolerance = 1;

    private static PlateDocument BuildMexicoPlate()
    {
        string svgPath = Path.Combine(RepoPaths.Root, "assets", "base", "mexico_plate.svg");
        // Detailed art (the outlined text, the logo) is simplified once here at 1 unit: that halves
        // it to ~10 KB, and — straight segments from then on — export escalation leaves it alone,
        // so the plate number gets the rest of the budget. Measured over 60 random plates, every
        // one then exports at the gentlest step (0.5) or none; the costliest, "888-888-8", at 1.
        var elements = DesignSvgFlattener.Flatten(PrepareForGt7(File.ReadAllText(svgPath)))
            .Select(s => (Element)new Element.Shape(DesignSvgFlattener.IsDetailed(s.Path) ? PathDataSimplifier.Simplify(s.Path, ArtTolerance) : s.Path, s.Fill))
            .ToList();

        RgbColor ink = RgbColor.Parse("#111111");

        // LICENSE PLATE USA has no usable dash (its '-' is a "DO NOT ENTER" road sign), so the two
        // dashes are static art at their fixed cells, and the field is drawn in three slices that
        // skip them. Photo: 62px wide, 20px thick, centered 51% down the character height.
        foreach (int cell in new[] { 3, 7 })
        {
            double centerX = FirstCellLeft + ((cell + 0.5) * Tracking);
            double centerY = Baseline - CharHeight + (0.51 * CharHeight);
            const double dashWidth = 38;
            const double dashThickness = 12;
            elements.Add(new Element.Shape(
                Shapes.RoundedRect(centerX - (dashWidth / 2), centerY - (dashThickness / 2), dashWidth, dashThickness, 2), ink));
        }

        foreach ((int start, int count) in new[] { (0, 3), (4, 3), (8, 1) })
        {
            elements.Add(new Element.TextField(PlateNumberFieldId, ink, CharStart: start, CharCount: count, Anchor: TextAnchor.Left)
            {
                Transform = Affine2.Translate(FirstCellLeft + (start * Tracking), Baseline),
            });
        }

        var field = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            // Letters and digits in any position, as asked — each 'X' is a free mix.
            Mask: "XXX-XXX-X",
            AllowedLetters: FullAlphabet,
            CharHeight: CharHeight,
            Tracking: Tracking,
            SpaceTracking: Tracking, // unused: the dashes are never outlined, see above
            PreferredFontFamily: "LICENSE PLATE USA",
            FallbackFontFamilies: PlateFontFallbacks);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "MX",
            DisplayName: "Mexico",
            Size: new SizeMm(760, 453),
            Elements: elements,
            Fields: [field]);
    }

    /// <summary>
    /// Everything this SVG draws doesn't fit GT7's 15 KB alongside a plate number (LICENSE PLATE
    /// USA's digits are very detailed: "888-888-8" alone is ~13 KB unsimplified). Dropped: the
    /// tiny footer text, and the heading's 2-unit outline stroke — ~10 KB of outlines on its own,
    /// and a ring that thin doesn't survive simplification anyway.
    /// </summary>
    private static string PrepareForGt7(string svg)
    {
        XDocument doc = XDocument.Parse(svg);
        foreach (XElement text in doc.Descendants().Where(e => e.Name.LocalName == "text").ToList())
        {
            if (double.Parse((string)text.Attribute("font-size")!, CultureInfo.InvariantCulture) < MinTextSize)
            {
                text.Remove();
                continue;
            }

            text.Attribute("stroke")?.Remove();
            text.Attribute("stroke-width")?.Remove();
        }

        return doc.ToString();
    }

    [Fact]
    public void GenerateAndWrite_MexicoTemplate()
    {
        string json = PlateDocumentSerializer.Serialize(BuildMexicoPlate());

        string outputPath = Path.Combine(RepoPaths.Root, "templates", "mexico.json");
        File.WriteAllText(outputPath, json);

        Assert.True(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("lnx224b", "LNX-224-B")]
    [InlineData("LNX-224-B", "LNX-224-B")]
    [InlineData("a1b2c3d", "A1B-2C3-D")]
    public void AutoFormat_InsertsTheDashes(string rawInput, string expected)
    {
        Assert.Equal(expected, MaskFormatter.AutoFormat(BuildMexicoPlate().Fields[0], rawInput));
    }

    [Fact]
    public void Slices_CoverEveryCharacterExceptTheDashes()
    {
        var slices = BuildMexicoPlate().Elements.OfType<Element.TextField>()
            .SelectMany(t => Enumerable.Range(t.CharStart, t.CharCount))
            .Order();

        Assert.Equal([0, 1, 2, 4, 5, 6, 8], slices);
    }

    [Fact]
    public void FlattenedArt_HasNoTextClipOrTranslucency_AndKeepsTheSvgColors()
    {
        PlateDocument document = BuildMexicoPlate();
        var fills = document.Elements.OfType<Element.Shape>().Select(s => s.Fill).ToHashSet();

        Assert.Contains(RgbColor.Parse("#ef4d43"), fills); // red bands, heading
        Assert.Contains(RgbColor.Parse("#f8afaa"), fills); // white triangles at 55% over the red band
        Assert.DoesNotContain(RgbColor.Parse("#a73b32"), fills); // heading stroke, dropped for the budget
        Assert.DoesNotContain(RgbColor.Parse("#666666"), fills); // size-8 footer text, dropped
    }

    [Theory]
    [InlineData("LNX-224-B")]
    [InlineData("WWW-888-M")]
    [InlineData("MMM-MMM-M")]
    [InlineData("888-888-8")] // the most expensive: LICENSE PLATE USA's 8 is very detailed
    public void CompileAndExport_WithRealFont_StaysWithinGt7Limit(string plate)
    {
        var outliner = new WpfGlyphOutliner(new FontResolver(Path.Combine(RepoPaths.Root, "fonts"), Path.Combine(RepoPaths.Root, "fonts-bundled")));
        Scene scene = SceneCompiler.Compile(BuildMexicoPlate(), new Dictionary<string, string> { [PlateNumberFieldId] = plate }, outliner);
        string svg = ExportEscalation.Write(scene);

        Assert.True(SizeBudget.IsWithinGt7Limit(svg), $"Expected < {SizeBudget.MaxBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(svg)}.");
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clip", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("opacity", svg, StringComparison.OrdinalIgnoreCase);

        string outDir = Path.Combine(RepoPaths.Root, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, $"mexico-{plate}.svg"), svg);
    }
}
