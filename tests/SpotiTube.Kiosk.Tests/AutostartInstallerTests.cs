using SpotiTube.Kiosk.Startup;
using Xunit;

namespace SpotiTube.Kiosk.Tests;

public class AutostartInstallerTests
{
    [Fact]
    public void Install_CreatesShortcutFile()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"startup-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        try
        {
            Assert.False(AutostartInstaller.IsInstalled(tempFolder, "SpotiTube.Kiosk"));

            AutostartInstaller.Install(tempFolder, "SpotiTube.Kiosk", @"C:\fake\SpotiTube.Kiosk.exe");

            Assert.True(AutostartInstaller.IsInstalled(tempFolder, "SpotiTube.Kiosk"));
        }
        finally
        {
            Directory.Delete(tempFolder, recursive: true);
        }
    }
}
