using System.ComponentModel;
using System.Windows.Threading;
using SpotiTube.Kiosk.Audio;
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaSessionWatcher _watcher;
    private readonly IVolumeController _volume;
    private readonly DispatcherTimer _positionTimer;

    public MainViewModel(IMediaSessionWatcher watcher, IVolumeController volume)
    {
        _watcher = watcher;
        _volume = volume;
        _watcher.PropertyChanged += (s, e) => Refresh();
        Refresh();

        // SMTC only raises TimelinePropertiesChanged on seek/track change for most apps (Spotify
        // included), never on a per-second cadence, so without this tick the seek bar sits frozen
        // for the whole track. This deliberately does NOT call Refresh(): that would also re-read
        // the volume over COM every second for no reason.
        _positionTimer = new DispatcherTimer(
            DispatcherPriority.Normal,
            System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _positionTimer.Tick += OnPositionTick;
        _positionTimer.Start();
    }

    private void OnPositionTick(object? sender, EventArgs e)
    {
        if (IsIdle) return;

        var current = _watcher.Current;
        if (current is null || current.Position == Position) return;

        Position = current.Position;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
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
        else
        {
            Title = string.Empty;
            Artist = string.Empty;
            AlbumArt = null;
            IsPlaying = false;
            CanPlay = false;
            CanPause = false;
            CanSkipNext = false;
            CanSkipPrevious = false;
            CanSeek = false;
            Position = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            Volume = 0f;
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

    public void Dispose()
    {
        _positionTimer.Tick -= OnPositionTick;
        _positionTimer.Stop();
    }
}
