namespace SpotiTube.Kiosk.Audio;

public static class AudioSessionMatcher
{
    /// <summary>
    /// SMTC source-app ids that are bare AUMIDs rather than executable names, mapped to the process
    /// name that actually owns the audio session. Edge reports "MSEdge" and Chrome reports "Chrome"
    /// (no ".exe", no "!" separator), neither of which would ever equal "msedge"/"chrome" on its own.
    /// </summary>
    private static readonly Dictionary<string, string> KnownAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MSEdge"] = "msedge",
            ["Chrome"] = "chrome",
        };

    public static AudioSessionInfo? FindMatch(IReadOnlyList<AudioSessionInfo> sessions, string sourceAppId)
    {
        if (string.IsNullOrEmpty(sourceAppId)) return null;

        var appPart = sourceAppId.Contains('!') ? sourceAppId.Split('!')[0] : sourceAppId;
        var target = StripExe(appPart);

        if (KnownAliases.TryGetValue(target, out var aliased))
        {
            target = aliased;
        }

        return sessions.FirstOrDefault(s =>
            string.Equals(StripExe(s.ProcessName), target, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Normalizes a process/app name by removing a trailing ".exe" so an SMTC id that carries the
    /// suffix ("Spotify.exe") and one that doesn't ("MSEdge") compare on the same footing against
    /// the process names reported by Core Audio ("Spotify.exe", "msedge.exe").
    /// </summary>
    private static string StripExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
}
