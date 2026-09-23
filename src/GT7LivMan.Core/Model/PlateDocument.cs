using GT7LivMan.Core.Geometry;

namespace GT7LivMan.Core.Model;

/// <summary>
/// A template: static art plus the fields a user fills in. <see cref="SchemaVersion"/> must be
/// bumped on any breaking change to this shape so a stale template on disk fails loudly instead of
/// silently mis-loading.
/// </summary>
/// <param name="DisplayName">Human-readable label for pickers (e.g. a country dropdown) — "España — Matrícula larga".</param>
public sealed record PlateDocument(
    string SchemaVersion,
    string CountryCode,
    string DisplayName,
    SizeMm Size,
    IReadOnlyList<Element> Elements,
    IReadOnlyList<FieldDef> Fields)
{
    public const string CurrentSchemaVersion = "1";
}
