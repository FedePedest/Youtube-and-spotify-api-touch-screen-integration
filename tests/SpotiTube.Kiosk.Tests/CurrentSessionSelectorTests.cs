using Xunit;
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.Tests;

public class CurrentSessionSelectorTests
{
    private static MediaSessionState Session(string id, PlaybackStatus status, DateTimeOffset lastUpdated) =>
        new(
            SourceAppId: id,
            Title: "Title-" + id,
            Artist: "Artist-" + id,
            AlbumArt: null,
            Status: status,
            CanPlay: true,
            CanPause: true,
            CanSkipNext: true,
            CanSkipPrevious: true,
            CanSeek: true,
            Position: TimeSpan.Zero,
            Duration: TimeSpan.FromMinutes(3),
            LastUpdated: lastUpdated);

    [Fact]
    public void NoSessions_ReturnsNull()
    {
        var result = CurrentSessionSelector.SelectCurrent(Array.Empty<MediaSessionState>());
        Assert.Null(result);
    }

    [Fact]
    public void NoPlayingSessions_ReturnsNull_EvenIfPaused()
    {
        var sessions = new[] { Session("Spotify.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow) };
        var result = CurrentSessionSelector.SelectCurrent(sessions);
        Assert.Null(result);
    }

    [Fact]
    public void OnePlayingSession_ReturnsIt()
    {
        var sessions = new[]
        {
            Session("Spotify.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow),
            Session("msedge.exe", PlaybackStatus.Playing, DateTimeOffset.UtcNow),
        };
        var result = CurrentSessionSelector.SelectCurrent(sessions);
        Assert.Equal("msedge.exe", result!.SourceAppId);
    }

    [Fact]
    public void MultiplePlayingSessions_ReturnsMostRecentlyUpdated()
    {
        var older = Session("Spotify.exe", PlaybackStatus.Playing, DateTimeOffset.UtcNow.AddSeconds(-10));
        var newer = Session("msedge.exe", PlaybackStatus.Playing, DateTimeOffset.UtcNow);
        var result = CurrentSessionSelector.SelectCurrent(new[] { older, newer });
        Assert.Equal("msedge.exe", result!.SourceAppId);
    }
}
