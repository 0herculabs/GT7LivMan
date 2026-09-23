using System.Globalization;
using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Svg;

/// <summary>
/// Parses an SVG "transform" attribute ("translate(...) rotate(...)") into one composed
/// <see cref="Affine2"/>. Numbers within a single function's argument list are expected to be
/// separated by whitespace/commas (true of every hand- or tool-authored file seen so far) — this
/// does not attempt to handle the fully general concatenated-number grammar <see cref="SvgPathParser"/> does.
/// </summary>
public static class SvgTransformParser
{
    public static Affine2 Parse(string? transform)
    {
        if (string.IsNullOrWhiteSpace(transform))
        {
            return Affine2.Identity;
        }

        Affine2 result = Affine2.Identity;
        int i = 0;
        while (i < transform.Length)
        {
            while (i < transform.Length && (char.IsWhiteSpace(transform[i]) || transform[i] == ','))
            {
                i++;
            }

            if (i >= transform.Length)
            {
                break;
            }

            int nameStart = i;
            while (i < transform.Length && char.IsLetter(transform[i]))
            {
                i++;
            }

            string name = transform[nameStart..i];

            while (i < transform.Length && char.IsWhiteSpace(transform[i]))
            {
                i++;
            }

            if (i >= transform.Length || transform[i] != '(')
            {
                throw new FormatException($"Expected '(' after '{name}' in transform '{transform}'.");
            }

            i++;
            int argsStart = i;
            int depth = 1;
            while (i < transform.Length && depth > 0)
            {
                if (transform[i] == '(')
                {
                    depth++;
                }
                else if (transform[i] == ')')
                {
                    depth--;
                }

                if (depth > 0)
                {
                    i++;
                }
            }

            if (depth != 0)
            {
                throw new FormatException($"Unbalanced parentheses in transform '{transform}'.");
            }

            string argsText = transform[argsStart..i];
            i++;

            double[] args = argsText
                .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToArray();

            result *= BuildFunction(name, args, transform);
        }

        return result;
    }

    private static Affine2 BuildFunction(string name, double[] args, string original) => (name, args.Length) switch
    {
        ("translate", 1) => Affine2.Translate(args[0], 0),
        ("translate", 2) => Affine2.Translate(args[0], args[1]),
        ("rotate", 1) => Affine2.Rotate(args[0]),
        ("rotate", 3) => Affine2.Translate(args[1], args[2]) * Affine2.Rotate(args[0]) * Affine2.Translate(-args[1], -args[2]),
        ("scale", 1) => Affine2.Scale(args[0], args[0]),
        ("scale", 2) => Affine2.Scale(args[0], args[1]),
        ("matrix", 6) => new Affine2(args[0], args[1], args[2], args[3], args[4], args[5]),
        _ => throw new NotSupportedException($"Unsupported transform function '{name}' with {args.Length} arg(s) in '{original}'."),
    };
}
