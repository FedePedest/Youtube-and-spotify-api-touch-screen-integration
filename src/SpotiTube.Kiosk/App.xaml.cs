using System.IO;
using System.Windows;
using SpotiTube.Kiosk.Audio;
using SpotiTube.Kiosk.Display;
using SpotiTube.Kiosk.Logging;
using SpotiTube.Kiosk.Media;
using SpotiTube.Kiosk.Resilience;
using SpotiTube.Kiosk.Startup;
using SpotiTube.Kiosk.ViewModels;

namespace SpotiTube.Kiosk;

public partial class App : System.Windows.Application
{
    private FileLogger? _logger;
    private MediaSessionWatcher? _watcher;
    private DisplayWatcherService? _displayWatcher;
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpotiTube.Kiosk");
        _logger = new FileLogger(Path.Combine(appDataDir, "kiosk.log"));

        InstallAutostartIfNeeded();

        _watcher = new MediaSessionWatcher();
        await RetryPolicy.RunWithRetryAsync(
            async () => { await _watcher.StartAsync(); return true; },
            maxAttempts: 3,
            onError: ex => _logger.Log($"MediaSessionWatcher start failed: {ex}"));

        var volumeController = new VolumeController();
        var viewModel = new MainViewModel(_watcher, volumeController);

        _window = new MainWindow();
        _window.Bind(viewModel);

        var monitorConfigPath = Path.Combine(appDataDir, "monitor.json");
        var locator = new MonitorLocator(monitorConfigPath);

        _displayWatcher = new DisplayWatcherService(
            locator,
            onShow: () => ShowOnTouchMonitor(locator),
            onHide: () => _window.Hide());

        _displayWatcher.CheckNow();
    }

    private void ShowOnTouchMonitor(MonitorLocator locator)
    {
        var display = locator.Locate();
        if (display is null || _window is null) return;

        var screen = System.Windows.Forms.Screen.AllScreens
            .FirstOrDefault(s => s.DeviceName == display.DeviceName);
        if (screen is null) return;

        _window.PlaceOnDisplay(screen);
        _window.Show();
    }

    private void InstallAutostartIfNeeded()
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        const string appName = "SpotiTube.Kiosk";
        if (AutostartInstaller.IsInstalled(startupFolder, appName)) return;

        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath)) return;

        AutostartInstaller.Install(startupFolder, appName, exePath);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _displayWatcher?.Dispose();
        _watcher?.Dispose();
        base.OnExit(e);
    }
}
