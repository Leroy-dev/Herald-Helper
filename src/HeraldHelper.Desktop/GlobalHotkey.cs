using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace HeraldHelper.Desktop;

/// <summary>
/// Registers a system-wide hotkey on a window's HWND (works while the game
/// has focus) and raises a callback on WM_HOTKEY. Settings string format:
/// "Ctrl+F9", "Alt+F1", "F9", "Ctrl+Shift+T" — empty disables.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ModNoRepeat = 0x4000;
    private const int ModAlt = 0x0001;
    private const int ModControl = 0x0002;
    private const int ModShift = 0x0004;
    private const int ModWin = 0x0008;

    private readonly Window _window;
    private readonly int _id;
    private readonly Action _callback;
    private IntPtr _hwnd;
    private HwndSource? _source;
    private bool _registered;

    public GlobalHotkey(Window window, int id, Action callback)
    {
        _window = window;
        _id = id;
        _callback = callback;
    }

    /// <summary>Re-registers with a new gesture string. Invalid text or empty
    /// disables the hotkey and returns false for non-empty input.</summary>
    public bool Apply(string? gestureText)
    {
        Unregister();

        if (string.IsNullOrWhiteSpace(gestureText))
        {
            return true; // disabled by choice
        }

        if (!TryParse(gestureText, out var modifiers, out var virtualKey))
        {
            return false;
        }

        var hwnd = EnsureHwnd();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        _registered = RegisterHotKey(hwnd, _id, ModNoRepeat | modifiers, virtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, _id);
            _registered = false;
        }
    }

    private IntPtr EnsureHwnd()
    {
        if (_hwnd != IntPtr.Zero)
        {
            return _hwnd;
        }

        _hwnd = new WindowInteropHelper(_window).EnsureHandle();
        if (_hwnd == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        return _hwnd;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == _id)
        {
            handled = true;
            _window.Dispatcher.BeginInvoke(_callback);
        }
        return IntPtr.Zero;
    }

    internal static bool TryParse(string text, out int modifiers, out int virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= ModControl; break;
                case "alt": modifiers |= ModAlt; break;
                case "shift": modifiers |= ModShift; break;
                case "win": modifiers |= ModWin; break;
                default: return false;
            }
        }

        var keyName = parts[^1];
        var key = keyName.StartsWith('F') && int.TryParse(keyName.AsSpan(1), out var fNum) && fNum is >= 1 and <= 24
            ? (Key)((int)Key.F1 + fNum - 1)
            : TryKeyName(keyName);
        // A lone modifier ("Ctrl+") parses to LeftCtrl — almost always a typo.
        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return false;
        }

        virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }

    private static Key TryKeyName(string name)
    {
        // Single letters/digits parse via KeyConverter; also accept NumPad and common named keys.
        try
        {
            var converted = (Key?)new KeyConverter().ConvertFromString(name);
            return converted ?? Key.None;
        }
        catch
        {
            return Key.None;
        }
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
