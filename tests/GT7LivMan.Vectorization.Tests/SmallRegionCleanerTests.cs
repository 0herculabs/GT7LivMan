using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Svg;

namespace GT7LivMan.Vectorization.Tests;

public class SmallRegionCleanerTests
{
    [Fact]
    public void Clean_RemovesThinSpeckleButPreservesCompactSmallDetail()
    {
        using var bitmap = new Bitmap(80, 40, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.FillRectangle(Brushes.Black, 5, 5, 20, 1);   // 20 px: edge speckle
            g.FillRectangle(Brushes.Black, 50, 5, 5, 6);   // 30 px: intentional dot
        }

        SmallRegionCleaner.Clean(bitmap, minimumArea: 24);

        Assert.Equal(0, bitmap.GetPixel(10, 5).A);
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(52, 8).ToArgb());
    }

    [Fact]
    public void Trace_DefaultSettings_PreserveCompactDisconnectedLogoDetails()
    {
        using var bitmap = new Bitmap(240, 160, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.FillEllipse(Brushes.Black, 20, 10, 200, 140);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            g.FillRectangle(Brushes.Transparent, 50, 90, 140, 45);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            for (int i = 0; i < 6; i++)
            {
                g.FillRectangle(Brushes.Black, 60 + (i * 20), 105, 5, 6);
            }
        }

        string path = Path.Combine(Path.GetTempPath(), $"gt7livman-small-details-{Guid.NewGuid():N}.png");
        try
        {
            bitmap.Save(path, ImageFormat.Png);
            string svg = RasterVectorizer.Trace(path, colorCount: 2, simplification: 1);
            GenericSvgImporter.Result imported = GenericSvgImporter.Import(svg);

            int shapeCount = imported.Elements.OfType<Element.Shape>().Count();
            Assert.InRange(shapeCount, 7, 8); // main silhouette + all six compact dots
        }
        finally
        {
            File.Delete(path);
        }
    }
}
