using System.Text.Json.Serialization;

namespace GT7LivMan.Core.Geometry;

/// <summary>
/// 2D affine transform: x' = A*x + C*y + E, y' = B*x + D*y + F. Mirrors SVG's own matrix
/// convention so parsing a "transform" attribute is a direct translation.
/// </summary>
public readonly record struct Affine2(double A, double B, double C, double D, double E, double F)
{
    public static readonly Affine2 Identity = new(1, 0, 0, 1, 0, 0);

    public static Affine2 Translate(double tx, double ty) => new(1, 0, 0, 1, tx, ty);

    public static Affine2 Rotate(double degrees)
    {
        double rad = degrees * Math.PI / 180.0;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);
        return new Affine2(cos, sin, -sin, cos, 0, 0);
    }

    public static Affine2 Scale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);

    /// <summary>Matrix product: Multiply(left, right).Apply(p) == left.Apply(right.Apply(p)) — matches how SVG composes a "transform" attribute's function list left to right.</summary>
    public static Affine2 Multiply(Affine2 left, Affine2 right) => new(
        (left.A * right.A) + (left.C * right.B),
        (left.B * right.A) + (left.D * right.B),
        (left.A * right.C) + (left.C * right.D),
        (left.B * right.C) + (left.D * right.D),
        (left.A * right.E) + (left.C * right.F) + left.E,
        (left.B * right.E) + (left.D * right.F) + left.F);

    public static Affine2 operator *(Affine2 left, Affine2 right) => Multiply(left, right);

    public Pt Apply(Pt p) => new((A * p.X) + (C * p.Y) + E, (B * p.X) + (D * p.Y) + F);

    [JsonIgnore]
    public bool IsIdentity => this == Identity;

    /// <summary>True for pure rotation/translation/uniform-scale — the only transforms an elliptical arc can pass through without re-fitting the ellipse.</summary>
    public bool IsSimilarity(double tolerance = 1e-6)
    {
        double lenCol1Sq = (A * A) + (B * B);
        double lenCol2Sq = (C * C) + (D * D);
        double dot = (A * C) + (B * D);
        return Math.Abs(lenCol1Sq - lenCol2Sq) < tolerance && Math.Abs(dot) < tolerance;
    }
}
