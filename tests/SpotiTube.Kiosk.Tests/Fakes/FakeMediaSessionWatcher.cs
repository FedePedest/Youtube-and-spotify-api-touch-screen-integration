using System.ComponentModel;
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.Tests.Fakes;

public sealed class FakeMediaSessionWatcher : IMediaSessionWatcher
{
    public MediaSessionState? Current { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void RaiseChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));

    public Task<bool> TogglePlayPauseAsync() => Task.FromResult(true);
    public Task<bool> SkipNextAsync() => Task.FromResult(true);
    public Task<bool> SkipPreviousAsync() => Task.FromResult(true);
    public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true);
}
