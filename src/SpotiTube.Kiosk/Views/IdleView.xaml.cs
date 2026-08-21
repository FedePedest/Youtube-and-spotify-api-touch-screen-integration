using System.Windows.Threading;

namespace SpotiTube.Kiosk.Views;

public partial class IdleView : System.Windows.Controls.UserControl
{
    private readonly DispatcherTimer _timer;

    public IdleView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => ClockText.Text = DateTime.Now.ToString("t");
        _timer.Start();
        ClockText.Text = DateTime.Now.ToString("t");
    }
}
