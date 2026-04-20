using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Banco.UI.Wpf.Converters;

public sealed class DoubleToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double width && width > 0)
        {
            return new GridLength(width);
        }

        return new GridLength(280);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is GridLength gridLength ? gridLength.Value : 280d;
    }
}
