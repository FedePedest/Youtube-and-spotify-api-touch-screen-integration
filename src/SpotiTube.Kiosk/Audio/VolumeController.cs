using System.Diagnostics;
using NAudio.CoreAudioApi;
using SpotiTube.Kiosk.Logging;

namespace SpotiTube.Kiosk.Audio;

/// <summary>
/// Controls per-app volume/mute via Windows Core Audio session APIs (NAudio), falling back to the
/// default render device's master volume when no audio session matches <c>sourceAppId</c> (e.g. the
/// app hasn't opened an audio stream yet, or genuinely has no matching session).
/// </summary>
public sealed class VolumeController : IVolumeController
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly FileLogger? _logger;

    /// <param name="logger">
    /// Optional sink for Core Audio failures. Every public method here talks to COM, which can fail
    /// for reasons entirely outside the app's control (default endpoint switched or removed mid-call,
    /// no active render device at all). These calls sit on the media-event path, so an escaping
    /// exception would take the whole kiosk down; instead each one is logged and degrades to a safe
    /// default.
    /// </param>
    public VolumeController(FileLogger? logger = null)
    {
        _logger = logger;
    }

    public float GetVolume(string sourceAppId)
    {
        try
        {
            using var device = GetMasterDevice();
            using var session = FindSession(device, sourceAppId);
            return session is not null
                ? session.SimpleAudioVolume.Volume
                : device.AudioEndpointVolume.MasterVolumeLevelScalar;
        }
        catch (Exception ex)
        {
            _logger?.Log($"GetVolume failed for '{sourceAppId}': {ex}");
            return 0f;
        }
    }

    public void SetVolume(string sourceAppId, float level)
    {
        try
        {
            level = Math.Clamp(level, 0f, 1f);
            using var device = GetMasterDevice();
            using var session = FindSession(device, sourceAppId);
            if (session is not null)
            {
                session.SimpleAudioVolume.Volume = level;
            }
            else
            {
                device.AudioEndpointVolume.MasterVolumeLevelScalar = level;
            }
        }
        catch (Exception ex)
        {
            _logger?.Log($"SetVolume failed for '{sourceAppId}': {ex}");
        }
    }

    public bool GetMute(string sourceAppId)
    {
        try
        {
            using var device = GetMasterDevice();
            using var session = FindSession(device, sourceAppId);
            return session is not null
                ? session.SimpleAudioVolume.Mute
                : device.AudioEndpointVolume.Mute;
        }
        catch (Exception ex)
        {
            _logger?.Log($"GetMute failed for '{sourceAppId}': {ex}");
            return false;
        }
    }

    public void SetMute(string sourceAppId, bool mute)
    {
        try
        {
            using var device = GetMasterDevice();
            using var session = FindSession(device, sourceAppId);
            if (session is not null)
            {
                session.SimpleAudioVolume.Mute = mute;
            }
            else
            {
                device.AudioEndpointVolume.Mute = mute;
            }
        }
        catch (Exception ex)
        {
            _logger?.Log($"SetMute failed for '{sourceAppId}': {ex}");
        }
    }

    /// <summary>
    /// Finds the audio session (if any) belonging to the process backing <paramref name="sourceAppId"/>
    /// via <see cref="AudioSessionMatcher.FindMatch"/>.
    /// </summary>
    /// <remarks>
    /// Every <see cref="AudioSessionControl"/> obtained from <paramref name="device"/>'s session
    /// manager wraps its own COM interface pointer that isn't released until Dispose is called (or
    /// the RCW is finalized). This method is expected to run on every poll tick from the caller, so
    /// non-matching candidates are disposed here before returning rather than left for the GC -
    /// otherwise every tick would leak one COM reference per system audio session that isn't the
    /// match.
    /// </remarks>
    private static AudioSessionControl? FindSession(MMDevice device, string sourceAppId)
    {
        var sessions = device.AudioSessionManager.Sessions;
        var candidates = new List<(AudioSessionInfo Info, AudioSessionControl Control)>();

        for (int i = 0; i < sessions.Count; i++)
        {
            var control = sessions[i];
            try
            {
                using var process = Process.GetProcessById((int)control.GetProcessID);
                candidates.Add((new AudioSessionInfo((int)control.GetProcessID, process.ProcessName + ".exe"), control));
            }
            catch (ArgumentException)
            {
                // Process exited between enumeration and lookup; skip it.
                control.Dispose();
            }
        }

        var match = AudioSessionMatcher.FindMatch(candidates.Select(c => c.Info).ToList(), sourceAppId);

        AudioSessionControl? result = null;
        foreach (var (info, control) in candidates)
        {
            if (result is null && match is not null && info.ProcessId == match.ProcessId)
            {
                result = control;
            }
            else
            {
                control.Dispose();
            }
        }

        return result;
    }

    private MMDevice GetMasterDevice() => _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
}
