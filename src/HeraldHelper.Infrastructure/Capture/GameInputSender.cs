using System.Diagnostics;
using System.Runtime.InteropServices;
using HeraldHelper.Application.Contracts;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>Send synthetic keypresses to the DAoC client via SendInput.</summary>
public interface IGameInputSender
{
    /// <summary>Send the keys in order while the game window is foreground.
    /// Returns false when no game window is found or it isn't focused.</summary>
    bool SendSequence(IReadOnlyList<int> virtualKeys);
}

/// <summary>
/// Overlay click-to-act support: the group overlay captures clicks without
/// stealing focus (WS_EX_NOACTIVATE), so the game stays foreground and these
/// keystrokes land as if typed — no focus swap, no flicker.
/// </summary>
public sealed class GameInputSender : IGameInputSender
{
    private readonly string _processName;
    private readonly IResponseDiagnostics? _diagnostics;
    private IntPtr _gameWindow;
    private DateTime _lastResolve = DateTime.MinValue;

    public GameInputSender(string? processName = null, IResponseDiagnostics? diagnostics = null)
    {
        _processName = string.IsNullOrWhiteSpace(processName) ? "game1127" : processName.Trim();
        _diagnostics = diagnostics;
    }

    public bool SendSequence(IReadOnlyList<int> virtualKeys)
    {
        if (virtualKeys.Count == 0)
        {
            return false;
        }

        ResolveGame();
        if (_gameWindow == IntPtr.Zero)
        {
            _diagnostics?.Log("[Input] no game window to send keys to");
            return false;
        }

        if (GetForegroundWindow() != _gameWindow)
        {
            _diagnostics?.Log("[Input] game window not foreground — keys skipped");
            return false;
        }

        var inputs = new INPUT[virtualKeys.Count * 2];
        for (var i = 0; i < virtualKeys.Count; i++)
        {
            inputs[i * 2] = KeyInput(virtualKeys[i], keyUp: false);
            inputs[i * 2 + 1] = KeyInput(virtualKeys[i], keyUp: true);
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            _diagnostics?.Log($"[Input] SendInput partial: {sent}/{inputs.Length} err={Marshal.GetLastWin32Error()}");
            return false;
        }
        return true;
    }

    private void ResolveGame()
    {
        if (_gameWindow != IntPtr.Zero && DateTime.UtcNow - _lastResolve < TimeSpan.FromSeconds(5))
        {
            return;
        }
        _lastResolve = DateTime.UtcNow;
        _gameWindow = IntPtr.Zero;

        var pidSet = new HashSet<int>();
        foreach (var name in new[] { _processName, "game", "connect" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                using (p) { pidSet.Add(p.Id); }
            }
        }
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                if (p.ProcessName.StartsWith("game1127", StringComparison.OrdinalIgnoreCase))
                {
                    pidSet.Add(p.Id);
                }
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
            _diagnostics?.Log($"[Input] bound to game window hwnd=0x{found.ToInt64():X}");
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
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0u
            }
        }
    };

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
