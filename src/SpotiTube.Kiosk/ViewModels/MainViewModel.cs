using System.ComponentModel;
using SpotiTube.Kiosk.Audio;
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IMediaSessionWatcher _watcher;
    private readonly IVolumeController _volume;

    public MainViewModel(IMediaSessionWatcher watcher, IVolumeController volume)
    {
        _watcher = watcher;
        _volume = volume;
        _watcher.PropertyChanged += (s, e) => Refresh();
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsIdle { get; private set; } = true;
    public string Title { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public byte[]? AlbumArt { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool CanPlay { get; private set; }
    public bool CanPause { get; private set; }
    public bool CanTogglePlayPause => CanPlay || CanPause;
    public bool CanSkipNext { get; private set; }
    public bool CanSkipPrevious { get; private set; }
    public bool CanSeek { get; private set; }
    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; private set; }
    public float Volume { get; private set; }

    private void Refresh()
    {
        var current = _watcher.Current;
        IsIdle = current is null;

        if (current is not null)
        {
            Title = current.Title;
            Artist = current.Artist;
            AlbumArt = current.AlbumArt;
            IsPlaying = current.Status == PlaybackStatus.Playing;
            CanPlay = current.CanPlay;
            CanPause = current.CanPause;
            CanSkipNext = current.CanSkipNext;
            CanSkipPrevious = current.CanSkipPrevious;
            CanSeek = current.CanSeek;
            Position = current.Position;
            Duration = current.Duration;
            Volume = _volume.GetVolume(current.SourceAppId);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public Task<bool> TogglePlayPauseAsync() => _watcher.TogglePlayPauseAsync();
    public Task<bool> SkipNextAsync() => _watcher.SkipNextAsync();
    public Task<bool> SkipPreviousAsync() => _watcher.SkipPreviousAsync();
    public Task<bool> SeekAsync(TimeSpan position) => _watcher.SeekAsync(position);

    public void SetVolume(float level)
    {
        var current = _watcher.Current;
        if (current is null) return;

        _volume.SetVolume(current.SourceAppId, level);
        Volume = level;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
    }
}
