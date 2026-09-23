using System.Text.Json.Serialization;

namespace GT7LivMan.Core.Geometry;

/// <summary>
/// One drawing command within a subpath, in absolute coordinates. Modeled after our own
/// primitives (not WPF's Geometry types) so Core stays free of a WPF dependency.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Line), "line")]
[JsonDerivedType(typeof(Cubic), "cubic")]
[JsonDerivedType(typeof(Quad), "quad")]
[JsonDerivedType(typeof(Arc), "arc")]
[JsonDerivedType(typeof(Close), "close")]
public abstract record Seg
{
    public sealed record Line(Pt To) : Seg;

    public sealed record Cubic(Pt C1, Pt C2, Pt To) : Seg;

    public sealed record Quad(Pt C, Pt To) : Seg;

    /// <summary>SVG elliptical arc, endpoint parameterization (the only one SVG's "A" command supports).</summary>
    public sealed record Arc(double Rx, double Ry, double XAxisRotationDeg, bool LargeArc, bool SweepClockwise, Pt To) : Seg;

    /// <summary>Closes the current subpath back to its start point ("Z").</summary>
    public sealed record Close : Seg;
}
