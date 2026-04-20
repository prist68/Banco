using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Banco.Sidebar.Converters;

public sealed class ResourceKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string resourceKey || string.IsNullOrWhiteSpace(resourceKey))
        {
            return null;
        }

        return Application.Current.TryFindResource(resourceKey) as Geometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
