using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Media;
using GT7LivMan.App.Rendering;
using GT7LivMan.Core.Model;
using GT7LivMan.Core.Svg;
using GT7LivMan.Typography.Wpf;

namespace GT7LivMan.App.ViewModels;

public sealed class PlateEditorViewModel : ObservableObject
{
    private readonly FontResolver _fontResolver = new(Path.Combine(AppContext.BaseDirectory, "fonts"));
    private readonly WpfGlyphOutliner _outliner;
    private readonly Random _random = new();

    private PlateDocument _document = null!;
    private TemplateCatalogEntry _selectedTemplate = null!;
    private bool _isValid;
    private ImageSource? _previewImage;
    private string _sizeLabel = string.Empty;
    private bool _isOverBudget;
    private string? _exportSvg;

    public PlateEditorViewModel()
    {
        _outliner = new WpfGlyphOutliner(_fontResolver);

        AvailableTemplates = new(TemplateCatalog.LoadAll(Path.Combine(AppContext.BaseDirectory, "templates")));
        if (AvailableTemplates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No se encontró ninguna plantilla en '{Path.Combine(AppContext.BaseDirectory, "templates")}'.");
        }

        SortTemplates();

        // Assigning through the property (not the backing field) so it runs the same
        // load/recompute logic a later user selection would.
        SelectedTemplate = AvailableTemplates[0];
    }

    /// <summary>Alphabetical by the name shown in the current language — re-sorted on a language change, since e.g. "USA" sorts last in English but "EE. UU." first in Spanish.</summary>
    public ObservableCollection<TemplateCatalogEntry> AvailableTemplates { get; }

    public TemplateCatalogEntry SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (!SetField(ref _selectedTemplate, value))
            {
                return;
            }

            LoadDocument(value.Document);
        }
    }

    /// <summary>One entry per <see cref="PlateDocument.Fields"/> — a template can have more than one independent field (Portugal's plate number and its separate inspection-date sticker).</summary>
    public ObservableCollection<FieldInputViewModel> FieldInputs { get; } = [];

    public bool IsValid
    {
        get => _isValid;
        private set => SetField(ref _isValid, value);
    }

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        private set => SetField(ref _previewImage, value);
    }

    public string SizeLabel
    {
        get => _sizeLabel;
        private set => SetField(ref _sizeLabel, value);
    }

    public bool IsOverBudget
    {
        get => _isOverBudget;
        private set => SetField(ref _isOverBudget, value);
    }

    /// <summary>The most recently exported SVG text, ready to write to disk; null while any field's current input is invalid.</summary>
    public string? ExportSvg
    {
        get => _exportSvg;
        private set => SetField(ref _exportSvg, value);
    }

    private void LoadDocument(PlateDocument document)
    {
        _document = document;

        FieldInputs.Clear();
        foreach (FieldDef field in document.Fields)
        {
            var input = new FieldInputViewModel(field, Recompute);
            input.SetInitialValue(BuildExampleValue(field));
            FieldInputs.Add(input);
        }

        Recompute();
    }

    /// <summary>Re-rolls every field on the current template at once — a document can have more than one (Portugal's plate number and its separate inspection-date sticker).</summary>
    public void RandomizeAllFields()
    {
        foreach (FieldInputViewModel input in FieldInputs)
        {
            input.Randomize(_random);
        }
    }

    public void RefreshLanguage()
    {
        foreach (TemplateCatalogEntry template in AvailableTemplates)
        {
            template.RefreshLanguage();
        }
        SortTemplates();
        foreach (FieldInputViewModel input in FieldInputs)
        {
            input.RefreshLanguage();
        }
    }

    // Moved in place rather than replaced, so the selected template stays selected.
    private void SortTemplates()
    {
        var comparer = StringComparer.Create(LocalizationService.Culture, ignoreCase: true);
        var sorted = AvailableTemplates.OrderBy(t => t.LocalizedDisplayName, comparer).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            int current = AvailableTemplates.IndexOf(sorted[i]);
            if (current != i)
            {
                AvailableTemplates.Move(current, i);
            }
        }
    }

    private static string BuildExampleValue(FieldDef field)
    {
        // A field with a closed picker (California's month) needs one of its own options as the
        // starting value — synthesizing one from the mask/AllowedLetters (e.g. "AAA") wouldn't be
        // a real month abbreviation and would start the field out invalid.
        if (field.DropdownOptions is { Count: > 0 } options)
        {
            return options[0];
        }

        var sb = new StringBuilder(field.Mask.Length);
        foreach (char maskChar in field.Mask)
        {
            sb.Append(maskChar switch
            {
                '9' => '1',
                'A' or 'X' or '*' => field.AllowedLetters.Length > 0 ? field.AllowedLetters[0] : 'A',
                _ => maskChar,
            });
        }

        return sb.ToString();
    }

    private void Recompute()
    {
        var fieldValues = new Dictionary<string, string>();
        bool allValid = true;

        foreach (FieldInputViewModel input in FieldInputs)
        {
            if (input.TryGetNormalizedValue(out string normalized))
            {
                fieldValues[input.FieldId] = normalized;
            }
            else
            {
                allValid = false;
            }
        }

        // Still typing one field (e.g. incomplete) is not an error — the preview just stays on
        // the last fully-valid render until every field is complete again, with no scolding
        // message; the Export button being disabled is signal enough.
        IsValid = allValid;
        if (!allValid)
        {
            ExportSvg = null;
            return;
        }

        Core.Rendering.Scene scene = SceneCompiler.Compile(_document, fieldValues, _outliner);

        PreviewImage = WpfSceneRenderer.Render(scene);

        string svg = ExportEscalation.Write(scene);
        ExportSvg = svg;

        int bytes = SizeBudget.MeasureUtf8Bytes(svg);
        SizeLabel = $"{bytes / 1024.0:F1} KB / {SizeBudget.MaxBytes / 1024.0:F1} KB";
        IsOverBudget = !SizeBudget.IsWithinGt7Limit(svg);
    }
}
