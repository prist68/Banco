using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Banco.UI.Avalonia.Lab;

public sealed class IconPathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string pathData && !string.IsNullOrWhiteSpace(pathData)
            ? StreamGeometry.Parse(pathData)
            : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
