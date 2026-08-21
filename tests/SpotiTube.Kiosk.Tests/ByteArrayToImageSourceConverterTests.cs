using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using Xunit;
using SpotiTube.Kiosk.Views.Converters;

namespace SpotiTube.Kiosk.Tests;

public class ByteArrayToImageSourceConverterTests
{
    // A 1x1 PNG - the smallest thing that proves the bytes actually get decoded.
    private static readonly byte[] OnePixelPng = System.Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static object Convert(object? value) =>
        new ByteArrayToImageSourceConverter().Convert(value, typeof(object), null, CultureInfo.InvariantCulture);

    [Fact]
    public void NullBytes_ReturnUnset()
    {
        Assert.Equal(DependencyProperty.UnsetValue, Convert(null));
    }

    [Fact]
    public void EmptyBytes_ReturnUnset()
    {
        Assert.Equal(DependencyProperty.UnsetValue, Convert(Array.Empty<byte>()));
    }

    [Fact]
    public void UndecodableBytes_ReturnUnset()
    {
        Assert.Equal(DependencyProperty.UnsetValue, Convert(new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void ImageBytes_ReturnFrozenBitmap()
    {
        var result = Convert(OnePixelPng);
        var bitmap = Assert.IsType<BitmapImage>(result);
        Assert.True(bitmap.IsFrozen);
        Assert.Equal(1, bitmap.PixelWidth);
    }
}
