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
    private byte[]? _accentColorSourceArt;

    // The last position SMTC actually reported and the instant it reported it at, used to estimate
    // the *current* position between real SMTC updates - see EstimateCurrentPosition below.
    private TimeSpan _positionAtCapture;
    private DateTimeOffset _positionCapturedAt;
    private double _playbackRate = 1.0;

    public MainViewModel(IMediaSessionWatcher watcher, IVolumeController volume)
    {
        _watcher = watcher;
        _volume = volume;
        _watcher.PropertyChanged += (s, e) => Refresh();
        Refresh();

        // SMTC only raises TimelinePropertiesChanged on seek/track change for most apps (Spotify
        // included), never on a per-second cadence, so Position itself sits frozen at whatever
        // snapshot SMTC last pushed. This tick doesn't re-read that snapshot (it wouldn't have moved
        // anyway) - it re-estimates the current position client-side from that snapshot's age via
        // EstimateCurrentPosition, so the seek bar keeps advancing smoothly between real SMTC
        // updates. Deliberately does NOT call Refresh(): that would also re-read the volume over COM
        // every second for no reason.
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

        var estimated = EstimateCurrentPosition();
        if (estimated == Position) return;

        Position = estimated;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
    }

    /// <summary>
    /// SMTC's Position is a snapshot, not a live clock - most apps only push a fresh one on
    /// play/pause/seek/track-change, never per-second. Interpolate the *current* position from the
    /// last snapshot's value, the instant it was captured, and the playback rate at that instant,
    /// rather than displaying an increasingly-stale number until the next real SMTC update.
    /// </summary>
    private TimeSpan EstimateCurrentPosition()
    {
        if (!IsPlaying) return _positionAtCapture;

        var elapsed = DateTimeOffset.UtcNow - _positionCapturedAt;
        if (elapsed <= TimeSpan.Zero) return _positionAtCapture;

        var estimated = _positionAtCapture + TimeSpan.FromTicks((long)(elapsed.Ticks * _playbackRate));
        return estimated > Duration ? Duration : estimated;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsIdle { get; private set; } = true;
    public string Title { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public byte[]? AlbumArt { get; private set; }
    public bool IsVideo { get; private set; }
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
    public string AccentColorHex { get; private set; } = AlbumArtColorExtractor.DefaultAccentColorHex;

    private void Refresh()
    {
        var current = _watcher.Current;
        IsIdle = current is null;

        if (current is not null)
        {
            Title = current.Title;
            Artist = current.Artist;
            AlbumArt = current.AlbumArt;
            IsVideo = current.IsVideo;
            IsPlaying = current.Status == PlaybackStatus.Playing;
            CanPlay = current.CanPlay;
            CanPause = current.CanPause;
            CanSkipNext = current.CanSkipNext;
            CanSkipPrevious = current.CanSkipPrevious;
            CanSeek = current.CanSeek;
            Duration = current.Duration;
            Volume = _volume.GetVolume(current.SourceAppId);

            _positionAtCapture = current.Position;
            _positionCapturedAt = current.PositionCapturedAt;
            _playbackRate = current.PlaybackRate;
            Position = EstimateCurrentPosition();

            if (current.IsVideo)
            {
                // Only theme the progress bar off music. A video's thumbnail (a YouTube video, a
                // video playing in some other tab, etc.) isn't "the color of the music" - keep the
                // default accent rather than tinting the bar off whatever frame the video's
                // thumbnail happens to be on.
                _accentColorSourceArt = null;
                AccentColorHex = AlbumArtColorExtractor.DefaultAccentColorHex;
            }
            else if (!ReferenceEquals(current.AlbumArt, _accentColorSourceArt))
            {
                // Extracting a color decodes and samples the image, so only redo it when the art
                // itself actually changed - not on every refresh triggered by something unrelated
                // (e.g. a skip-button availability change) while the same track keeps playing.
                _accentColorSourceArt = current.AlbumArt;
                AccentColorHex = AlbumArtColorExtractor.ExtractAccentColorHex(current.AlbumArt);
            }
        }
        else
        {
            Title = string.Empty;
            Artist = string.Empty;
            AlbumArt = null;
            IsVideo = false;
            IsPlaying = false;
            CanPlay = false;
            CanPause = false;
            CanSkipNext = false;
            CanSkipPrevious = false;
            CanSeek = false;
            Position = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            Volume = 0f;
            _positionAtCapture = TimeSpan.Zero;
            _positionCapturedAt = default;
            _playbackRate = 1.0;
            _accentColorSourceArt = null;
            AccentColorHex = AlbumArtColorExtractor.DefaultAccentColorHex;
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
