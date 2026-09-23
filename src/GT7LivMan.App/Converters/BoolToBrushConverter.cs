using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GT7LivMan.App.Converters;

/// <summary>Picks <see cref="TrueBrush"/>/<see cref="FalseBrush"/> based on a bound bool — used for the size-budget label's red/green state.</summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public Brush TrueBrush { get; set; } = Brushes.Red;

    public Brush FalseBrush { get; set; } = Brushes.Green;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
