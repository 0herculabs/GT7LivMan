using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Core.Tests.Svg;

public class GenericSvgImporterTests
{
    [Fact]
    public void Import_PlainRectWithHexFill_ProducesOneShape()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 50\">" +
                     "<rect x=\"0\" y=\"0\" width=\"100\" height=\"50\" fill=\"#ff0000\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        Element.Shape shape = Assert.IsType<Element.Shape>(Assert.Single(result.Elements));
        Assert.Equal(RgbColor.Parse("#ff0000"), shape.Fill);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Import_ReadsSizeFromViewBox_AndOffsetsForNonZeroOrigin()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"10 20 100 50\">" +
                     "<rect x=\"10\" y=\"20\" width=\"30\" height=\"10\" fill=\"#000000\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        Assert.Equal(new SizeMm(100, 50), result.Size);

        // The rect sits at the viewBox's own origin (10,20) — after the importer's offset
        // correction it should land back at the document's own (0,0).
        var shape = (Element.Shape)result.Elements.Single();
        SubPath sub = shape.Path.SubPaths[0];
        Assert.Equal(0, sub.Start.X, precision: 6);
        Assert.Equal(0, sub.Start.Y, precision: 6);
    }

    [Fact]
    public void Import_FallsBackToWidthHeight_WhenNoViewBox()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"300\" height=\"150\">" +
                     "<rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"#000\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        Assert.Equal(new SizeMm(300, 150), result.Size);
    }

    [Fact]
    public void Import_FallsBackToDefaultSize_WithWarning_WhenNeitherViewBoxNorSize()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"#000\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        Assert.Equal(new SizeMm(200, 200), result.Size);
        Assert.Contains(result.Warnings, w => w.Contains("200x200", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("rgb(255, 0, 0)", 255, 0, 0)]
    [InlineData("rgb(0,128,0)", 0, 128, 0)]
    [InlineData("red", 255, 0, 0)]
    [InlineData("navy", 0, 0, 128)]
    public void Import_ParsesRgbFunctionAndNamedColors(string paint, byte r, byte g, byte b)
    {
        string svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">" +
                     $"<rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"{paint}\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        var shape = (Element.Shape)result.Elements.Single();
        Assert.Equal(new RgbColor(r, g, b), shape.Fill);
    }

    [Fact]
    public void Import_ParsesFillFromStyleAttribute_NotJustThePresentationAttribute()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">" +
                     "<rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" style=\"fill:#00ff00;stroke:none\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        var shape = (Element.Shape)result.Elements.Single();
        Assert.Equal(RgbColor.Parse("#00ff00"), shape.Fill);
    }

    [Fact]
    public void Import_FlattensLinearGradientToAverageColor_AndWarns()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">" +
                     "<defs><linearGradient id=\"g1\">" +
                     "<stop offset=\"0\" stop-color=\"#000000\"/>" +
                     "<stop offset=\"1\" stop-color=\"#ffffff\"/>" +
                     "</linearGradient></defs>" +
                     "<rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"url(#g1)\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        var shape = (Element.Shape)result.Elements.Single();
        Assert.Equal(new RgbColor(128, 128, 128), shape.Fill); // average of black and white
        Assert.Contains(result.Warnings, w => w.Contains("degradado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Import_DoesNotEmitShapesFromInsideDefs()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">" +
                     "<defs><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"#ff0000\"/></defs>" +
                     "<rect x=\"0\" y=\"0\" width=\"5\" height=\"5\" fill=\"#0000ff\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        var shape = Assert.IsType<Element.Shape>(Assert.Single(result.Elements));
        Assert.Equal(RgbColor.Parse("#0000ff"), shape.Fill);
    }

    [Fact]
    public void Import_IgnoresOpacity_ButWarnsWhenLessThanOne()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">" +
                     "<rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"#ff0000\" opacity=\"0.5\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        Assert.Single(result.Elements); // still drawn, just opaque
        Assert.Contains(result.Warnings, w => w.Contains("opacidad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Import_SkipsTextImageAndUse_WithSummarizedWarnings()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">" +
                     "<text x=\"0\" y=\"10\" fill=\"#000\">Hola</text>" +
                     "<image x=\"0\" y=\"0\" width=\"10\" height=\"10\" href=\"data:image/png;base64,AAAA\"/>" +
                     "<use href=\"#missing\"/>" +
                     "<rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"#00ff00\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        Assert.Single(result.Elements); // only the rect
        Assert.Contains(result.Warnings, w => w.Contains("<text>", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("imagen", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, w => w.Contains("<use>", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_ImportsFilledPolygonAndPolyline()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">" +
                     "<polygon points=\"0,0 10,0 5,8\" fill=\"#111111\"/>" +
                     "<polyline points=\"0,0 10,0 5,8\" fill=\"#222222\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        Assert.Equal(2, result.Elements.Count);
    }

    [Fact]
    public void Import_SkipsUnfilledStrokeOnlyPolyline_WithWarning()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">" +
                     "<polyline points=\"0,0 10,0 5,8\" fill=\"none\" stroke=\"#000\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        Assert.Empty(result.Elements);
        Assert.Contains(result.Warnings, w => w.Contains("polyline", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_StrokeOnlyLine_ProducesAFilledRibbon()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 10\">" +
                     "<line x1=\"0\" y1=\"5\" x2=\"100\" y2=\"5\" stroke=\"#123456\" stroke-width=\"2\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        var shape = (Element.Shape)result.Elements.Single();
        Assert.Equal(RgbColor.Parse("#123456"), shape.Fill);
        Assert.True(shape.Path.SubPaths[0].IsClosed);
    }

    [Fact]
    public void Import_UnrecognizedColorName_SkipsShapeAndWarns_RatherThanThrowing()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">" +
                     "<rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"totally-not-a-color\"/></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        Assert.Empty(result.Elements);
        Assert.Contains(result.Warnings, w => w.Contains("totally-not-a-color", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_NeverThrows_OnAKitchenSinkOfUnsupportedContent()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">" +
                     "<text>x</text><image href=\"x\"/><use href=\"#x\"/>" +
                     "<rect fill=\"url(#missing)\" x=\"0\" y=\"0\" width=\"1\" height=\"1\"/>" +
                     "<circle cx=\"5\" cy=\"5\" r=\"0\" fill=\"#fff\"/>" +
                     "<polygon points=\"bad-data\" fill=\"#fff\"/>" +
                     "<path d=\"not a real path\" fill=\"#fff\"/>" +
                     "<weirdcustomtag fill=\"#fff\"/>" +
                     "</svg>";

        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);
        Assert.NotNull(result);
    }

    [Fact]
    public void Import_MalformedXml_ThrowsACleanFormatException()
    {
        // A user could upload a file that isn't actually valid SVG/XML at all (truncated,
        // corrupted, or just the wrong file renamed) — that should still fail with a clear,
        // catchable error, not an unhandled XmlException.
        Assert.Throws<FormatException>(() => GenericSvgImporter.Import("<svg><rect"));
    }

    [Fact]
    public void Import_InheritsGroupFill_ForChildrenWithNoFillOfTheirOwn()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">" +
                     "<g fill=\"#abcdef\"><rect x=\"0\" y=\"0\" width=\"5\" height=\"5\"/></g></svg>";
        GenericSvgImporter.Result result = GenericSvgImporter.Import(svg);

        var shape = (Element.Shape)result.Elements.Single();
        Assert.Equal(RgbColor.Parse("#abcdef"), shape.Fill);
    }
}
