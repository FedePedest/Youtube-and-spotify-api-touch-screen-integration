using System.Drawing;
using System.Drawing.Imaging;
using Xunit;
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.Tests;

public class AlbumArtColorExtractorTests
{
    private static byte[] SolidColorPng(Color color)
    {
        using var bitmap = new Bitmap(8, 8);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(color);
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    [Fact]
    public void NullBytes_ReturnsDefaultAccent()
    {
        Assert.Equal(
            AlbumArtColorExtractor.DefaultAccentColorHex,
            AlbumArtColorExtractor.ExtractAccentColorHex(null));
    }

    [Fact]
    public void EmptyBytes_ReturnsDefaultAccent()
    {
        Assert.Equal(
            AlbumArtColorExtractor.DefaultAccentColorHex,
            AlbumArtColorExtractor.ExtractAccentColorHex(Array.Empty<byte>()));
    }

    [Fact]
    public void UndecodableBytes_ReturnsDefaultAccent()
    {
        Assert.Equal(
            AlbumArtColorExtractor.DefaultAccentColorHex,
            AlbumArtColorExtractor.ExtractAccentColorHex(new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void GrayscaleImage_ReturnsDefaultAccent()
    {
        // No saturated pixels anywhere - nothing for the extractor to build a hue from.
        var art = SolidColorPng(Color.FromArgb(128, 128, 128));
        Assert.Equal(AlbumArtColorExtractor.DefaultAccentColorHex, AlbumArtColorExtractor.ExtractAccentColorHex(art));
    }

    [Fact]
    public void SolidRedImage_ExtractsReddishAccent()
    {
        var art = SolidColorPng(Color.FromArgb(220, 30, 30));
        var (r, g, b) = ParseRgb(AlbumArtColorExtractor.ExtractAccentColorHex(art));
        Assert.True(r > g && r > b, $"expected a reddish accent, got #{r:X2}{g:X2}{b:X2}");
    }

    [Fact]
    public void SolidBlueImage_ExtractsBluishAccent()
    {
        var art = SolidColorPng(Color.FromArgb(30, 30, 220));
        var (r, g, b) = ParseRgb(AlbumArtColorExtractor.ExtractAccentColorHex(art));
        Assert.True(b > r && b > g, $"expected a bluish accent, got #{r:X2}{g:X2}{b:X2}");
    }

    // Parses the trailing "RRGGBB" of an "#AARRGGBB" hex string without pulling in a WPF color
    // parser just for this test.
    private static (byte R, byte G, byte B) ParseRgb(string argbHex)
    {
        var hex = argbHex.TrimStart('#');
        var r = System.Convert.ToByte(hex.Substring(2, 2), 16);
        var g = System.Convert.ToByte(hex.Substring(4, 2), 16);
        var b = System.Convert.ToByte(hex.Substring(6, 2), 16);
        return (r, g, b);
    }
}
