using Xunit;
using SpotiTube.Kiosk.Audio;

namespace SpotiTube.Kiosk.Tests;

public class AudioSessionMatcherTests
{
    [Fact]
    public void EmptySourceAppId_ReturnsNull()
    {
        var sessions = new[] { new AudioSessionInfo(100, "Spotify.exe") };
        Assert.Null(AudioSessionMatcher.FindMatch(sessions, ""));
    }

    [Fact]
    public void ExactExeMatch_ReturnsIt()
    {
        var sessions = new[]
        {
            new AudioSessionInfo(100, "Spotify.exe"),
            new AudioSessionInfo(200, "msedge.exe"),
        };
        var result = AudioSessionMatcher.FindMatch(sessions, "Spotify.exe");
        Assert.Equal(100, result!.ProcessId);
    }

    [Fact]
    public void AumidWithBangSeparator_MatchesExeNamePrefix()
    {
        var sessions = new[] { new AudioSessionInfo(200, "msedge.exe") };
        var result = AudioSessionMatcher.FindMatch(sessions, "msedge.exe!App");
        Assert.Equal(200, result!.ProcessId);
    }

    [Fact]
    public void NoMatch_ReturnsNull()
    {
        var sessions = new[] { new AudioSessionInfo(100, "Spotify.exe") };
        Assert.Null(AudioSessionMatcher.FindMatch(sessions, "chrome.exe"));
    }
}
