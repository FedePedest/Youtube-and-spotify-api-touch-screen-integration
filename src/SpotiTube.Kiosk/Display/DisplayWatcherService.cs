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

        // SystemEvents.DisplaySettingsChanged is raised on its own dedicated thread, never the WPF
        // UI thread, and both callbacks manipulate the main window (Show/Hide/PlaceOnDisplay).
        // Marshal here, at the source, so callers don't each need their own threading awareness.
        if (action == MonitorPresenceAction.Show) RunOnUiThread(_onShow);
        else if (action == MonitorPresenceAction.Hide) RunOnUiThread(_onHide);
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the WPF UI thread, or inline when there is no WPF
    /// <see cref="System.Windows.Application"/> (unit tests) or we are already on the UI thread.
    /// </summary>
    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
