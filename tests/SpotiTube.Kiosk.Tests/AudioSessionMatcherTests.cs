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
    public void BareEdgeAumid_MatchesMsedgeProcess()
    {
        // Edge reports its SMTC source app id as a bare "MSEdge" - no ".exe", no "!" separator.
        var sessions = new[]
        {
            new AudioSessionInfo(100, "Spotify.exe"),
            new AudioSessionInfo(200, "msedge.exe"),
        };
        var result = AudioSessionMatcher.FindMatch(sessions, "MSEdge");
        Assert.Equal(200, result!.ProcessId);
    }

    [Fact]
    public void BareChromeAumid_MatchesChromeProcess()
    {
        var sessions = new[]
        {
            new AudioSessionInfo(100, "Spotify.exe"),
            new AudioSessionInfo(300, "chrome.exe"),
        };
        var result = AudioSessionMatcher.FindMatch(sessions, "Chrome");
        Assert.Equal(300, result!.ProcessId);
    }

    [Fact]
    public void ExeSuffixIsOptionalOnEitherSide()
    {
        var sessions = new[] { new AudioSessionInfo(100, "Spotify.exe") };
        Assert.Equal(100, AudioSessionMatcher.FindMatch(sessions, "Spotify")!.ProcessId);
    }

    [Fact]
    public void NoMatch_ReturnsNull()
    {
        var sessions = new[] { new AudioSessionInfo(100, "Spotify.exe") };
        Assert.Null(AudioSessionMatcher.FindMatch(sessions, "chrome.exe"));
    }
}
