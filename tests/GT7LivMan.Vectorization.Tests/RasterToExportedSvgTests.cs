using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Vectorization.Tests;

/// <summary>
/// The full SVG Generator chain for a raster upload, exactly as <c>VinylGeneratorViewModel</c>
/// runs it: file on disk → <see cref="RasterVectorizer"/> → <see cref="GenericSvgImporter"/> →
/// <see cref="PlateDocument"/> (no fields) → <see cref="SceneCompiler"/> → <see cref="ExportEscalation"/>.
/// Mirrors what EndToEndPlateExportTests already does for the License Plate Generator's templates.
/// </summary>
public class RasterToExportedSvgTests
{
    private static string CreateSampleLogoPng()
    {
        using var bitmap = new Bitmap(300, 300, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.FillEllipse(Brushes.Red, 60, 60, 180, 180);
            g.FillRectangle(Brushes.Navy, 110, 110, 80, 80);
        }

        string path = Path.Combine(Path.GetTempPath(), $"gt7livman-e2e-logo-{Guid.NewGuid():N}.png");
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    [Fact]
    public void RasterUpload_TracedAndImportedAndExported_ProducesConformantSvgWithinBudget()
    {
        string pngPath = CreateSampleLogoPng();
        try
        {
            string tracedSvg = RasterVectorizer.Trace(pngPath, colorCount: 4, simplification: 1.0);
            GenericSvgImporter.Result imported = GenericSvgImporter.Import(tracedSvg);

            Assert.NotEmpty(imported.Elements);

            var document = new PlateDocument(
                SchemaVersion: PlateDocument.CurrentSchemaVersion,
                CountryCode: "VINYL",
                DisplayName: "Vinilo de prueba",
                Size: imported.Size,
                Elements: imported.Elements,
                Fields: []);

            Core.Rendering.Scene scene = SceneCompiler.Compile(document, new Dictionary<string, string>(), new ThrowingTextOutliner());
            string exportedSvg = ExportEscalation.Write(scene);

            Assert.True(
                SizeBudget.IsWithinTarget(exportedSvg),
                $"Expected <= {SizeBudget.TargetBytes} bytes, got {SizeBudget.MeasureUtf8Bytes(exportedSvg)}.");
            Assert.DoesNotContain("<text", exportedSvg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<image", exportedSvg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stroke", exportedSvg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("gradient", exportedSvg, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("version=\"1.1\"", exportedSvg, StringComparison.Ordinal);

            string outDir = Path.Combine(RepoPaths.Root, "out");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "vinyl-raster-sample.svg"), exportedSvg);
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void RasterUpload_RingOnTransparentBackground_KeepsItsHole()
    {
        // The counter of an "e" on a logo with a transparent background: the tracer outlines the
        // glyph as a solid blob and describes the hole as a separate transparent region on top of
        // it, so without HoleCutter the letter exports filled in solid.
        using var bitmap = new Bitmap(400, 400, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        using (var ring = new System.Drawing.Drawing2D.GraphicsPath())
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            ring.AddEllipse(50, 50, 300, 300);
            ring.AddEllipse(150, 150, 100, 100);
            g.FillPath(Brushes.Red, ring);
        }

        string pngPath = Path.Combine(Path.GetTempPath(), $"gt7livman-ring-{Guid.NewGuid():N}.png");
        bitmap.Save(pngPath, ImageFormat.Png);
        try
        {
            string tracedSvg = RasterVectorizer.Trace(pngPath, colorCount: 4, simplification: 1.0);
            GenericSvgImporter.Result imported = GenericSvgImporter.Import(tracedSvg);
            IReadOnlyList<Element> withHoles = HoleCutter.CutHoles(imported.Elements, imported.TransparentRegions);

            var ringShape = Assert.IsType<Element.Shape>(Assert.Single(withHoles));
            Assert.Equal(FillRule.EvenOdd, ringShape.Path.Rule);
            Assert.Equal(2, ringShape.Path.SubPaths.Count);

            // The hole's contour must sit inside the ring's, not the other way round — getting that
            // backwards inverts the artwork (the background gets cut into the shape instead).
            static double HorizontalSpan(SubPath sub)
            {
                List<double> xs = [sub.Start.X, .. sub.Segments.OfType<Seg.Line>().Select(l => l.To.X), .. sub.Segments.OfType<Seg.Quad>().Select(q => q.To.X)];
                return xs.Max() - xs.Min();
            }

            Assert.True(HorizontalSpan(ringShape.Path.SubPaths[1]) < HorizontalSpan(ringShape.Path.SubPaths[0]));
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    private sealed class ThrowingTextOutliner : ITextOutliner
    {
        public IReadOnlyList<PathData> Outline(
            string text, string preferredFontFamily, IReadOnlyList<string> fallbackFontFamilies, double charHeight, double tracking, double spaceTracking) =>
            throw new InvalidOperationException("A vinyl document has no text fields — nothing should ever call the outliner.");
    }
}
