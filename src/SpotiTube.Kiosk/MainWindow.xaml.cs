using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SpotiTube.Kiosk.Display;
using SpotiTube.Kiosk.ViewModels;

namespace SpotiTube.Kiosk;

public partial class MainWindow : Window
{
    private static readonly TimeSpan CursorTrackingInterval = TimeSpan.FromMilliseconds(150);

    private readonly DispatcherTimer _cursorTrackingTimer;
    private System.Drawing.Rectangle? _kioskMonitorBounds;
    private System.Drawing.Point _lastKnownGoodPosition;
    private IntPtr _lastKnownGoodForegroundWindow;

    public MainWindow()
    {
        InitializeComponent();
        PreviewMouseUp += OnPreviewMouseUp;
        SourceInitialized += (_, _) => WindowBackdropBlur.Enable(this);

        // Seed with wherever the cursor happens to be right now, so a touch that lands before the
        // first tick below still has something sane to restore to.
        _lastKnownGoodPosition = System.Windows.Forms.Cursor.Position;
        _lastKnownGoodForegroundWindow = GetForegroundWindow();

        _cursorTrackingTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = CursorTrackingInterval,
        };
        _cursorTrackingTimer.Tick += OnCursorTrackingTick;
        _cursorTrackingTimer.Start();
    }

    /// <summary>
    /// Touch input on this monitor is promoted to a real Windows mouse message, and by the time
    /// that promotion happens the single system-wide cursor has *already* been relocated to the
    /// touch point - and this window has already been given keyboard focus - so there's no "before
    /// the touch" state left to snapshot on MouseDown, only the touch's own result. Instead,
    /// continuously remember the last cursor position seen outside this kiosk monitor and the last
    /// foreground window that wasn't this one, then restore both once the tap/click (or slider
    /// drag) fully completes - so using the kiosk never steals the mouse position or keyboard focus
    /// from whatever the user was doing on another monitor.
    /// </summary>
    private void OnCursorTrackingTick(object? sender, EventArgs e)
    {
        var position = System.Windows.Forms.Cursor.Position;
        if (_kioskMonitorBounds is null || !_kioskMonitorBounds.Value.Contains(position))
        {
            _lastKnownGoodPosition = position;
        }

        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero && foreground != new WindowInteropHelper(this).Handle)
        {
            _lastKnownGoodForegroundWindow = foreground;
        }
    }

    private void OnPreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // PreviewMouseUp tunnels down *before* the bubble-phase MouseUp that actually drives
        // Button.Click, slider thumb drag completion, etc. Restoring synchronously here - especially
        // handing keyboard focus to another window via SetForegroundWindow - was interrupting that
        // routing, so touches appeared to do nothing. Defer to a Background-priority dispatch: that
        // runs only after the current input event has fully finished routing through the tree.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, RestoreCursorAndFocus);
    }

    private void RestoreCursorAndFocus()
    {
        System.Windows.Forms.Cursor.Position = _lastKnownGoodPosition;

        if (_lastKnownGoodForegroundWindow != IntPtr.Zero && IsWindow(_lastKnownGoodForegroundWindow))
        {
            SetForegroundWindow(_lastKnownGoodForegroundWindow);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    public void Bind(MainViewModel viewModel)
    {
        NowPlaying.DataContext = viewModel;
        viewModel.PropertyChanged += (s, e) => UpdateVisibility(viewModel);
        UpdateVisibility(viewModel);
    }

    private void UpdateVisibility(MainViewModel viewModel)
    {
        Idle.Visibility = viewModel.IsIdle ? Visibility.Visible : Visibility.Collapsed;
        NowPlaying.Visibility = viewModel.IsIdle ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Moves the window onto <paramref name="screen"/>, makes it visible, and fills it.
    /// </summary>
    /// <remarks>
    /// Screen.Bounds is in physical pixels while WPF's Left/Top/Width/Height are device-independent
    /// units, so copying the bounds across only lands correctly when every monitor sits at 100%
    /// scaling. Instead, nudge the (restored) window to a point inside the target screen and let the
    /// OS window manager maximize it there: the maximize is DPI-aware, so no manual pixel/DIP
    /// conversion is needed and a scaling mismatch between the primary display and the touch panel
    /// no longer mis-positions or mis-sizes the kiosk.
    ///
    /// Until the window has a real HWND (i.e. before its first <see cref="Show"/>), Windows has no
    /// monitor to resolve "maximized" against and always uses the primary display's work area —
    /// setting WindowState to Maximized first would maximize onto the wrong screen on any machine
    /// where the touch monitor isn't the primary. Show() at the target Left/Top while still Normal
    /// so the HWND exists on the correct monitor before asking Windows to maximize it there.
    /// </remarks>
    public void PlaceOnDisplay(System.Windows.Forms.Screen screen)
    {
        _kioskMonitorBounds = screen.Bounds;
        WindowState = WindowState.Normal;
        Left = screen.Bounds.Left + 10;
        Top = screen.Bounds.Top + 10;
        Show();
        WindowState = WindowState.Maximized;
    }
}
