# GT7LivMan

Decal creator for **Gran Turismo 7**, for Windows. It generates SVG files that follow the rules of GT7's official **Decal Uploader**, ready to upload and use in the game's livery editor.

It has two tabs:

- **License Plate Generator**: license plates from several countries.
- **SVG Generator**: turns a logo or image (JPG, PNG or SVG) into a GT7 decal.

The interface opens in English and can be switched to Spanish from the language selector at the top right.

![GT7LivMan demo](docs/media/demo01.gif)

## Installation

1. Download `GT7LivMan-v0.5.zip` from [Releases](../../releases) and unzip it into any folder.
2. Run `GT7LivMan.exe`.

Nothing else needs to be installed (64-bit Windows 10/11). The `templates`, `fonts` and `fonts-bundled` folders must stay next to the exe.

The first time, Windows may show *"Windows protected your PC"* because the executable is not signed: click **More info → Run anyway**.

## License Plate Generator

![License Plate Generator](docs/media/001.png)

1. Pick the template from the dropdown.
2. Type the plate number. There's no need to type the separators: `1234bbc` becomes `1234 BBC`.
3. **Random license plate** fills every field with a plausible random combination.
4. The preview on the right updates instantly.
5. The size indicator shows the SVG's KB against GT7's limit (15 KB).
6. **Export SVG...** saves the file.

| Template | Format | Fields |
|---|---|---|
| Spain - License v1 | `9999 AAA` | Plate number |
| Portugal - License v1 | `XX XX XX` | Plate number (no separator dots) |
| Portugal - License v2 | `XX XX XX` | Plate number (with separator dots) + inspection date (month and year) |
| UK - Front | `XXXX XXX` | Plate number (white background) |
| UK - Back | `XXXX XXX` | Plate number (yellow background) |
| USA - California | `XXXXXXX` | Plate number + inspection month (dropdown) + year |
| USA - New York | `AAA 9999` | Plate number |

`9` = digit, `A` = letter, `X` = digit or letter.

## SVG Generator

![SVG Generator](docs/media/002.png)

1. **Load image...** and pick a JPG, PNG or SVG.
2. If it's a JPG or PNG, adjust the controls:
   - **Colors** (2-16): how many flat colors are used. Meant for logos and graphics, not photos.
   - **Simplification**: more simplification means less detail and fewer KB.
3. If the warnings panel appears, check what was approximated or dropped from the original. GT7 doesn't support text, gradients, transparency or embedded images.
4. **Export SVG...** saves the file.

## Uploading to GT7

Upload the `.svg` at **gran-turismo.com → My Page → Decal Uploader**. It will show up in the game under **Showcase → My Library → My Items**.

## Fonts

The license plate fonts belong to their authors and **are not included with the app**. Without them, the app uses free fallback fonts (`fonts-bundled`): the result is valid, but less faithful to the original.

For each template to look exact, download its font, copy the `.otf` / `.ttf` into the `fonts` folder next to `GT7LivMan.exe` **without renaming it**, and restart the app.

| Template | File in `fonts` | Source |
|---|---|---|
| Spain | `MESPREG.otf` | [fonts2u.com](https://es.fonts2u.com/download/matricula-espanola.fuente) |
| UK - Front / Back | `CharlesWright-Bold.otf` | [dafont.com](https://dl.dafont.com/dl/?f=charles_wright) |
| USA - California | `LICENSE PLATE USA.ttf` + `CharlesWright-Bold.otf` | [dafont.com](https://dl.dafont.com/dl/?f=license_plate_usa) |
| USA - New York | `Zurich Extra Condensed Regular.otf` | [fontsgeek](https://fontsgeek.com/fonts/zurich-extra-condensed-regular#) |
| Portugal | `DIN1451.otf` (*Mittelschrift* variant, not included) | — |

---

To build from source, generate the zip or add countries, see [docs/DESARROLLO.md](docs/DESARROLLO.md).

## License

The code is released under the [MIT License](LICENSE). Bundled third-party components keep their own licenses:

- [ImageTracer.NET](src/GT7LivMan.Vectorization/ImageTracer/): Unlicense (public domain).
- [Overpass and Roboto Condensed](fonts-bundled/): SIL Open Font License 1.1.

## Support

GT7LivMan is free. If you find it useful and want to show your support, you can make a donation on Ko-fi:

[![Ko-fi](https://img.shields.io/badge/Ko--fi-F16061?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/herculabs)
