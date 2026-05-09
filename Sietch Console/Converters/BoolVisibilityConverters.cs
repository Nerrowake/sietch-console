using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sietch_Console.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(bool))]
public class BoolInverseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
