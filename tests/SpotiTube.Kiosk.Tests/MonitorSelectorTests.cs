using Xunit;
using SpotiTube.Kiosk.Display;

namespace SpotiTube.Kiosk.Tests;

public class MonitorSelectorTests
{
    [Fact]
    public void NoDisplays_ReturnsNull()
    {
        Assert.Null(MonitorSelector.SelectTouchMonitor(Array.Empty<DisplayInfo>(), configuredDeviceName: null));
    }

    [Fact]
    public void PicksSmallestAreaDisplay()
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
    public void SmallestByArea_NotJustNarrowest()
    {
        // DISPLAY2 is narrower but taller, giving it a larger area than DISPLAY3.
        var displays = new[]
        {
            new DisplayInfo("\\\\.\\DISPLAY1", 1920, 1080, true),
            new DisplayInfo("\\\\.\\DISPLAY2", 800, 1200, false),
            new DisplayInfo("\\\\.\\DISPLAY3", 1024, 600, false),
        };
        var result = MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: null);
        Assert.Equal("\\\\.\\DISPLAY3", result!.DeviceName);
    }

    [Fact]
    public void TiedSmallestArea_PrefersNonPrimary()
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
    public void ConfiguredDeviceName_TakesPriorityOverSmallestArea()
    {
        var displays = new[]
        {
            new DisplayInfo("\\\\.\\DISPLAY1", 1920, 1080, true),
            new DisplayInfo("\\\\.\\DISPLAY2", 1024, 600, false),
        };
        var result = MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: "\\\\.\\DISPLAY1");
        Assert.Equal("\\\\.\\DISPLAY1", result!.DeviceName);
    }

    [Fact]
    public void ConfiguredDeviceName_NotFound_FallsBackToSmallestArea()
    {
        var displays = new[]
        {
            new DisplayInfo("\\\\.\\DISPLAY1", 1920, 1080, true),
            new DisplayInfo("\\\\.\\DISPLAY2", 1024, 600, false),
        };
        var result = MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: "\\\\.\\DISPLAY9");
        Assert.Equal("\\\\.\\DISPLAY2", result!.DeviceName);
    }
}
