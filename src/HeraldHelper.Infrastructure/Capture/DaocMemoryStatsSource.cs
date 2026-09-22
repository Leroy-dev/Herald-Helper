using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>
/// Reads the client's live adapter values straight from process memory.
/// The adapter registry is a std::map&lt;std::string, record*&gt;; one heap scan for
/// a known adapter name finds a node, _Parent links climb to the head sentinel,
/// and an inorder walk yields every registered adapter's record address. Records
/// are then decoded per kind (numeric double / rendered text — see
/// <see cref="DaocAdapterMapReader"/>), so no per-build offsets are needed.
///
/// Requires elevation (OpenProcess VM_READ). Wraps the capture chain as a
/// passthrough and refreshes on capture polls (throttled); merges its values
/// over whatever the inner chain reports (chat.log /showadapter lines).
/// </summary>
public sealed class DaocMemoryStatsSource : IWindowAwareChatCaptureService, IChatCaptureService, IAdapterValueSource, IChatCaptureSourceTelemetry, IDisposable
{
    private static readonly string[] DefaultProcessNames = ["game1127.dll", "eden.dll", "game.dll"];
    // The stats object holds several type-partitioned maps (numeric map at
    // obj+4, text map at obj+0x10, ...). Anchors from each partition: numeric
    // names land in the numeric map, text names (resist percentages, player
    // name) in the text map. All maps found are unioned.
    private static readonly string[] AnchorNames =
        ["stats_hitpoints\0", "stats_name\0", "stats_thrust\0", "summary_target\0", "stats_spells\0"];
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(750);

    private readonly IWindowAwareChatCaptureService _inner;
    private readonly string[] _processNames;
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly object _sync = new();

    private IntPtr _process;
    private int _processId;
    private bool _bound;
    private bool _mapBound;
    private DateTime _lastProbe = DateTime.MinValue;
    private DateTime _lastPoll = DateTime.MinValue;
    private Dictionary<string, long> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private string? _bindError;
    // The client's own active-effects array: "EFFECTS\0" marker, then fixed
    // 0x5C records — name[44] at +0x04, iconId u32 around +0x44. Live-verified
    // on Eden: names + icon ids of every buff currently on the player.
    private long _effectsBase = -1;

    public DaocMemoryStatsSource(
        IWindowAwareChatCaptureService inner,
        string? processName = null,
        IResponseDiagnostics? diagnostics = null)
    {
        _inner = inner;
        _processNames = string.IsNullOrWhiteSpace(processName) ? DefaultProcessNames : [processName.Trim()];
        _diagnostics = diagnostics;
    }

    public bool MapBound { get { lock (_sync) return _mapBound; } }
    public int AdapterCount { get { lock (_sync) return _records.Count; } }
    public string? BindError { get { lock (_sync) return _bindError; } }

    public string LastChatSource =>
        (_inner as IChatCaptureSourceTelemetry)?.LastChatSource ?? "OCR";

    public IReadOnlyDictionary<string, string> LatestAdapterValues
    {
        get
        {
            lock (_sync)
            {
                var merged = (_inner as IAdapterValueSource)?.LatestAdapterValues
                             is { } inner
                    ? new Dictionary<string, string>(inner, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in _values)
                {
                    merged[kv.Key] = kv.Value;
                }
                return merged;
            }
        }
    }

    public Task<string> CaptureChatTextAsync(ScreenRegion region, CancellationToken cancellationToken)
    {
        Poll();
        return _inner is IChatCaptureService chat ? chat.CaptureChatTextAsync(region, cancellationToken)
            : Task.FromResult(string.Empty);
    }

    public Task<string> CaptureWindowTextAsync(OcrWatchRegion watchRegion, ShardType shardType, CancellationToken cancellationToken)
    {
        Poll();
        return _inner.CaptureWindowTextAsync(watchRegion, shardType, cancellationToken);
    }

