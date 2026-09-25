using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>
/// Builds templates/argentina.json from argentina_plate.svg: the Mercosur plate Argentina has
/// issued since 2016 — "REPUBLICA ARGENTINA" on the blue band, black characters in the Argentine
/// Mercosur format "AB 603 SJ" (2 letters, 3 digits, 2 letters) in FE-Schrift, like Brazil's.
/// </summary>
public class ArgentinaTemplateGeneratorTests
{
    private const string PlateNumberFieldId = "plateNumber";
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static PlateDocument BuildArgentinaPlate()
    {
        string svgPath = Path.Combine(RepoPaths.Root, "assets", "base", "argentina_plate.svg");
        var elements = SvgReader.ReadStaticShapes(File.ReadAllText(svgPath))
            .Select(s => (Element)new Element.Shape(s.Path, s.Fill))
            .ToList();

        // Measured off a screenshot of a real plate ("AB 603 SJ", argentinaplate.png) drawn on
        // exactly this 713x231 canvas (frame and blue band line up), so pixels are units:
        //   character ink y 85..198 (114 tall), centers 83,166 | 280.5,353,426 | 544.5,619
        //   pitch averaged to 76 across letters and digits; 116.5 between the groups' facing
        //   centers, so each space cell is 116.5 - 76 = 40.5; block centered at x=351.5.
        // FE-Font's ink is 0.893 em tall against its 0.736 em cap height and reaches 0.16 em below
        // the baseline: CharHeight 94 gives 114 of ink, the baseline at 177.6 ends it at 198.
        elements.Add(new Element.TextField(PlateNumberFieldId, RgbColor.Parse("#111111"), Anchor: TextAnchor.Center)
        {
            Transform = Affine2.Translate(351.5, 177.6),
        });

        var field = new FieldDef(
            Id: PlateNumberFieldId,
            Label: "Número de matrícula",
            Mask: "AA 999 AA",
            AllowedLetters: FullAlphabet,
            CharHeight: 94,
            Tracking: 76,
            SpaceTracking: 40.5,
            PreferredFontFamily: "FE-Font",
            FallbackFontFamilies: ["Roboto Condensed", "Overpass"]);

        return new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "AR",
            DisplayName: "Argentina",
            Size: new SizeMm(713, 231),
            Elements: elements,
            Fields: [field]);
    }

    [Fact]
    public void GenerateAndWrite_ArgentinaTemplate()
    {
        string outputPath = Path.Combine(RepoPaths.Root, "templates", "argentina.json");
        File.WriteAllText(outputPath, PlateDocumentSerializer.Serialize(BuildArgentinaPlate()));

        Assert.True(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("ab603sj", "AB 603 SJ")]
    [InlineData("AB 603 SJ", "AB 603 SJ")]
    public void AutoFormat_InsertsTheSpaces(string rawInput, string expected)
    {
        Assert.Equal(expected, MaskFormatter.AutoFormat(BuildArgentinaPlate().Fields[0], rawInput));
    }

    [Theory]
    [InlineData("AB 603 SJ", true)]
    [InlineData("AB 60 SJ", false)]
    [InlineData("A1 603 SJ", false)]
    public void MaskValidator_AcceptsTheArgentineMercosurFormat(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(BuildArgentinaPlate().Fields[0], value));
    }

    [Fact]
    public void MercosurArc_IsAStroke_NotAFilledBlob()
    {
        // The arc under the stars is a fill="none" stroke inside the stars' white <g>: imported
        // right, it's a thin ribbon, not the open curve closed and filled.
        // Both are about the same size (the arc is shallow), so what tells them apart is the path
        // itself: a stroke becomes a ribbon of straight segments, the bug kept the original curves.
        var white = RgbColor.Parse("#ffffff");
        var arcs = BuildArgentinaPlate().Elements.OfType<Element.Shape>()
            .Where(s => s.Fill == white && Bounds(s.Path) is { Width: > 60, X: < 40 })
            .ToList();

        var arc = Assert.Single(arcs);
        Assert.DoesNotContain(arc.Path.SubPaths.SelectMany(sub => sub.Segments), seg => seg is Seg.Cubic);
    }

    private static (double X, double Width, double Height) Bounds(PathData path)
    {
        var points = path.SubPaths.SelectMany(sub => new[] { sub.Start }.Concat(sub.Segments.Select(seg => seg switch
        {
            Seg.Line l => l.To,
            Seg.Cubic c => c.To,
            Seg.Quad q => q.To,
            Seg.Arc a => a.To,
            _ => sub.Start,
        }))).ToList();
        return (points.Min(p => p.X), points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y));
    }

    [Fact]
    public void PlateNumber_FitsInsideTheFrame()
    {
        double width = TextMetrics.AdvanceWidth("AB 603 SJ", 76, 40.5);
        Assert.InRange(351.5 - (width / 2), 17, 695);
        Assert.InRange(351.5 + (width / 2), 17, 695);
    }

    [Fact]
    public void Compile_StaticElementsOnly_ProducesConformantSvgWithinBudget()
    {
        PlateDocument document = BuildArgentinaPlate();
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
