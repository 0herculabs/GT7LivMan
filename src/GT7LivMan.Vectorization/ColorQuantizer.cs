using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace GT7LivMan.Vectorization;

/// <summary>
/// Real color quantization (median-cut), run on a bitmap before handing it to ImageTracer.NET.
/// Necessary for two reasons: ImageTracer.NET's own <c>Options.ColorQuantization.NumberOfColors</c>
/// is never actually read anywhere in its tracing path — confirmed by inspecting the vendored
/// source, it's dead configuration — so without a real quantization step the "how many colors"
/// control would silently do nothing. More importantly for performance: a real photo's antialiased
/// edges and JPEG compression noise contain thousands of near-distinct pixel colors, and each one
/// becomes its own tiny traced region against ImageTracer.NET's fixed internal palette — collapsing
/// the image down to a small number of flat colors first (what a decal needs anyway) is what keeps
/// the downstream tracing fast, not just working at a smaller pixel size.
/// </summary>
internal static class ColorQuantizer
{
    /// <summary>
    /// Alpha is flattened to "in the decal" or "not in the decal" at this cutoff rather than kept
    /// per-pixel. GT7 has no alpha on a decal fill at all, but the real reason is performance: a
    /// real logo PNG has antialiased edges spanning all 256 alpha levels, and ImageTracer.NET keys
    /// its palette (and so its per-color full-image layer allocations) on the whole ARGB tuple — so
    /// quantizing RGB alone still leaves 256 distinct "colors" behind. One observed logo was a
    /// single RGB color whose entire shape lived in the alpha channel.
    /// </summary>
    private const byte OpaqueAlphaThreshold = 128;

    // Euclidean distance in 8-bit RGB. A color count is an upper bound: almost
    // identical shades are less useful in a flat decal than one continuous region.
    private const int SimilarColorDistance = 24;

    /// <summary>Every transparent pixel collapses to this exact value, so they contribute exactly one palette entry between them no matter what RGB the source left behind them.</summary>
    private static readonly Color TransparentEntry = Color.FromArgb(0, 0, 0, 0);

    public static Bitmap Quantize(Bitmap source, int colorCount)
    {
        int width = source.Width;
        int height = source.Height;
        var rect = new Rectangle(0, 0, width, height);

        (byte B, byte G, byte R, byte A)[] pixels = ReadPixels(source, rect);

        // Only opaque pixels get clustered. A transparent pixel's RGB is invisible (and often
        // garbage — a fully transparent PNG pixel is frequently pure black), so letting it into the
        // clustering both wastes palette slots and drags cluster centers toward a color nobody sees.
        var opaquePixels = new List<(byte B, byte G, byte R, byte A)>(pixels.Length);
        foreach ((byte B, byte G, byte R, byte A) pixel in pixels)
        {
            if (pixel.A >= OpaqueAlphaThreshold)
            {
                opaquePixels.Add(pixel);
            }
        }

        Color[] palette = opaquePixels.Count > 0
            ? MedianCut(opaquePixels, colorCount)
            : [Color.FromArgb(255, 0, 0, 0)];

        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        WritePixels(result, rect, pixels, palette);
        return result;
    }

    private static (byte B, byte G, byte R, byte A)[] ReadPixels(Bitmap source, Rectangle rect)
    {
        BitmapData data = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] bytes = new byte[data.Stride * rect.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            int pixelCount = rect.Width * rect.Height;
            var pixels = new (byte B, byte G, byte R, byte A)[pixelCount];
            // Format32bppArgb is stored as B,G,R,A per pixel in memory (little-endian ARGB).
            for (int y = 0; y < rect.Height; y++)
            {
                int rowStart = y * data.Stride;
                int pixelRowStart = y * rect.Width;
                for (int x = 0; x < rect.Width; x++)
                {
                    int offset = rowStart + (x * 4);
                    pixels[pixelRowStart + x] = (bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
                }
            }

            return pixels;
        }
        finally
        {
            source.UnlockBits(data);
        }
    }

