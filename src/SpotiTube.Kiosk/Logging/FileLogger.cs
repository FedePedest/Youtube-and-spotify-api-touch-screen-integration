using System.IO;

namespace SpotiTube.Kiosk.Logging;

public sealed class FileLogger
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly object _lock = new();

    public FileLogger(string path, long maxBytes = 1_000_000)
    {
        _path = path;
        _maxBytes = maxBytes;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public void Log(string message)
    {
        lock (_lock)
        {
            File.AppendAllText(_path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
            TrimIfTooLarge();
        }
    }

    private void TrimIfTooLarge()
    {
        var info = new FileInfo(_path);
        if (info.Exists && info.Length > _maxBytes)
        {
            var lines = File.ReadAllLines(_path);
            File.WriteAllLines(_path, lines.Skip(lines.Length / 2));
        }
    }
}
