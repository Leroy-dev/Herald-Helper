using System.Text;
using HeraldHelper.Infrastructure.Capture;

namespace HeraldHelper.Tests;

/// <summary>
/// Synthetic in-memory client heap: a std::map-style adapter registry
/// (Left/Parent/Right + embedded std::string name + record ptr) plus value
/// records, read through the same <see cref="DaocAdapterMapReader.Read"/>
/// delegate shape the live source uses.
/// </summary>
public sealed class DaocAdapterMapReaderTests
{
    private const long Base = 0x10000000;

    private sealed class FakeHeap
    {
        private readonly byte[] _mem = new byte[0x40000];

        public long Alloc(int size, out int offset)
        {
            offset = _next;
            _next += size;
            return Base + offset;
        }
        private int _next;

        public void Write(long addr, byte[] data) => Array.Copy(data, 0, _mem, addr - Base, data.Length);
        public void WriteU32(long addr, uint v) => Write(addr, BitConverter.GetBytes(v));
        public void WriteBytes(long addr, params byte[] data) => Write(addr, data);
        public void WriteAscii(long addr, string s) => Write(addr, Encoding.ASCII.GetBytes(s));
        public void WriteF64(long addr, double v) => Write(addr, BitConverter.GetBytes(v));

        public byte[]? Read(long addr, int size)
        {
            if (addr < Base || addr + size > Base + _mem.Length)
            {
                return null;
            }
            var buf = new byte[size];
            Array.Copy(_mem, addr - Base, buf, 0, size);
            return buf;
        }

        /// <summary>Map node: Left@0 Parent@4 Right@8 ; string name @16 ; record @40.</summary>
        public long Node(string name, long left, long parent, long right, long record)
        {
            var node = Alloc(0x40, out _);
            WriteU32(node + 0, (uint)left);
            WriteU32(node + 4, (uint)parent);
            WriteU32(node + 8, (uint)right);
            var nb = Encoding.ASCII.GetBytes(name);
            if (nb.Length <= 15)
            {
                Write(node + 16, nb);
            }
            else
            {
                var chars = Alloc(nb.Length + 1, out _);
                Write(chars, nb);
                WriteU32(node + 16, (uint)chars); // SSO union holds char* for len>15
            }
            WriteU32(node + 32, (uint)nb.Length);
            WriteU32(node + 36, (uint)Math.Max(15, nb.Length));
            WriteU32(node + 40, (uint)record);
            return node;
        }

        /// <summary>Numeric record: doubles @8/+0x10, MinValue bound @0x18.</summary>
        public long NumericRecord(double value)
        {
            var rec = Alloc(DaocAdapterMapReader.RecordReadSize, out _);
            WriteU32(rec, 0x1FD1D400); // back-link
            WriteF64(rec + 8, 1000000);
            WriteF64(rec + DaocAdapterMapReader.RecordNumericValue, value);
            WriteBytes(rec + DaocAdapterMapReader.RecordNumericBound,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0xFF);
            return rec;
        }

        /// <summary>Text record: rendered string as std::string @ +0xC.</summary>
        public long TextRecord(string text)
        {
            var rec = Alloc(DaocAdapterMapReader.RecordReadSize, out _);
            WriteU32(rec, 0x0095238C); // type tag (module VA in real records)
            var tb = Encoding.ASCII.GetBytes(text);
            if (tb.Length <= 15)
            {
                Write(rec + DaocAdapterMapReader.RecordTextBuf, tb);
            }
            else
            {
                var chars = Alloc(tb.Length + 1, out _);
                Write(chars, tb);
                WriteU32(rec + DaocAdapterMapReader.RecordTextBuf, (uint)chars);
            }
            WriteU32(rec + DaocAdapterMapReader.RecordTextSize, (uint)tb.Length);
            WriteU32(rec + DaocAdapterMapReader.RecordTextCap, (uint)Math.Max(15, tb.Length));
            return rec;
        }

