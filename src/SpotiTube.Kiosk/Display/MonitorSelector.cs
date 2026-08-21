namespace SpotiTube.Kiosk.Display;

public static class MonitorSelector
{
    public const int TargetWidth = 1024;
    public const int TargetHeight = 600;

    public static DisplayInfo? SelectTouchMonitor(IReadOnlyList<DisplayInfo> displays, string? configuredDeviceName)
    {
        if (!string.IsNullOrEmpty(configuredDeviceName))
        {
            var configured = displays.FirstOrDefault(d => d.DeviceName == configuredDeviceName);
            if (configured is not null) return configured;
        }

        var matches = displays.Where(d => d.WidthPx == TargetWidth && d.HeightPx == TargetHeight).ToList();
        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => matches.FirstOrDefault(d => !d.IsPrimary) ?? matches[0],
        };
    }
}
