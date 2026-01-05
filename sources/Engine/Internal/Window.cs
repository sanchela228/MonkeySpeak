using System.Runtime.InteropServices;

namespace Engine.Internal;

public class Window
{
    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

    // DWM API
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2;
    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    
    public static void SetWindowRoundedCorners()
    {
        #if WINDOWS
            IntPtr hwnd = GetActiveWindow();

            int pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));

            int dark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        #else
            IntPtr hwnd = GetActiveWindow();

            int pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));

            int dark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        #endif
    }
}