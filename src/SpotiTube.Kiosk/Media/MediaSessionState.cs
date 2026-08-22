namespace SpotiTube.Kiosk.Media;

public enum PlaybackStatus { Closed, Stopped, Paused, Playing, Changing }

public sealed record MediaSessionState(
    string SourceAppId,
    string Title,
    string Artist,
    byte[]? AlbumArt,
    PlaybackStatus Status,
    bool CanPlay,
    bool CanPause,
    bool CanSkipNext,
    bool CanSkipPrevious,
    bool CanSeek,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset LastUpdated,
    bool IsVideo = false,
    // The instant SMTC's own Position snapshot was captured, and the rate playback was advancing
    // at that instant - together these let a consumer estimate the *current* position between real
    // SMTC updates (most apps only push Position on play/pause/seek/track-change, never per-second)
    // via Position + elapsed-since-PositionCapturedAt * PlaybackRate, instead of just displaying an
    // increasingly-stale snapshot.
    DateTimeOffset PositionCapturedAt = default,
    double PlaybackRate = 1.0);
