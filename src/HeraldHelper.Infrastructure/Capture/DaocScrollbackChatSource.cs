using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>
/// Realtime chat source with no /chatlog requirement: reads the client's
/// chat text arena — the heap region(s) where the scrollback's wrapped
/// display lines live. Each poll extracts the NUL-terminated printable
/// strings and diffs against the previous snapshot; strings that are new
/// (allocated at the arena's growing edge) are the fresh chat segments.
///
/// Arena discovery is structural: a region whose string density and content
/// look like chat text (mixed sentences + chat-shaped prefixes). Lines are
/// stored window-wrapped (~40-char segments); segments at the measured wrap
/// width join with the next segment to rebuild the logical line. Multiple
/// chat windows keep their own copies — identical text emitted within a few
/// seconds is deduped.
///
/// Requires elevation (same as DaocMemoryChatSource). Needs at least one
/// chat line to have occurred to find the arena — binds lazily until then.
/// </summary>
public sealed class DaocScrollbackChatSource : IChatCaptureService, IWindowAwareChatCaptureService, IChatCaptureSourceTelemetry, IDisposable
{
    private static readonly string[] DefaultProcessNames = ["game1127.dll", "eden.dll", "game.dll"];
    private static readonly string[] ArenaHints =
        ["You ", " casts ", " was ", " says,", " killed ", "[Guild]", "[LFG]", "You say", "entered"];

    private readonly IWindowAwareChatCaptureService _fallback;
    private readonly string[] _processNames;
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly HashSet<string> _regionKeys = new(StringComparer.OrdinalIgnoreCase) { "chat" };
    private readonly object _sync = new();

    private IntPtr _process;
    private int _processId;
    private bool _bound;
    private DateTime _lastProbe = DateTime.MinValue;
    private DateTime _lastRescan = DateTime.MinValue;

    // Arena state: candidate regions + their last-seen string sets.
    private readonly List<Arena> _arenas = [];
    private int _wrapWidth;
    private readonly StringBuilder _pendingLine = new();
    private readonly Queue<(string Text, DateTime At)> _recentEmitted = new();
    private string? _bindError;

    private sealed class Arena(long baseAddr, long size)
    {
        public long Base = baseAddr;
        public long Size = size;
        public int Score;

        /// <summary>addr → last text seen there. Emit on a new address OR a
        /// changed string at a reused one — identical chat lines re-arrive as
        /// fresh allocations, so text-identity dedupe would drop repeats.</summary>
        public readonly Dictionary<long, string> Seen = new();
    }

    private readonly List<Arena> _candidates = [];

    public DaocScrollbackChatSource(
        IWindowAwareChatCaptureService fallback,
        string? processName = null,
        IEnumerable<string>? regionKeys = null,
        IResponseDiagnostics? diagnostics = null)
    {
        _fallback = fallback;
        _processNames = string.IsNullOrWhiteSpace(processName)
            ? DefaultProcessNames
            : [processName.Trim()];
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
    public string LastChatSource =>
        _servedChat ? "scrollback" : (_fallback as IChatCaptureSourceTelemetry)?.LastChatSource ?? "OCR";
    public string? BindError { get { lock (_sync) return _bindError; } }

    public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken)
    {
        var text = ReadNewLines();
        if (text is not null)
        {
            _servedChat = true;
            return Task.FromResult(text);
        }
        _servedChat = false;
        return _fallback is IChatCaptureService chatFallback
            ? chatFallback.CaptureChatTextAsync(region, cancellationToken)
            : Task.FromResult(string.Empty);
    }

    public Task<string> CaptureWindowTextAsync(OcrWatchRegion watchRegion, ShardType shardType, CancellationToken cancellationToken)
    {
        if (!_regionKeys.Contains(watchRegion.Key) && !_regionKeys.Contains(watchRegion.Label))
        {
            return _fallback.CaptureWindowTextAsync(watchRegion, shardType, cancellationToken);
        }
        var text = ReadNewLines();
        if (text is not null)
        {
            _servedChat = true;
            return Task.FromResult(text);
        }
        _servedChat = false;
        return _fallback.CaptureWindowTextAsync(watchRegion, shardType, cancellationToken);
    }

