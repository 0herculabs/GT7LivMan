using System.Text.Json.Serialization;
using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Model;

/// <summary>
/// One node in a <see cref="PlateDocument"/>. Generic and composable on purpose: the Spain MVP
/// template is just <see cref="Shape"/>s imported from the base SVG plus one <see cref="TextField"/>
/// for the plate number — a later country or a free-form decal is the same tree with more/fewer nodes.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Group), "group")]
[JsonDerivedType(typeof(Shape), "shape")]
[JsonDerivedType(typeof(TextField), "textField")]
public abstract record Element
{
    /// <summary>User-editable on top of the element's own (already-flattened) geometry — how the canvas editor moves/scales/rotates a node without re-baking its path.</summary>
    public Affine2 Transform { get; init; } = Affine2.Identity;

    public sealed record Group(IReadOnlyList<Element> Children) : Element;

    public sealed record Shape(PathData Path, RgbColor Fill) : Element;

    /// <summary>
    /// Placeholder resolved against a <see cref="FieldDef"/> by <see cref="FieldId"/> and rendered
    /// by an <see cref="ITextOutliner"/> at compile time. <see cref="CharStart"/>/<see cref="CharCount"/>
    /// render only a slice of the field's value (default: the whole thing) — e.g. Portugal's
    /// inspection-date sticker is one 4-digit field split across two stacked TextField elements,
    /// one showing characters 0-1 (month) above the divider line and the other 2-3 (year) below it.
    /// <see cref="Anchor"/> decides what the transform origin means horizontally.
    /// </summary>
    public sealed record TextField(
        string FieldId,
        RgbColor Fill,
        int CharStart = 0,
        int CharCount = -1,
        TextAnchor Anchor = TextAnchor.Left) : Element;
}
