using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>
/// Realtime chat source: reads the client's CRT chat.log buffer directly from
/// process memory. The client fprintf's each line into a ~4KB _iobuf; pending
/// bytes live at _base.._ptr and hit disk only on buffer-full or /chatlog off.
/// Reading the buffer yields the unflushed tail — real-time, no input
/// injection, no file lag.
///
/// Requires HeraldHelper to run elevated (both Eden and Blackthorn deny
/// OpenProcess to lower-integrity callers). The FILE* global RVA is derived
/// from the module image on disk by <see cref="ClientChatLogFilePtrLocator"/>,
/// then validated at runtime via _iobuf shape (sane _bufsiz etc.). Note Eden
/// flushes eagerly (64KB bufsiz, disk is real-time) — memory read matters
/// most on Blackthorn where the client keeps ~4KB buffered.
/// </summary>
public sealed class DaocMemoryChatSource : IChatCaptureService, IWindowAwareChatCaptureService, IDisposable, IChatCaptureSourceTelemetry
{
    // VS2003-era CRT _iobuf, 32-bit process.
    private const int IobufPtr = 0;
    private const int IobufBase = 8;
    private const int IobufBufsiz = 24;
    private const int IobufSize = 32;

    private static readonly string[] DefaultProcessNames = ["game1127.dll", "eden.dll", "game.dll"];

    private readonly IWindowAwareChatCaptureService _fallback;
    private readonly string[] _processNames;
    private readonly int? _rvaOverride;
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly HashSet<string> _regionKeys = new(StringComparer.OrdinalIgnoreCase) { "chat" };
    private readonly object _sync = new();
    private IntPtr _process;
    private int _processId;
    private IntPtr _moduleBase;
    private int _filePtrRva;
    private int _lastPending;
    private string _pendingPartial = string.Empty;
    private bool _bound;
    private DateTime _lastProbe = DateTime.MinValue;

    public DaocMemoryChatSource(
        IWindowAwareChatCaptureService fallback,
        string? processName = null,
        int? rvaOverride = null,
        IEnumerable<string>? regionKeys = null,
        IResponseDiagnostics? diagnostics = null)
    {
        _fallback = fallback;
        _processNames = string.IsNullOrWhiteSpace(processName)
            ? DefaultProcessNames
            : [processName.Trim()];
        _rvaOverride = rvaOverride;
        if (regionKeys is not null)
        {
            foreach (var key in regionKeys.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                _regionKeys.Add(key.Trim());
            }
        }
        _diagnostics = diagnostics;
    }

    private bool _servedChat;

    // Lines memory emitted that the client will flush to disk later — the
    // chat.log tailer echoes them on the next read and the queue drops those
    // echoes, so a flush gap exposes ONLY the lines memory never saw.
    private readonly Queue<string> _awaitingFlush = new();
    private const int MaxAwaitingFlush = 400;
    private static readonly Regex LogTimestamp = new(@"^\[\d{1,2}:\d{2}:\d{2}\]\s?", RegexOptions.Compiled);
    private static readonly Regex AdapterLineEcho = new(@"^\S+ \((?:scalar|text)\): ", RegexOptions.Compiled);

    public string LastChatSource =>
        _servedChat ? "memory" : (_fallback as IChatCaptureSourceTelemetry)?.LastChatSource ?? "OCR";