    /// <summary>Null = unbound (defer to fallback); non-null = live (possibly empty).</summary>
    private string? ReadNewLines()
    {
        lock (_sync)
        {
            if (!EnsureBound())
            {
                return null;
            }
            if (_arenas.Count == 0)
            {
                if (!TryFindArenas())
                {
                    return null;
                }
            }

            var fresh = new List<string>();
            var dead = new List<Arena>();
            foreach (var arena in _arenas)
            {
                // regions grow as the allocator commits more pages — refresh
                if (VirtualQueryEx(_process, new IntPtr(arena.Base), out var mbi,
                        Marshal.SizeOf<MemoryBasicInformation>()) != 0 &&
                    mbi.State == 0x1000 && mbi.BaseAddress == arena.Base)
                {
                    arena.Size = mbi.RegionSize;
                }
                var buf = ReadBytes(arena.Base, (int)arena.Size);
                if (buf is null)
                {
                    dead.Add(arena);
                    continue;
                }
                foreach (var (offset, s) in ExtractStrings(buf))
                {
                    var addr = arena.Base + offset;
                    if (arena.Seen.TryGetValue(addr, out var old) && old == s)
                    {
                        continue; // unchanged string — not a new line
                    }
                    arena.Seen[addr] = s;
                    fresh.Add(s);
                    _wrapWidth = Math.Max(_wrapWidth, s.Length);
                }
            }
            foreach (var d in dead)
            {
                _arenas.Remove(d);
            }

            if (fresh.Count == 0)
            {
                return string.Empty;
            }
            return EmitLines(fresh);
        }
    }

    private string EmitLines(List<string> segments)
    {
        var sb = new StringBuilder();
        foreach (var rawSeg in segments)
        {
            // segment metadata can leak printable bytes at the boundary —
            // drop leading junk before the first plausible line-start char.
            var seg = TrimToLineStart(rawSeg);
            if (seg.Length == 0)
            {
                continue;
            }
            // rejoin at word boundary: a stripped segment may need its space back
            if (_pendingLine.Length > 0 && _pendingLine[^1] != ' ' && !rawSeg.StartsWith(' '))
            {
                _pendingLine.Append(' ');
            }
            _pendingLine.Append(seg);
            if (_wrapWidth > 0 && seg.Length >= _wrapWidth)
            {
                continue; // wrapped — continuation follows
            }
            var line = _pendingLine.ToString();
            _pendingLine.Clear();
            if (line.Length < 3 || !LooksLikeChat(line) || IsDuplicate(line))
            {
                continue;
            }
            sb.Append(line).Append('\n');
            _recentEmitted.Enqueue((line, DateTime.UtcNow));
            while (_recentEmitted.Count > 16)
            {
                _recentEmitted.Dequeue();
            }
        }
        var result = sb.ToString();
        if (result.Length > 0)
        {
            var first = result.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            _diagnostics?.Log($"[Scrollback] +{segments.Count} seg, first: {(first.Length > 60 ? first[..60] + "…" : first)}");
        }
        return result;
    }

    /// <summary>A full chat line: reasonable start + a sentence ending.</summary>
    internal static bool IsCompleteLine(string s)
    {
        var t = TrimToLineStart(s);
        if (t.Length < 12 || !LooksLikeChat(t))
        {
            return false;
        }
        var last = t[^1];
        return last is '.' or '!' or '?' or '"' or ']' or ')' or '>';
    }

    internal static string TrimToLineStart(string s)
    {
        // Segment metadata leaks printable bytes at the boundary: a 1-4 char
        // junk run ending in a marker ('>', '=', 'B') before the real text.
        for (var i = 1; i < Math.Min(5, s.Length - 1); i++)
        {
            if (s[i] is '>' or '=' or 'B' &&
                (char.IsUpper(s[i + 1]) || s[i + 1] is '[' or '(' or '<' or '*'))
            {
                return s[(i + 1)..];
            }
        }
        var i2 = 0;
        while (i2 < s.Length && !(char.IsLetterOrDigit(s[i2]) || s[i2] is '[' or '<' or '(' or '*' or '#'))
        {
            i2++;
        }
        return s[i2..];
    }

    /// <summary>Loose filter — the event parser does the real matching; this
    /// drops obvious non-chat allocations (paths, ids, symbol soup).</summary>
    internal static bool LooksLikeChat(string s)
    {
        if (s.Length < 8 || !s.Contains(' '))
        {
            return false;
        }
        if (!(char.IsLetterOrDigit(s[0]) || s[0] is '[' or '(' or '*' or '<'))
        {
            return false;
        }
        var letters = s.Count(char.IsLetter);
        var vowels = s.Count(c => "aeiouAEIOU".IndexOf(c) >= 0);
        if (letters < 4 || vowels < 2 ||
            s.Count(c => char.IsLetterOrDigit(c) || c is ' ' or '[' or ']' or '(' or ')' or '\'' or '"' or ',' or '.' or '!' or '?' or ':' or ';' or '-' or '+' or '%' or '<' or '>') < s.Length - 2)
        {
            return false;
        }
        // Asset/identifier lines ("Bip01 L Finger01", "Editable Mesh") have
        // every token TitleCase; real chat always carries lowercase words.
        var hasLowerWord = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(w => w.Count(char.IsLetter) >= 3 && w.All(c => !char.IsLetter(c) || char.IsLower(c)));
        return hasLowerWord;
    }

