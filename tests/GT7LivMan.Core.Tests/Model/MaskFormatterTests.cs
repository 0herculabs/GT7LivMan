using GT7LivMan.Core.Model;

namespace GT7LivMan.Core.Tests.Model;

public class MaskFormatterTests
{
    private static readonly FieldDef SpainField = new(
        Id: "plateNumber",
        Label: "Número de matrícula",
        Mask: "9999 AAA",
        AllowedLetters: "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
        CharHeight: 237,
        Tracking: 173,
        SpaceTracking: 86,
        PreferredFontFamily: "MESPREG",
        FallbackFontFamilies: ["Overpass"]);

    [Theory]
    [InlineData("1234bbc", "1234 BBC")] // no space typed -> auto-inserted
    [InlineData("1234 bbc", "1234 BBC")] // space typed -> preserved, still uppercased
    [InlineData("1234  bbc", "1234 BBC")] // stray extra space -> stripped and reinserted correctly
    [InlineData("12", "12")] // still typing digits -> no trailing literal yet
    [InlineData("1234", "1234")] // exactly the digit group -> no trailing space until a letter is typed
    [InlineData("1234b", "1234 B")] // first letter typed -> space appears automatically
    [InlineData("", "")]
    public void AutoFormat_InsertsMaskLiterals_AsTheUserTypes(string rawInput, string expected)
    {
        Assert.Equal(expected, MaskFormatter.AutoFormat(SpainField, rawInput));
    }

    [Fact]
    public void AutoFormat_ThenMaskValidator_AcceptsTheResultOnceComplete()
    {
        string formatted = MaskFormatter.AutoFormat(SpainField, "1234bbc");
        Assert.True(MaskValidator.IsValid(SpainField, formatted));
    }

    private static readonly FieldDef FreeBlockField = new(
        Id: "plateNumber",
        Label: "Número de matrícula",
        Mask: "XX XX XX",
        AllowedLetters: "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
        CharHeight: 142,
        Tracking: 95,
        SpaceTracking: 67,
        PreferredFontFamily: "DIN1451",
        FallbackFontFamilies: ["Roboto Condensed"]);

    [Theory]
    [InlineData("12ab34", "12 AB 34")]
    [InlineData("ab1234", "AB 12 34")]
    [InlineData("aa00aa", "AA 00 AA")]
    public void AutoFormat_XMask_AcceptsAnyMixOfDigitsAndLettersPerBlock(string rawInput, string expected)
    {
        Assert.Equal(expected, MaskFormatter.AutoFormat(FreeBlockField, rawInput));
    }