    /// <summary>Refresh adapter values from process memory (throttled).</summary>
    public void Poll()
    {
        lock (_sync)
        {
            if (DateTime.UtcNow - _lastPoll < PollInterval)
            {
                return;
            }
            _lastPoll = DateTime.UtcNow;

            if (!EnsureBound())
            {
                return;
            }
            if (!_mapBound && !TryBindMap())
            {
                return;
            }

            // Read every record batched by contiguous runs — records sit in a few
            // dense arrays, so a handful of reads covers hundreds of adapters.
            var ordered = _records.Values.Distinct().OrderBy(a => a).ToArray();
            var updated = 0;
            var i = 0;
            while (i < ordered.Length)
            {
                var start = ordered[i];
                var end = start + DaocAdapterMapReader.RecordReadSize;
                var j = i + 1;
                while (j < ordered.Length && ordered[j] <= end + 0x40 && ordered[j] + DaocAdapterMapReader.RecordReadSize - start <= 0x8000)
                {
                    end = ordered[j] + DaocAdapterMapReader.RecordReadSize;
                    j++;
                }
                var buf = new byte[end - start];
                if (ReadProcessMemory(_process, new IntPtr(start), buf, buf.Length, out var got) && got > 0)
                {
                    var run = buf[..got];
                    foreach (var kv in _records)
                    {
                        if (kv.Value < start || kv.Value >= end)
                        {
                            continue;
                        }
                        var slice = (int)(kv.Value - start);
                        if (slice + DaocAdapterMapReader.RecordReadSize > run.Length)
                        {
                            continue;
                        }
                        var rec = new byte[DaocAdapterMapReader.RecordReadSize];
                        Array.Copy(run, slice, rec, 0, rec.Length);
                        var value = DaocAdapterMapReader.DecodeRecord(
                            (a, n) => a == kv.Value ? rec : ReadBytes(a, n), kv.Value);
                        if (value is not null)
                        {
                            if (!_values.TryGetValue(kv.Key, out var old) || old != value)
                            {
                                _values[kv.Key] = value;
                                updated++;
                                if (IsInteresting(kv.Key))
                                {
                                    _diagnostics?.Log($"[MemStats] {kv.Key} = {value}");
                                }
                            }
                        }
                    }
                }
                i = j;
            }
            if (updated > 0)
            {
                _diagnostics?.Log($"[MemStats] poll: {_values.Count} values ({updated} changed)");
            }

            PollEffects();
        }
    }

    private const int EffectRecordStride = 0x5C;
    private const int EffectNameOffset = 0x04;
    private const int EffectNameLength = 44;
    private const int EffectIconOffset = 0x44;
    private const int MaxEffects = 32;
    private static readonly byte[] EffectsMarker = "EFFECTS\0"u8.ToArray();

