namespace GT7LivMan.Core.Model;

/// <summary>
/// Validates a field value against its <see cref="FieldDef.Mask"/>: '9' requires a digit, 'A'
/// requires a character from <see cref="FieldDef.AllowedLetters"/> (case-insensitive, normalized to
/// upper), 'X' requires either (a free choice between a digit and a letter — Portugal's blocks
/// aren't fixed to one or the other, unlike Spain's), '*' allows any character at all — letter,
/// digit, symbol or space, North Carolina's free vanity text — and may be left empty at the end of
/// the mask, and anything else in the mask must match that exact literal character.
/// </summary>
public static class MaskValidator
{
    public static bool IsValid(FieldDef field, string value) => TryNormalize(field, value, out _);

    /// <summary>Uppercases the value and checks it against the mask; returns false without throwing on any mismatch (wrong length, wrong character class, wrong literal). A field with <see cref="FieldDef.RequireDropdownSelection"/> additionally rejects anything that isn't exactly one of its <see cref="FieldDef.DropdownOptions"/> — the mask shape alone can't express "a real month abbreviation".</summary>
    public static bool TryNormalize(FieldDef field, string value, out string normalized)
    {
        normalized = value.ToUpperInvariant();
        if (field.DigitPadChar is char pad)
        {
            // Valid only in its canonical right-aligned form, with at least one digit and no leading zero.
            string digits = new([.. normalized.Where(char.IsAsciiDigit)]);
            return digits.Length > 0 && digits[0] != '0'
                && normalized == MaskFormatter.FormatPaddedNumber(field.Mask, pad, digits);
        }

        // Trailing '*' slots are optional (a vanity plate can be shorter than its 8 cells); every
        // other mask position has to be filled.
        int requiredLength = field.Mask.TrimEnd('*').Length;
        if (normalized.Length > field.Mask.Length || normalized.Length < Math.Max(requiredLength, 1))
        {
            return false;
        }

        if (field.RequireDropdownSelection && field.DropdownOptions?.Contains(normalized, StringComparer.Ordinal) != true)
        {
            return false;
        }

        for (int i = 0; i < normalized.Length; i++)
        {
            char maskChar = field.Mask[i];
            char valueChar = normalized[i];

            bool ok = maskChar switch
            {
                '9' => char.IsAsciiDigit(valueChar),
                'A' => field.AllowedLetters.Contains(valueChar, StringComparison.Ordinal),
                'X' => char.IsAsciiDigit(valueChar) || field.AllowedLetters.Contains(valueChar, StringComparison.Ordinal),
                '*' => !char.IsControl(valueChar),
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