    [Theory]
    [InlineData("12 AB 34", true)]
    [InlineData("AB 12 34", true)]
    [InlineData("AA 00 AA", true)]
    [InlineData("12 34 56", true)]
    [InlineData("AB CD EF", true)]
    public void MaskValidator_XMask_AcceptsAnyMixOfDigitsAndLettersPerBlock(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(FreeBlockField, value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void GenerateRandom_AlwaysProducesAMaskValidValue(int seed)
    {
        var random = new Random(seed);

        Assert.True(MaskValidator.IsValid(SpainField, MaskFormatter.GenerateRandom(SpainField, random)));
        Assert.True(MaskValidator.IsValid(FreeBlockField, MaskFormatter.GenerateRandom(FreeBlockField, random)));
    }

    [Fact]
    public void GenerateRandom_RespectsMaskShape_DigitsWhereMask9_LettersWhereMaskA()
    {
        string value = MaskFormatter.GenerateRandom(SpainField, new Random(42));

        Assert.Equal(8, value.Length);
        Assert.True(char.IsAsciiDigit(value[0]));
        Assert.True(char.IsAsciiDigit(value[1]));
        Assert.True(char.IsAsciiDigit(value[2]));
        Assert.True(char.IsAsciiDigit(value[3]));
        Assert.Equal(' ', value[4]);
        Assert.Contains(value[5], SpainField.AllowedLetters);
        Assert.Contains(value[6], SpainField.AllowedLetters);
        Assert.Contains(value[7], SpainField.AllowedLetters);
    }

    [Fact]
    public void GenerateRandom_XMask_MixesDigitsAndLettersAcrossManySamples()
    {
        // Not a single fixed seed: 'X' should land on both digits and letters over enough draws,
        // not silently collapse to always-one-or-the-other.
        var random = new Random(7);
        bool sawDigit = false;
        bool sawLetter = false;

        for (int i = 0; i < 200 && !(sawDigit && sawLetter); i++)
        {
            char c = MaskFormatter.GenerateRandom(FreeBlockField, random)[0];
            if (char.IsAsciiDigit(c))
            {
                sawDigit = true;
            }
            else
            {
                sawLetter = true;
            }
        }

        Assert.True(sawDigit, "Expected at least one digit across 200 draws.");
        Assert.True(sawLetter, "Expected at least one letter across 200 draws.");
    }

    private static readonly FieldDef MonthField = new(
        Id: "month",
        Label: "Mes",
        Mask: "AAA",
        AllowedLetters: "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
        CharHeight: 65,
        Tracking: 63,
        SpaceTracking: 63,
        PreferredFontFamily: "CharlesWright-Bold",
        FallbackFontFamilies: ["Roboto Condensed"],
        DropdownOptions: ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"]);

    private static readonly FieldDef RestrictedMonthField = MonthField with { RequireDropdownSelection = true };

    private static readonly FieldDef YearField = new(
        Id: "year",
        Label: "Año",
        Mask: "9999",
        AllowedLetters: string.Empty,
        CharHeight: 50,
        Tracking: 48,
        SpaceTracking: 48,
        PreferredFontFamily: "CharlesWright-Bold",
        FallbackFontFamilies: ["Roboto Condensed"],
        RandomYearMin: 1990);

    [Fact]
    public void GenerateRandom_DropdownField_AlwaysPicksOneOfTheListedOptions()
    {
        // A mask of "AAA" alone can't distinguish a real month abbreviation from three random
        // letters — DropdownOptions is what makes the random value actually plausible.
        var random = new Random(11);

        for (int i = 0; i < 50; i++)
        {
            string value = MaskFormatter.GenerateRandom(MonthField, random);
            Assert.Contains(value, MonthField.DropdownOptions!);
        }
    }

    [Fact]
    public void GenerateRandom_DropdownField_EventuallyCoversMoreThanOneOption()
    {
        var random = new Random(11);
        var seen = new HashSet<string>();

        for (int i = 0; i < 50; i++)
        {
            seen.Add(MaskFormatter.GenerateRandom(MonthField, random));
        }

        Assert.True(seen.Count > 1, "Expected more than one distinct month across 50 draws.");
    }

    [Fact]
    public void GenerateRandom_YearField_StaysWithinRandomYearMinAndTheCurrentYear()
    {
        var random = new Random(3);

        for (int i = 0; i < 50; i++)
        {
            string value = MaskFormatter.GenerateRandom(YearField, random);

            Assert.Equal(4, value.Length);
            Assert.True(MaskValidator.IsValid(YearField, value));

            int year = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(year, YearField.RandomYearMin!.Value, DateTime.Now.Year);
        }
    }

    [Theory]
    [InlineData("JAN", true)]
    [InlineData("dec", true)] // lower-case still normalizes and matches
    [InlineData("ZZZ", false)] // right shape (3 letters), but not a real option
    [InlineData("JA", false)] // wrong length
    public void MaskValidator_RequireDropdownSelection_OnlyAcceptsExactlyListedOptions(string value, bool expected)
    {
        Assert.Equal(expected, MaskValidator.IsValid(RestrictedMonthField, value));
    }

    [Fact]
    public void MaskValidator_WithoutRequireDropdownSelection_StillAcceptsFreeTextOfTheRightShape()
    {
        // MonthField itself (no RequireDropdownSelection) keeps the permissive behavior everywhere
        // else in the app: DropdownOptions is a picker, not a restriction, unless opted in.
        Assert.True(MaskValidator.IsValid(MonthField, "ZZZ"));
    }

    private static readonly FieldDef PortugalPlateField = FreeBlockField with { RandomizeAsLetterAndDigitBlocks = true };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void GenerateRandom_LetterAndDigitBlocks_ProducesExactlyOneAllLetterBlock_AndTwoAllDigitBlocks(int seed)
    {
        string value = MaskFormatter.GenerateRandom(PortugalPlateField, new Random(seed));
        string[] blocks = value.Split(' ');

        Assert.Equal(3, blocks.Length);
        Assert.All(blocks, b => Assert.Equal(2, b.Length));

        int letterBlocks = blocks.Count(b => b.All(char.IsAsciiLetter));
        int digitBlocks = blocks.Count(b => b.All(char.IsAsciiDigit));
        int mixedBlocks = blocks.Count(b => !b.All(char.IsAsciiLetter) && !b.All(char.IsAsciiDigit));

        Assert.Equal(1, letterBlocks);
        Assert.Equal(2, digitBlocks);
        Assert.Equal(0, mixedBlocks);
    }

    [Fact]
    public void GenerateRandom_LetterAndDigitBlocks_VariesWhichBlockGetsTheLetters()
    {
        var random = new Random(21);
        var letterBlockPositions = new HashSet<int>();

        for (int i = 0; i < 100; i++)
        {
            string[] blocks = MaskFormatter.GenerateRandom(PortugalPlateField, random).Split(' ');
            letterBlockPositions.Add(Array.FindIndex(blocks, b => b.All(char.IsAsciiLetter)));
        }

        Assert.True(letterBlockPositions.Count > 1, "Expected the letter block's position to vary across 100 draws.");
    }

    private static readonly FieldDef InspectionDateField = new(
        Id: "inspectionDate",
        Label: "Fecha ITV (mes/año)",
        Mask: "9999",
        AllowedLetters: string.Empty,
        CharHeight: 50,
        Tracking: 37,
        SpaceTracking: 37,
        PreferredFontFamily: "DIN1451",
        FallbackFontFamilies: ["Roboto Condensed"],
        RandomMonthDigitsStart: 0,
        RandomTwoDigitYearDigits: new RandomTwoDigitYearSpec(Start: 2, MinTwoDigitYear: 90));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void GenerateRandom_InspectionDate_MonthIsAlwaysZeroOneToTwelve(int seed)
    {
        var random = new Random(seed);

        for (int i = 0; i < 50; i++)
        {
            string value = MaskFormatter.GenerateRandom(InspectionDateField, random);
            int month = int.Parse(value[..2], System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(month, 1, 12);
        }
    }

    [Fact]
    public void GenerateRandom_InspectionDate_TwoDigitYearWrapsFromMinThroughTheCurrentYear()
    {
        // 90 (1990) through the current two-digit year wraps through "99" -> "00" — a plain
        // int.Next(90, currentYear) would be an invalid (usually empty or backwards) range once the
        // current two-digit year is less than 90, so this specifically exercises that wraparound.
        var random = new Random(9);
        int currentTwoDigitYear = DateTime.Now.Year % 100;

        for (int i = 0; i < 200; i++)
        {
            string value = MaskFormatter.GenerateRandom(InspectionDateField, random);
            int year = int.Parse(value[2..], System.Globalization.CultureInfo.InvariantCulture);

            bool inLateRange = year is >= 90 and <= 99;
            bool inEarlyRange = year >= 0 && year <= currentTwoDigitYear;
            Assert.True(inLateRange || inEarlyRange, $"Year {year:D2} is outside 90-99 or 00-{currentTwoDigitYear:D2}.");
        }
    }
}