    private static void WritePixels(Bitmap result, Rectangle rect, (byte B, byte G, byte R, byte A)[] pixels, Color[] palette)
    {
        BitmapData data = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] bytes = new byte[data.Stride * rect.Height];
            for (int y = 0; y < rect.Height; y++)
            {
                int rowStart = y * data.Stride;
                int pixelRowStart = y * rect.Width;
                for (int x = 0; x < rect.Width; x++)
                {
                    (byte B, byte G, byte R, byte A) pixel = pixels[pixelRowStart + x];
                    Color output = pixel.A >= OpaqueAlphaThreshold ? Nearest(pixel, palette) : TransparentEntry;
                    int offset = rowStart + (x * 4);
                    bytes[offset] = output.B;
                    bytes[offset + 1] = output.G;
                    bytes[offset + 2] = output.R;
                    bytes[offset + 3] = output.A;
                }
            }

            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            result.UnlockBits(data);
        }
    }

    /// <summary>Splits pixels into up to <paramref name="colorCount"/> buckets near the median along the widest channel, keeping equal channel values together, then merges near-identical average colors before assigning pixels.</summary>
    private static Color[] MedianCut(IReadOnlyList<(byte B, byte G, byte R, byte A)> pixels, int colorCount)
    {
        colorCount = Math.Max(1, colorCount);
        var initialBucket = new List<(byte B, byte G, byte R, byte A)>(pixels);
        var buckets = new List<List<(byte B, byte G, byte R, byte A)>> { initialBucket };

        while (buckets.Count < colorCount)
        {
            int splitIndex = IndexOfWidestBucket(buckets);
            if (splitIndex < 0)
            {
                break; // every remaining bucket is down to a single distinct-enough color
            }

            List<(byte B, byte G, byte R, byte A)> bucket = buckets[splitIndex];
            int channel = WidestChannel(bucket);
            bucket.Sort((a, b) => channel switch
            {
                0 => a.R.CompareTo(b.R),
                1 => a.G.CompareTo(b.G),
                _ => a.B.CompareTo(b.B),
            });

            int mid = bucket.Count / 2;
            // Never divide a run of identical channel values between two buckets: a large
            // flat logo color would otherwise occupy several palette slots, mixed with
            // unrelated colors. Antialiased edges then map to those artificial mixtures.
            int Channel((byte B, byte G, byte R, byte A) p) => channel == 0 ? p.R : channel == 1 ? p.G : p.B;
            int lower = mid;
            while (lower > 0 && Channel(bucket[lower - 1]) == Channel(bucket[mid])) lower--;
            int upper = mid;
            while (upper < bucket.Count && Channel(bucket[upper]) == Channel(bucket[mid])) upper++;
            mid = lower > 0 && (upper == bucket.Count || mid - lower <= upper - mid) ? lower : upper;
            buckets[splitIndex] = bucket.GetRange(0, mid);
            buckets.Add(bucket.GetRange(mid, bucket.Count - mid));
        }

        // Neighboring buckets can still average to almost the same color. Tracing them
        // separately fragments otherwise solid edges, and PathOmit removes pieces of
        // the resulting thin strips. Merge near-identical shades before assigning pixels.
        for (int i = 0; i < buckets.Count; i++)
        {
            for (int j = i + 1; j < buckets.Count; j++)
            {
                Color a = AverageColor(buckets[i]);
                Color b = AverageColor(buckets[j]);
                int dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
                if (dr * dr + dg * dg + db * db > SimilarColorDistance * SimilarColorDistance) continue;
                buckets[i].AddRange(buckets[j]);
                buckets.RemoveAt(j);
                i = -1;
                break;
            }
        }
        return [.. buckets.Where(b => b.Count > 0).Select(AverageColor)];
    }

    private static int IndexOfWidestBucket(List<List<(byte B, byte G, byte R, byte A)>> buckets)
    {
        int bestIndex = -1;
        int bestRange = 0;
        for (int i = 0; i < buckets.Count; i++)
        {
            if (buckets[i].Count < 2)
            {
                continue;
            }

            int range = ChannelRange(buckets[i], WidestChannel(buckets[i]));
            if (range > bestRange)
            {
                bestRange = range;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int WidestChannel(List<(byte B, byte G, byte R, byte A)> bucket)
    {
        (byte MinR, byte MaxR, byte MinG, byte MaxG, byte MinB, byte MaxB) = MinMax(bucket);
        int rRange = MaxR - MinR, gRange = MaxG - MinG, bRange = MaxB - MinB;
        if (rRange >= gRange && rRange >= bRange)
        {
            return 0;
        }

        return gRange >= bRange ? 1 : 2;
    }

    private static int ChannelRange(List<(byte B, byte G, byte R, byte A)> bucket, int channel)
    {
        (byte MinR, byte MaxR, byte MinG, byte MaxG, byte MinB, byte MaxB) = MinMax(bucket);
        return channel switch
        {
            0 => MaxR - MinR,
            1 => MaxG - MinG,
            _ => MaxB - MinB,
        };
    }

    private static (byte MinR, byte MaxR, byte MinG, byte MaxG, byte MinB, byte MaxB) MinMax(List<(byte B, byte G, byte R, byte A)> bucket)
    {
        byte minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;
        foreach ((byte b, byte g, byte r, _) in bucket)
        {
            if (r < minR) minR = r;
            if (r > maxR) maxR = r;
            if (g < minG) minG = g;
            if (g > maxG) maxG = g;
            if (b < minB) minB = b;
            if (b > maxB) maxB = b;
        }

        return (minR, maxR, minG, maxG, minB, maxB);
    }

    private static Color AverageColor(List<(byte B, byte G, byte R, byte A)> bucket)
    {
        long r = 0, g = 0, b = 0;
        foreach ((byte bb, byte gg, byte rr, _) in bucket)
        {
            r += rr;
            g += gg;
            b += bb;
        }

        int n = bucket.Count;
        return Color.FromArgb(255, (int)(r / n), (int)(g / n), (int)(b / n));
    }

    private static Color Nearest((byte B, byte G, byte R, byte A) pixel, Color[] palette)
    {
        Color best = palette[0];
        int bestDist = int.MaxValue;
        foreach (Color c in palette)
        {
            int dr = c.R - pixel.R;
            int dg = c.G - pixel.G;
            int db = c.B - pixel.B;
            int dist = (dr * dr) + (dg * dg) + (db * db);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = c;
            }
        }

        return best;
    }
}