    /// <summary>Re-read the self-effects array and publish synthetic
    /// self_effectN / self_effect_iconN adapter keys.</summary>
    private void PollEffects()
    {
        if (_effectsBase < 0)
        {
            return;
        }

        var buf = ReadBytes(_effectsBase + EffectNameOffset, MaxEffects * EffectRecordStride);
        if (buf is null)
        {
            _effectsBase = -1; // list moved — re-scan next bind
            _mapBound = false;
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slot = 0;
        for (var i = 0; i < MaxEffects; i++)
        {
            var nameStart = i * EffectRecordStride;
            if (nameStart + EffectNameLength > buf.Length)
            {
                break;
            }
            var span = buf.AsSpan(nameStart, EffectNameLength);
            var nul = span.IndexOf((byte)0);
            var name = Encoding.ASCII.GetString(span[..(nul < 0 ? EffectNameLength : nul)]).Trim();
            if (name.Length < 3 || name.Any(c => c is < ' ' or > '~') || !seen.Add(name))
            {
                continue;
            }

            _values[$"self_effect{slot}"] = name;
            // Icon id sits near record+0x44 but jitters ±4 — first plausible
            // u16 in the record tail wins.
            var tail = nameStart - EffectNameOffset + 0x3E;
            for (var o = tail; o >= 0 && o + 2 <= nameStart - EffectNameOffset + EffectRecordStride; o += 2)
            {
                var iconId = BitConverter.ToUInt16(buf, o);
                if (iconId is > 0 and < 10000)
                {
                    _values[$"self_effect_icon{slot}"] = iconId.ToString();
                    break;
                }
            }
            slot++;
        }

        // Clear stale slots beyond what we just saw.
        for (var i = slot; _values.Remove($"self_effect{i}") | _values.Remove($"self_effect_icon{i}"); i++)
        {
        }
    }

    private static bool IsInteresting(string name) =>
        name.StartsWith("stats_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("summary_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("group_", StringComparison.OrdinalIgnoreCase) ||
        name is "concentration" or "combat_mode" or "compass_heading" or "bounty_points";

    private bool TryBindMap()
    {
        // Union every map discovered via the anchors — each adapter type owns
        // its own map (numeric at obj+4, text at obj+0x10, ...).
        var merged = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var mapsFound = 0;
        foreach (var anchor in AnchorNames)
        {
            var needle = System.Text.Encoding.ASCII.GetBytes(anchor);
            long addr = 0;
            var anchorDone = false;
            while (!anchorDone)
            {
                if (VirtualQueryEx(_process, new IntPtr(addr), out var mbi, Marshal.SizeOf<MemoryBasicInformation>()) == 0)
                {
                    break;
                }
                addr = mbi.BaseAddress + mbi.RegionSize;
                if (mbi.State != 0x1000 || (mbi.Protect & 0xEE) == 0 || (mbi.Protect & 0x100) != 0 ||
                    mbi.Type == 0x1000000 || mbi.RegionSize <= 0 || mbi.RegionSize > 64 << 20)
                {
                    continue;
                }
                var buf = new byte[mbi.RegionSize];
                if (!ReadProcessMemory(_process, new IntPtr(mbi.BaseAddress), buf, buf.Length, out var got))
                {
                    continue;
                }
                for (var i = 0; i + needle.Length <= got; i++)
                {
                    var eq = true;
                    for (var j = 0; j < needle.Length; j++)
                    {
                        if (buf[i + j] != needle[j]) { eq = false; break; }
                    }
                    if (!eq)
                    {
                        continue;
                    }
                    var node = mbi.BaseAddress + i - DaocAdapterMapReader.NodeNameBuf;
                    if (!DaocAdapterMapReader.IsNode(ReadBytes, node))
                    {
                        continue;
                    }
                    var map = DaocAdapterMapReader.WalkMap(ReadBytes, node);
                    if (map.Count > 5)
                    {
                        foreach (var kv in map)
                        {
                            merged.TryAdd(kv.Key, kv.Value);
                        }
                        mapsFound++;
                        anchorDone = true; // one good map per anchor is enough
                        break;
                    }
                }
            }
        }
        if (mapsFound > 0)
        {
            _records = merged;
            _mapBound = true;
            _bindError = null;
            _effectsBase = TryLocateEffectsArray();
            _diagnostics?.Log($"[MemStats] bound: {merged.Count} adapters across {mapsFound} registry map(s)" +
                              (_effectsBase >= 0 ? $", effects array @0x{_effectsBase:X}" : ", no effects array"));
            return true;
        }
        _bindError = "adapter map not found";
        return false;
    }

    /// <summary>Find the "EFFECTS\0" marker and return the array base.
    /// Validated by checking the first record's name field reads as ASCII.</summary>
    private long TryLocateEffectsArray()
    {
        long addr = 0;
        while (true)
        {
            if (VirtualQueryEx(_process, new IntPtr(addr), out var mbi, Marshal.SizeOf<MemoryBasicInformation>()) == 0)
            {
                break;
            }
            addr = mbi.BaseAddress + mbi.RegionSize;
            if (mbi.State != 0x1000 || (mbi.Protect & 0xEE) == 0 || (mbi.Protect & 0x100) != 0 ||
                mbi.Type == 0x1000000 || mbi.RegionSize <= 0 || mbi.RegionSize > 64 << 20)
            {
                continue;
            }
            var buf = new byte[mbi.RegionSize];
            if (!ReadProcessMemory(_process, new IntPtr(mbi.BaseAddress), buf, buf.Length, out var got))
            {
                continue;
            }
            for (var i = 0; i + EffectsMarker.Length <= got; i++)
            {
                var eq = true;
                for (var j = 0; j < EffectsMarker.Length; j++)
                {
                    if (buf[i + j] != EffectsMarker[j]) { eq = false; break; }
                }
                if (!eq)
                {
                    continue;
                }

                var baseAddr = mbi.BaseAddress + i + EffectsMarker.Length;
                // records start ~0x34 after the marker; first name must read
                // as printable ASCII — rejects stray "EFFECTS" literals.
                var probe = ReadBytes(baseAddr + 0x38, EffectNameLength);
                if (probe is not null)
                {
                    var nul = Array.IndexOf(probe, (byte)0);
                    var candidate = Encoding.ASCII.GetString(probe[..(nul < 0 ? EffectNameLength : nul)]).Trim();
                    if (candidate.Length >= 3 && candidate.All(c => c is >= ' ' and <= '~'))
                    {
                        return baseAddr + 0x34;
                    }
                }
            }
        }
        return -1;
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
                sawProcess = true;
                var handle = OpenProcess(0x0410, false, proc.Id);
                if (handle == IntPtr.Zero)
                {
                    sawOpenFail = true;
                    continue;
                }
                _process = handle;
                _processId = proc.Id;
                _bound = true;
                _mapBound = false;
                _diagnostics?.Log($"[MemStats] bound pid={proc.Id} module={name}");
                return true;
            }
        }
        _bindError = sawProcess
            ? sawOpenFail ? "OpenProcess denied — run elevated" : "no client process bound"
            : "no client process found";
        _diagnostics?.Log($"[MemStats] {_bindError}");
        return false;
    }

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
        _mapBound = false;
        _records = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
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
