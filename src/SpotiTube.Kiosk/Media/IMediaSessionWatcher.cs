using System.ComponentModel;

namespace SpotiTube.Kiosk.Media;

public interface IMediaSessionWatcher : INotifyPropertyChanged
{
    MediaSessionState? Current { get; }
    Task<bool> TogglePlayPauseAsync();
    Task<bool> SkipNextAsync();
    Task<bool> SkipPreviousAsync();
    Task<bool> SeekAsync(TimeSpan position);
}
