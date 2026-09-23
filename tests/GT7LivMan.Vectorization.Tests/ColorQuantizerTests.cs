using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace GT7LivMan.Vectorization.Tests;

public class ColorQuantizerTests
{
    [Fact]
    public void Quantize_UnbalancedFlatColors_PreservesEveryColorWhenPaletteHasRoom()
    {
        using var source = new Bitmap(100, 10, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(source))
        {
            g.Clear(Color.Red);
            g.FillRectangle(Brushes.Green, 80, 0, 15, 10);
            g.FillRectangle(Brushes.Blue, 95, 0, 5, 10);
        }

        using Bitmap result = ColorQuantizer.Quantize(source, 3);

        Assert.Equal(Color.Red.ToArgb(), result.GetPixel(0, 5).ToArgb());
        Assert.Equal(Color.Green.ToArgb(), result.GetPixel(85, 5).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), result.GetPixel(99, 5).ToArgb());
    }

    [Fact]
    public void Trace_StraightTriangleWithNearIdenticalShades_DoesNotFragmentItsEdges()
    {
        // Resampling a flat logo produces slightly different RGB values along its
        // edges. These must not become separate jagged regions in the exported path.
        using var source = new Bitmap(240, 160, PixelFormat.Format32bppArgb);
        for (int y = 10; y < 150; y++)
        {
            int right = 220 - 3 * Math.Abs(y - 80);
            for (int x = 10; x <= right; x++)
            {
                source.SetPixel(x, y, (x + y) % 3 == 0
                    ? Color.FromArgb(0, 126, 59)
                    : Color.FromArgb(0, 119, 56));
            }
        }

        string path = Path.Combine(Path.GetTempPath(), $"gt7livman-edge-{Guid.NewGuid():N}.png");
        try
        {
            source.Save(path, ImageFormat.Png);
            string svg = RasterVectorizer.Trace(path, 6, 1);
            var imported = Core.Svg.GenericSvgImporter.Import(svg);
            var shape = Assert.IsType<Core.Model.Element.Shape>(Assert.Single(imported.Elements));
            int segments = shape.Path.SubPaths.Sum(p => p.Segments.Count);
            Assert.True(segments <= 12, $"A straight triangle produced {segments} segments.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Quantize_MergingSimilarShades_PreservesTransparencyAndDistinctDetails()
    {
        using var source = new Bitmap(100, 20, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(source))
        {
            g.Clear(Color.Transparent);
            using var green = new SolidBrush(Color.FromArgb(0, 119, 56));
            using var nearbyGreen = new SolidBrush(Color.FromArgb(0, 126, 59));
            g.FillRectangle(green, 10, 0, 40, 20);
            g.FillRectangle(nearbyGreen, 50, 0, 40, 20);
            g.FillRectangle(Brushes.Red, 90, 0, 10, 20);
        }

        using Bitmap result = ColorQuantizer.Quantize(source, 6);

        Assert.Equal(0, result.GetPixel(0, 5).A);
        Assert.Equal(result.GetPixel(20, 5).ToArgb(), result.GetPixel(60, 5).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), result.GetPixel(95, 5).ToArgb());
    }
}
