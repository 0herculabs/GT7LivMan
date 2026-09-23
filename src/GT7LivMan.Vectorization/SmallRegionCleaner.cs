using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GT7LivMan.Vectorization;

/// <summary>
/// Removes isolated color components that are too small to be meaningful at the tracer's working
/// resolution. Unlike ImageTracer's PathOmit, this measures filled area instead of perimeter, so a
/// compact dot survives while a long one-pixel antialiasing fragment does not.
/// </summary>
internal static class SmallRegionCleaner
{
    public static void Clean(Bitmap bitmap, int minimumArea)
    {
        if (minimumArea <= 1)
        {
            return;
        }

        int width = bitmap.Width;
        int height = bitmap.Height;
        var rect = new Rectangle(0, 0, width, height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int strideInts = data.Stride / 4;
            int[] raw = new int[strideInts * height];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);

            int[] pixels = new int[width * height];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(raw, y * strideInts, pixels, y * width, width);
            }

            bool[] visited = new bool[pixels.Length];
            var queue = new Queue<int>();
            var component = new List<int>();

            for (int start = 0; start < pixels.Length; start++)
            {
                if (visited[start])
                {
                    continue;
                }

                int color = pixels[start];
                visited[start] = true;
                queue.Enqueue(start);
                component.Clear();

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    component.Add(current);
                    int cx = current % width;
                    int cy = current / width;

                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int ny = cy + oy;
                        if (ny < 0 || ny >= height) continue;
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = cx + ox;
                            if ((ox == 0 && oy == 0) || nx < 0 || nx >= width) continue;
                            int neighbor = ny * width + nx;
                            if (!visited[neighbor] && pixels[neighbor] == color)
                            {
                                visited[neighbor] = true;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }

                if (component.Count >= minimumArea)
                {
                    continue;
                }

                var borderColors = new Dictionary<int, int>();
                foreach (int current in component)
                {
                    int cx = current % width;
                    int cy = current / width;
                    Add(cx - 1, cy);
                    Add(cx + 1, cy);
                    Add(cx, cy - 1);
                    Add(cx, cy + 1);

                    void Add(int x, int y)
                    {
                        if (x < 0 || x >= width || y < 0 || y >= height) return;
                        int neighborColor = pixels[y * width + x];
                        if (neighborColor != color)
                        {
                            borderColors[neighborColor] = borderColors.GetValueOrDefault(neighborColor) + 1;
                        }
                    }
                }

                if (borderColors.Count == 0)
                {
                    continue;
                }

                int replacement = default;
                int bestCount = -1;
                foreach ((int candidate, int count) in borderColors)
                {
                    if (count > bestCount)
                    {
                        replacement = candidate;
                        bestCount = count;
                    }
                }

                foreach (int current in component)
                {
                    pixels[current] = replacement;
                }
            }

            for (int y = 0; y < height; y++)
            {
                Array.Copy(pixels, y * width, raw, y * strideInts, width);
            }
            Marshal.Copy(raw, 0, data.Scan0, raw.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
