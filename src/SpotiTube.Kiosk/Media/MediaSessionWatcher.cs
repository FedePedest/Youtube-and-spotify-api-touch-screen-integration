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
    // Keyed by session object identity, not by SourceAppUserModelId. Two concurrent sessions
    // (e.g. two browser tabs, or two windows of the same app) can legitimately share one
    // SourceAppUserModelId while being distinct GlobalSystemMediaTransportControlsSession
    // instances that each need their own tracked state and their own live event subscription.
    // Object-identity keying relies on CsWinRT reusing the same managed wrapper for a given
    // native session as long as a strong managed reference to it is kept alive (which these
    // dictionaries provide) - this is the normal CsWinRT RCW-caching behavior.
    private readonly Dictionary<GlobalSystemMediaTransportControlsSession, MediaSessionState> _states = new();
    private readonly HashSet<GlobalSystemMediaTransportControlsSession> _subscribedSessions = new();

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private MediaSessionState? _current;
    private GlobalSystemMediaTransportControlsSession? _currentSession;

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
        var seen = new HashSet<GlobalSystemMediaTransportControlsSession>();

        foreach (var session in sessions)
        {
            seen.Add(session);

            // Only attach handlers the first time we see this exact session object.
            // RefreshAllAsync runs on every SessionsChanged tick (e.g. an unrelated session
            // opening or closing elsewhere), so without this guard a session that persists
            // across ticks would accumulate duplicate subscriptions and OnSessionChangedAsync
            // would fire once per accumulated subscription instead of once per real event.
            if (_subscribedSessions.Add(session))
            {
                session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            }

            await UpdateStateAsync(session);
        }

        // Sessions that disappeared: drop their tracked state and unsubscribe. Because
        // everything here is keyed by session object identity, two same-appId sessions are
        // tracked and cleaned up independently - removing one doesn't touch the other.
        foreach (var stale in _states.Keys.Where(s => !seen.Contains(s)).ToList())
        {
            _states.Remove(stale);
            if (_subscribedSessions.Remove(stale))
            {
                stale.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                stale.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                stale.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }
        }

        ApplySelection();
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
        if (!_subscribedSessions.Contains(session)) return;

        await UpdateStateAsync(session);
        ApplySelection();
    }

    private async Task UpdateStateAsync(GlobalSystemMediaTransportControlsSession session)
    {
        _states.TryGetValue(session, out var existing);
        var newState = await ReadStateAsync(session, existing);

        // If nothing meaningful changed, keep the existing record (and its LastUpdated
        // timestamp) rather than overwriting it. Without this, every RefreshAllAsync pass
        // triggered by an unrelated session appearing/disappearing would stamp a fresh
        // LastUpdated on every tracked session, which would both cause Current's
        // PropertyChanged to fire spuriously (the record's structural equality includes
        // LastUpdated) and skew CurrentSessionSelector's most-recently-updated tie-break.
        if (existing is not null && ContentEquals(existing, newState))
        {
            return;
        }

        _states[session] = newState;
    }

    /// <summary>
    /// Recomputes <see cref="Current"/> (and the session object backing it) from the current
    /// <see cref="_states"/> snapshot via <see cref="CurrentSessionSelector.SelectCurrent"/>.
    /// </summary>
    private void ApplySelection()
    {
        var selected = CurrentSessionSelector.SelectCurrent(_states.Values.ToList());
        _currentSession = FindSessionForState(selected);
        Current = selected;
    }

    /// <summary>
    /// Maps a <see cref="MediaSessionState"/> back to the session object that produced it.
    /// <paramref name="state"/> is always one of the exact object instances currently held in
    /// <see cref="_states"/>'s values (CurrentSessionSelector only filters/reorders, it never
    /// copies), so a reference-equality scan always finds it when <paramref name="state"/> is
    /// non-null.
    /// </summary>
    private GlobalSystemMediaTransportControlsSession? FindSessionForState(MediaSessionState? state)
    {
        if (state is null) return null;

        foreach (var kvp in _states)
        {
            if (ReferenceEquals(kvp.Value, state)) return kvp.Key;
        }

        return null;
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

    private static async Task<MediaSessionState> ReadStateAsync(
        GlobalSystemMediaTransportControlsSession session,
        MediaSessionState? previous)
    {
        var props = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();

        var title = props?.Title ?? string.Empty;
        var artist = props?.Artist ?? string.Empty;

        // Reading the thumbnail means opening and copying a stream, which is comparatively
        // expensive and happens on every TimelinePropertiesChanged tick during active
        // playback even though the artwork itself only changes when the track changes. Reuse
        // the previously read bytes whenever the track identity (app + title + artist) hasn't
        // moved instead of re-reading and re-comparing it every time.
        byte[]? art;
        if (previous is not null
            && previous.SourceAppId == session.SourceAppUserModelId
            && previous.Title == title
            && previous.Artist == artist)
        {
            art = previous.AlbumArt;
        }
        else if (props?.Thumbnail is not null)
        {
            using var stream = await props.Thumbnail.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(ms);
            art = ms.ToArray();
        }
        else
        {
            art = null;
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
            Title: title,
            Artist: artist,
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
        var session = _currentSession;
        return session is null ? Task.FromResult(false) : action(session).AsTask();
    }

    public void Dispose()
    {
        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        foreach (var session in _subscribedSessions)
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }

        _subscribedSessions.Clear();
        _states.Clear();
        _currentSession = null;
        _manager = null;
    }
}
