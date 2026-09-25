using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Rendering;

namespace GT7LivMan.Core.Model;

/// <summary>
/// Flattens a <see cref="PlateDocument"/> plus field values into a <see cref="Scene"/> — the single
/// source of truth <see cref="Svg.SvgWriter"/> and the WPF preview both render from.
/// </summary>
public static class SceneCompiler
{
    public static Scene Compile(PlateDocument document, IReadOnlyDictionary<string, string> fieldValues, ITextOutliner outliner)
    {
        Dictionary<string, FieldDef> fieldsById = document.Fields.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var elements = new List<SceneElement>();

        Walk(document.Elements, Affine2.Identity, fieldsById, fieldValues, outliner, elements);

        return new Scene(document.Size, elements);
    }

    private static void Walk(
        IReadOnlyList<Element> nodes,
        Affine2 ambient,
        IReadOnlyDictionary<string, FieldDef> fieldsById,
        IReadOnlyDictionary<string, string> fieldValues,
        ITextOutliner outliner,
        List<SceneElement> sink)
    {
        foreach (Element node in nodes)
        {
            Affine2 effective = ambient * node.Transform;

            switch (node)
            {
                case Element.Group group:
                    Walk(group.Children, effective, fieldsById, fieldValues, outliner, sink);
                    break;

                case Element.Shape shape:
                    sink.Add(new SceneElement(PathDataTransform.Apply(shape.Path, effective), shape.Fill));
                    break;

                case Element.TextField textField:
                    CompileTextField(textField, effective, fieldsById, fieldValues, outliner, sink);
                    break;

                default:
                    throw new NotSupportedException($"Unknown element type {node.GetType()}.");
            }
        }
    }

    private static void CompileTextField(
        Element.TextField textField,
        Affine2 effective,
        IReadOnlyDictionary<string, FieldDef> fieldsById,
        IReadOnlyDictionary<string, string> fieldValues,
        ITextOutliner outliner,
        List<SceneElement> sink)
    {
        if (!fieldsById.TryGetValue(textField.FieldId, out FieldDef? field))
        {
            throw new InvalidOperationException($"TextField references unknown field id '{textField.FieldId}'.");
        }

        string fullValue = fieldValues.GetValueOrDefault(textField.FieldId, string.Empty);
        if (fullValue.Length == 0)
        {
            return;
        }

        int start = Math.Clamp(textField.CharStart, 0, fullValue.Length);
        int count = textField.CharCount < 0
            ? fullValue.Length - start
            : Math.Min(textField.CharCount, fullValue.Length - start);
        string value = fullValue.Substring(start, count);
        if (value.Length == 0)
        {
            return;
        }

        IReadOnlyList<PathData> glyphs = Outline(value, field, outliner);
        if (glyphs.Count == 0)
        {
            return;
        }

        // The outliner lays text out from its own origin leftwards-first; shifting here (in the
        // text's local space, before the element's transform) is what makes a Center anchor keep
        // the block put no matter which characters were typed.
        double advanceWidth = TextMetrics.AdvanceWidth(value, field.Tracking, field.SpaceTracking);
        double anchorOffset = TextMetrics.AnchorOffset(textField.Anchor, advanceWidth);
        Affine2 placement = effective * Affine2.Translate(anchorOffset, 0);

        // Merge every glyph's subpaths into one PathData: one <path> per field beats one per
        // glyph for byte size, and they all share a fill so nothing about z-order is lost.
        var subPaths = new List<SubPath>();
        foreach (PathData glyph in glyphs)
        {
            subPaths.AddRange(PathDataTransform.Apply(glyph, placement).SubPaths);
        }

        sink.Add(new SceneElement(new PathData(subPaths, FillRule.NonZero), textField.Fill));
    }

    /// <summary>
    /// The font's outlines for <paramref name="value"/>, except that any character with one of the
    /// field's <see cref="FieldDef.GlyphShapes"/> gets that shape instead, centered in its cell; the
    /// runs of characters in between are still outlined by the font, at their usual positions.
    /// </summary>
    private static List<PathData> Outline(string value, FieldDef field, ITextOutliner outliner)
    {
        IReadOnlyList<PathData> OutlineRun(string text) => outliner.Outline(
            text, field.PreferredFontFamily, field.FallbackFontFamilies, field.CharHeight, field.Tracking, field.SpaceTracking);

        if (field.GlyphShapes is not { Count: > 0 } shapes)
        {
            return [.. OutlineRun(value)];
        }

        var result = new List<PathData>();
        int runStart = 0;
        double runPenX = 0;
        double penX = 0;

        void FlushRun(int end)
        {
            if (end > runStart)
            {
                result.AddRange(OutlineRun(value[runStart..end]).Select(g => PathDataTransform.Apply(g, Affine2.Translate(runPenX, 0))));
            }
        }

        for (int i = 0; i < value.Length; i++)
        {
            double advance = TextMetrics.Advance(value[i], field.Tracking, field.SpaceTracking);
            if (shapes.TryGetValue(value[i].ToString(), out PathData? shape))
            {
                FlushRun(i);
                result.Add(PathDataTransform.Apply(shape, Affine2.Translate(penX + (advance / 2), 0)));
                runStart = i + 1;
                runPenX = penX + advance;
            }

            penX += advance;
        }

        FlushRun(value.Length);
        return result;
    }
}
