using Xunit;
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.Tests;

public class CurrentSessionSelectorTests
{
    private static MediaSessionState Session(
        string id, PlaybackStatus status, DateTimeOffset lastUpdated, bool isVideo = false) =>
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
            LastUpdated: lastUpdated,
            IsVideo: isVideo);

    [Fact]
    public void NoSessions_ReturnsNull()
    {
        var result = CurrentSessionSelector.SelectCurrent(Array.Empty<MediaSessionState>());
        Assert.Null(result);
    }

    [Fact]
    public void NoPlayingSessions_ReturnsMostRecentlyUpdatedPausedSession()
    {
        // A paused session must stay "current" so its Play button remains reachable on the
        // touchscreen - otherwise pausing would be a one-way door.
        var older = Session("Spotify.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow.AddSeconds(-10));
        var newer = Session("msedge.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow);
        var result = CurrentSessionSelector.SelectCurrent(new[] { older, newer });
        Assert.Equal("msedge.exe", result!.SourceAppId);
    }

    [Fact]
    public void LonePausedSession_IsReturned()
    {
        var sessions = new[] { Session("Spotify.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow) };
        var result = CurrentSessionSelector.SelectCurrent(sessions);
        Assert.Equal("Spotify.exe", result!.SourceAppId);
    }

    [Fact]
    public void AllSessionsClosed_ReturnsNull()
    {
        var sessions = new[]
        {
            Session("Spotify.exe", PlaybackStatus.Closed, DateTimeOffset.UtcNow.AddSeconds(-10)),
            Session("msedge.exe", PlaybackStatus.Closed, DateTimeOffset.UtcNow),
        };
        Assert.Null(CurrentSessionSelector.SelectCurrent(sessions));
    }

    [Fact]
    public void ClosedSessionIsSkipped_InFavorOfNonClosedOne()
    {
        var closedButNewer = Session("msedge.exe", PlaybackStatus.Closed, DateTimeOffset.UtcNow);
        var pausedButOlder = Session("Spotify.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow.AddSeconds(-10));
        var result = CurrentSessionSelector.SelectCurrent(new[] { closedButNewer, pausedButOlder });
        Assert.Equal("Spotify.exe", result!.SourceAppId);
    }

    [Fact]
    public void StoppedSession_IsReturned_WhenNothingIsPlaying()
    {
        var sessions = new[] { Session("Spotify.exe", PlaybackStatus.Stopped, DateTimeOffset.UtcNow) };
        var result = CurrentSessionSelector.SelectCurrent(sessions);
        Assert.Equal("Spotify.exe", result!.SourceAppId);
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

    [Fact]
    public void PausedVideoSession_DoesNotOutrankOlderPausedMusicSession_InFallback()
    {
        // A stale/idle paused video tab must not steal "current" from the music session just
        // because it happens to have a more recent LastUpdated timestamp.
        var music = Session("Spotify.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow.AddSeconds(-10));
        var video = Session("msedge.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow, isVideo: true);
        var result = CurrentSessionSelector.SelectCurrent(new[] { video, music });
        Assert.Equal("Spotify.exe", result!.SourceAppId);
    }

    [Fact]
    public void PausedVideoSession_IsReturned_WhenNoMusicSessionExists()
    {
        var video = Session("msedge.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow, isVideo: true);
        var result = CurrentSessionSelector.SelectCurrent(new[] { video });
        Assert.Equal("msedge.exe", result!.SourceAppId);
    }

    [Fact]
    public void PlayingVideoSession_StillOutranksPausedMusicSession()
    {
        // The "playing" bucket stays type-agnostic - an actively playing video is exactly as
        // current as playing music, only the fallback tie-break favors music.
        var music = Session("Spotify.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow);
        var video = Session("msedge.exe", PlaybackStatus.Playing, DateTimeOffset.UtcNow.AddSeconds(-10), isVideo: true);
        var result = CurrentSessionSelector.SelectCurrent(new[] { video, music });
        Assert.Equal("msedge.exe", result!.SourceAppId);
    }
}
