using System.IO;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;
using Xunit.Abstractions;

namespace GT7LivMan.Typography.Wpf.Tests;

/// <summary>
/// Diagnostics, not assertions: reports which file each template's font resolves to, and how wide
/// each character's ink is relative to its fixed-pitch cell. The width report is how you tell
/// whether a template's Tracking suits the font it ends up using — anything over 1.0 would have
/// collided with its neighbour before <see cref="WpfGlyphOutliner"/> condensed it.
/// </summary>
public class FontResolverDiagnosticsTests(ITestOutputHelper output)
{
    private static FontResolver Resolver => new(
        Path.Combine(RepoPaths.Root, "fonts"),
        Path.Combine(RepoPaths.Root, "fonts-bundled"));

    [Fact]
    public void Print_WhereEachTemplateFontResolvesFrom()
    {
        foreach (string preferred in new[] { "MESPREG", "Alte DIN 1451 Mittelschrift", "CharlesWright-Bold", "LICENSE PLATE USA", "Roboto Condensed", "Overpass" })
        {
            FontResolution r = Resolver.Resolve(preferred, ["Roboto Condensed", "Overpass"]);
            output.WriteLine($"{preferred,-18} Source={r.Source,-17} IsFallback={r.IsFallback,-5} {r.Typeface.FontUri}");
        }
    }

    /// <summary>
    /// How wide a font's characters naturally are, per unit of cap height — the single number that
    /// says whether a font suits a plate at all. A real plate font sits near 0.6; much above that
    /// and it can't fit a plate's fixed cells without being squashed.
    /// </summary>
    [Fact]
    public void Print_NaturalInkWidthPerCapHeight()
    {
        var outliner = new WpfGlyphOutliner(Resolver);
        const double charHeight = 142;

        foreach (string family in new[] { "MESPREG", "Alte DIN 1451 Mittelschrift", "CharlesWright-Bold", "LICENSE PLATE USA", "Roboto Condensed", "Overpass" })
        {
            double total = 0;
            int counted = 0;
            double widest = 0;
            char widestChar = ' ';

            foreach (char ch in "023456789ABCDEFGHJKLMNOPQRSTUVWXYZ") // skips the naturally narrow 1 and I
            {
                // An enormous cell disables condensing, so this measures the glyph's true width.
                IReadOnlyList<PathData> glyphs = outliner.Outline(
                    ch.ToString(), family, ["Roboto Condensed"], charHeight, tracking: 100_000, spaceTracking: 100_000);

                if (glyphs.Count == 0 || glyphs[0].SubPaths.Count == 0)
                {
                    continue;
                }

                (double min, double max) = HorizontalInkExtent(glyphs[0]);
                double width = max - min;
                total += width;
                counted++;
                if (width > widest)
                {
                    widest = width;
                    widestChar = ch;
                }
            }

            output.WriteLine($"{family,-18} avg={total / counted / charHeight:F3}  widest='{widestChar}' {widest / charHeight:F3}");
        }
    }

    [Theory]
    [InlineData("MESPREG", 237, 173)] // Spain, with the real plate font present
    [InlineData("Alte DIN 1451 Mittelschrift", 142, 95)] // Portugal
    public void Print_InkWidthPerCell(string preferredFamily, double charHeight, double tracking)
    {
        var outliner = new WpfGlyphOutliner(Resolver);
        FontResolution resolution = Resolver.Resolve(preferredFamily, ["Roboto Condensed", "Overpass"]);
        output.WriteLine($"{preferredFamily}: charHeight={charHeight} tracking={tracking} -> {resolution.Typeface.FontUri}");

        foreach (char ch in "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        {
            IReadOnlyList<PathData> glyphs = outliner.Outline(
                ch.ToString(), preferredFamily, ["Roboto Condensed", "Overpass"], charHeight, tracking, tracking);

            if (glyphs.Count == 0 || glyphs[0].SubPaths.Count == 0)
            {
                output.WriteLine($"  '{ch}': no ink");
                continue;
            }

            (double min, double max) = HorizontalInkExtent(glyphs[0]);
            output.WriteLine($"  '{ch}': ink={max - min,6:F1}  ink/cell={(max - min) / tracking,5:F2}  left={min,6:F1}");
        }
    }

    private static (double Min, double Max) HorizontalInkExtent(PathData path)
    {
        double min = double.MaxValue;
        double max = double.MinValue;

        void Visit(Pt p)
        {
            min = Math.Min(min, p.X);
            max = Math.Max(max, p.X);
        }

        foreach (SubPath sub in path.SubPaths)
        {
            Visit(sub.Start);
            foreach (Seg seg in sub.Segments)
            {
                switch (seg)
                {
                    case Seg.Line l: Visit(l.To); break;
                    case Seg.Cubic c: Visit(c.C1); Visit(c.C2); Visit(c.To); break;
                    case Seg.Quad q: Visit(q.C); Visit(q.To); break;
                    case Seg.Arc a: Visit(a.To); break;
                }
            }
        }

        return (min, max);
    }
}
