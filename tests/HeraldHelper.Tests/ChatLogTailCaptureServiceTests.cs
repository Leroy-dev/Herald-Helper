using System.Text;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Capture;

namespace HeraldHelper.Tests;

public sealed class ChatLogTailCaptureServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _logPath;

    public ChatLogTailCaptureServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hh-chatlog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _logPath = Path.Combine(_dir, "chat.log");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Poll_MissingFile_ReturnsEmpty()
    {
        var sut = new ChatLogTailCaptureService(new StubOcrCapture(), _logPath);

        var text = await sut.CaptureChatTextAsync(new ScreenRegion(0, 0, 10, 10), CancellationToken.None);

        Assert.Equal(string.Empty, text);
        Assert.Null(sut.ResolvedPath);
    }

    [Fact]
    public async Task FirstPoll_AttachesAtEnd_ThenReadsOnlyAppendedLines()
    {
        await File.WriteAllTextAsync(_logPath, "[12:00:00] old backlog line.\n");
        var sut = new ChatLogTailCaptureService(new StubOcrCapture(), _logPath);

        Assert.Equal(string.Empty, await Poll(sut));

        await AppendAsync("[12:00:01] You hit the foe for 45.\n[12:00:02] The foe resists your spell!\n");
        var text = await Poll(sut);

        Assert.Equal("You hit the foe for 45.\nThe foe resists your spell!\n", text);
    }

    [Fact]
    public async Task Poll_HoldsPartialTrailingLine_UntilNewlineArrives()
    {
        File.WriteAllText(_logPath, string.Empty);
        var sut = new ChatLogTailCaptureService(new StubOcrCapture(), _logPath);
        await Poll(sut); // attach

        await AppendAsync("[12:00:01] You cast");
        Assert.Equal(string.Empty, await Poll(sut));

        await AppendAsync(" a spell!\n");
        Assert.Equal("You cast a spell!\n", await Poll(sut));
    }

    [Fact]
    public async Task Poll_TruncatedFile_ResetsOffset()
    {
        File.WriteAllText(_logPath, "[12:00:00] seed\n");
        var sut = new ChatLogTailCaptureService(new StubOcrCapture(), _logPath);
        await Poll(sut);
        await AppendAsync("[12:00:01] a\n");
        await Poll(sut);

        // Simulate external truncation (e.g. user clears the log).
        await File.WriteAllTextAsync(_logPath, "[13:00:00] fresh start.\n");
        var text = await Poll(sut);

        Assert.Equal("fresh start.\n", text);
    }

    [Fact]
    public async Task NonChatRegion_RoutesToOcrFallback()
    {
        var stub = new StubOcrCapture();
        var sut = new ChatLogTailCaptureService(stub, _logPath);

        var text = await sut.CaptureWindowTextAsync(
            new OcrWatchRegion("character-stats", "Stats", new ScreenRegion(0, 0, 10, 10)),
            ShardType.Eden,
            CancellationToken.None);

        Assert.Equal("ocr-text", text);
        Assert.Equal(1, stub.WindowCalls);
    }

    [Fact]
    public async Task ConfiguredRegionKey_RoutesToLogTail()
    {
        File.WriteAllText(_logPath, string.Empty);
        var stub = new StubOcrCapture();
        var sut = new ChatLogTailCaptureService(stub, _logPath, regionKeys: ["."]);
        // attach
        await sut.CaptureWindowTextAsync(
            new OcrWatchRegion("custom7", ".", new ScreenRegion(0, 0, 10, 10)),
            ShardType.Blackthorn,
            CancellationToken.None);
        await AppendAsync("[12:00:01] chat via custom window region\n");

        var text = await sut.CaptureWindowTextAsync(
            new OcrWatchRegion("custom7", ".", new ScreenRegion(0, 0, 10, 10)),
            ShardType.Blackthorn,
            CancellationToken.None);

        Assert.Equal("chat via custom window region\n", text);
        Assert.Equal(0, stub.WindowCalls);
    }

    private async Task<string> Poll(ChatLogTailCaptureService sut) =>
        await sut.CaptureWindowTextAsync(
            new OcrWatchRegion("chat", "Chat", new ScreenRegion(0, 0, 10, 10)),
            ShardType.Eden,
            CancellationToken.None);

    private async Task AppendAsync(string text)
    {
        await using var stream = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        var bytes = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes);
    }

    [Fact]
    public async Task Capture_AdapterLines_AreStashedNotParsed()
    {
        File.WriteAllText(_logPath, string.Empty);
        var stub = new StubOcrCapture();
        var sut = new ChatLogTailCaptureService(stub, _logPath);
        await Poll(sut);
        await AppendAsync(
            "[12:00:01] You hit the wolf for 42 damage.\n" +
            "[12:00:01] stats_thrust (text): \"+20%\"\n" +
            "[12:00:02] group_health0 (scalar): 73.500\n" +
            "[12:00:02] There are no scalar or text adapters named: \"zz\".\n" +
            "[12:00:03] The wolf dies!\n");

        var text = await Poll(sut);

        Assert.Equal("You hit the wolf for 42 damage.\nThe wolf dies!\n", text);
        Assert.Equal("+20%", sut.LatestAdapterValues["stats_thrust"]);
        Assert.Equal("73.500", sut.LatestAdapterValues["group_health0"]);
        Assert.DoesNotContain("zz", sut.LatestAdapterValues.Keys);
    }

    [Fact]
    public void RelayDiscovery_ReadsEndpointFromHistoryFile()
    {
        var session = Path.Combine(_dir, "ver", "sessions", "abc");
        var historyDir = Path.Combine(session, "cef-cache", "Default");
        Directory.CreateDirectory(historyDir);
        var url = "http://127.0.0.1:60126/btui/map?btuiRelayUrl=http%3A%2F%2F127.0.0.1%3A60124" +
                  "&btuiRelayCommandUrl=http%3A%2F%2F127.0.0.1%3A60125" +
                  "&btuiRelayToken=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        File.WriteAllBytes(Path.Combine(historyDir, "History"), Encoding.Latin1.GetBytes(url));

        var ep = BlackthornRelayDiscovery.TryDiscover(Path.Combine(_dir, "ver"));

        Assert.NotNull(ep);
        Assert.Equal("http://127.0.0.1:60124/events", ep!.EventsUrl.ToString());
        Assert.Equal("http://127.0.0.1:60125/commands", ep.CommandsUrl.ToString());
        Assert.Equal(64, ep.Token.Length);
    }

    [Fact]
    public void FilePtrLocator_FindsStoreRva_FromSyntheticPe()
    {
        // Minimal PE32: .text at RVA 0x1000 (raw 0x200), .data at RVA 0x3000 (raw 0x400).
        // .text holds:  push <strVA> ; call ... ; mov [0x400000+0x3100], eax
        // .rdata holds "chat.log\0" at RVA 0x4000 (raw 0x600).
        var img = new byte[0x1000];
        BitConverter.GetBytes(0x400).CopyTo(img, 0x3C);          // e_lfanew
        img[0x400] = (byte)'P'; img[0x401] = (byte)'E';
        BitConverter.GetBytes((ushort)3).CopyTo(img, 0x400 + 6); // 3 sections
        BitConverter.GetBytes((ushort)0x10B).CopyTo(img, 0x400 + 24);
        BitConverter.GetBytes(0x400000u).CopyTo(img, 0x400 + 24 + 28); // imagebase
        void Sec(int i, string name, uint va, uint vsz, uint rawPtr, uint rawSize)
        {
            var o = 0x400 + 24 + 224 + i * 40;
            Encoding.ASCII.GetBytes(name.PadRight(8, '\0')).CopyTo(img, o);
            BitConverter.GetBytes(vsz).CopyTo(img, o + 8);
            BitConverter.GetBytes(va).CopyTo(img, o + 12);
            BitConverter.GetBytes(rawSize).CopyTo(img, o + 16);
            BitConverter.GetBytes(rawPtr).CopyTo(img, o + 20);
        }
        Sec(0, ".text", 0x1000, 0x200, 0x200, 0x200);
        Sec(1, ".data", 0x3000, 0x200, 0x400, 0x200);
        Sec(2, ".rdata", 0x4000, 0x200, 0x600, 0x200);
        Encoding.ASCII.GetBytes("chat.log\0").CopyTo(img, 0x600);  // VA 0x404000
        var text = 0x200;
        img[text] = 0x68;                                        // push imm32
        BitConverter.GetBytes(0x404000u).CopyTo(img, text + 1);
        img[text + 5] = 0xE8; BitConverter.GetBytes(0).CopyTo(img, text + 6); // call rel32
        img[text + 10] = 0xA3;                                   // mov [imm32], eax
        BitConverter.GetBytes(0x403100u).CopyTo(img, text + 11); // -> RVA 0x3100 in .data

        var cands = ClientChatLogFilePtrLocator.LocateCandidates(img);

        Assert.Contains(0x3100, cands);
    }

    [Theory]
    [InlineData("F12", 0x7B)]
    [InlineData("f5", 0x74)]
    [InlineData("NumPad3", 0x63)]
    [InlineData("0x7B", 0x7B)]
    [InlineData("123", 123)]
    [InlineData("K", (int)'K')]
    [InlineData(null, 0x7B)]
    public void PumpKeyParsing_MapsNamesToVirtualKeys(string? raw, int expected)
    {
        Assert.Equal(expected, DaocChatLogPump.ParseVirtualKey(raw));
    }

    private sealed class StubOcrCapture : IWindowAwareChatCaptureService
    {
        public int WindowCalls;

        public Task<string> CaptureWindowTextAsync(
            OcrWatchRegion watchRegion,
            ShardType shardType,
            CancellationToken cancellationToken)
        {
            WindowCalls++;
            return Task.FromResult("ocr-text");
        }
    }
}
