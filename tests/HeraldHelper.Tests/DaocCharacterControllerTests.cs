using HeraldHelper.Desktop;
using HeraldHelper.Desktop.Controllers;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Tests;

public sealed class DaocCharacterControllerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppDataStore _store;

    public DaocCharacterControllerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hh_test_{Guid.NewGuid()}.db");
        _store = new AppDataStore(_dbPath);
        _store.Initialize();
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_dbPath);
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public void SaveOcrWindows_ShortCharacterName_DoesNotDuplicateKey()
    {
        // "Min" normalizes to the same string as the legacy key, which used to cause
        // a SQLite UNIQUE constraint failure because two updates had the same key.
        var controller = new DaocCharacterController(_store);
        var windows = new[]
        {
            new DaocWindowDefinition("Chat", "Chat", new ScreenRegion(10, 10, 100, 100))
        };

        var ex = Record.Exception(() => controller.SaveOcrWindows(ShardType.Blackthorn, "Min", windows));

        Assert.Null(ex);
        var loaded = controller.LoadOcrWindows(ShardType.Blackthorn, "Min");
        Assert.Single(loaded);
        Assert.Equal("Chat", loaded[0].Key);
    }

    [Fact]
    public void SaveOcrWindows_KeepsExistingSettings()
    {
        _store.SaveSettings([
            new ConfigEntry { Key = "some.other.setting", Value = "keep" }
        ]);

        var controller = new DaocCharacterController(_store);
        controller.SaveOcrWindows(ShardType.Blackthorn, "Min", [
            new DaocWindowDefinition("Chat", "Chat", new ScreenRegion(10, 10, 100, 100))
        ]);

        var map = _store.LoadSettingsMap();
        Assert.True(map.ContainsKey("some.other.setting"));
        Assert.Equal("keep", map["some.other.setting"]);
        Assert.True(map.ContainsKey("daoc.ocr.windows.blackthorn.min"));
    }
}
