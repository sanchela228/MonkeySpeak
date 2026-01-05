using System.Runtime.InteropServices;
using Raylib_cs;

namespace Engine.Internal;

public class Mouse
{
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }
    
    public static Point GetCursorPos()
    {
        #if WINDOWS
            GetCursorPos(out var point);
            return point;
        #else
            GetCursorPos(out var point);
            return point;
        #endif
        
        return new Point();
    }
}