using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sietch_Console.Converters;

// Collapsed when value is null or empty string; Visible otherwise
[ValueConversion(typeof(object), typeof(Visibility))]
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
