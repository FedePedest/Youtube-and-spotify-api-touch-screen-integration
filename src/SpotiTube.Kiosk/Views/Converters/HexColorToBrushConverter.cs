using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.Views.Converters;

/// <summary>
/// Converts an "#AARRGGBB" hex string (as produced by <see cref="AlbumArtColorExtractor"/>) into a
/// frozen <see cref="SolidColorBrush"/> for binding to a control's Foreground/Background.
/// </summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string ?? AlbumArtColorExtractor.DefaultAccentColorHex;

        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch (Exception ex) when (ex is FormatException or NullReferenceException)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(AlbumArtColorExtractor.DefaultAccentColorHex)!;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
