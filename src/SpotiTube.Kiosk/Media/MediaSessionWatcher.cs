using System.ComponentModel;
using System.IO;
using SpotiTube.Kiosk.Threading;
using Windows.Foundation;
using Windows.Media;
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

    // Serializes every mutation of _states/_subscribedSessions. WinRT delivers SessionsChanged and
    // the per-session change events on arbitrary thread-pool threads, and the handlers below await
    // mid-body, so two refreshes can legitimately be in flight at the same time (e.g. a track change
    // in one app while another app's session opens). Non-concurrent Dictionary/HashSet under
    // concurrent mutation can corrupt their internal state or throw from an in-progress enumeration.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly TimeSpan FollowUpRecheckDelay = TimeSpan.FromSeconds(3);

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private MediaSessionState? _current;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private (GlobalSystemMediaTransportControlsSession Session, string Title, string Artist)? _lastScheduledFollowUp;
    private volatile bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MediaSessionState? Current
    {
        get => _current;
        private set
        {
            if (!Equals(_current, value))
            {
                _current = value;
                // Marshal to the UI thread at the source: this setter runs on whatever thread WinRT
                // delivered the SMTC callback on, and downstream consumers (the view-model, the
                // window's visibility switch, XAML bindings) all touch DependencyObjects.
                UiThread.Run(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current))));
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
        if (!await EnterGateAsync().ConfigureAwait(false)) return;
        try
        {
            await RefreshAllCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            ExitGate();
        }
    }

    private async Task RefreshAllCoreAsync()
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

    private async Task OnSessionChangedAsync(GlobalSystemMediaTransportControlsSession session, bool forceFreshRead = false)
    {
        if (!await EnterGateAsync().ConfigureAwait(false)) return;
        try
        {
            if (!_subscribedSessions.Contains(session)) return;

            await UpdateStateAsync(session, forceFreshRead).ConfigureAwait(false);
            ApplySelection();
        }
        finally
        {
            ExitGate();
        }
    }

    /// <summary>
    /// SMTC's very first read of a newly-current track can catch it mid-transition - title/artist
    /// or artwork that hasn't settled yet - and since nothing necessarily fires another change event
    /// once it does settle, a wrong read can otherwise stick for the rest of the track. Schedule one
    /// forced, cache-bypassing re-read a few seconds after a track becomes current to catch up if
    /// that happened, without polling continuously.
    /// </summary>
    private void ScheduleFollowUpRecheck(GlobalSystemMediaTransportControlsSession session)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(FollowUpRecheckDelay).ConfigureAwait(false);
                await OnSessionChangedAsync(session, forceFreshRead: true).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort re-verification; the session may have closed or the read may have
                // failed transiently. The regular event-driven path still covers real changes.
            }
        });
    }

    /// <summary>
    /// Acquires <see cref="_gate"/>. Returns false when the watcher has been (or is being) disposed,
    /// in which case the caller must not run its body and must not call <see cref="ExitGate"/>.
    /// </summary>
    private async Task<bool> EnterGateAsync()
    {
        if (_disposed) return false;

        try
        {
            await _gate.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Disposed while an in-flight WinRT callback was waiting to enter; nothing left to do.
            return false;
        }

        if (_disposed)
        {
            ExitGate();
            return false;
        }

        return true;
    }

    private void ExitGate()
    {
        try
        {
            _gate.Release();
        }
        catch (ObjectDisposedException)
        {
            // Disposed while we held the gate; there is nothing to release into.
        }
    }

    private async Task UpdateStateAsync(GlobalSystemMediaTransportControlsSession session, bool forceFreshRead = false)
    {
        _states.TryGetValue(session, out var existing);
        // forceFreshRead deliberately passes null instead of existing: ReadStateAsync only takes its
        // "reuse the cached thumbnail" shortcut when previous is non-null, so this makes it actually
        // re-fetch title/artist/art from the live session rather than trusting what's cached. The
        // ContentEquals check below still runs against the real existing state either way, so a
        // forced read that confirms nothing actually changed still doesn't churn LastUpdated.
        var newState = await ReadStateAsync(session, forceFreshRead ? null : existing);

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
        var session = FindSessionForState(selected);
        _currentSession = session;
        Current = selected;

        if (selected is not null && session is not null)
        {
            var trackIdentity = (session, selected.Title, selected.Artist);
            if (_lastScheduledFollowUp != trackIdentity)
            {
                _lastScheduledFollowUp = trackIdentity;
                ScheduleFollowUpRecheck(session);
            }
        }
        else
        {
            _lastScheduledFollowUp = null;
        }
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
        && a.IsVideo == b.IsVideo
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
        var isVideo = props?.PlaybackType == MediaPlaybackType.Video;

        var status = playback.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackStatus.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackStatus.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => PlaybackStatus.Changing,
            _ => PlaybackStatus.Closed,
        };

        // Reading the thumbnail means opening and copying a stream, which is comparatively
        // expensive and happens on every TimelinePropertiesChanged tick during active playback
        // even though the artwork itself usually only changes when the track changes. Reuse the
        // previously read bytes whenever the track identity (app + title + artist) hasn't moved -
        // *except* we never trust a cached "no art" result (the source's thumbnail sometimes isn't
        // ready yet the first time we read it, so a null must keep retrying rather than sticking
        // forever) and we re-read once on the transition into Paused, since that's exactly when a
        // source is prone to swapping in a different/updated thumbnail behind our back (e.g. a
        // YouTube video's thumbnail settling after the initial frame). This is deliberately just the
        // transition, not "whenever Paused": re-reading on every tick of an already-paused session
        // would re-stamp its LastUpdated each time even when the bytes come back unchanged-but-not-
        // byte-identical (a re-encoded video frame, say), letting a paused/inactive session's
        // timestamp keep creeping forward and wrongly outrank the session actually in use.
        var justPaused = status == PlaybackStatus.Paused && previous?.Status != PlaybackStatus.Paused;

        byte[]? art;
        var canReuseCachedArt =
            previous is not null
            && previous.AlbumArt is not null
            && previous.SourceAppId == session.SourceAppUserModelId
            && previous.Title == title
            && previous.Artist == artist
            && !justPaused;

        if (canReuseCachedArt)
        {
            art = previous!.AlbumArt;
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
            LastUpdated: DateTimeOffset.UtcNow,
            IsVideo: isVideo,
            PositionCapturedAt: timeline.LastUpdatedTime,
            PlaybackRate: playback.PlaybackRate ?? 1.0);
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
        if (_disposed) return;

        // Set first so any WinRT callback still queued behind the gate bails out instead of
        // mutating the collections we are about to tear down.
        _disposed = true;

        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        foreach (var session in _subscribedSessions.ToList())
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }

        _subscribedSessions.Clear();
        _states.Clear();
        _currentSession = null;
        _manager = null;
        _gate.Dispose();
    }
}
