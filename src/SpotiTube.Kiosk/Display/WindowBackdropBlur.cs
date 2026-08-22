using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SpotiTube.Kiosk.Display;

/// <summary>
/// Turns on the OS compositor's blur-behind effect for a window, so whatever is actually rendering
/// on the desktop beneath it - including a live Wallpaper Engine wallpaper - shows through, softened,
/// instead of the window painting its own static background. Lets the kiosk's background be
/// controlled the same way the rest of the desktop's wallpaper is.
/// </summary>
public static class WindowBackdropBlur
{
    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_BLURBEHIND = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <summary>
    /// Enables blur-behind on <paramref name="window"/>, tinted with a low-alpha black so text
    /// drawn over it stays legible without hiding the blurred wallpaper underneath. Must be called
    /// after the window's HWND exists (e.g. from <c>SourceInitialized</c>).
    /// </summary>
    public static void Enable(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        // ABGR, ~20% opacity black - a slight tint over the OS blur rather than a heavy overlay.
        const int tintAbgr = unchecked((int)0x33000000);

        var accent = new AccentPolicy
        {
            AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND,
            GradientColor = tintAbgr,
        };

        var accentSize = Marshal.SizeOf<AccentPolicy>();
        var accentPtr = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentSize,
                Data = accentPtr,
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
