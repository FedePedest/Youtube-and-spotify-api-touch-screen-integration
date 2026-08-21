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

    public void PlaceOnDisplay(System.Windows.Forms.Screen screen)
    {
        Left = screen.Bounds.Left;
        Top = screen.Bounds.Top;
        Width = screen.Bounds.Width;
        Height = screen.Bounds.Height;
        WindowState = WindowState.Normal;
    }
}
