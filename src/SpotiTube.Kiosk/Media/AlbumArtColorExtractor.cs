using System.Drawing;
using System.IO;

namespace SpotiTube.Kiosk.Media;

/// <summary>
/// Picks a single accent color out of album art, for theming the seek/volume bar fill to match
/// whatever's currently playing instead of a fixed color.
/// </summary>
public static class AlbumArtColorExtractor
{
    /// <summary>Spotify green - used whenever there's no art to extract a color from.</summary>
    public const string DefaultAccentColorHex = "#FF1DB954";

    private const int SampleSize = 16;

    /// <summary>
    /// Returns an "#AARRGGBB" hex color built from the dominant hue in <paramref name="albumArt"/>,
    /// rendered at a fixed, deliberately vivid saturation/lightness so it always reads clearly as a
    /// progress-bar fill regardless of how dark, bright, or washed-out the source art is. Falls back
    /// to <see cref="DefaultAccentColorHex"/> when there's no art, it can't be decoded, or it's
    /// essentially grayscale (no meaningful hue to extract).
    /// </summary>
    public static string ExtractAccentColorHex(byte[]? albumArt)
    {
        if (albumArt is null || albumArt.Length == 0) return DefaultAccentColorHex;

        try
        {
            using var ms = new MemoryStream(albumArt);
            using var source = new Bitmap(ms);
            using var thumb = new Bitmap(source, new Size(SampleSize, SampleSize));

            double sinSum = 0, cosSum = 0, weightSum = 0;
            for (var y = 0; y < thumb.Height; y++)
            {
                for (var x = 0; x < thumb.Width; x++)
                {
                    var pixel = thumb.GetPixel(x, y);
                    var saturation = pixel.GetSaturation();
                    var lightness = pixel.GetBrightness();

                    // Skip grayscale, near-black, and near-white pixels - borders, shadows, and
                    // white backgrounds are common in album art and make poor accent colors, so
                    // they shouldn't get to vote on the result.
                    if (saturation < 0.25 || lightness < 0.15 || lightness > 0.9) continue;

                    var hueRadians = pixel.GetHue() * Math.PI / 180.0;
                    sinSum += Math.Sin(hueRadians) * saturation;
                    cosSum += Math.Cos(hueRadians) * saturation;
                    weightSum += saturation;
                }
            }

            if (weightSum <= 0) return DefaultAccentColorHex;

            var averageHueDegrees = Math.Atan2(sinSum, cosSum) * 180.0 / Math.PI;
            if (averageHueDegrees < 0) averageHueDegrees += 360;

            var accent = FromHsl(averageHueDegrees, saturation: 0.62, lightness: 0.52);
            return $"#FF{accent.R:X2}{accent.G:X2}{accent.B:X2}";
        }
        catch
        {
            // Whatever SMTC handed us wasn't a decodable image; fall back rather than crash.
            return DefaultAccentColorHex;
        }
    }

    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var huePrime = hue / 60.0;
        var x = chroma * (1 - Math.Abs(huePrime % 2 - 1));

        var (r1, g1, b1) = huePrime switch
        {
            < 1 => (chroma, x, 0.0),
            < 2 => (x, chroma, 0.0),
            < 3 => (0.0, chroma, x),
            < 4 => (0.0, x, chroma),
            < 5 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };

        var m = lightness - chroma / 2;
        var r = (byte)Math.Round((r1 + m) * 255);
        var g = (byte)Math.Round((g1 + m) * 255);
        var b = (byte)Math.Round((b1 + m) * 255);
        return Color.FromArgb(r, g, b);
    }
}
