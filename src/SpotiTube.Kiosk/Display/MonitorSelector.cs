namespace SpotiTube.Kiosk.Display;

public static class MonitorSelector
{
    public static DisplayInfo? SelectTouchMonitor(IReadOnlyList<DisplayInfo> displays, string? configuredDeviceName)
    {
        if (!string.IsNullOrEmpty(configuredDeviceName))
        {
            var configured = displays.FirstOrDefault(d => d.DeviceName == configuredDeviceName);
            if (configured is not null) return configured;
        }

        if (displays.Count == 0) return null;

        // The touch monitor is reliably the smallest-area display in any realistic desk setup,
        // regardless of its exact resolution. Prefer a non-primary display on a tie since a
        // touch kiosk monitor is essentially never configured as the Windows primary display.
        var smallestArea = displays.Min(d => (long)d.WidthPx * d.HeightPx);
        var smallest = displays.Where(d => (long)d.WidthPx * d.HeightPx == smallestArea).ToList();
        return smallest.Count == 1 ? smallest[0] : smallest.FirstOrDefault(d => !d.IsPrimary) ?? smallest[0];
    }
}
