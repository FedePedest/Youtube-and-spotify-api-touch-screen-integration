using SpotiTube.Kiosk.Audio;

namespace SpotiTube.Kiosk.Tests.Fakes;

public sealed class FakeVolumeController : IVolumeController
{
    public float VolumeLevel = 0.5f;
    public bool Muted;

    public float GetVolume(string sourceAppId) => VolumeLevel;
    public void SetVolume(string sourceAppId, float level) => VolumeLevel = level;
    public bool GetMute(string sourceAppId) => Muted;
    public void SetMute(string sourceAppId, bool mute) => Muted = mute;
}
