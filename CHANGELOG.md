# Changelog

## v0.6

### New license plates

- **Argentina**: Mercosur plate, `AB 603 SJ` format.
- **Brazil**: Mercosur plate, `BCA9G35` format.
- **France**: SIV format (`AV-456-CE`, no I, O or U). The department number (01–95, 2A/2B) is shown in the right band.
- **Japan**: private car, following the real rules:
  - **Registration office**: 87 two-kanji offices, picked from a list.
  - **Classification number**: 3 characters.
  - **Hiragana**: only those used on private cars, picked from a list.
  - **Serial number**: right-aligned with `・` padding (`・・12`, `12-34`).
  - **Font**: Yu Gothic, included with Windows, so nothing to download.
- **Mexico**: Estado de México, `LNX-224-B` format. The dashes are inserted automatically.
- **Netherlands - Front / Back**: choose the `XX-XX-XX` or `XX-XXX-X` format from a dropdown and the dashes are placed for it.
- **USA - North Carolina**: up to 8 free characters, spaces and symbols included (`GO HEELS`, `NC-2026!`).

### Improvements

- **Portugal**: supports the *Alte DIN 1451 Mittelschrift* font (link in the README). Before, it always used the fallback font.
- **Country list**: sorted alphabetically in the interface language, and re-sorted when you switch language.
- **Taller window**: no template needs vertical scrolling anymore.
- **Better exports for detailed plates**: when a plate is too detailed to fit GT7's 15 KB, the export now simplifies only the character outlines, and starts more gently. Rounded corners, frames and thin bars are no longer distorted. Text stays sharper.
- **Missing characters**: a character the plate font lacks is now drawn with a fallback font instead of disappearing.
