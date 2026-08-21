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
    DateTimeOffset LastUpdated);
