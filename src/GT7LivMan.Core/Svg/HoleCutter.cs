using GT7LivMan.Core.Geometry;
using GT7LivMan.Core.Model;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// Turns a traced raster's transparent regions into real holes in the shapes they sit inside.
/// <para>
/// A region tracer has no concept of a hole: it outlines each area of flat color as its own closed
/// shape, so the counter of an "e" comes back as a separate region stacked on top of the solid blob
/// the letter was traced as, and the picture only looks right because that region gets painted over
/// the letter afterwards. That works while the region has a color to paint — but on a logo with a
/// transparent background the counter is transparent, there is nothing to paint, and the letter
/// stays filled in solid.
/// </para>
/// <para>
/// So each transparent region is instead appended to the shape beneath it as an extra subpath under
/// the EvenOdd fill rule, which cuts it out no matter which way either contour winds. That is also
/// how a hand-drawn vector logo would have described the same letter in the first place.
/// </para>
/// </summary>
public static class HoleCutter
{
    /// <param name="shapes">Visible shapes in paint order.</param>
    /// <param name="transparentRegions">Geometry of the regions that were traced as transparent — see <see cref="GenericSvgImporter.Result.TransparentRegions"/>.</param>
    public static IReadOnlyList<Element> CutHoles(IReadOnlyList<Element> shapes, IReadOnlyList<PathData> transparentRegions)
    {
        if (transparentRegions.Count == 0)
        {
            return shapes;
        }

        var result = new List<Element>(shapes);
        var flattened = new List<List<Pt>>?[result.Count];

        foreach (PathData region in transparentRegions)
        {
            if (!TryFindInteriorPoint(region, out Pt probe))
            {
                continue;
            }

            Bounds regionBounds = BoundsOf(Flatten(region));

            // Last one wins: with nested regions the innermost containing shape is the one the hole
            // actually belongs to, and it is the most recently painted of the ones containing it.
            int owner = -1;
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i] is not Element.Shape shape)
                {
                    continue;
                }

                flattened[i] ??= Flatten(shape.Path);

                // A hole has to fit inside the shape it punches. Without this, the transparent
                // background qualifies too: only its outer contour is traced, and taken on its own
                // that rectangle encloses the artwork's center — so the background would be cut
                // into the artwork and the colors would come out inverted.
                if (regionBounds.FitsStrictlyInside(BoundsOf(flattened[i]!)) && Contains(flattened[i]!, probe))
                {
                    owner = i;
                }
            }

            if (owner < 0)
            {
                // The transparent background itself: it surrounds the artwork rather than sitting
                // inside any of it, so there is nothing to cut it out of.
                continue;
            }

            var target = (Element.Shape)result[owner];
            result[owner] = target with
            {
                Path = new PathData([.. target.Path.SubPaths, .. region.SubPaths], FillRule.EvenOdd),
            };
            flattened[owner] = null;
        }

        return result;
    }

    private static List<List<Pt>> Flatten(PathData path) =>
        [.. path.SubPaths.Select(PathDataSimplifier.Flatten)];

    private readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public bool FitsStrictlyInside(Bounds outer) =>
            MinX >= outer.MinX && MaxX <= outer.MaxX
            && MinY >= outer.MinY && MaxY <= outer.MaxY
            && (MaxX - MinX) * (MaxY - MinY) < (outer.MaxX - outer.MinX) * (outer.MaxY - outer.MinY);
    }

    private static Bounds BoundsOf(List<List<Pt>> contours)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (List<Pt> contour in contours)
        {
            foreach (Pt p in contour)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
        }

        return new Bounds(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Finds a point that is genuinely inside <paramref name="region"/>. The bounding box center
    /// isn't good enough — for a crescent or a "C" shaped region it can land outside the shape — so
    /// this casts a ray across the middle of the box and takes the midpoint of the first span that
    /// is actually interior.
    /// </summary>
    private static bool TryFindInteriorPoint(PathData region, out Pt point)
    {
        point = default;
        List<List<Pt>> contours = Flatten(region);
        if (contours.Count == 0 || contours.All(c => c.Count < 3))
        {
            return false;
        }

        double minY = contours.Min(c => c.Min(p => p.Y));
        double maxY = contours.Max(c => c.Max(p => p.Y));
        double y = (minY + maxY) / 2;

        var crossings = new List<double>();
        foreach (List<Pt> contour in contours)
        {
            for (int i = 0; i < contour.Count; i++)
            {
                Pt a = contour[i];
                Pt b = contour[(i + 1) % contour.Count];
                if (a.Y == b.Y || y < Math.Min(a.Y, b.Y) || y >= Math.Max(a.Y, b.Y))
                {
                    continue;
                }

                crossings.Add(a.X + ((y - a.Y) / (b.Y - a.Y) * (b.X - a.X)));
            }
        }

        if (crossings.Count < 2)
        {
            return false;
        }

        crossings.Sort();
        // Even-odd: the span between crossing 0 and 1 is inside the region.
        point = new Pt((crossings[0] + crossings[1]) / 2, y);
        return true;
    }

    private static bool Contains(List<List<Pt>> contours, Pt point)
    {
        bool inside = false;
        foreach (List<Pt> contour in contours)
        {
            for (int i = 0; i < contour.Count; i++)
            {
                Pt a = contour[i];
                Pt b = contour[(i + 1) % contour.Count];
                if (a.Y > point.Y != b.Y > point.Y
                    && point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }
}
