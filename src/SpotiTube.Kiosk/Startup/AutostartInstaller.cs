using System.IO;
using System.Runtime.InteropServices;

namespace SpotiTube.Kiosk.Startup;

public static class AutostartInstaller
{
    public static string GetShortcutPath(string startupFolder, string appName) =>
        Path.Combine(startupFolder, $"{appName}.lnk");

    public static void Install(string startupFolder, string appName, string targetExePath)
    {
        var shortcutPath = GetShortcutPath(startupFolder, appName);
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell COM component is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetExePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExePath);
            shortcut.WindowStyle = 7; // minimized
            shortcut.Save();
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
        }
    }

    public static bool IsInstalled(string startupFolder, string appName) =>
        File.Exists(GetShortcutPath(startupFolder, appName));
}
