using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Banco.UI.Wpf.Converters;

/// <summary>Converte un percorso locale stringa in BitmapImage; null se il file manca o è illeggibile.</summary>
[ValueConversion(typeof(string), typeof(BitmapImage))]
public sealed class LocalPathToBitmapImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var decodeWidth = parameter is string s && int.TryParse(s, out var w) ? w : 130;

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var local = new BitmapImage();
            local.BeginInit();
            local.UriSource = new Uri(path, UriKind.Absolute);
            local.DecodePixelWidth = decodeWidth;
            local.CacheOption = BitmapCacheOption.OnLoad;
            local.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            local.EndInit();
            local.Freeze();
            return local;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
