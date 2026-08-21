namespace SpotiTube.Kiosk.Audio;

public static class AudioSessionMatcher
{
    public static AudioSessionInfo? FindMatch(IReadOnlyList<AudioSessionInfo> sessions, string sourceAppId)
    {
        if (string.IsNullOrEmpty(sourceAppId)) return null;

        var exeName = sourceAppId.Contains('!') ? sourceAppId.Split('!')[0] : sourceAppId;

        return sessions.FirstOrDefault(s =>
            string.Equals(s.ProcessName, exeName, StringComparison.OrdinalIgnoreCase));
    }
}
