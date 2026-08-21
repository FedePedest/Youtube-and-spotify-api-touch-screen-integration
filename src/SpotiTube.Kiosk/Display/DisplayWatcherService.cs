using Microsoft.Win32;

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

        if (action == MonitorPresenceAction.Show) _onShow();
        else if (action == MonitorPresenceAction.Hide) _onHide();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
