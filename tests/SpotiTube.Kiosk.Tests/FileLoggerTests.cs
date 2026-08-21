using Xunit;
using SpotiTube.Kiosk.Logging;

namespace SpotiTube.Kiosk.Tests;

public class FileLoggerTests
{
    [Fact]
    public void Log_AppendsMessage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"log-{Guid.NewGuid()}.txt");
        try
        {
            var logger = new FileLogger(path);
            logger.Log("hello");
            Assert.Contains("hello", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Log_TrimsWhenOverLimit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"log-{Guid.NewGuid()}.txt");
        try
        {
            var logger = new FileLogger(path, maxBytes: 100);
            for (int i = 0; i < 20; i++)
            {
                logger.Log($"line {i} padding padding padding");
            }
            Assert.True(new FileInfo(path).Length < 2000);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
