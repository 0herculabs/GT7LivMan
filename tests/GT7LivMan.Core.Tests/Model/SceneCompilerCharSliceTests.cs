using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Rendering;

namespace GT7LivMan.Core.Tests.Model;

/// <summary>Covers Element.TextField's CharStart/CharCount slicing — the mechanism behind a stacked "one field, two lines" layout like Portugal's inspection-date sticker (month above a divider, year below, both sourced from one 4-digit field).</summary>
public class SceneCompilerCharSliceTests
{
    [Fact]
    public void Compile_SlicesOneFieldAcrossTwoTextFieldElements()
    {
        var field = new FieldDef(
            Id: "inspectionDate",
            Label: "Fecha ITV",
            Mask: "9999",
            AllowedLetters: string.Empty,
            CharHeight: 50,
            Tracking: 30,
            SpaceTracking: 30,
            PreferredFontFamily: "DIN1451",
            FallbackFontFamilies: ["Overpass"]);

        var document = new PlateDocument(
            SchemaVersion: PlateDocument.CurrentSchemaVersion,
            CountryCode: "PT",
            DisplayName: "test",
            Size: new SizeMm(200, 100),
            Elements:
            [
                new Element.TextField("inspectionDate", RgbColor.Parse("#111111"), CharStart: 0, CharCount: 2)
                {
                    Transform = Affine2.Translate(0, 40),
                },
                new Element.TextField("inspectionDate", RgbColor.Parse("#111111"), CharStart: 2, CharCount: 2)
                {
                    Transform = Affine2.Translate(0, 90),
                },
            ],
            Fields: [field]);

        var outliner = new RecordingOutliner();
        var scene = SceneCompiler.Compile(document, new Dictionary<string, string> { ["inspectionDate"] = "0212" }, outliner);

        Assert.Equal(["02", "12"], outliner.CallsWithText);
        Assert.Equal(2, scene.Elements.Count);
    }

    [Fact]
    public void Compile_CenterAnchor_ShiftsTheBlockLeftByHalfItsAdvanceWidth()
    {
        var field = new FieldDef(
            Id: "plate",
            Label: "Matrícula",
            Mask: "999",
            AllowedLetters: string.Empty,
            CharHeight: 100,
            Tracking: 40,
            SpaceTracking: 40,
            PreferredFontFamily: "DIN1451",
            FallbackFontFamilies: ["Overpass"]);

        Scene Compile(TextAnchor anchor)
        {
            var document = new PlateDocument(
                SchemaVersion: PlateDocument.CurrentSchemaVersion,
                CountryCode: "XX",
                DisplayName: "test",
                Size: new SizeMm(200, 100),
                Elements: [new Element.TextField("plate", RgbColor.Parse("#000000"), Anchor: anchor) { Transform = Affine2.Translate(100, 50) }],
                Fields: [field]);

            return SceneCompiler.Compile(document, new Dictionary<string, string> { ["plate"] = "123" }, new RecordingOutliner());
        }

        // The stub outliner puts every glyph's start at the text origin, so the subpath start
        // directly reveals where the block was anchored.
        double left = Compile(TextAnchor.Left).Elements[0].Path.SubPaths[0].Start.X;
        double center = Compile(TextAnchor.Center).Elements[0].Path.SubPaths[0].Start.X;
        double right = Compile(TextAnchor.Right).Elements[0].Path.SubPaths[0].Start.X;

        Assert.Equal(100, left, precision: 6);
        Assert.Equal(100 - (3 * 40 / 2.0), center, precision: 6); // advance width 120 -> shifted 60 left
        Assert.Equal(100 - (3 * 40), right, precision: 6);
    }

    private sealed class RecordingOutliner : ITextOutliner
    {
        public List<string> CallsWithText { get; } = [];

        public IReadOnlyList<PathData> Outline(
            string text, string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies, double charHeight, double tracking, double spaceTracking)
        {
            CallsWithText.Add(text);
            return [PathData.Single(new SubPath(new Pt(0, 0), [new Seg.Line(new Pt(1, 1)), new Seg.Close()]))];
        }
    }
}
