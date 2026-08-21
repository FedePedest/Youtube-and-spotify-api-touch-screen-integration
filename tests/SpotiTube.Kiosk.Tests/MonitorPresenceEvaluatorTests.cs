using SpotiTube.Kiosk.Display;
using Xunit;

namespace SpotiTube.Kiosk.Tests;

public class MonitorPresenceEvaluatorTests
{
    private static readonly DisplayInfo Display = new("\\\\.\\DISPLAY2", 1024, 600, false);
    private static readonly DisplayInfo OtherDisplay = new("\\\\.\\DISPLAY3", 1024, 600, false);

    [Fact]
    public void MonitorAppears_ReturnsShow()
    {
        Assert.Equal(MonitorPresenceAction.Show, MonitorPresenceEvaluator.Evaluate(null, Display));
    }

    [Fact]
    public void MonitorDisappears_ReturnsHide()
    {
        Assert.Equal(MonitorPresenceAction.Hide, MonitorPresenceEvaluator.Evaluate(Display, null));
    }

    [Fact]
    public void MonitorUnchanged_ReturnsNoChange()
    {
        Assert.Equal(MonitorPresenceAction.NoChange, MonitorPresenceEvaluator.Evaluate(Display, Display));
    }

    [Fact]
    public void MonitorSwapsToDifferentDevice_ReturnsShow()
    {
        Assert.Equal(MonitorPresenceAction.Show, MonitorPresenceEvaluator.Evaluate(Display, OtherDisplay));
    }

    [Fact]
    public void NeitherPresent_ReturnsNoChange()
    {
        Assert.Equal(MonitorPresenceAction.NoChange, MonitorPresenceEvaluator.Evaluate(null, null));
    }
}