        /// <summary>Head sentinel: empty name, no record, Parent → root.</summary>
        public long Head(long root)
        {
            var head = Alloc(0x40, out _);
            WriteU32(head + 0, (uint)root);
            WriteU32(head + 4, (uint)root);
            WriteU32(head + 8, (uint)root);
            return head;
        }
    }

    /// <summary>
    ///       combat_mode
    ///      /           \
    /// stats_hitpoints  stats_crush
    /// </summary>
    private static (FakeHeap heap, long anyNode) BuildMap()
    {
        var h = new FakeHeap();
        var hp = h.NumericRecord(785);
        var crush = h.TextRecord("+14%");
        var mode = h.NumericRecord(0);
        // leaf children first (their links dangle to null-ish heap ptrs — use head later)
        var left = h.Node("stats_hitpoints", 0x20000, 0x20000, 0x20000, hp);
        var right = h.Node("stats_crush", 0x20000, 0x20000, 0x20000, crush);
        var root = h.Node("combat_mode", left, 0x20000, right, mode);
        // fix children's parent links
        h.WriteU32(left + 4, (uint)root);
        h.WriteU32(right + 4, (uint)root);
        var head = h.Head(root);
        h.WriteU32(root + 4, (uint)head);      // root._Parent = head
        return (h, left);
    }

    [Fact]
    public void WalkMap_FromLeafNode_EnumeratesAllAdapters()
    {
        var (h, anyNode) = BuildMap();

        var map = DaocAdapterMapReader.WalkMap(h.Read, anyNode);

        Assert.Equal(3, map.Count);
        Assert.True(map.ContainsKey("stats_hitpoints"));
        Assert.True(map.ContainsKey("stats_crush"));
        Assert.True(map.ContainsKey("combat_mode"));
    }

    [Fact]
    public void DecodeRecord_NumericRecord_ReturnsFormattedDouble()
    {
        var (h, _) = BuildMap();
        var rec = h.NumericRecord(785);

        Assert.Equal("785", DaocAdapterMapReader.DecodeRecord(h.Read, rec));
        Assert.Equal("73.5", DaocAdapterMapReader.DecodeRecord(h.Read, h.NumericRecord(73.5)));
    }

    [Fact]
    public void DecodeRecord_TextRecord_ReturnsRenderedString()
    {
        var (h, _) = BuildMap();

        Assert.Equal("+14%", DaocAdapterMapReader.DecodeRecord(h.Read, h.TextRecord("+14%")));
        Assert.Equal("Forest Sauvage", DaocAdapterMapReader.DecodeRecord(h.Read, h.TextRecord("Forest Sauvage")));
    }

    [Fact]
    public void DecodeRecord_GarbageRecord_ReturnsNull()
    {
        var h = new FakeHeap();
        var rec = h.Alloc(DaocAdapterMapReader.RecordReadSize, out _);
        h.WriteBytes(rec, Enumerable.Repeat((byte)0xAB, 0x40).ToArray());

        Assert.Null(DaocAdapterMapReader.DecodeRecord(h.Read, rec));
    }

    [Fact]
    public void ReadNodeName_RejectsNonAsciiGarbage()
    {
        var h = new FakeHeap();
        var node = h.Alloc(0x40, out _);
        h.WriteU32(node + 32, 8);
        h.WriteU32(node + 36, 15);
        h.WriteBytes(node + 16, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08);

        Assert.Equal(string.Empty, DaocAdapterMapReader.ReadNodeName(h.Read, node));
    }

    [Fact]
    public void WalkMap_LongHeapNames_DecodesViaCharPointer()
    {
        var h = new FakeHeap();
        var rec = h.TextRecord("x");
        var leaf = h.Node("a_very_long_adapter_name_beyond_sso", 0x20000, 0x20000, 0x20000, rec);
        var head = h.Head(leaf);
        h.WriteU32(leaf + 4, (uint)head);

        var map = DaocAdapterMapReader.WalkMap(h.Read, leaf);

        Assert.True(map.ContainsKey("a_very_long_adapter_name_beyond_sso"));
    }
}
