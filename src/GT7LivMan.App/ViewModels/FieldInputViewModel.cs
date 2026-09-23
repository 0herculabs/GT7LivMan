using GT7LivMan.Core.Model;

namespace GT7LivMan.App.ViewModels;

/// <summary>One editable field's live text-box state — a document can carry more than one independent field (Portugal's plate number and its inspection-date sticker), so each gets its own of these rather than the editor hardcoding a single input.</summary>
public sealed class FieldInputViewModel : ObservableObject
{
    private readonly FieldDef _field;
    private readonly Action _onChanged;
    private string _value = string.Empty;

    public FieldInputViewModel(FieldDef field, Action onChanged)
    {
        _field = field;
        _onChanged = onChanged;
    }

    public string FieldId => _field.Id;

    public string Label => LocalizationService.FieldLabel(_field.Label);

    public void RefreshLanguage() => OnPropertyChanged(nameof(Label));

    /// <summary>A closed set of suggested values (e.g. California's 3-letter month codes) for a picker alongside the free-text box — null/empty means this field is a plain text box.</summary>
    public IReadOnlyList<string>? DropdownOptions => _field.DropdownOptions;

    public bool HasDropdownOptions => DropdownOptions is { Count: > 0 };

    /// <summary>When true, this field's dropdown is a closed picker (California's month) rather than a shortcut alongside free text — the UI should show a non-editable combo box.</summary>
    public bool RequireDropdownSelection => _field.RequireDropdownSelection;

    public string Value
    {
        get => _value;
        set
        {
            // Auto-inserts the mask's literal characters (e.g. Spain's group-separating space) as
            // the user types, so they never have to type it themselves.
            string formatted = MaskFormatter.AutoFormat(_field, value);
            if (SetField(ref _value, formatted))
            {
                _onChanged();
            }
        }
    }

    /// <summary>Sets <see cref="Value"/> without invoking the change callback — for initial setup (e.g. loading a template's starting example), where there's nothing to recompute yet.</summary>
    public void SetInitialValue(string value)
    {
        SetField(ref _value, MaskFormatter.AutoFormat(_field, value));
    }

    public void Randomize(Random random) => Value = MaskFormatter.GenerateRandom(_field, random);

    public bool TryGetNormalizedValue(out string normalized) => MaskValidator.TryNormalize(_field, Value, out normalized);
}
