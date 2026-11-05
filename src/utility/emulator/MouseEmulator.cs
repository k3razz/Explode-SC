using System;
using System.Runtime.InteropServices;

namespace MyDynamicESP;

public static class MouseEmulator
{
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    private const uint MOUSEEVENTF_MOVE = 0x0001;

    public static void Move(int dx, int dy)
    {
        try { mouse_event(MOUSEEVENTF_MOVE, dx, dy, 0, UIntPtr.Zero); } catch { }
    }
}
