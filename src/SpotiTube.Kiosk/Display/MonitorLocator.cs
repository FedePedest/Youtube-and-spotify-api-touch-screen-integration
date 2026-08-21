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
        var config = JsonSerializer.Deserialize<MonitorConfig>(File.ReadAllText(_configPath));
        return config?.DeviceName;
    }

    private sealed record MonitorConfig(string DeviceName);
}
