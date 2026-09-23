namespace GT7LivMan.Core.Geometry;

public static class Shapes
{
    /// <summary>
    /// Builds a rounded rectangle as an explicit path (straight edges + quarter-circle arcs)
    /// rather than an SVG &lt;rect rx&gt;, since GT7's parser support for rect corner radii is
    /// undocumented — a hand-built path sidesteps that unknown entirely.
    /// </summary>
    public static PathData RoundedRect(double x, double y, double width, double height, double r)
    {
        if (r <= 0)
        {
            return PathData.Single(new SubPath(new Pt(x, y),
            [
                new Seg.Line(new Pt(x + width, y)),
                new Seg.Line(new Pt(x + width, y + height)),
                new Seg.Line(new Pt(x, y + height)),
                new Seg.Close(),
            ]));
        }

        return PathData.Single(new SubPath(new Pt(x + r, y),
        [
            new Seg.Line(new Pt(x + width - r, y)),
            new Seg.Arc(r, r, 0, false, true, new Pt(x + width, y + r)),
            new Seg.Line(new Pt(x + width, y + height - r)),
            new Seg.Arc(r, r, 0, false, true, new Pt(x + width - r, y + height)),
            new Seg.Line(new Pt(x + r, y + height)),
            new Seg.Arc(r, r, 0, false, true, new Pt(x, y + height - r)),
            new Seg.Line(new Pt(x, y + r)),
            new Seg.Arc(r, r, 0, false, true, new Pt(x + r, y)),
            new Seg.Close(),
        ]));
    }

    /// <summary>The exact same boundary as <see cref="RoundedRect"/>, traversed the opposite way — pairing one of each as subpaths of a single NonZero-wound PathData produces a ring (see <see cref="RoundedRectRing"/>), the same way a glyph's outer contour and inner counter carve a hole in "O".</summary>
    public static PathData RoundedRectCcw(double x, double y, double width, double height, double r)
    {
        if (r <= 0)
        {
            return PathData.Single(new SubPath(new Pt(x, y),
            [
                new Seg.Line(new Pt(x, y + height)),
                new Seg.Line(new Pt(x + width, y + height)),
                new Seg.Line(new Pt(x + width, y)),
                new Seg.Close(),
            ]));
        }

        return PathData.Single(new SubPath(new Pt(x + r, y),
        [
            new Seg.Arc(r, r, 0, false, false, new Pt(x, y + r)),
            new Seg.Line(new Pt(x, y + height - r)),
            new Seg.Arc(r, r, 0, false, false, new Pt(x + r, y + height)),
            new Seg.Line(new Pt(x + width - r, y + height)),
            new Seg.Arc(r, r, 0, false, false, new Pt(x + width, y + height - r)),
            new Seg.Line(new Pt(x + width, y + r)),
            new Seg.Arc(r, r, 0, false, false, new Pt(x + width - r, y)),
            new Seg.Line(new Pt(x + r, y)),
            new Seg.Close(),
        ]));
    }

    /// <summary>
    /// A stroke-only rounded rect (fill="none", just an outline) as a single filled shape: an
    /// outer boundary (expanded by half the stroke width, wound clockwise) plus an inner boundary
    /// (inset by half the stroke width, wound counter-clockwise) — the two cancel under the NonZero
    /// fill rule everywhere except the thin ring between them. Needed because <see cref="Svg.SvgWriter"/>
    /// never emits an SVG "stroke" attribute, only flat fills.
    /// </summary>
    public static PathData RoundedRectRing(double x, double y, double width, double height, double r, double strokeWidth)
    {
        double half = strokeWidth / 2;
        PathData outer = RoundedRect(x - half, y - half, width + strokeWidth, height + strokeWidth, r + half);
        PathData inner = RoundedRectCcw(x + half, y + half, width - strokeWidth, height - strokeWidth, Math.Max(0, r - half));
        return new PathData([.. outer.SubPaths, .. inner.SubPaths], FillRule.NonZero);
    }

