using System.Globalization;
using System.Text;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>
/// Walks the client's adapter registry (a VS2003-era std::map&lt;std::string, record*&gt;
/// embedded in the stats/UI adapter object) purely through memory reads, and
/// decodes each adapter's live value record.
///
/// Node layout (MSVC 2003 _Tree_node): _Left@0, _Parent@4, _Right@8,
/// color/isnil bytes @12..15, _Myval@+16 = { std::string name (16-byte SSO
/// buffer, size@+16, cap@+20 relative to the string), record ptr } — so the
/// record pointer sits at node+40.
///
/// Record kinds observed on Eden (auto-classified per record, no per-build
/// offsets needed):
///   numeric  (0x20 bytes): doubles at +8/+0x10/+0x18; the +0x18 bound is
///             double.MinValue when unset — that 8-byte sentinel is the
///             signature. Live value = double @ +0x10.
///   text     (0x24 bytes): std::string @ +0xC (SSO buffer 16B, size@+0x1C,
///             cap@+0x20; cap &gt; 0xF means +0xC holds a heap char* instead).
///             Live value = the rendered text the client itself displays
///             (e.g. "+14%" for resists).
/// </summary>
public static class DaocAdapterMapReader
{
    public const int NodeLeft = 0;
    public const int NodeParent = 4;
    public const int NodeRight = 8;
    public const int NodeNameBuf = 16;
    public const int NodeNameSize = 32;
    public const int NodeNameCap = 36;
    public const int NodeRecord = 40;

    public const int RecordNumericValue = 0x10;
    public const int RecordNumericBound = 0x18;
    public const int RecordTextBuf = 0x0C;
    public const int RecordTextSize = 0x1C;
    public const int RecordTextCap = 0x20;
    public const int RecordReadSize = 0x40;

    /// <summary>Read <paramref name="size"/> bytes at <paramref name="addr"/>; null on failure.</summary>
    public delegate byte[]? Read(long addr, int size);

    private static readonly byte[] NumericBoundSentinel =
        [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0xFF]; // double.MinValue LE

    public static bool IsHeapPointer(uint p) => p is >= 0x10000 and < 0x7FFF0000;

    /// <summary>Decode the node's std::string name (SSO inline or heap pointer).</summary>
    public static string ReadNodeName(Read read, long node)
    {
        var hdr = read(node + NodeNameBuf, 24);
        if (hdr is null || hdr.Length < 24)
        {
            return string.Empty;
        }
        var size = BitConverter.ToInt32(hdr, 16);
        var cap = BitConverter.ToInt32(hdr, 20);
        if (size <= 0 || cap <= 0 || size > cap || cap > 0x4000)
        {
            return string.Empty;
        }
        byte[] chars;
        if (cap <= 15)
        {
            chars = hdr[..size];
        }
        else
        {
            var ptr = BitConverter.ToUInt32(hdr, 0);
            if (!IsHeapPointer(ptr))
            {
                return string.Empty;
            }
            chars = read(ptr, size) ?? [];
        }
        if (chars.Length < size || chars.Any(b => b < 0x20 || b > 0x7E))
        {
            return string.Empty;
        }
        return Encoding.ASCII.GetString(chars, 0, size);
    }

    /// <summary>Structural check: plausible map node (heap links + name + record).</summary>
    public static bool IsNode(Read read, long node)
    {
        if (node < 0x10000)
        {
            return false;
        }
        var hdr = read(node, NodeRecord + 4);
        if (hdr is null || hdr.Length < NodeRecord + 4)
        {
            return false;
        }
        return IsHeapPointer(BitConverter.ToUInt32(hdr, NodeLeft)) &&
               IsHeapPointer(BitConverter.ToUInt32(hdr, NodeParent)) &&
               IsHeapPointer(BitConverter.ToUInt32(hdr, NodeRight)) &&
               IsHeapPointer(BitConverter.ToUInt32(hdr, NodeRecord)) &&
               ReadNodeName(read, node).Length > 0;
    }

