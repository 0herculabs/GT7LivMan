using System.Text.Json;
using System.Text.Json.Serialization;

namespace GT7LivMan.Core.Model;

/// <summary>JSON (de)serialization for template files on disk (templates/*.json) — a design-time artifact, not GT7 export output, so verbosity doesn't matter here.</summary>
public static class PlateDocumentSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Enums as names, not numbers: these files are meant to be readable and hand-tweakable.
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(PlateDocument document) => JsonSerializer.Serialize(document, Options);

    public static PlateDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<PlateDocument>(json, Options)
        ?? throw new FormatException("Deserialized PlateDocument was null.");
}
