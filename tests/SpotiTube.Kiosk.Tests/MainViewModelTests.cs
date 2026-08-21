using Xunit;
using SpotiTube.Kiosk.Media;
using SpotiTube.Kiosk.Tests.Fakes;
using SpotiTube.Kiosk.ViewModels;

namespace SpotiTube.Kiosk.Tests;

public class MainViewModelTests
{
    private static MediaSessionState PlayingSession(bool canSkipNext = true) => new(
        SourceAppId: "Spotify.exe",
        Title: "Song",
        Artist: "Artist",
        AlbumArt: null,
        Status: PlaybackStatus.Playing,
        CanPlay: true,
        CanPause: true,
        CanSkipNext: canSkipNext,
        CanSkipPrevious: true,
        CanSeek: true,
        Position: TimeSpan.Zero,
        Duration: TimeSpan.FromMinutes(3),
        LastUpdated: DateTimeOffset.UtcNow);

    [Fact]
    public void IsIdle_WhenNoCurrentSession()
    {
        var watcher = new FakeMediaSessionWatcher { Current = null };
        var vm = new MainViewModel(watcher, new FakeVolumeController());
        Assert.True(vm.IsIdle);
    }

    [Fact]
    public void ShowsNowPlaying_WhenSessionActive()
    {
        var watcher = new FakeMediaSessionWatcher();
        var vm = new MainViewModel(watcher, new FakeVolumeController());

        watcher.Current = PlayingSession();
        watcher.RaiseChanged();

        Assert.False(vm.IsIdle);
        Assert.Equal("Song", vm.Title);
        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public void DisablesSkipNext_WhenSessionDoesNotSupportIt()
    {
        var watcher = new FakeMediaSessionWatcher();
        var vm = new MainViewModel(watcher, new FakeVolumeController());

        watcher.Current = PlayingSession(canSkipNext: false);
        watcher.RaiseChanged();

        Assert.False(vm.CanSkipNext);
    }

    [Fact]
    public void DisablesPlayPause_WhenSessionSupportsNeitherPlayNorPause()
    {
        var watcher = new FakeMediaSessionWatcher();
        var vm = new MainViewModel(watcher, new FakeVolumeController());

        watcher.Current = new MediaSessionState(
            SourceAppId: "Spotify.exe",
            Title: "Song",
            Artist: "Artist",
            AlbumArt: null,
            Status: PlaybackStatus.Playing,
            CanPlay: false,
            CanPause: false,
            CanSkipNext: true,
            CanSkipPrevious: true,
            CanSeek: true,
            Position: TimeSpan.Zero,
            Duration: TimeSpan.FromMinutes(3),
            LastUpdated: DateTimeOffset.UtcNow);
        watcher.RaiseChanged();

        Assert.False(vm.CanTogglePlayPause);
    }

    [Fact]
    public void SetVolume_UpdatesVolumeControllerAndProperty()
    {
        var watcher = new FakeMediaSessionWatcher();
        var volume = new FakeVolumeController();
        var vm = new MainViewModel(watcher, volume);

        watcher.Current = PlayingSession();
        watcher.RaiseChanged();

        vm.SetVolume(0.8f);

        Assert.Equal(0.8f, volume.VolumeLevel);
        Assert.Equal(0.8f, vm.Volume);
    }
}