    /// <summary>
    /// From any valid node: climb _Parent to the head sentinel (its _Myval has
    /// an empty name), then take head._Parent = root and walk the tree.
    /// Returns name → record-address pairs.
    /// </summary>
    public static Dictionary<string, long> WalkMap(Read read, long anyNode, int maxNodes = 0x10000)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var head = 0L;
        var cur = anyNode;
        var seen = new HashSet<long>();
        for (var i = 0; i < 256; i++)
        {
            var pb = read(cur + NodeParent, 4);
            if (pb is null || pb.Length < 4)
            {
                break;
            }
            var parent = BitConverter.ToUInt32(pb);
            if (!IsHeapPointer(parent) || !seen.Add(parent))
            {
                head = cur;
                break;
            }
            if (ReadNodeName(read, parent).Length == 0)
            {
                head = parent;
                break;
            }
            cur = parent;
        }
        if (head == 0)
        {
            return result;
        }
        var rootb = read(head + NodeParent, 4);
        if (rootb is null || rootb.Length < 4)
        {
            return result;
        }
        var root = BitConverter.ToUInt32(rootb);
        Walk(read, root, new HashSet<long>(), result, maxNodes);
        return result;
    }

    private static void Walk(Read read, long node, HashSet<long> seen, Dictionary<string, long> outp, int maxNodes)
    {
        if (seen.Count >= maxNodes || !seen.Add(node) || !IsNode(read, node))
        {
            return;
        }
        var name = ReadNodeName(read, node);
        var rec = BitConverter.ToUInt32(read(node + NodeRecord, 4) ?? new byte[4]);
        if (name.Length > 0 && IsHeapPointer(rec))
        {
            outp.TryAdd(name, rec);
        }
        Walk(read, BitConverter.ToUInt32(read(node + NodeLeft, 4) ?? new byte[4]), seen, outp, maxNodes);
        Walk(read, BitConverter.ToUInt32(read(node + NodeRight, 4) ?? new byte[4]), seen, outp, maxNodes);
    }

    /// <summary>
    /// Decode one adapter record into its display value.
    /// Numeric → invariant int/short-float text ("785", "73.5").
    /// Text → the rendered string verbatim ("+14%", "Forest Sauvage").
    /// Null when the record shape is unrecognized.
    /// </summary>
    public static string? DecodeRecord(Read read, long record)
    {
        var rec = read(record, RecordReadSize);
        if (rec is null || rec.Length < RecordReadSize)
        {
            return null;
        }

        // numeric record: +0x18 bound is double.MinValue when unset
        if (rec.AsSpan(RecordNumericBound, 8).SequenceEqual(NumericBoundSentinel))
        {
            var value = BitConverter.ToDouble(rec, RecordNumericValue);
            if (double.IsFinite(value) && Math.Abs(value) < 1e15)
            {
                return FormatNumber(value);
            }
        }

        // text record: std::string at +0xC (SSO cap 15 or heap char*)
        var size = BitConverter.ToInt32(rec, RecordTextSize);
        var cap = BitConverter.ToInt32(rec, RecordTextCap);
        if (size >= 0 && size <= cap && cap is >= 15 and <= 0x4000)
        {
            byte[] chars;
            if (cap == 15)
            {
                chars = rec[RecordTextBuf..(RecordTextBuf + Math.Min(size, 15))];
            }
            else
            {
                var ptr = BitConverter.ToUInt32(rec, RecordTextBuf);
                chars = IsHeapPointer(ptr) ? read(ptr, size) ?? [] : [];
            }
            if (chars.Length > 0)
            {
                var text = Encoding.ASCII.GetString(chars).Split('\0')[0];
                if (text.Length > 0 && text.All(c => c >= 0x20 && c < 0x7F))
                {
                    return text;
                }
            }
        }
        return null;
    }

    private static string FormatNumber(double value)
    {
        var rounded = Math.Round(value);
        return Math.Abs(value - rounded) < 0.0005
            ? ((long)rounded).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
