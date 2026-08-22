using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SpotiTube.Kiosk.Views.Converters;

/// <summary>
/// Used to show the Play glyph while paused and the Pause glyph while playing, the inverse of
/// what <see cref="System.Windows.Controls.BooleanToVisibilityConverter"/> gives you.
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
