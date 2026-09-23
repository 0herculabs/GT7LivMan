using System.Globalization;

namespace GT7LivMan.Core.Geometry;

/// <summary>
/// Opaque flat fill color. GT7's uploader doesn't reliably honor alpha/blend on fills, so we don't
/// model one — every shape is a solid color, and transparency comes only from areas we don't draw.
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public string ToHex()
    {
        Span<char> chars = stackalloc char[7];
        chars[0] = '#';
        R.TryFormat(chars[1..3], out _, "x2", CultureInfo.InvariantCulture);
        G.TryFormat(chars[3..5], out _, "x2", CultureInfo.InvariantCulture);
        B.TryFormat(chars[5..7], out _, "x2", CultureInfo.InvariantCulture);
        return new string(chars);
    }

    /// <summary>Accepts both "#rrggbb" and the CSS/SVG "#rgb" shorthand (each digit doubled — "#111" means "#111111").</summary>
    public static RgbColor Parse(string hex)
    {
        ReadOnlySpan<char> s = hex.AsSpan().TrimStart('#');

        if (s.Length == 3)
        {
            byte r3 = byte.Parse(s[..1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g3 = byte.Parse(s[1..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b3 = byte.Parse(s[2..3], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new RgbColor((byte)(r3 * 17), (byte)(g3 * 17), (byte)(b3 * 17));
        }

        if (s.Length != 6)
        {
            throw new FormatException($"Expected a 3- or 6-digit hex color, got '{hex}'.");
        }

        byte r = byte.Parse(s[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte g = byte.Parse(s[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte b = byte.Parse(s[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new RgbColor(r, g, b);
    }
}
