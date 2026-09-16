using System.Text;

namespace HeraldHelper.Infrastructure.Capture;

/// <summary>
/// Derives the RVA of the chat.log FILE* global inside a DAoC client module
/// (game1127.dll, game.dll, ...) without hardcoded addresses.
///
/// The &amp;chatlog handler does: strncpy(buf,"chat.log",..) ; fopen(buf,mode) ;
/// mov [global], eax. So: find the "chat.log\0" string, find the `push &lt;strVA&gt;`
/// reference, then the first `mov [imm32], eax` (A3 or 89 05) targeting .data is
/// the FILE* store. Callers should treat results as ordered candidates and
/// validate at runtime (a wrong hit yields an invalid _iobuf).
/// </summary>
public static class ClientChatLogFilePtrLocator
{
    private sealed record Section(string Name, uint Va, uint VSize, uint RawPtr, uint RawSize);

    /// <summary>Return ordered candidate RVAs for the chat.log FILE* global.</summary>
    public static IReadOnlyList<int> LocateCandidates(byte[] image)
    {
        if (image.Length < 0x400 || !TryParsePe(image, out var imageBase, out var sections))
        {
            return [];
        }

        var text = sections.FirstOrDefault(s => s.Name == ".text");
        var dataRanges = sections.Where(s => s.Name is ".data" or ".rdata").ToArray();
        if (text is null || dataRanges.Length == 0)
        {
            return [];
        }

        var candidates = new List<int>();
        foreach (var stringVa in FindStringVas(image, sections, imageBase, "chat.log\0"u8.ToArray()))
        {
            foreach (var pushOffset in FindPushRefs(image, text, imageBase, stringVa))
            {
                foreach (var candidate in ScanStoreForward(image, text, imageBase, dataRanges, pushOffset))
                {
                    if (!candidates.Contains(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }
        }
        return candidates;
    }

    /// <summary>Convenience: locate candidates directly from a module file on disk.</summary>
    public static IReadOnlyList<int> LocateCandidates(string modulePath) =>
        LocateCandidates(File.ReadAllBytes(modulePath));

    private static bool TryParsePe(byte[] d, out uint imageBase, out Section[] sections)
    {
        imageBase = 0;
        sections = [];
        var pe = BitConverter.ToInt32(d, 0x3C);
        if (pe <= 0 || pe + 24 > d.Length || d[pe] != 'P' || d[pe + 1] != 'E')
        {
            return false;
        }
        var nsec = BitConverter.ToUInt16(d, pe + 6);
        var opt = pe + 24;
        if (BitConverter.ToUInt16(d, opt) != 0x10B)
        {
            return false; // PE32 only (these clients are 32-bit)
        }
        imageBase = BitConverter.ToUInt32(d, opt + 28);
        var soff = opt + 224;
        var list = new List<Section>();
        for (var i = 0; i < nsec && soff + i * 40 + 40 <= d.Length; i++)
        {
            var off = soff + i * 40;
            var name = Encoding.ASCII.GetString(d, off, 8).TrimEnd('\0');
            var vsz = BitConverter.ToUInt32(d, off + 8);
            var va = BitConverter.ToUInt32(d, off + 12);
            var rawSize = BitConverter.ToUInt32(d, off + 16);
            var rawPtr = BitConverter.ToUInt32(d, off + 20);
            list.Add(new Section(name, va, vsz, rawPtr, rawSize));
        }
        sections = list.ToArray();
        return sections.Length > 0;
    }

    private static IEnumerable<uint> FindStringVas(byte[] d, Section[] secs, uint imageBase, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= d.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (d[i + j] != needle[j]) { match = false; break; }
            }
            if (!match)
            {
                continue;
            }
            foreach (var s in secs)
            {
                if (s.RawPtr <= i && i < s.RawPtr + s.RawSize)
                {
                    yield return imageBase + s.Va + (uint)(i - s.RawPtr);
                    break;
                }
            }
        }
    }

    private static IEnumerable<uint> FindPushRefs(byte[] d, Section text, uint imageBase, uint stringVa)
    {
        var needle = BitConverter.GetBytes(stringVa);
        var start = (int)text.RawPtr;
        var end = Math.Min(d.Length, start + (int)text.RawSize) - 5;
        for (var i = start; i < end; i++)
        {
            if (d[i] == 0x68 &&
                d[i + 1] == needle[0] && d[i + 2] == needle[1] &&
                d[i + 3] == needle[2] && d[i + 4] == needle[3])
            {
                yield return imageBase + text.Va + (uint)(i - text.RawPtr); // VA of the push insn
            }
        }
    }

    private static IEnumerable<int> ScanStoreForward(
        byte[] d, Section text, uint imageBase, Section[] dataRanges, uint pushVa)
    {
        // From the push instruction, scan forward looking for the first
        // 'mov [imm32], eax' (A3) or 'mov [imm32], eax' (89 05) where imm32
        // lands inside a .data/.rdata section — the fopen result store.
        var startOff = (int)(text.RawPtr + (pushVa - imageBase - text.Va)) + 5;
        var endOff = Math.Min(d.Length, startOff + 400);
        for (var i = startOff; i + 5 <= endOff; i++)
        {
            uint immOffset;
            if (d[i] == 0xA3)
            {
                immOffset = (uint)(i + 1);
            }
            else if (d[i] == 0x89 && d[i + 1] == 0x05)
            {
                immOffset = (uint)(i + 2);
            }
            else
            {
                continue;
            }
            var target = BitConverter.ToUInt32(d, (int)immOffset);
            var rva = target - imageBase;
            foreach (var s in dataRanges)
            {
                if (s.Va <= rva && rva < s.Va + s.VSize)
                {
                    yield return (int)rva;
                    break;
                }
            }
        }
    }
}
