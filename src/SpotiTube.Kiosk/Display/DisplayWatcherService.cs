using Microsoft.Win32;
using SpotiTube.Kiosk.Threading;

namespace SpotiTube.Kiosk.Display;

public sealed class DisplayWatcherService : IDisposable
{
    private readonly MonitorLocator _locator;
    private readonly Action _onShow;
    private readonly Action _onHide;
    private DisplayInfo? _lastKnown;

    public DisplayWatcherService(MonitorLocator locator, Action onShow, Action onHide)
    {
        _locator = locator;
        _onShow = onShow;
        _onHide = onHide;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void CheckNow() => OnDisplaySettingsChanged(this, EventArgs.Empty);

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        var current = _locator.Locate();
        var action = MonitorPresenceEvaluator.Evaluate(_lastKnown, current);
        _lastKnown = current;

        // SystemEvents.DisplaySettingsChanged is raised on its own dedicated thread, never the WPF
        // UI thread, and both callbacks manipulate the main window (Show/Hide/PlaceOnDisplay).
        // Marshal here, at the source, so callers don't each need their own threading awareness.
        if (action == MonitorPresenceAction.Show) UiThread.Run(_onShow);
        else if (action == MonitorPresenceAction.Hide) UiThread.Run(_onHide);
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
