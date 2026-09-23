using System.Windows;
using System.Windows.Controls;
using GT7LivMan.App.ViewModels;

namespace GT7LivMan.App;

/// <summary>Picks between a plain text box, an editable dropdown (a picker alongside free text), and a closed dropdown (California's month — <see cref="FieldInputViewModel.RequireDropdownSelection"/>) per field — most fields have no <see cref="FieldInputViewModel.DropdownOptions"/> at all, so this keeps the dropdown arrow off ones that don't need it.</summary>
public sealed class FieldInputTemplateSelector : DataTemplateSelector
{
    public DataTemplate? PlainTemplate { get; set; }

    public DataTemplate? DropdownTemplate { get; set; }

    public DataTemplate? RestrictedDropdownTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) => item switch
    {
        FieldInputViewModel { HasDropdownOptions: true, RequireDropdownSelection: true } => RestrictedDropdownTemplate,
        FieldInputViewModel { HasDropdownOptions: true } => DropdownTemplate,
        _ => PlainTemplate,
    };
}
