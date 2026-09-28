using System.Runtime.InteropServices;

namespace BimArena.Desktop;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom);

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", EntryPoint = "ClipCursor")] internal static extern bool Clip(ref Rect rectangle);
    [DllImport("user32.dll", EntryPoint = "ClipCursor")] internal static extern bool Unclip(nint rectangle);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rectangle);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
}
