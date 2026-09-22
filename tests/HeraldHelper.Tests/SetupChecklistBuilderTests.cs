using HeraldHelper.Application.Services;

namespace HeraldHelper.Tests;

public sealed class SetupChecklistBuilderTests
{
    private static SetupChecklistInput Input(
        bool conservative = false,
        bool chatMem = true,
        bool statsMem = true,
        bool elevated = true,
        bool chatRegion = true,
        bool watchRegions = true,
        bool chatLog = false,
        bool relay = false,
        bool uiFolder = true,
        bool catalogs = true,
        bool character = true,
        bool ocr = true,
        int overlayElements = 5) => new(
        conservative, chatMem, statsMem, elevated, chatRegion, watchRegions,
        chatLog, relay, uiFolder, catalogs, character, ocr, overlayElements);

    private static SetupCheckItem Item(IReadOnlyList<SetupCheckItem> items, string name) =>
        items.Single(x => x.Name == name);

    [Fact]
    public void FullyConfigured_AllOk()
    {
        var items = SetupChecklistBuilder.Build(Input());
        Assert.All(items, x => Assert.True(x.Ok, x.Name));
    }

    [Fact]
    public void ConservativeMode_SkipsElevationAndMemChecks()
    {
        var items = SetupChecklistBuilder.Build(Input(
            conservative: true, chatMem: true, statsMem: true, elevated: false));

        Assert.DoesNotContain(items, x => x.Name == "Elevation");
        Assert.DoesNotContain(items, x => x.Name == "Memory chat source");
        Assert.Single(items, x => x.Name.Contains("conservative", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NotElevated_MemoryChecksFail()
    {
        var items = SetupChecklistBuilder.Build(Input(elevated: false));
        Assert.False(Item(items, "Elevation").Ok);
    }

    [Fact]
    public void NoChatSource_Fails()
    {
        var items = SetupChecklistBuilder.Build(Input(
            chatRegion: false, chatMem: false, chatLog: false, relay: false));
        Assert.False(Item(items, "Chat source").Ok);
    }

    [Fact]
    public void RelayAlone_CountsAsChatSource()
    {
        var items = SetupChecklistBuilder.Build(Input(
            chatRegion: false, chatMem: false, relay: true));
        Assert.True(Item(items, "Chat source").Ok);
    }

    [Fact]
    public void NoCharacter_NoCatalogs_NoUiFolder_AllFail()
    {
        var items = SetupChecklistBuilder.Build(Input(
            character: false, catalogs: false, uiFolder: false));
        Assert.False(Item(items, "Character selected").Ok);
        Assert.False(Item(items, "Ability catalogs").Ok);
        Assert.False(Item(items, "Custom UI package").Ok);
    }
}
