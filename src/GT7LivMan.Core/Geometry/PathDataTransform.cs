namespace GT7LivMan.Core.Geometry;

/// <summary>Applies an <see cref="Affine2"/> to a <see cref="PathData"/>, flattening a shape's own transform into its coordinates.</summary>
public static class PathDataTransform
{
    public static PathData Apply(PathData path, Affine2 t)
    {
        if (t.IsIdentity)
        {
            return path;
        }

        var subPaths = new List<SubPath>(path.SubPaths.Count);
        foreach (SubPath sub in path.SubPaths)
        {
            var segs = new List<Seg>(sub.Segments.Count);
            foreach (Seg seg in sub.Segments)
            {
                segs.Add(seg switch
                {
                    Seg.Line l => new Seg.Line(t.Apply(l.To)),
                    Seg.Cubic c => new Seg.Cubic(t.Apply(c.C1), t.Apply(c.C2), t.Apply(c.To)),
                    Seg.Quad q => new Seg.Quad(t.Apply(q.C), t.Apply(q.To)),
                    Seg.Arc a => TransformArc(a, t),
                    Seg.Close => new Seg.Close(),
                    _ => throw new NotSupportedException($"Unknown segment type {seg.GetType()}."),
                });
            }

            subPaths.Add(new SubPath(t.Apply(sub.Start), segs));
        }

        return new PathData(subPaths, path.Rule);
    }

    private static Seg.Arc TransformArc(Seg.Arc arc, Affine2 t)
    {
        if (!t.IsSimilarity())
        {
            throw new NotSupportedException(
                "Transforming an elliptical arc needs a similarity transform (rotation/translation/uniform " +
                "scale only) — skew or non-uniform scale would require re-fitting the ellipse, which isn't implemented.");
        }

        double det = (t.A * t.D) - (t.B * t.C);
        if (det < 0)
        {
            throw new NotSupportedException(
                "Transforming an elliptical arc through a reflection (negative-determinant transform) isn't implemented.");
        }

        double scale = Math.Sqrt((t.A * t.A) + (t.B * t.B));
        double rotationDeg = Math.Atan2(t.B, t.A) * 180.0 / Math.PI;

        return new Seg.Arc(
            arc.Rx * scale,
            arc.Ry * scale,
            arc.XAxisRotationDeg + rotationDeg,
            arc.LargeArc,
            arc.SweepClockwise,
            t.Apply(arc.To));
    }
}
