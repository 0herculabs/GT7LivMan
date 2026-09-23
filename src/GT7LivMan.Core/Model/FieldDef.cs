namespace GT7LivMan.Core.Model;

/// <summary>
/// Describes one user-editable text field: its character mask/validation and the typography to
/// render it with. <see cref="Mask"/> uses '9' for a digit and 'A' for a letter from
/// <see cref="AllowedLetters"/>; any other character in the mask is a literal (e.g. the space in
/// Spain's "9999 AAA"). <see cref="CharHeight"/>/<see cref="Tracking"/>/<see cref="SpaceTracking"/> are in the document's own units.
/// </summary>
/// <param name="Label">Human-readable label for the input UI — e.g. "Número de matrícula", "Fecha ITV". A document can have more than one field (Portugal's plate number and its inspection-date sticker are independent fields), so each needs its own label.</param>
/// <param name="Tracking">Fixed pen advance after each non-whitespace character.</param>
/// <param name="SpaceTracking">
/// Fixed pen advance after a literal whitespace character in the mask. Measured against a real
/// plate photo, this is smaller than <see cref="Tracking"/>, not larger — Tracking already carries
/// a full character's width plus its own trailing gap, so the invisible space only needs to add
/// the extra gap on top of that to reach the real plate's wider digit/letter group separation.
/// </param>
/// <param name="DropdownOptions">
/// A closed set of suggested values (e.g. California's 3-letter month codes) the UI offers as a
/// picker alongside the free-text box. By default picking one is a shortcut, not a restriction:
/// typing something else is still accepted, same permissive philosophy as everywhere else — unless
/// <see cref="RequireDropdownSelection"/> says otherwise for this field. Null/empty means a plain
/// text field with no picker. Also steers <see cref="MaskFormatter.GenerateRandom"/>: a field with
/// options picks one of them at random instead of random mask characters, since a mask like "AAA"
/// can't tell "a valid month abbreviation" from "three random letters" on its own.
/// </param>
/// <param name="RequireDropdownSelection">
/// When true (only meaningful alongside <see cref="DropdownOptions"/>), the field is a closed
/// picker, not a suggestion: the UI shows a non-editable combo box instead of an editable one, and
/// <see cref="MaskValidator"/> rejects any value that isn't exactly one of the listed options. Used
/// for California's month sticker — unlike a free-text mask, "a real 3-letter month abbreviation"
/// isn't a shape a mask can express, so the mask alone can't stop e.g. "ZZZ" from validating.
/// </param>
/// <param name="RandomYearMin">
/// For a plain numeric year field (no <see cref="DropdownOptions"/>), the earliest year
/// <see cref="MaskFormatter.GenerateRandom"/> may pick; the latest is always the current year, so
/// this stays correct without needing to be refreshed as time passes. Null means the field's digits
/// are randomized independently instead (the general '9' behavior).
/// </param>
/// <param name="RandomizeAsLetterAndDigitBlocks">
/// When true, <see cref="MaskFormatter.GenerateRandom"/> treats the mask's runs of non-literal
/// characters (Portugal's three "XX" blocks) as whole blocks rather than independent characters:
/// exactly one block at random is filled with letters, the rest with digits, and a block is never a
/// mix of both. Plain per-'X' randomization could otherwise land a block like "A5" — a shape no real
/// Portuguese plate series uses.
/// </param>
/// <param name="RandomMonthDigitsStart">
/// Mask index where a 2-digit month ("01"–"12") should be written by
/// <see cref="MaskFormatter.GenerateRandom"/> — e.g. 0 for Portugal's combined "9999" month+year
/// inspection-date field. Null means no digit run is treated as a month.
/// </param>
/// <param name="RandomTwoDigitYearDigits">
/// Mask index plus the earliest two-digit year <see cref="MaskFormatter.GenerateRandom"/> should
/// pick from (e.g. Start 2, MinTwoDigitYear 90 for Portugal's inspection-date field: year digits
/// start at index 2, and the earliest plausible sticker is "90" for 1990). The range wraps at
/// "99"→"00" and always stops at the current two-digit year — manually typing a later one (e.g.
/// "28") still works, this only caps what Random License Plate picks. Null means no digit run is
/// treated as a two-digit year.
/// </param>
public sealed record FieldDef(
    string Id,
    string Label,
    string Mask,
    string AllowedLetters,
    double CharHeight,
    double Tracking,
    double SpaceTracking,
    string PreferredFontFamily,
    IReadOnlyList<string> FallbackFontFamilies,
    IReadOnlyList<string>? DropdownOptions = null,
    bool RequireDropdownSelection = false,
    int? RandomYearMin = null,
    bool RandomizeAsLetterAndDigitBlocks = false,
    int? RandomMonthDigitsStart = null,
    RandomTwoDigitYearSpec? RandomTwoDigitYearDigits = null);

/// <summary>Where a two-digit year run sits in a <see cref="FieldDef.Mask"/> and the earliest year <see cref="MaskFormatter.GenerateRandom"/> may pick for it (a plain record, not a value tuple, so it round-trips through <see cref="PlateDocumentSerializer"/>).</summary>
public sealed record RandomTwoDigitYearSpec(int Start, int MinTwoDigitYear);
