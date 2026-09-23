namespace GT7LivMan.Core.Model;

/// <summary>
/// Validates a field value against its <see cref="FieldDef.Mask"/>: '9' requires a digit, 'A'
/// requires a character from <see cref="FieldDef.AllowedLetters"/> (case-insensitive, normalized to
/// upper), 'X' requires either (a free choice between a digit and a letter — Portugal's blocks
/// aren't fixed to one or the other, unlike Spain's), and anything else in the mask must match
/// that exact literal character.
/// </summary>
public static class MaskValidator
{
    public static bool IsValid(FieldDef field, string value) => TryNormalize(field, value, out _);

    /// <summary>Uppercases the value and checks it against the mask; returns false without throwing on any mismatch (wrong length, wrong character class, wrong literal). A field with <see cref="FieldDef.RequireDropdownSelection"/> additionally rejects anything that isn't exactly one of its <see cref="FieldDef.DropdownOptions"/> — the mask shape alone can't express "a real month abbreviation".</summary>
    public static bool TryNormalize(FieldDef field, string value, out string normalized)
    {
        normalized = value.ToUpperInvariant();
        if (normalized.Length != field.Mask.Length)
        {
            return false;
        }

        if (field.RequireDropdownSelection && field.DropdownOptions?.Contains(normalized, StringComparer.Ordinal) != true)
        {
            return false;
        }

        for (int i = 0; i < field.Mask.Length; i++)
        {
            char maskChar = field.Mask[i];
            char valueChar = normalized[i];

            bool ok = maskChar switch
            {
                '9' => char.IsAsciiDigit(valueChar),
                'A' => field.AllowedLetters.Contains(valueChar, StringComparison.Ordinal),
                'X' => char.IsAsciiDigit(valueChar) || field.AllowedLetters.Contains(valueChar, StringComparison.Ordinal),
                _ => valueChar == maskChar,
            };

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
