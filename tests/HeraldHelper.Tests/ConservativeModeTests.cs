using HeraldHelper.Application.Contracts;
using HeraldHelper.Infrastructure.Capture;
using HeraldHelper.Infrastructure.Composition;

namespace HeraldHelper.Tests;

public sealed class ConservativeModeTests
{
    private static readonly IReadOnlyDictionary<string, string> MemFlags =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["chatMemReadEnabled"] = "true",
            ["statsMemReadEnabled"] = "true"
        };

    [Fact]
    public void MemoryFlags_On_ProduceMemoryChain()
    {
        var (_, _, _, _, chain) = AppComposition.Build(MemFlags, []);

        var memoryNodes = 0;
        for (IWindowAwareChatCaptureService? node = chain; node is not null;)
        {
            if (node is DaocScrollbackChatSource or DaocMemoryChatSource or DaocMemoryStatsSource)
            {
                memoryNodes++;
            }
            node = GetFallback(node);
        }
        Assert.Equal(3, memoryNodes);
    }

    [Fact]
    public void ConservativeMode_SuppressesAllMemorySources()
    {
        var map = new Dictionary<string, string>(MemFlags, StringComparer.OrdinalIgnoreCase)
        {
            ["conservativeMode"] = "true"
        };
        var (_, _, _, _, chain) = AppComposition.Build(map, []);

        // nothing in the chain may be a memory source
        for (IWindowAwareChatCaptureService? node = chain; node is not null;)
        {
            Assert.IsNotType<DaocScrollbackChatSource>(node);
            Assert.IsNotType<DaocMemoryChatSource>(node);
            Assert.IsNotType<DaocMemoryStatsSource>(node);
            node = GetFallback(node);
        }
        Assert.IsType<ScreenCaptureOcrService>(chain); // bare capture, no wrappers
    }

    /// <summary>Decorators keep their inner service in a private field —
    /// walk it via reflection for the assertion.</summary>
    private static IWindowAwareChatCaptureService? GetFallback(object source) =>
        source.GetType()
            .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Where(f => typeof(IWindowAwareChatCaptureService).IsAssignableFrom(f.FieldType))
            .Select(f => (IWindowAwareChatCaptureService?)f.GetValue(source))
            .FirstOrDefault();
}
