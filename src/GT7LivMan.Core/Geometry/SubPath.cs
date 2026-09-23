namespace GT7LivMan.Core.Geometry;

/// <summary>A single "M ... Z?" contour: a start point plus the segments drawn from it.</summary>
public sealed record SubPath(Pt Start, IReadOnlyList<Seg> Segments)
{
    public bool IsClosed => Segments.Count > 0 && Segments[^1] is Seg.Close;
}
