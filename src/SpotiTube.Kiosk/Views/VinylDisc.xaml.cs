using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SpotiTube.Kiosk.Views;

/// <summary>
/// A spinning vinyl record with the current track's album art rendered as its center label.
/// Spins continuously while <see cref="IsPlaying"/> is true, and pauses in place (like a real
/// turntable) rather than restarting when playback stops.
/// </summary>
public partial class VinylDisc : System.Windows.Controls.UserControl
{
    private static readonly TimeSpan RevolutionDuration = TimeSpan.FromSeconds(1.8);

    public static readonly DependencyProperty AlbumArtProperty = DependencyProperty.Register(
        nameof(AlbumArt), typeof(byte[]), typeof(VinylDisc), new PropertyMetadata(null));

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(VinylDisc),
        new PropertyMetadata(false, OnIsPlayingChanged));

    private AnimationClock? _spinClock;

    public VinylDisc()
    {
        InitializeComponent();
    }

    public byte[]? AlbumArt
    {
        get => (byte[]?)GetValue(AlbumArtProperty);
        set => SetValue(AlbumArtProperty, value);
    }

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((VinylDisc)d).UpdateSpin((bool)e.NewValue);

    private void UpdateSpin(bool isPlaying)
    {
        if (isPlaying)
        {
            if (_spinClock is null)
            {
                var animation = new DoubleAnimation(0, 360, RevolutionDuration)
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                };
                _spinClock = (AnimationClock)animation.CreateClock(true);
                SpinRotation.ApplyAnimationClock(RotateTransform.AngleProperty, _spinClock);
            }
            else
            {
                _spinClock.Controller!.Resume();
            }
        }
        else
        {
            _spinClock?.Controller?.Pause();
        }
    }
}
