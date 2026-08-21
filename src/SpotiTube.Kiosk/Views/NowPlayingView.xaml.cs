using System.Windows.Controls;
using SpotiTube.Kiosk.ViewModels;

namespace SpotiTube.Kiosk.Views;

public partial class NowPlayingView : System.Windows.Controls.UserControl
{
    public NowPlayingView()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private async void OnPlayPauseClick(object sender, System.Windows.RoutedEventArgs e) =>
        await (Vm?.TogglePlayPauseAsync() ?? Task.CompletedTask);

    private async void OnNextClick(object sender, System.Windows.RoutedEventArgs e) =>
        await (Vm?.SkipNextAsync() ?? Task.CompletedTask);

    private async void OnPreviousClick(object sender, System.Windows.RoutedEventArgs e) =>
        await (Vm?.SkipPreviousAsync() ?? Task.CompletedTask);

    private void OnVolumeChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e) =>
        Vm?.SetVolume((float)e.NewValue);

    private async void OnSeekBarReleased(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm is null) return;
        var seconds = ((Slider)sender).Value;
        await Vm.SeekAsync(TimeSpan.FromSeconds(seconds));
    }
}
