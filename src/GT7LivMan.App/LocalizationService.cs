using System.Globalization;
using System.Text.RegularExpressions;

namespace GT7LivMan.App;

public sealed record LanguageOption(string Code, string DisplayName);

/// <summary>Small runtime-localization catalog for the two languages supported by the desktop app.</summary>
public static class LocalizationService
{
    public static readonly IReadOnlyList<LanguageOption> Languages =
    [
        new("en", "English"),
        new("es", "Español"),
    ];

    public static string CurrentLanguage { get; private set; } = "en";

    public static bool IsSpanish => CurrentLanguage == "es";

    /// <summary>For language-correct alphabetical sorting.</summary>
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsSpanish ? "es-ES" : "en-US");

    public static void SetLanguage(string code) => CurrentLanguage = code == "es" ? "es" : "en";

    /// <summary>"GT7LivMan - v0.5": same in every language, version taken from the csproj's &lt;Version&gt;.</summary>
    public static string WindowTitle { get; } =
        $"GT7LivMan - v{typeof(LocalizationService).Assembly.GetName().Version!.ToString(2)}";

    public static string Text(string key) => (key, IsSpanish) switch
    {
        ("WindowTitle", _) => WindowTitle,
        ("Language", false) => "Language",
        ("Language", true) => "Idioma",
        ("Website", false) => "Website",
        ("Website", true) => "Web",
        ("SupportKofi", false) => "Support on Ko-fi",
        ("SupportKofi", true) => "Apoyar en Ko-fi",
        ("VinylTab", false) => "SVG Generator",
        ("VinylTab", true) => "Generador SVG",
        ("PlateTab", false) => "License Plate Generator",
        ("PlateTab", true) => "Generador de matrículas",
        ("LoadImage", false) => "Load image...",
        ("LoadImage", true) => "Cargar imagen...",
        ("CountryTemplate", false) => "Country / template",
        ("CountryTemplate", true) => "País / plantilla",
        ("PlateFormat", false) => "Format",
        ("PlateFormat", true) => "Formato",
        ("RandomPlate", false) => "Random license plate",
        ("RandomPlate", true) => "Matrícula aleatoria",
        ("Colors", false) => "Colors: ",
        ("Colors", true) => "Colores: ",
        ("Simplification", false) => "Simplification (further right = less detail, fewer KB)",
        ("Simplification", true) => "Simplificación (más a la derecha = menos detalle, menos KB)",
        ("SvgSize", false) => "SVG size (GT7 limit: 15 KB)",
        ("SvgSize", true) => "Tamaño del SVG (límite GT7: 15 KB)",
        ("ExportSvg", false) => "Export SVG...",
        ("ExportSvg", true) => "Exportar SVG...",
        ("Ready", false) => "Load a JPG, PNG or SVG image to begin.",
        ("Ready", true) => "Carga una imagen JPG, PNG o SVG para empezar.",
        ("NoShapes", false) => "No usable shapes could be extracted from this file — try another image or increase the color count.",
        ("NoShapes", true) => "No se pudo extraer ninguna forma aprovechable de este archivo — prueba con otra imagen o con más colores.",
        ("SvgFiles", false) => "SVG files (*.svg)|*.svg",
        ("SvgFiles", true) => "Archivos SVG (*.svg)|*.svg",
        ("ImageFiles", false) => "Images and SVG (*.jpg;*.jpeg;*.png;*.svg)|*.jpg;*.jpeg;*.png;*.svg",
        ("ImageFiles", true) => "Imágenes y SVG (*.jpg;*.jpeg;*.png;*.svg)|*.jpg;*.jpeg;*.png;*.svg",
        ("ExportComplete", false) => "Export complete",
        ("ExportComplete", true) => "Exportación completada",
        _ => key,
    };

    public static string FieldLabel(string source) => (source, IsSpanish) switch
    {
        ("Número de matrícula", false) => "License plate number",
        ("Matrícula", false) => "License plate",
        ("Mes", false) => "Month",
        ("Año", false) => "Year",
        ("Fecha ITV", false) => "Inspection date",
        ("Fecha ITV (mes/año)", false) => "Inspection date (month/year)",
        ("Oficina de registro", false) => "Registration office",
        ("Número de clasificación", false) => "Classification number",
        ("Número de serie", false) => "Serial number",
        ("Departamento", false) => "Department",
        _ => source,
    };

    public static string TemplateName(string source) => (source, IsSpanish) switch
    {
        ("Spain - License v1", true) => "España - Matrícula v1",
        ("Portugal - License v1", true) => "Portugal - Matrícula v1",
        ("Portugal - License v2", true) => "Portugal - Matrícula v2",
        ("UK - Front", true) => "Reino Unido - Delantera",
        ("UK - Back", true) => "Reino Unido - Trasera",
        ("Netherlands - Front", true) => "Países Bajos - Delantera",
        ("Netherlands - Back", true) => "Países Bajos - Trasera",
        ("Mexico", true) => "México",
        ("Japan", true) => "Japón",
        ("Brazil", true) => "Brasil",
        ("France", true) => "Francia",
        ("USA - North Carolina", true) => "EE. UU. - Carolina del Norte",
        ("USA - California", true) => "EE. UU. - California",
        ("USA - New York", true) => "EE. UU. - Nueva York",
        _ => source,
    };

    public static string UnsupportedFormat(string extension) => IsSpanish
        ? $"Formato no soportado: {extension}. Usa JPG, PNG o SVG."
        : $"Unsupported format: {extension}. Use JPG, PNG or SVG.";

    public static string ImportFailed(string detail) => IsSpanish
        ? $"No se pudo importar el archivo: {detail}"
        : $"The file could not be imported: {TranslateImportError(detail)}";

    public static string ShapesImported(int count) => IsSpanish
        ? $"{count} forma(s) importadas."
        : $"{count} shape(s) imported.";

    public static string PlateExported(string path) => IsSpanish
        ? $"Matrícula exportada a:\n{path}\n\nSúbela en gran-turismo.com → Mi página → Decal Uploader."
        : $"License plate exported to:\n{path}\n\nUpload it at gran-turismo.com → My Page → Decal Uploader.";

    public static string VinylExported(string path) => IsSpanish
        ? $"Vinilo exportado a:\n{path}\n\nSúbelo en gran-turismo.com → Mi página → Decal Uploader."
        : $"Vinyl exported to:\n{path}\n\nUpload it at gran-turismo.com → My Page → Decal Uploader.";

    public static string TranslateWarning(string message)
    {
        if (IsSpanish) return message;
        if (message == "El SVG no declara viewBox ni width/height válidos; se ha usado un lienzo de 200x200 por defecto.")
            return "The SVG does not declare a valid viewBox or width/height; a default 200×200 canvas was used.";

        (string Pattern, string Replacement)[] replacements =
        [
            (@"^Se ignoraron (\d+) elemento\(s\) <text>.*$", "$1 <text> element(s) were skipped — convert them to outlines before importing the SVG."),
            (@"^Se ignoraron (\d+) imagen\(es\) incrustada\(s\).*$", "$1 embedded image(s) were skipped — GT7 does not support embedded bitmaps."),
            (@"^Se ignoraron (\d+) elemento\(s\) <use>.*$", "$1 <use> element(s) were skipped — symbol references are not resolved."),
            (@"^(\d+) degradado\(s\).*$", "$1 gradient(s) were approximated with their average color — GT7 only supports flat fills."),
            (@"^Se ignoró la opacidad de (\d+) forma\(s\).*$", "Opacity was ignored for $1 shape(s) — GT7 does not reliably support alpha fills."),
            (@"^No se pudieron interpretar (\d+) trazado\(s\) <path>\.$", "$1 <path> path(s) could not be parsed."),
            (@"^(\d+) referencia\(s\) de color.*$", "$1 paint reference(s) could not be resolved and were replaced with gray."),
            (@"^Se ignoraron (\d+) elemento\(s\) <([^>]+)> sin relleno.*$", "$1 unfilled <$2> element(s) were skipped (unsupported decorative stroke)."),
            (@"^Se ignoraron (\d+) elemento\(s\) <([^>]+)> no soportados\.$", "$1 unsupported <$2> element(s) were skipped."),
            (@"^No se reconocieron (\d+) valor\(es\) de color: (.+)$", "$1 color value(s) were not recognized: $2"),
        ];
        foreach ((string pattern, string replacement) in replacements)
        {
            if (Regex.IsMatch(message, pattern)) return Regex.Replace(message, pattern, replacement);
        }
        return message;
    }

    private static string TranslateImportError(string detail)
    {
        if (detail.StartsWith("El archivo no es un SVG válido:", StringComparison.Ordinal))
            return "The file is not a valid SVG:" + detail["El archivo no es un SVG válido:".Length..];
        return detail == "El SVG no tiene elemento raíz." ? "The SVG has no root element." : detail;
    }
}
