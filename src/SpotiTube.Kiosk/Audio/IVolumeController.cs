namespace SpotiTube.Kiosk.Audio;

public interface IVolumeController
{
    float GetVolume(string sourceAppId);
    void SetVolume(string sourceAppId, float level);
    bool GetMute(string sourceAppId);
    void SetMute(string sourceAppId, bool mute);
}
