using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SpotiTube.Kiosk.Views.Converters;

/// <summary>
/// Converts the raw album-art bytes carried by <c>MediaSessionState.AlbumArt</c> into an
/// <see cref="System.Windows.Media.ImageSource"/> for the Now Playing view's blurred background.
/// </summary>
public sealed class ByteArrayToImageSourceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
        {
            return DependencyProperty.UnsetValue;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            // Decode eagerly so the bitmap no longer depends on the stream once EndInit returns.
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            // Freezing makes the bitmap safe to hand to any thread and cheaper for WPF to render.
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            // Whatever SMTC handed us wasn't a decodable image; show no background rather than
            // taking the app down over artwork.
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
