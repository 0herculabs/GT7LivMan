using System.Text;
using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Rendering;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// Serializes a <see cref="Scene"/> to an SVG conforming to the GT7 Decal Uploader's rules: SVG
/// 1.1, no &lt;text&gt;, no embedded bitmaps, no gradients/filters/blend modes, no XML
/// declaration/comments/ids/metadata, transparent background (we simply never paint one).
/// Fill rule is a standard, widely-supported SVG feature (unlike the above) — a path imported as
/// <see cref="FillRule.EvenOdd"/> (e.g. hand-authored artwork with self-intersecting curves, like
/// California's cursive wordmark) gets an explicit <c>fill-rule="evenodd"</c>; NonZero (the SVG
/// default, and what every glyph outline winds) omits the attribute entirely.
/// </summary>
public static class SvgWriter
{
    public static string Write(Scene scene, SvgWriterOptions? options = null)
    {
        options ??= SvgWriterOptions.Default;

        var sb = new StringBuilder();
        int decimals = options.Decimals;
        string w = SvgFormat.Number(scene.Size.Width, decimals);
        string h = SvgFormat.Number(scene.Size.Height, decimals);

        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"")
          .Append(w).Append("mm\" height=\"").Append(h)
          .Append("mm\" viewBox=\"0 0 ").Append(w).Append(' ').Append(h).Append("\">");

        AppendElements(sb, scene.Elements, options);

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static void AppendElements(StringBuilder sb, IReadOnlyList<SceneElement> elements, SvgWriterOptions options)
    {
        int i = 0;
        while (i < elements.Count)
        {
            int runEnd = i + 1;
            if (options.GroupByFill)
            {
                // Same fill AND same winding rule: two EvenOdd shapes can share a <g>, but an
                // EvenOdd shape can't be folded into a run of NonZero ones (or vice versa) just
                // because the color matches — the rule applies to the whole group.
                while (runEnd < elements.Count
                    && elements[runEnd].Fill == elements[i].Fill
                    && elements[runEnd].Path.Rule == elements[i].Path.Rule)
                {
                    runEnd++;
                }
            }

            int runLength = runEnd - i;
            string hex = elements[i].Fill.ToHex();
            string fillRuleAttr = elements[i].Path.Rule == FillRule.EvenOdd ? " fill-rule=\"evenodd\"" : string.Empty;

            if (runLength == 1)
            {
                sb.Append("<path fill=\"").Append(hex).Append('"').Append(fillRuleAttr).Append(" d=\"")
                  .Append(PathSerializer.Serialize(elements[i].Path, options)).Append("\"/>");
            }
            else
            {
                sb.Append("<g fill=\"").Append(hex).Append('"').Append(fillRuleAttr).Append('>');
                for (int j = i; j < runEnd; j++)
                {
                    sb.Append("<path d=\"").Append(PathSerializer.Serialize(elements[j].Path, options)).Append("\"/>");
                }

                sb.Append("</g>");
            }

            i = runEnd;
        }
    }
}
