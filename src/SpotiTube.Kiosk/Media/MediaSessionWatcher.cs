using System.ComponentModel;
using System.IO;
using Windows.Foundation;
using Windows.Media.Control;

namespace SpotiTube.Kiosk.Media;

/// <summary>
/// Wraps the Windows System Media Transport Controls (SMTC) session manager to expose
/// a single "current" <see cref="MediaSessionState"/> selected via
/// <see cref="CurrentSessionSelector.SelectCurrent"/>.
/// </summary>
public sealed class MediaSessionWatcher : IMediaSessionWatcher, IDisposable
{
    // Sessions and their known state are keyed by SourceAppUserModelId rather than by the
    // GlobalSystemMediaTransportControlsSession object itself. WinRT projections are not
    // guaranteed to hand back the same managed wrapper instance for the same underlying
    // native session across repeated GetSessions() calls, so keying by object reference
    // risks losing track of "already subscribed" sessions and re-subscribing duplicate
    // event handlers on every SessionsChanged tick.
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _sessionsByAppId = new();
    private readonly Dictionary<string, MediaSessionState> _states = new();
    private readonly HashSet<string> _subscribedAppIds = new();

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private MediaSessionState? _current;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MediaSessionState? Current
    {
        get => _current;
        private set
        {
            if (!Equals(_current, value))
            {
                _current = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
            }
        }
    }

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += OnSessionsChanged;
        await RefreshAllAsync();
    }

    private async void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        await RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        if (_manager is null) return;

        var sessions = _manager.GetSessions();
        var seenAppIds = new HashSet<string>();

        foreach (var session in sessions)
        {
            var appId = session.SourceAppUserModelId;
            seenAppIds.Add(appId);
            _sessionsByAppId[appId] = session;

            // Only attach handlers the first time we see this app id. RefreshAllAsync runs
            // on every SessionsChanged tick (e.g. an unrelated session opening or closing
            // elsewhere), so without this guard sessions that persist across ticks would
            // accumulate duplicate subscriptions and OnSessionChangedAsync would fire once
            // per accumulated subscription instead of once per real event.
            if (_subscribedAppIds.Add(appId))
            {
                session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            }

            await UpdateStateAsync(session, appId);
        }

        // Drop bookkeeping for sessions that no longer exist so a later reappearance
        // (e.g. the same app id reused by a new session object) resubscribes cleanly.
        foreach (var staleAppId in _states.Keys.Where(id => !seenAppIds.Contains(id)).ToList())
        {
            _states.Remove(staleAppId);
            _sessionsByAppId.Remove(staleAppId);
            _subscribedAppIds.Remove(staleAppId);
        }

        Current = CurrentSessionSelector.SelectCurrent(_states.Values.ToList());
    }

    private async void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
        => await OnSessionChangedAsync(sender);

    private async void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
        => await OnSessionChangedAsync(sender);

    private async void OnTimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args)
        => await OnSessionChangedAsync(sender);

    private async Task OnSessionChangedAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var appId = session.SourceAppUserModelId;
        if (!_subscribedAppIds.Contains(appId)) return;

        // The sender delivered by the event is the freshest wrapper for this session; keep
        // it so TryXAsync calls below always go through a live object.
        _sessionsByAppId[appId] = session;
        await UpdateStateAsync(session, appId);
        Current = CurrentSessionSelector.SelectCurrent(_states.Values.ToList());
    }

    private async Task UpdateStateAsync(GlobalSystemMediaTransportControlsSession session, string appId)
    {
        var newState = await ReadStateAsync(session);

        // If nothing meaningful changed, keep the existing record (and its LastUpdated
        // timestamp) rather than overwriting it. Without this, every RefreshAllAsync pass
        // triggered by an unrelated session appearing/disappearing would stamp a fresh
        // LastUpdated on every tracked session, which would both cause Current's
        // PropertyChanged to fire spuriously (the record's structural equality includes
        // LastUpdated) and skew CurrentSessionSelector's most-recently-updated tie-break.
        if (_states.TryGetValue(appId, out var existing) && ContentEquals(existing, newState))
        {
            return;
        }

        _states[appId] = newState;
    }

    private static bool ContentEquals(MediaSessionState a, MediaSessionState b) =>
        a.SourceAppId == b.SourceAppId
        && a.Title == b.Title
        && a.Artist == b.Artist
        && a.Status == b.Status
        && a.CanPlay == b.CanPlay
        && a.CanPause == b.CanPause
        && a.CanSkipNext == b.CanSkipNext
        && a.CanSkipPrevious == b.CanSkipPrevious
        && a.CanSeek == b.CanSeek
        && a.Position == b.Position
        && a.Duration == b.Duration
        && AlbumArtEquals(a.AlbumArt, b.AlbumArt);

    private static bool AlbumArtEquals(byte[]? a, byte[]? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.AsSpan().SequenceEqual(b);
    }

    private static async Task<MediaSessionState> ReadStateAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var props = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();

        byte[]? art = null;
        if (props?.Thumbnail is not null)
        {
            using var stream = await props.Thumbnail.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(ms);
            art = ms.ToArray();
        }

        var status = playback.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackStatus.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackStatus.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => PlaybackStatus.Changing,
            _ => PlaybackStatus.Closed,
        };

        var controls = playback.Controls;

        return new MediaSessionState(
            SourceAppId: session.SourceAppUserModelId,
            Title: props?.Title ?? string.Empty,
            Artist: props?.Artist ?? string.Empty,
            AlbumArt: art,
            Status: status,
            CanPlay: controls.IsPlayEnabled,
            CanPause: controls.IsPauseEnabled,
            CanSkipNext: controls.IsNextEnabled,
            CanSkipPrevious: controls.IsPreviousEnabled,
            CanSeek: controls.IsPlaybackPositionEnabled,
            Position: timeline.Position,
            Duration: timeline.EndTime - timeline.StartTime,
            LastUpdated: DateTimeOffset.UtcNow);
    }

    public Task<bool> TogglePlayPauseAsync() => WithCurrentSessionAsync(s => s.TryTogglePlayPauseAsync());
    public Task<bool> SkipNextAsync() => WithCurrentSessionAsync(s => s.TrySkipNextAsync());
    public Task<bool> SkipPreviousAsync() => WithCurrentSessionAsync(s => s.TrySkipPreviousAsync());
    public Task<bool> SeekAsync(TimeSpan position) =>
        WithCurrentSessionAsync(s => s.TryChangePlaybackPositionAsync(position.Ticks));

    private Task<bool> WithCurrentSessionAsync(
        Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> action)
    {
        var session = GetCurrentSession();
        return session is null ? Task.FromResult(false) : action(session).AsTask();
    }

    private GlobalSystemMediaTransportControlsSession? GetCurrentSession()
    {
        if (_current is null) return null;
        return _sessionsByAppId.TryGetValue(_current.SourceAppId, out var session) ? session : null;
    }

    public void Dispose()
    {
        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        foreach (var (appId, session) in _sessionsByAppId)
        {
            if (!_subscribedAppIds.Contains(appId)) continue;
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }

        _subscribedAppIds.Clear();
        _sessionsByAppId.Clear();
        _states.Clear();
        _manager = null;
    }
}
