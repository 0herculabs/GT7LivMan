using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace GT7LivMan.Vectorization.Tests;

public class RasterVectorizerTests
{
    /// <summary>Draws a tiny flat-color synthetic logo (not a checked-in binary asset) — a red circle on a white background — and saves it as a real PNG file, the shape <see cref="RasterVectorizer.Trace"/> is actually built for.</summary>
    private static string CreateSampleLogoPng()
    {
        using var bitmap = new Bitmap(200, 200, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.FillEllipse(Brushes.Red, 40, 40, 120, 120);
        }

        string path = Path.Combine(Path.GetTempPath(), $"gt7livman-test-logo-{Guid.NewGuid():N}.png");
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    [Fact]
    public void Trace_SimpleTwoColorLogo_ProducesAnSvgWithFlatColorPaths()
    {
        string pngPath = CreateSampleLogoPng();
        try
        {
            string svg = RasterVectorizer.Trace(pngPath, colorCount: 4, simplification: 1.0);

            Assert.False(string.IsNullOrWhiteSpace(svg));
            Assert.Contains("<svg", svg, StringComparison.Ordinal);
            Assert.Contains("<path", svg, StringComparison.Ordinal);
            Assert.Contains("viewBox", svg, StringComparison.Ordinal);
            Assert.DoesNotContain("<image", svg, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void Trace_IsCultureInvariant_UnderSpanishLocale()
    {
        // ImageTracer.NET itself formats path coordinates with the calling thread's current
        // culture, not invariant — under es-ES a naive ToString() turns "90.5" into "90,5", which
        // any SVG path parser (including our own) misreads as two separate numbers instead of one,
        // silently corrupting the traced shape. RasterVectorizer.Trace must produce identical
        // output regardless of the caller's ambient culture.
        string pngPath = CreateSampleLogoPng();
        CultureInfo original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
            string underSpanish = RasterVectorizer.Trace(pngPath, colorCount: 4, simplification: 1.0);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            string underInvariant = RasterVectorizer.Trace(pngPath, colorCount: 4, simplification: 1.0);

            Assert.Equal(underInvariant, underSpanish);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void Trace_FewerColors_ProducesNoMorePathsThanMoreColors()
    {
        string pngPath = CreateSampleLogoPng();
        try
        {
            string svgFewColors = RasterVectorizer.Trace(pngPath, colorCount: 2, simplification: 1.0);
            string svgManyColors = RasterVectorizer.Trace(pngPath, colorCount: 16, simplification: 1.0);

            int CountPaths(string svg) => svg.Split("<path").Length - 1;

            Assert.True(
                CountPaths(svgFewColors) <= CountPaths(svgManyColors),
                $"Expected quantizing to fewer colors to not produce more regions ({CountPaths(svgFewColors)} vs {CountPaths(svgManyColors)}).");
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    private static string CreateColorfulGradientPng(int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Rectangle(0, 0, size, size), Color.Red, Color.Blue, 45f))
        {
            // A smooth gradient is the worst case for color count: every row of pixels differs
            // slightly, so without real quantization this would produce thousands of distinct colors.
            g.FillRectangle(brush, 0, 0, size, size);
        }

        string path = Path.Combine(Path.GetTempPath(), $"gt7livman-test-gradient-{Guid.NewGuid():N}.png");
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    [Fact]
    public void Trace_QuantizesAGradient_DownToApproximatelyTheRequestedColorCount()
    {
        // Regression guard for a real bug: ImageTracer.NET's own ColorQuantization.NumberOfColors
        // option is silently ignored by its tracing path, so without RasterVectorizer's own
        // pre-quantization step this control would do nothing and a smooth gradient would trace to
        // hundreds of near-identical color regions.
        string pngPath = CreateColorfulGradientPng(200);
        try
        {
            string svg = RasterVectorizer.Trace(pngPath, colorCount: 4, simplification: 1.0);
            int distinctFills = Regex.Matches(svg, "fill=\"(rgb\\([^)]+\\))\"")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .Count();

            Assert.True(distinctFills <= 4, $"Expected at most 4 distinct fill colors, got {distinctFills}.");
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    /// <summary>
    /// A logo PNG as actually downloaded from the web: transparent background, antialiased edges,
    /// and — in the worst real case observed — every visible pixel the same RGB with the whole shape
    /// carried by the alpha channel alone.
    /// </summary>
    private static string CreateAntialiasedTransparentLogoPng(int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.Black, size / 8, size / 8, size * 3 / 4, size * 3 / 4);
            using var font = new Font("Arial", size / 6f, FontStyle.Bold);
            g.DrawString("GT", font, Brushes.Black, size / 4f, size * 0.4f);
        }

        string path = Path.Combine(Path.GetTempPath(), $"gt7livman-test-alpha-{Guid.NewGuid():N}.png");
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    [Fact]
    public void Trace_AntialiasedTransparentLogo_StaysFastAndDoesNotExplodeIntoHundredsOfColors()
    {
        // Regression guard for the bug that made real logos unusable: quantizing RGB while passing
        // the original per-pixel alpha through left 256 distinct ARGB values behind (antialiased
        // edges span every alpha level), and ImageTracer.NET keys its per-color full-image layer
        // allocations on the whole ARGB tuple — so the palette, and the cost, never actually shrank.
        string pngPath = CreateAntialiasedTransparentLogoPng(1200);
        try
        {
            var sw = Stopwatch.StartNew();
            string svg = RasterVectorizer.Trace(pngPath, colorCount: 6, simplification: 1.0);
            sw.Stop();

            int distinctFills = Regex.Matches(svg, "fill=\"(rgb\\([^)]+\\))\"")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .Count();

            Assert.True(distinctFills <= 7, $"Expected at most 7 distinct fills (6 colors + transparent), got {distinctFills}.");
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"Trace took {sw.Elapsed.TotalSeconds:F1}s, expected well under 10s.");
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void Trace_LargeImage_CompletesWithinASaneTimeBudget()
    {
        // Regression guard for a real performance bug: ImageTracer.NET's layer-separation step
        // used to allocate one full-image-sized array PER PALETTE COLOR regardless of how many
        // colors the image actually had (256, always) — a ~3000px source image took over a minute
        // and ~700 MB before RasterVectorizer started downscaling and pre-quantizing before tracing.
        // 2000x2000 here, well past the point that used to matter, should now trace in a few seconds.
        string pngPath = CreateColorfulGradientPng(2000);
        try
        {
            var sw = Stopwatch.StartNew();
            string svg = RasterVectorizer.Trace(pngPath, colorCount: 6, simplification: 1.0);
            sw.Stop();

            Assert.False(string.IsNullOrWhiteSpace(svg));
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), $"Trace took {sw.Elapsed.TotalSeconds:F1}s, expected well under 15s.");
        }
        finally
        {
            File.Delete(pngPath);
        }
    }
}
