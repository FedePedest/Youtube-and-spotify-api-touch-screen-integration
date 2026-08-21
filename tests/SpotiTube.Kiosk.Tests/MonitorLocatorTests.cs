using System;
using System.IO;
using Xunit;
using SpotiTube.Kiosk.Display;

namespace SpotiTube.Kiosk.Tests;

public class MonitorLocatorTests
{
    [Fact]
    public void Locate_WithMalformedConfigFile_DoesNotThrow_FallsBackToResolutionMatch()
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"monitor-config-{Guid.NewGuid()}.json");
        File.WriteAllText(configPath, "{ this is not valid json ");
        try
        {
            var locator = new MonitorLocator(configPath);

            var exception = Record.Exception(() => locator.Locate());

            Assert.Null(exception);
        }
        finally
        {
            if (File.Exists(configPath)) File.Delete(configPath);
        }
    }
}
