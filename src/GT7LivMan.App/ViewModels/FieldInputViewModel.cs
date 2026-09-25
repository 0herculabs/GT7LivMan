using GT7LivMan.Core.Model;

namespace GT7LivMan.App.ViewModels;

/// <summary>One editable field's live text-box state — a document can carry more than one independent field (Portugal's plate number and its inspection-date sticker), so each gets its own of these rather than the editor hardcoding a single input.</summary>
public sealed class FieldInputViewModel : ObservableObject
{
    private readonly FieldDef _field;
    private readonly Action _onChanged;
    private string _value = string.Empty;
    private string _selectedMask;

    /// <summary>The field as currently configured: its only mask is <see cref="SelectedMask"/>.</summary>
    private FieldDef _active;

    public FieldInputViewModel(FieldDef field, Action onChanged)
    {
        _field = field;
        _onChanged = onChanged;
        _selectedMask = field.Mask;
        _active = field.WithMask(field.Mask);
    }

    public string FieldId => _field.Id;

    public string Label => LocalizationService.FieldLabel(_field.Label);

    public string FormatLabel => LocalizationService.Text("PlateFormat");

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(FormatLabel));
    }

    /// <summary>The formats the user can pick between (the Netherlands' "XX-XX-XX" / "XX-XXX-X") — a single entry for most fields, which then show no picker.</summary>
    public IReadOnlyList<string> MaskOptions => _field.AllMasks();

    public bool HasMaskOptions => MaskOptions.Count > 1;

    /// <summary>The chosen format. Changing it re-lays out what's already been typed with the new format's separators.</summary>
    public string SelectedMask
    {
        get => _selectedMask;
        set
        {
            if (!SetField(ref _selectedMask, value))
            {
                return;
            }

            _active = _field.WithMask(value);
            SetField(ref _value, MaskFormatter.AutoFormat(_active, _value), nameof(Value));
            // Recompute even if the text came out identical: its validity depends on the format.
            _onChanged();
        }
    }

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
            string formatted = MaskFormatter.AutoFormat(_active, value);
            if (SetField(ref _value, formatted))
            {
                _onChanged();
            }
        }
    }

    /// <summary>Sets <see cref="Value"/> without invoking the change callback — for initial setup (e.g. loading a template's starting example), where there's nothing to recompute yet.</summary>
    public void SetInitialValue(string value)
    {
        SetField(ref _value, MaskFormatter.AutoFormat(_active, value));
    }

    /// <summary>Random value in the currently selected format.</summary>
    public void Randomize(Random random) => Value = MaskFormatter.GenerateRandom(_active, random);

    public bool TryGetNormalizedValue(out string normalized) => MaskValidator.TryNormalize(_active, Value, out normalized);
}
