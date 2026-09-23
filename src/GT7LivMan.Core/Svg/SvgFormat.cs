using System.Globalization;
using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// The single point through which every number reaches an emitted SVG attribute. On an es-ES
/// machine, "12.5".ToString() with the current culture silently becomes "12,5" and corrupts the
/// path grammar — every numeric formatter in this codebase must go through here instead of a bare
/// ToString(). Also shrinks output: leading/trailing zeros are the cheapest byte-size lever we have.
/// </summary>
public static class SvgFormat
{
    /// <summary>
    /// Rounds to <paramref name="decimals"/> places and formats invariant-culture, stripping a
    /// leading "0" ("0.5" -> ".5") and trailing zeros ("1.50" -> "1.5", "2.00" -> "2").
    /// </summary>
    public static string Number(double value, int decimals)
    {
        if (decimals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Decimals must be >= 0.");
        }

        double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        if (rounded == 0)
        {
            // Normalize -0 to 0.
            rounded = 0;
        }

        string s = rounded.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        if (s.Contains('.', StringComparison.Ordinal))
        {
            s = s.TrimEnd('0').TrimEnd('.');
        }

        if (s.StartsWith("0.", StringComparison.Ordinal))
        {
            s = s[1..];
        }
        else if (s.StartsWith("-0.", StringComparison.Ordinal))
        {
            s = "-" + s[2..];
        }

        return s.Length == 0 ? "0" : s;
    }

    public static string Point(Pt p, int decimals) => Number(p.X, decimals) + "," + Number(p.Y, decimals);
}
