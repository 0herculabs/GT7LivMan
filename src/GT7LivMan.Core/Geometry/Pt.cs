namespace GT7LivMan.Core.Geometry;

public readonly record struct Pt(double X, double Y)
{
    public Pt Translate(double dx, double dy) => new(X + dx, Y + dy);
}
