using System.IO;
using System.ComponentModel;
using GT7LivMan.Core.Model;

namespace GT7LivMan.App;

// A plain class, not a record: a record's synthesized structural Equals/GetHashCode/== combined
// with implementing INotifyPropertyChanged breaks WPF's ComboBox — SelectedItem stops
// propagating in either direction after exactly one change (reproduced with a ~10-line
// repro unrelated to this app's ViewModels: any record type implementing INotifyPropertyChanged
// used as a TwoWay-bound Selector.SelectedItem has this problem; neither trait alone does).
// Reference equality is what we actually want anyway — "same instance" should no-op.
public sealed class TemplateCatalogEntry : INotifyPropertyChanged
{
    public TemplateCatalogEntry(string filePath, PlateDocument document)
    {
        FilePath = filePath;
        Document = document;
    }

    public string FilePath { get; }
    public PlateDocument Document { get; }

    public string LocalizedDisplayName => LocalizationService.TemplateName(Document.DisplayName);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshLanguage() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedDisplayName)));

    public override string ToString() => Document.DisplayName;
}

/// <summary>Loads every template under templates/*.json so the country picker can be built from whatever's actually on disk instead of a hardcoded list in the App.</summary>
public static class TemplateCatalog
{
    public static IReadOnlyList<TemplateCatalogEntry> LoadAll(string templatesFolder)
    {
        if (!Directory.Exists(templatesFolder))
        {
            return [];
        }

        return Directory.EnumerateFiles(templatesFolder, "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new TemplateCatalogEntry(f, PlateDocumentSerializer.Deserialize(File.ReadAllText(f))))
            .ToList();
    }
}