    public async Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken)
    {
        var mem = ReadPendingLines();
        var tail = await PollTailerAsync(t => t.CaptureChatTextAsync(region, cancellationToken));
        if (mem is null)
        {
            // Not bound / can't read — defer to the wrapped chain.
            _servedChat = false;
            return tail ?? (_fallback is IChatCaptureService chatFallback
                ? await chatFallback.CaptureChatTextAsync(region, cancellationToken)
                : string.Empty);
        }
        _servedChat = true;
        return tail + mem;
    }

    public async Task<string> CaptureWindowTextAsync(
        OcrWatchRegion watchRegion,
        ShardType shardType,
        CancellationToken cancellationToken)
    {
        if (!_regionKeys.Contains(watchRegion.Key) && !_regionKeys.Contains(watchRegion.Label))
        {
            return await _fallback.CaptureWindowTextAsync(watchRegion, shardType, cancellationToken);
        }
        var mem = ReadPendingLines();
        var tail = await PollTailerAsync(t => t.CaptureWindowTextAsync(watchRegion, shardType, cancellationToken));
        if (mem is null)
        {
            _servedChat = false;
            return tail ?? await _fallback.CaptureWindowTextAsync(watchRegion, shardType, cancellationToken);
        }
        _servedChat = true;
        return tail + mem;
    }

    /// <summary>Null when the immediate inner isn't the chat.log tailer —
    /// keeps its file offset warm so a flush gap is recoverable, and returns
    /// only the lines memory never emitted (tailer echoes are skipped via
    /// the _awaitingFlush queue).</summary>
    private async Task<string?> PollTailerAsync(Func<ChatLogTailCaptureService, Task<string>> call)
    {
        if (_fallback is not ChatLogTailCaptureService tailer)
        {
            return null;
        }

        var tail = await call(tailer);
        if (tail.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var recovered = 0;
        foreach (var line in tail.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (_awaitingFlush.Count > 0 &&
                string.Equals(_awaitingFlush.Peek(), line, StringComparison.Ordinal))
            {
                _awaitingFlush.Dequeue();
                continue;
            }
            recovered++;
            sb.Append(line).Append('\n');
        }
        if (recovered > 0)
        {
            _diagnostics?.Log($"[MemChat] recovered {recovered} line(s) flushed to disk between polls");
        }
        return sb.ToString();
    }

    /// <summary>Null = unbound/unreadable (caller should defer to fallback);
    /// non-null = bound — possibly empty when no new lines are pending.</summary>
    private string? ReadPendingLines()
    {
        lock (_sync)
        {
            if (!EnsureBound())
            {
                return null;
            }

            var filePtr = ReadInt32(_moduleBase + _filePtrRva);
            if (filePtr == 0)
            {
                // chatlog off — nothing buffered; let the fallback cover.
                return null;
            }

            var iob = new byte[IobufSize];
            if (!Read(new IntPtr(filePtr), iob))
            {
                return null;
            }
            var ptr = BitConverter.ToInt32(iob, IobufPtr);
            var basePtr = BitConverter.ToInt32(iob, IobufBase);
            var bufsiz = BitConverter.ToInt32(iob, IobufBufsiz);
            var pending = ptr - basePtr;
            if (basePtr == 0 || !IsSaneBufsiz(bufsiz) || pending < 0 || pending > bufsiz)
            {
                return null;
            }

            // Buffer flushed between polls (ptr reset) — emit only the new tail.
            var from = pending >= _lastPending ? _lastPending : 0;
            _lastPending = pending;
            var newBytes = pending - from;
            if (newBytes <= 0)
            {
                return string.Empty;
            }

            var buf = new byte[newBytes];
            if (!Read(new IntPtr(basePtr + from), buf))
            {
                return null;
            }
            var text = _pendingPartial + Encoding.UTF8.GetString(buf);
            var lastNl = text.LastIndexOf('\n');
            if (lastNl < 0)
            {
                _pendingPartial = text;
                return string.Empty;
            }
            _pendingPartial = text[(lastNl + 1)..];
            var emitted = text[..(lastNl + 1)];
            // Record every emitted line so the tailer's post-flush echo can
            // be told apart from lines that were flushed before we saw them.
            foreach (var line in emitted.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var normalized = LogTimestamp.Replace(line.TrimEnd('\r'), string.Empty);
                if (normalized.Length == 0 || AdapterLineEcho.IsMatch(normalized))
                {
                    continue;
                }
                _awaitingFlush.Enqueue(normalized);
            }
            while (_awaitingFlush.Count > MaxAwaitingFlush)
            {
                _awaitingFlush.Dequeue();
            }
            var firstLine = emitted.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (firstLine?.Length > 60) firstLine = firstLine[..60] + "…";
            _diagnostics?.Log($"[MemChat] +{newBytes}B -> {firstLine}");
            return emitted;
        }
    }

    private bool EnsureBound()
    {
        if (_bound && IsProcessAlive())
        {
            return true;
        }
        Unbind();

        if (DateTime.UtcNow - _lastProbe < TimeSpan.FromSeconds(5))
        {
            return false;
        }
        _lastProbe = DateTime.UtcNow;
        var sawProcess = false;
        var sawOpenFail = false;

        foreach (var name in _processNames)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                using var _proc = proc;   // GetProcessesByName leaks handles otherwise
                sawProcess = true;
                var handle = OpenProcess(0x0410, false, proc.Id); // QUERY_INFORMATION | VM_READ
                if (handle == IntPtr.Zero)
                {
                    sawOpenFail = true;
                    continue;
                }
                var baseAddr = FindModuleBase(handle, name);
                if (baseAddr == IntPtr.Zero)
                {
                    CloseHandle(handle);
                    continue;
                }
                var modulePath = GetModulePath(handle, name);
                var rvas = _rvaOverride.HasValue
                    ? [_rvaOverride.Value]
                    : modulePath is null ? [] : ClientChatLogFilePtrLocator.LocateCandidates(modulePath);
                foreach (var rva in rvas)
                {
                    if (ValidateCandidate(handle, baseAddr + rva))
                    {
                        _process = handle;
                        _processId = proc.Id;
                        _moduleBase = baseAddr;
                        _filePtrRva = rva;
                        _bound = true;
                        _lastPending = 0;
                        _pendingPartial = string.Empty;
                        _diagnostics?.Log($"[MemChat] bound pid={proc.Id} module={name} base=0x{baseAddr.ToInt64():X} filePtrRva=0x{rva:X}");
                        return true;
                    }
                }
                CloseHandle(handle);
            }
        }

        _diagnostics?.Log(sawProcess
            ? sawOpenFail
                ? "[MemChat] client process found but OpenProcess denied — run HeraldHelper elevated"
                : "[MemChat] client open but no valid FILE* candidate — /chatlog on? unknown client build?"
            : "[MemChat] no client process found");
        return false;
    }

    /// <summary>Read FILE* at candidate VA and check it points at a sane _iobuf.</summary>
    private bool ValidateCandidate(IntPtr handle, IntPtr globalAddr)
    {
        var pbuf = new byte[4];
        if (!ReadProcessMemory(handle, globalAddr, pbuf, 4, out _) || BitConverter.ToInt32(pbuf) == 0)
        {
            return false; // chatlog off or wrong candidate — can't validate yet
        }
        var iob = new byte[IobufSize];
        var fp = BitConverter.ToInt32(pbuf);
        if (!ReadProcessMemory(handle, new IntPtr(fp), iob, iob.Length, out _))
        {
            return false;
        }
        var ptr = BitConverter.ToInt32(iob, IobufPtr);
        var basePtr = BitConverter.ToInt32(iob, IobufBase);
        var bufsiz = BitConverter.ToInt32(iob, IobufBufsiz);
        return basePtr != 0 && IsSaneBufsiz(bufsiz) && ptr >= basePtr && ptr - basePtr <= bufsiz;
    }

    // CRT setvbuf sizes vary per client build — Blackthorn 4KB, Eden 64KB.
    private static bool IsSaneBufsiz(int bufsiz) => bufsiz is >= 512 and <= 1 << 20;

    private int ReadInt32(IntPtr addr)
    {
        var buf = new byte[4];
        return Read(addr, buf) ? BitConverter.ToInt32(buf) : 0;
    }

    private bool Read(IntPtr addr, byte[] buf) =>
        ReadProcessMemory(_process, addr, buf, buf.Length, out _);

    private bool IsProcessAlive()
    {
        try
        {
            return _bound && !Process.GetProcessById(_processId).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void Unbind()
    {
        if (_process != IntPtr.Zero)
        {
            CloseHandle(_process);
        }
        _process = IntPtr.Zero;
        _bound = false;
        _lastPending = 0;
        _pendingPartial = string.Empty;
    }

    private static IntPtr FindModuleBase(IntPtr handle, string moduleName)
    {
        var mods = new IntPtr[512];
        if (!EnumProcessModules(handle, mods, mods.Length * IntPtr.Size, out var need))
        {
            return IntPtr.Zero;
        }
        var count = Math.Min(need / IntPtr.Size, mods.Length);
        for (var i = 0; i < count; i++)
        {
            var name = new char[260];
            if (GetModuleFileNameEx(handle, mods[i], name, name.Length) > 0 &&
                new string(name).TrimEnd('\0').EndsWith(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                return mods[i];
            }
        }
        return IntPtr.Zero;
    }

    private static string? GetModulePath(IntPtr handle, string moduleName)
    {
        var mods = new IntPtr[512];
        if (!EnumProcessModules(handle, mods, mods.Length * IntPtr.Size, out var need))
        {
            return null;
        }
        var count = Math.Min(need / IntPtr.Size, mods.Length);
        for (var i = 0; i < count; i++)
        {
            var name = new char[520];
            if (GetModuleFileNameEx(handle, mods[i], name, name.Length) > 0)
            {
                var s = new string(name).TrimEnd('\0');
                if (s.EndsWith(moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    return s;
                }
            }
        }
        return null;
    }

    public void Dispose()
    {
        Unbind();
        (_fallback as IDisposable)?.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr h);
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModules(IntPtr h, IntPtr[] mods, int sz, out int need);
    [DllImport("psapi.dll", CharSet = CharSet.Unicode)]
    private static extern int GetModuleFileNameEx(IntPtr h, IntPtr mod, char[] name, int sz);
}
