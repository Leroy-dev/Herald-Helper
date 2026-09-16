using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HeraldHelper.Application.Contracts;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>
/// Experimental flush pump for chat.log tailing. The DAoC client buffers ~4KB of
/// chat before the CRT flushes to disk; pressing a client-bound key whose macro
/// prints local text (e.g. /showadapter) pushes pending lines past the boundary.
/// Sends synthetic keystrokes via SendInput — only while the game window is
/// foreground, since DirectInput ignores background input anyway.
/// </summary>
public sealed class DaocChatLogPump
{
    private readonly int[] _virtualKeys;
    private readonly int _pressesPerPoll;
    private readonly string _processName;
    private readonly IResponseDiagnostics? _diagnostics;
    private int? _gamePid;
    private IntPtr _gameWindow;
    private DateTime _lastResolve = DateTime.MinValue;
    private int _keyCursor;

    public DaocChatLogPump(
        IReadOnlyList<int> virtualKeys,
        int pressesPerPoll = 2,
        string? processName = null,
        IResponseDiagnostics? diagnostics = null)
    {
        _virtualKeys = virtualKeys is { Count: > 0 } ? [.. virtualKeys] : [0x7B];
        _pressesPerPoll = Math.Clamp(pressesPerPoll, 1, 10);
        _processName = string.IsNullOrWhiteSpace(processName) ? "game1127" : processName.Trim();
        _diagnostics = diagnostics;
    }

    /// <summary>Send pump keystrokes if the game window is currently foreground.</summary>
    public void Pump()
    {
        ResolveGame();
        if (_gameWindow == IntPtr.Zero)
        {
            return;
        }

        if (GetForegroundWindow() != _gameWindow)
        {
            return;
        }

        var inputs = new INPUT[_pressesPerPoll * 2];
        for (var i = 0; i < _pressesPerPoll; i++)
        {
            var vk = _virtualKeys[_keyCursor];
            _keyCursor = (_keyCursor + 1) % _virtualKeys.Length;
            inputs[i * 2] = KeyInput(vk, keyUp: false);
            inputs[i * 2 + 1] = KeyInput(vk, keyUp: true);
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            _diagnostics?.Log($"[ChatLog] pump SendInput partial: {sent}/{inputs.Length} err={Marshal.GetLastWin32Error()}");
        }
    }

    private void ResolveGame()
    {
        // Re-resolve at most once every 5s — process may restart.
        if (_gameWindow != IntPtr.Zero && DateTime.UtcNow - _lastResolve < TimeSpan.FromSeconds(5))
        {
            return;
        }
        _lastResolve = DateTime.UtcNow;

        _gamePid = null;
        _gameWindow = IntPtr.Zero;
        var pidSet = new HashSet<int>();
        foreach (var name in new[] { _processName, "game", "connect" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                pidSet.Add(p.Id);
            }
        }
        // The Blackthorn client runs as "game1127.dll" (name includes extension).
        foreach (var p in Process.GetProcesses())
        {
            if (p.ProcessName.StartsWith("game1127", StringComparison.OrdinalIgnoreCase))
            {
                pidSet.Add(p.Id);
            }
        }

        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out int wpid);
            if (pidSet.Contains(wpid) && IsWindowVisible(hwnd))
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (found != IntPtr.Zero)
        {
            _gameWindow = found;
            GetWindowThreadProcessId(found, out int gpid);
            _gamePid = gpid;
            _diagnostics?.Log($"[ChatLog] pump bound to game window pid={gpid} hwnd=0x{found.ToInt64():X}");
        }
    }

    private static INPUT KeyInput(int vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = 0,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0u,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        }
    };

    /// <summary>Parse a comma/semicolon-separated key list (e.g. "F4,F5,F6").</summary>
    public static int[] ParseVirtualKeyList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [ParseVirtualKey(null)];
        }
        return raw.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(ParseVirtualKey)
                  .ToArray();
    }

    /// <summary>Parse a pump key name: F1-F24, NumPad0-9, or a single char.</summary>
    public static int ParseVirtualKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0x7B; // F12
        }
        raw = raw.Trim();
        if (raw.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(raw[1..], out var fn) && fn is >= 1 and <= 24)
        {
            return 0x6F + fn; // VK_F1 = 0x70
        }
        if (raw.StartsWith("numpad", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(raw[6..], out var num) && num is >= 0 and <= 9)
        {
            return 0x60 + num;
        }
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(raw[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex))
        {
            return hex;
        }
        if (int.TryParse(raw, out var dec))
        {
            return dec;
        }
        return char.ToUpperInvariant(raw[0]); // VK for A-Z/0-9 is the ASCII code
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hwnd, out int pid);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
