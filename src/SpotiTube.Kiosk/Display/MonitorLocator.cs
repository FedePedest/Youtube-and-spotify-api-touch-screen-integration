using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace SpotiTube.Kiosk.Display;

public sealed class MonitorLocator
{
    private readonly string _configPath;

    public MonitorLocator(string configPath)
    {
        _configPath = configPath;
    }

    public DisplayInfo? Locate()
    {
        var displays = Screen.AllScreens
            .Select(s => new DisplayInfo(s.DeviceName, s.Bounds.Width, s.Bounds.Height, s.Primary))
            .ToList();

        return MonitorSelector.SelectTouchMonitor(displays, ReadConfiguredDeviceName());
    }

    public void SaveConfiguredDeviceName(string deviceName)
    {
        File.WriteAllText(_configPath, JsonSerializer.Serialize(new MonitorConfig(deviceName)));
    }

    private string? ReadConfiguredDeviceName()
    {
        if (!File.Exists(_configPath)) return null;
        try
        {
            var config = JsonSerializer.Deserialize<MonitorConfig>(File.ReadAllText(_configPath));
            return config?.DeviceName;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A truncated/malformed config file (e.g. after an ungraceful shutdown on an
            // unattended kiosk device, or a mid-write read) must not crash Locate(). Fall
            // back to "no configured name" so resolution-based matching still runs.
            return null;
        }
    }

    private sealed record MonitorConfig(string DeviceName);
}
