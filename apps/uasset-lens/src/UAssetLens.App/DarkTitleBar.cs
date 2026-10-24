using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace UAssetLens.App;

// Asks Windows for a dark title bar so the window matches its contents.
static class DarkTitleBar
{
    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void Apply(Window window) => window.SourceInitialized += (_, _) =>
    {
        int on = 1;
        _ = DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
    };

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
