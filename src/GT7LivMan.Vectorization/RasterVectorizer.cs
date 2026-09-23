using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Threading;
using ImageTracerNet;

namespace GT7LivMan.Vectorization;

/// <summary>
/// Thin wrapper around the vendored ImageTracer.NET (see ImageTracer/LICENSE-ImageTracer.NET.txt)
/// for the two knobs that actually matter for a decal — how many flat colors to quantize down to,
/// and how tightly the traced curves follow the source pixels — so callers never touch the
/// vendored library's own option types directly.
/// </summary>
public static class RasterVectorizer
{
    /// <summary>
    /// ImageTracer.NET's color-matching step compares every pixel against its full palette with no
    /// downsampling of its own — cost scales directly with pixel count, so a phone photo (routinely
    /// 3000+ px on a side) takes minutes and spikes CPU/RAM where a small test image takes
    /// milliseconds. GT7 decals are flat-fill art under a 15 KB budget anyway — nothing is lost by
    /// working at a much lower resolution before tracing, and this caps the worst case regardless of
    /// what the user uploads.
    /// </summary>
    private const int MaxWorkingDimension = 800;

    /// <summary>
    /// Traces a raster image file (JPG/PNG/etc. — anything <see cref="System.Drawing.Bitmap"/> can
    /// load) to an SVG string: quantizes it down to <paramref name="colorCount"/> flat colors, then
    /// outlines each color region as a path.
    /// </summary>
    /// <param name="colorCount">How many flat colors to quantize the image down to. A real photo
    /// will never look photographic at a handful of colors, but GT7's flat-fill-only, 15 KB budget
    /// makes that unavoidable — this tool targets logos/flat graphics, where a small count (the
    /// default here, well below ImageTracer.NET's own default of 16) keeps the traced region count,
    /// and so the exported byte size, down.</param>
    /// <param name="simplification">
    /// How hard to simplify the traced outline. Drives both the curve-fitting tolerance
    /// (ImageTracer.NET's line/quadratic-spline thresholds) and how small a region has to be before
    /// it's dropped entirely: higher values mean fewer, smoother, larger shapes (smaller export,
    /// less faithful to the source), lower values follow the source pixels more literally.
    /// </param>
    public static string Trace(string filePath, int colorCount, double simplification)
    {
        // Small raster details are cleaned by filled area before tracing. Keep ImageTracer's own
        // low default here: its PathOmit measures perimeter, which used to erase compact intentional
        // details such as the dots in the GitHub mark while retaining long one-pixel edge noise.
        int pathOmit = Math.Max(1, (int)Math.Round(BasePathOmit * simplification));
        return Trace(filePath, colorCount, simplification, pathOmit);
    }

    /// <summary>Region-size cutoff at <paramref name="simplification"/> = 1 (the UI default) — see <see cref="Trace(string, int, double)"/> for how it was chosen.</summary>
    private const int BasePathOmit = 8;

    /// <summary>Minimum connected filled area at the 800px working scale. Compact logo details survive; isolated antialiasing and compression fragments do not.</summary>
    private const int MinimumRegionArea = 24;

    internal static string Trace(string filePath, int colorCount, double simplification, int pathOmit)
    {
        var options = new Options
        {
            // Set for documentation purposes only — ImageTracer.NET never actually reads
            // ColorQuantization.NumberOfColors in its tracing path (dead configuration in the
            // vendored library). ColorQuantizer.Quantize below is what really controls color count.
            ColorQuantization = { NumberOfColors = colorCount },
            Tracing = { LTres = simplification, QTres = simplification, PathOmit = pathOmit },
            // Viewbox=true so the output <svg> declares its own width/height/viewBox — what
            // GenericSvgImporter reads to size the resulting PlateDocument.
            SvgRendering = { Viewbox = true },
        };

        // Not ImageTracer.ImageToSvg(string, Options): that overload loads its own Bitmap
        // internally and never disposes it, leaving filePath locked for the rest of the process —
        // loading (and disposing) it here instead keeps the file usable immediately afterward.
        using Bitmap original = new(filePath);
        using Bitmap downscaled = Downscale(original, MaxWorkingDimension);

        // Reducing to colorCount flat colors before tracing (not just relying on ImageTracer.NET's
        // own — nonfunctional — quantization) is what actually keeps this fast: an unprocessed
        // photo's antialiased edges and JPEG noise contain thousands of near-distinct pixel colors,
        // and each one becomes its own tiny traced region otherwise, regardless of image size.
        using Bitmap image = ColorQuantizer.Quantize(downscaled, colorCount);
        SmallRegionCleaner.Clean(image, MinimumRegionArea);

        // ImageTracer.NET formats every path coordinate with the calling thread's current culture,
        // not invariant — on a machine whose decimal separator isn't '.' (e.g. es-ES's ','), "90.5"
        // comes out as "90,5", which is silently misread as two separate numbers by any SVG path
        // parser (ours included), corrupting every traced shape without throwing. Force invariant
        // culture just for the call, then restore whatever the caller had.
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            return ImageTracer.ImageToSvg(image, options);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    /// <summary>Returns <paramref name="source"/> unchanged (as a fresh disposable copy, so the caller can always dispose the result) if it already fits within <paramref name="maxDimension"/> on its longer side, otherwise a proportionally scaled-down copy.</summary>
    private static Bitmap Downscale(Bitmap source, int maxDimension)
    {
        int longerSide = Math.Max(source.Width, source.Height);
        if (longerSide <= maxDimension)
        {
            return new Bitmap(source);
        }

        double scale = (double)maxDimension / longerSide;
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));

        var scaled = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, 0, 0, width, height);
        }

        return scaled;
    }
}
