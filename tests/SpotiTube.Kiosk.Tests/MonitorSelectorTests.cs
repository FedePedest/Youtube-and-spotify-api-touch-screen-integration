using Xunit;
using SpotiTube.Kiosk.Display;

namespace SpotiTube.Kiosk.Tests;

public class MonitorSelectorTests
{
    [Fact]
    public void NoDisplaysMatchResolution_ReturnsNull()
    {
        var displays = new[] { new DisplayInfo("\\\\.\\DISPLAY1", 1920, 1080, true) };
        Assert.Null(MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: null));
    }

    [Fact]
    public void OneDisplayMatchesResolution_ReturnsIt()
    {
        var displays = new[]
        {
            new DisplayInfo("\\\\.\\DISPLAY1", 1920, 1080, true),
            new DisplayInfo("\\\\.\\DISPLAY2", 1024, 600, false),
        };
        var result = MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: null);
        Assert.Equal("\\\\.\\DISPLAY2", result!.DeviceName);
    }

    [Fact]
    public void MultipleMatch_PrefersNonPrimary()
    {
        var displays = new[]
        {
            new DisplayInfo("\\\\.\\DISPLAY1", 1024, 600, true),
            new DisplayInfo("\\\\.\\DISPLAY2", 1024, 600, false),
        };
        var result = MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: null);
        Assert.Equal("\\\\.\\DISPLAY2", result!.DeviceName);
    }

    [Fact]
    public void ConfiguredDeviceName_TakesPriorityOverResolutionMatch()
    {
        var displays = new[]
        {
            new DisplayInfo("\\\\.\\DISPLAY1", 1920, 1080, true),
            new DisplayInfo("\\\\.\\DISPLAY2", 1024, 600, false),
        };
        var result = MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: "\\\\.\\DISPLAY1");
        Assert.Equal("\\\\.\\DISPLAY1", result!.DeviceName);
    }
}
