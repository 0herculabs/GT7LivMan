using System.Globalization;
using System.Text;

namespace GT7LivMan.Core.Model;

/// <summary>
/// Auto-inserts a <see cref="FieldDef.Mask"/>'s literal characters (e.g. the space in Spain's
/// "9999 AAA") as the user types, so they never have to type the literal themselves — typing
/// "1234bbc" and "1234 bbc" both normalize to "1234 BBC". Typing the literal manually still works:
/// it's stripped out along with any other non-alphanumeric input before mask characters are
/// re-inserted at their correct positions.
/// </summary>
public static class MaskFormatter
{
    public static string AutoFormat(FieldDef field, string rawInput)
    {
        if (field.DigitPadChar is char pad)
        {
            // Leading zeros are padding on such a plate, never digits: "0012" is just 12.
            string digits = new string([.. rawInput.Where(char.IsAsciiDigit)]).TrimStart('0');
            return digits.Length == 0 ? string.Empty : FormatPaddedNumber(field.Mask, pad, digits);
        }

        // A free '*' slot takes symbols and spaces too, so those can't be stripped out as stray
        // separators the way they are for a letters-and-digits mask.
        bool freeText = field.Mask.Contains('*');
        string content = new(rawInput.Where(c => freeText ? !char.IsControl(c) : char.IsLetterOrDigit(c)).ToArray());
        content = content.ToUpperInvariant();

        var sb = new StringBuilder(field.Mask.Length);
        int contentIndex = 0;

        foreach (char maskChar in field.Mask)
        {
            if (contentIndex >= content.Length)
            {
                break;
            }

            if (IsSlot(maskChar))
            {
                sb.Append(content[contentIndex]);
                contentIndex++;
            }
            else
            {
                // A literal mask position (e.g. the space) is inserted automatically, never
                // consumed from the user's own input — that's what lets them skip typing it.
                sb.Append(maskChar);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Right-aligns <paramref name="digits"/> (no leading zero; extra digits beyond the mask's
    /// '9' slots are dropped) into <paramref name="mask"/>'s digit slots, filling empty leading
    /// slots with <paramref name="pad"/>. The mask's literals show only when every slot is a digit,
    /// otherwise they're a blank: "・・ 12", "・1 23", "12-34" for "99-99".
    /// </summary>
    internal static string FormatPaddedNumber(string mask, char pad, string digits)
    {
        int slots = mask.Count(c => c == '9');
        if (digits.Length > slots)
        {
            digits = digits[..slots];
        }

        string filled = new string(pad, slots - digits.Length) + digits;
        bool full = digits.Length == slots;
        var sb = new StringBuilder(mask.Length);
        int next = 0;
        foreach (char maskChar in mask)
        {
            sb.Append(maskChar == '9' ? filled[next++] : full ? maskChar : ' ');
        }

        return sb.ToString();
    }

    private static bool IsSlot(char maskChar) => maskChar is '9' or 'A' or 'X' or '*';

    /// <summary>Each run of non-literal characters in <paramref name="mask"/> (e.g. each "XX" block).</summary>
    private static List<(int Start, int Length)> Blocks(string mask)
    {
        var blocks = new List<(int Start, int Length)>();
        int i = 0;
        while (i < mask.Length)
        {
            if (!IsSlot(mask[i]))
            {
                i++;
                continue;
            }

            int start = i;
            while (i < mask.Length && IsSlot(mask[i]))
            {
                i++;
            }

            blocks.Add((start, i - start));
        }

        return blocks;
    }

    /// <summary>
    /// Generates a random value for <paramref name="field"/>. A field with <see cref="FieldDef.DropdownOptions"/>
    /// picks one of them (e.g. a real month abbreviation, not three random letters). A field with
    /// <see cref="FieldDef.RandomYearMin"/> picks a whole year between that and the current year and
    /// spreads its digits across the mask's digit slots. A field with
    /// <see cref="FieldDef.RandomizeAsLetterAndDigitBlocks"/> fills whole runs of the mask ("XX XX XX"
    /// blocks) with either all letters or all digits, never a mix, with exactly one block at random
    /// getting letters. Otherwise: '9' a random digit, 'A' a random letter from
    /// <see cref="FieldDef.AllowedLetters"/>, 'X' a coin-flip between the two, and any other mask
    /// character copied through as-is. Whatever the base value, <see cref="FieldDef.RandomMonthDigitsStart"/>
    /// and <see cref="FieldDef.RandomTwoDigitYearDigits"/> then overwrite their own digit runs with a
    /// plausible month/year (Portugal's combined "9999" inspection-date field uses both at once).
    /// </summary>
    public static string GenerateRandom(FieldDef field, Random random)
    {
        if (field.DropdownOptions is { Count: > 0 } options)
        {
            return options[random.Next(options.Count)];
        }

        if (field.DigitPadChar is char pad)
        {
            int slots = field.Mask.Count(c => c == '9');
            int number = random.Next(1, (int)Math.Pow(10, slots));
            return FormatPaddedNumber(field.Mask, pad, number.ToString(CultureInfo.InvariantCulture));
        }

        if (field.RandomYearMin is int minYear)
        {
            int year = random.Next(minYear, DateTime.Now.Year + 1);
            int digitSlots = field.Mask.Count(c => c is '9' or 'X');
            string digits = year.ToString(CultureInfo.InvariantCulture).PadLeft(digitSlots, '0');
            return FillDigitSlots(field.Mask, digits);
        }

        string value = field.RandomizeAsAlternatingBlocks ? GenerateAlternatingBlocks(field, random)
            : field.RandomizeAsLetterAndDigitBlocks ? GenerateLetterAndDigitBlocks(field, random)
            : GenerateCharByChar(field, random);

        if (field.RandomMonthDigitsStart is int monthStart)
        {
            int month = random.Next(1, 13);
            value = Overwrite(value, monthStart, month.ToString("D2", CultureInfo.InvariantCulture));
        }

        if (field.RandomTwoDigitYearDigits is { } yearSpec)
        {
            int currentTwoDigitYear = DateTime.Now.Year % 100;
            // Inclusive count of years from MinTwoDigitYear up to the current one, wrapping through
            // "99" -> "00" (e.g. Min 90, current 26 covers 90-99 then 00-26: 37 years, not -64).
            int span = (((currentTwoDigitYear - yearSpec.MinTwoDigitYear) % 100) + 100) % 100 + 1;
            int year = (yearSpec.MinTwoDigitYear + random.Next(span)) % 100;
            value = Overwrite(value, yearSpec.Start, year.ToString("D2", CultureInfo.InvariantCulture));
        }

        return value;
    }

    private static string GenerateCharByChar(FieldDef field, Random random)
    {
        var sb = new StringBuilder(field.Mask.Length);

        foreach (char maskChar in field.Mask)
        {
            sb.Append(maskChar switch
            {
                '9' => (char)('0' + random.Next(10)),
                'A' => field.AllowedLetters[random.Next(field.AllowedLetters.Length)],
                // '*' could be anything, but a random plate reads as a plate with letters and digits.
                'X' or '*' => random.Next(2) == 0
                    ? (char)('0' + random.Next(10))
                    : field.AllowedLetters[random.Next(field.AllowedLetters.Length)],
                _ => maskChar,
            });
        }

        return sb.ToString();
    }

    /// <summary>Fills each run of non-literal mask characters (e.g. each "XX" block) entirely with letters or entirely with digits — never both — with exactly one run, chosen at random, getting letters.</summary>
    private static string GenerateLetterAndDigitBlocks(FieldDef field, Random random)
    {
        List<(int Start, int Length)> blocks = Blocks(field.Mask);
        int letterBlockIndex = blocks.Count > 0 ? random.Next(blocks.Count) : -1;
        return FillBlocks(field, random, blocks, b => b == letterBlockIndex);
    }

    /// <summary>Fills each block entirely with letters or entirely with digits, neighbouring blocks always alternating, the first block's type chosen at random.</summary>
    private static string GenerateAlternatingBlocks(FieldDef field, Random random)
    {
        int firstIsLetters = random.Next(2);
        return FillBlocks(field, random, Blocks(field.Mask), b => b % 2 == firstIsLetters);
    }

    private static string FillBlocks(FieldDef field, Random random, List<(int Start, int Length)> blocks, Func<int, bool> isLetterBlock)
    {
        char[] output = field.Mask.ToCharArray();

        for (int b = 0; b < blocks.Count; b++)
        {
            (int start, int length) = blocks[b];
            bool letters = isLetterBlock(b);
            for (int c = 0; c < length; c++)
            {
                output[start + c] = letters
                    ? field.AllowedLetters[random.Next(field.AllowedLetters.Length)]
                    : (char)('0' + random.Next(10));
            }
        }

        return new string(output);
    }

    private static string Overwrite(string value, int start, string replacement)
    {
        char[] chars = value.ToCharArray();
        replacement.CopyTo(0, chars, start, replacement.Length);
        return new string(chars);
    }

    private static string FillDigitSlots(string mask, string digits)
    {
        var sb = new StringBuilder(mask.Length);
        int digitIndex = 0;

        foreach (char maskChar in mask)
        {
            sb.Append(maskChar is '9' or 'X' ? digits[digitIndex++] : maskChar);
        }

        return sb.ToString();
    }
}
