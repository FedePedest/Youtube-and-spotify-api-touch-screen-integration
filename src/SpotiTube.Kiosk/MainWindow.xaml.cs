using System.ComponentModel;
using System.Windows;
using SpotiTube.Kiosk.ViewModels;

namespace SpotiTube.Kiosk;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

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
    /// Moves the window onto <paramref name="screen"/> and fills it.
    /// </summary>
    /// <remarks>
    /// Screen.Bounds is in physical pixels while WPF's Left/Top/Width/Height are device-independent
    /// units, so copying the bounds across only lands correctly when every monitor sits at 100%
    /// scaling. Instead, nudge the (restored) window to a point inside the target screen and let the
    /// OS window manager maximize it there: the maximize is DPI-aware, so no manual pixel/DIP
    /// conversion is needed and a scaling mismatch between the primary display and the touch panel
    /// no longer mis-positions or mis-sizes the kiosk.
    /// </remarks>
    public void PlaceOnDisplay(System.Windows.Forms.Screen screen)
    {
        WindowState = WindowState.Normal;
        Left = screen.Bounds.Left + 10;
        Top = screen.Bounds.Top + 10;
        WindowState = WindowState.Maximized;
    }
}
