using System.Runtime.InteropServices;

namespace HeraldHelper.Desktop;

internal static class OverlayTextWindowInterop
{
    internal const int GwlExstyle = -20;
    internal const long WsExToolWindow = 0x00000080L;
    internal const long WsExNoActivate = 0x08000000L;
    internal const long WsExLayered = 0x00080000L;
    internal const long WsExTransparent = 0x00000020L;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtrNative(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtrNative(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    internal static IntPtr GetWindowLongPtr(IntPtr hwnd)
    {
        return GetWindowLongPtrNative(hwnd, GwlExstyle);
    }

    internal static IntPtr SetWindowLongPtr(IntPtr hwnd, IntPtr newLong)
    {
        return SetWindowLongPtrNative(hwnd, GwlExstyle, newLong);
    }
}