    /// <summary>Drop per-window copies (same text lands within ~1 poll) and
    /// wrapped fragments whose full line was already emitted. Kept narrow —
    /// legit repeats (same cast twice) arrive seconds apart and must flow.</summary>
    private bool IsDuplicate(string line) =>
        _recentEmitted.Any(x =>
            DateTime.UtcNow - x.At < TimeSpan.FromSeconds(1.5) &&
            (x.Text == line || (line.Length < 55 && x.Text.Contains(line, StringComparison.Ordinal))));

    /// <summary>NUL-terminated printable ASCII strings ≥4 chars, with their
    /// byte offset inside the buffer (offset = identity across polls).</summary>
    internal static IEnumerable<(int Offset, string Text)> ExtractStrings(byte[] buf)
    {
        var start = -1;
        for (var i = 0; i <= buf.Length; i++)
        {
            var printable = i < buf.Length && buf[i] is >= 32 and < 127;
            if (printable && start < 0)
            {
                start = i;
            }
            else if (!printable && start >= 0)
            {
                if (i - start >= 4)
                {
                    yield return (start, Encoding.ASCII.GetString(buf, start, i - start));
                }
                start = -1;
            }
        }
    }

    /// <summary>
    /// Find regions that hold chat scrollback text: extract strings and count
    /// chat-shaped hints (sentence words + channel brackets). A region needs
    /// a few hits to qualify — binds lazily once any chat exists.
    /// </summary>
    private bool TryFindArenas()
    {
        if (DateTime.UtcNow - _lastRescan < TimeSpan.FromSeconds(10))
        {
            return false;
        }
        _lastRescan = DateTime.UtcNow;

        foreach (var (rb, rs) in ReadableRegions())
        {
            if (rs > 4 << 20)
            {
                continue; // arenas are modest
            }
            var buf = ReadBytes(rb, (int)rs);
            if (buf is null)
            {
                continue;
            }
            var strings = ExtractStrings(buf).Select(x => x.Text).ToArray();
            var hintHits = strings.Count(s => ArenaHints.Any(h => s.Contains(h, StringComparison.Ordinal)));
            // The message-queue arena holds complete lines (sentence endings);
            // wrapped scrollback regions hold ~40-char fragments. Rank by how
            // many strings look like finished chat lines.
            var completeLines = strings.Count(IsCompleteLine);
            if (hintHits < 10 || strings.Length < 30)
            {
                continue;
            }
            var arena = new Arena(rb, rs) { Score = completeLines * 4 + hintHits };
            foreach (var (offset, s) in ExtractStrings(buf))
            {
                arena.Seen[rb + offset] = s;
            }
            _candidates.Add(arena);
        }
        // keep only the top arena(s) — per-window copies cause dupes otherwise
        _arenas.AddRange(_candidates.OrderByDescending(a => a.Score).Take(2));
        _candidates.Clear();
        if (_arenas.Count > 0)
        {
            _bindError = null;
            _diagnostics?.Log($"[Scrollback] arenas bound: {_arenas.Count} region(s), scores=[{string.Join(",", _arenas.Select(a => a.Score))}]");
            return true;
        }
        _bindError = "no chat text found yet (say something in game)";
        return false;
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
        var saw = false;
        foreach (var name in _processNames)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                saw = true;
                var handle = OpenProcess(0x0410, false, proc.Id);
                if (handle == IntPtr.Zero)
                {
                    _bindError = "OpenProcess denied — run elevated";
                    continue;
                }
                _process = handle;
                _processId = proc.Id;
                _bound = true;
                _diagnostics?.Log($"[Scrollback] bound pid={proc.Id} module={name}");
                return true;
            }
        }
        _bindError = saw ? "client open denied" : "no client process";
        return false;
    }

    private IEnumerable<(long Base, long Size)> ReadableRegions()
    {
        long addr = 0;
        while (VirtualQueryEx(_process, new IntPtr(addr), out var mbi, Marshal.SizeOf<MemoryBasicInformation>()) != 0)
        {
            addr = mbi.BaseAddress + mbi.RegionSize;
            if (mbi.State == 0x1000 && (mbi.Protect & 0xEE) != 0 && (mbi.Protect & 0x100) == 0 &&
                mbi.Type != 0x1000000 && mbi.RegionSize is > 0 and < 4 << 20)
            {
                yield return (mbi.BaseAddress, mbi.RegionSize);
            }
        }
    }

    private static bool IsHeapPtr(uint v) => v is >= 0x10000 and < 0x7FFF0000;

    private byte[]? ReadBytes(long addr, int size)
    {
        var buf = new byte[size];
        return ReadProcessMemory(_process, new IntPtr(addr), buf, size, out var read) && read > 0
            ? buf[..read]
            : null;
    }

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
        _arenas.Clear();
        _pendingLine.Clear();
    }

    public void Dispose() => Unbind();

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public long BaseAddress, AllocationBase;
        public int AllocationProtect;
        public long RegionSize;
        public int State, Protect, Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")]
    private static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MemoryBasicInformation mbi, int len);
}