    /// <summary>A full circle as two semicircular arcs (SVG's "A" command can't span 360° in one go).</summary>
    public static PathData Circle(double cx, double cy, double r) => Ellipse(cx, cy, r, r);

    /// <summary>A full ellipse as two half-ellipse arcs, same reasoning as <see cref="Circle"/>.</summary>
    public static PathData Ellipse(double cx, double cy, double rx, double ry) => PathData.Single(new SubPath(new Pt(cx + rx, cy),
    [
        new Seg.Arc(rx, ry, 0, false, true, new Pt(cx - rx, cy)),
        new Seg.Arc(rx, ry, 0, false, true, new Pt(cx + rx, cy)),
        new Seg.Close(),
    ]));

    public static PathData Polygon(IReadOnlyList<Pt> points)
    {
        if (points.Count < 3)
        {
            throw new ArgumentException("A polygon needs at least 3 points.", nameof(points));
        }

        List<Seg> segs = [.. points.Skip(1).Select(p => (Seg)new Seg.Line(p)), new Seg.Close()];
        return PathData.Single(new SubPath(points[0], segs));
    }

    /// <summary>
    /// A stroke-only open path (fill="none", just a thin decorative line — e.g. a curved seam under
    /// a plate's color band) as a single filled ribbon: the flattened centerline offset by half the
    /// stroke width on each side, with square-cut ends at the two original endpoints. Needed for the
    /// same reason as <see cref="RoundedRectRing"/> — <see cref="Svg.SvgWriter"/> never emits a
    /// "stroke" attribute — but for an arbitrary open curve rather than a closed rounded rect.
    /// </summary>
    public static PathData StrokeToFill(SubPath open, double strokeWidth)
    {
        List<Pt> centerline = PathDataSimplifier.Flatten(open);
        if (centerline.Count < 2)
        {
            throw new ArgumentException("A stroked path needs at least 2 points.", nameof(open));
        }

        double half = strokeWidth / 2;
        var normals = new List<(double Nx, double Ny)>(centerline.Count);
        for (int i = 0; i < centerline.Count; i++)
        {
            // Interior points average the normals of their two adjacent segments so the ribbon
            // doesn't kink at each sampled point; endpoints just use their one segment's normal.
            (double Nx, double Ny) before = i > 0 ? SegmentNormal(centerline[i - 1], centerline[i]) : (0, 0);
            (double Nx, double Ny) after = i < centerline.Count - 1 ? SegmentNormal(centerline[i], centerline[i + 1]) : (0, 0);
            double nx = before.Nx + after.Nx;
            double ny = before.Ny + after.Ny;
            double len = Math.Sqrt((nx * nx) + (ny * ny));
            normals.Add(len < 1e-9 ? (0, 0) : (nx / len, ny / len));
        }

        var outerSide = new List<Pt>(centerline.Count);
        var innerSide = new List<Pt>(centerline.Count);
        for (int i = 0; i < centerline.Count; i++)
        {
            (double nx, double ny) = normals[i];
            outerSide.Add(new Pt(centerline[i].X + (nx * half), centerline[i].Y + (ny * half)));
            innerSide.Add(new Pt(centerline[i].X - (nx * half), centerline[i].Y - (ny * half)));
        }

        innerSide.Reverse();
        List<Seg> segs = [.. outerSide.Skip(1).Select(p => (Seg)new Seg.Line(p)), .. innerSide.Select(p => (Seg)new Seg.Line(p)), new Seg.Close()];
        return PathData.Single(new SubPath(outerSide[0], segs));
    }

    private static (double Nx, double Ny) SegmentNormal(Pt from, Pt to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double len = Math.Sqrt((dx * dx) + (dy * dy));
        return len < 1e-9 ? (0, 0) : (-dy / len, dx / len);
    }
}
