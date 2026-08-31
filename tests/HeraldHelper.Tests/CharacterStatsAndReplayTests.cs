using System.Text.Json;
using HeraldHelper.Application.Models;
using HeraldHelper.Application.Services;
using HeraldHelper.Desktop;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Ocr;
using HeraldHelper.Infrastructure.Configuration;
using System.Windows.Media.Imaging;

namespace HeraldHelper.Tests;

public sealed class CharacterStatsAndReplayTests
{
    [Fact]
    public void CharacterStatsParser_ReadsCompactStatusWindow()
    {
        const string text = "Str: 150 Thr: +29% Con: 125 Cru: +26% Dex: 188 Sla: +28% " +
                            "Qui: 135 Hea: +31% Int: 195 Col: +26% Cha: 190 Mat: +26% " +
                            "Pie: 135 Ene: +26% Emp: 135 Spi: +26%";

        var result = CharacterStatsParser.Parse(text, ShardType.Eden, "Rooy", null, DateTimeOffset.UtcNow);

        Assert.NotNull(result);
        Assert.Equal(188, result!.Dexterity);
        Assert.Equal(195, result.Intelligence);
        Assert.Equal(190, result.Charisma);
        Assert.Equal(135, result.Piety);
    }

    [Fact]
    public void CharacterStatsParser_ToleratesGermanOcrSubstitutionsAndExtraSeparators()
    {
        const string text = "Str: 150 COH;h125 Dez: 188 Qui: 135 lnt: 195 Cha; 190 Pie: 135 Emp: 135";

        var result = CharacterStatsParser.Parse(text, ShardType.Blackthorn, "Min", null, DateTimeOffset.UtcNow);

        Assert.NotNull(result);
        Assert.Equal(125, result!.Constitution);
        Assert.Equal(188, result.Dexterity);
        Assert.Equal(195, result.Intelligence);
    }

    [Fact]
    public void CharacterStatsParser_ReadsShortCustomWindowLabels()
    {
        const string text = "ST: 150 CO: 125 DE: 188 QU: 135 IN: 195 CH: 190 PI: 135 EM: 135";

        var result = CharacterStatsParser.Parse(text, ShardType.Blackthorn, "Min", null, DateTimeOffset.UtcNow);

        Assert.NotNull(result);
        Assert.Equal(150, result!.Strength);
        Assert.Equal(125, result.Constitution);
        Assert.Equal(188, result.Dexterity);
        Assert.Equal(195, result.Intelligence);
    }

    [Fact]
    public void CastMetricsCalculator_AppliesDexterityAndItemBonuses()
    {
        var stats = CreateStats(dexterity: 188, intelligence: 195, castSpeed: 10, spellDamage: 10);
        var spell = new CastSpellInfo("Test", 3, null, 200, "Heat", "Wizard", 50);

        Assert.Equal(2.124, CastMetricsCalculator.CalculateCastTime(3, stats, true), 3);
        Assert.Equal(270, CastMetricsCalculator.EstimateDamage(spell, stats, "Wizard", true));
        Assert.Equal(3, CastMetricsCalculator.CalculateCastTime(3, stats, false));
        Assert.Equal(3, CastMetricsCalculator.CalculateCastTime(3, stats, true, isFixedCastTime: true));
    }

    [Fact]
    public void CastMetricsCalculator_DoesNotReduceNormalCastsBelowTwoSeconds()
    {
        var stats = CreateStats(dexterity: 500, intelligence: 195, castSpeed: 25, spellDamage: 0);

        Assert.Equal(2, CastMetricsCalculator.CalculateCastTime(3, stats, true));
        Assert.Equal(1.5, CastMetricsCalculator.CalculateCastTime(1.5, stats, true));
    }

    [Theory]
    [InlineData("Wizard", 111)]
    [InlineData("Vale Walker", 111)]
    [InlineData("Runemaster", 222)]
    [InlineData("Bone-Dancer", 222)]
    [InlineData("Druid", 333)]
    [InlineData("Minstrel", 444)]
    [InlineData("Armsman", null)]
    [InlineData("Nightshade", null)]
    [InlineData(null, null)]
    public void CharacterStatsSnapshot_ResolvesAcuityByClass(string? className, int? expected)
    {
        var stats = new CharacterStatsSnapshot(
            ShardType.Eden, "Test", null, null, 188, null,
            Intelligence: 111, Piety: 222, Empathy: 333, Charisma: 444,
            CastingSpeedPercent: 0, SpellDamagePercent: 0, DateTimeOffset.UtcNow);

        Assert.Equal(expected, stats.ResolveAcuity(className));
    }

    [Fact]
    public void CastMetricsCalculator_UsesPietyForRunemasterDamage()
    {
        var stats = new CharacterStatsSnapshot(
            ShardType.Eden, "Test", null, null, 188, null,
            Intelligence: 60, Piety: 180, Empathy: null, Charisma: null,
            CastingSpeedPercent: 0, SpellDamagePercent: 0, DateTimeOffset.UtcNow);
        var spell = new CastSpellInfo("Test", 3, null, 100, "Cold", "Runemaster", 50);

        Assert.Equal(120, CastMetricsCalculator.EstimateDamage(spell, stats, "Runemaster", true));
    }

    [Fact]
    public void FileOcrReplaySink_WritesChangedFramesOnly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new FileOcrReplaySink(directory);
            var capture = new OcrReplayCapture("Chat", new ScreenRegion(1, 2, 3, 4), [1, 2, 3], "You target [Alice].", "Test OCR");
            var parsed = new ChatParseResult(new TargetEvent("Alice", TargetMembership.Member), [], null);

            sink.Record(ShardType.Eden, "Rooy", DateTimeOffset.UtcNow, [capture], parsed);
            sink.Record(ShardType.Eden, "Rooy", DateTimeOffset.UtcNow, [capture], parsed);

            var recordDirectory = Assert.Single(Directory.GetDirectories(directory));
            Assert.True(File.Exists(Path.Combine(recordDirectory, "capture-1.png")));
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(recordDirectory, "record.json")));
            Assert.Equal("Rooy", document.RootElement.GetProperty("characterName").GetString());
            Assert.Equal("Alice", document.RootElement.GetProperty("parseResult").GetProperty("TargetEvent").GetProperty("Name").GetString());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void BlackthornIconLoader_CropsOfficialSpriteCell()
    {
        var loader = new IconImageLoader();

        var image = loader.Load(new IconSpriteRef("blackthorn/spells/spl_100.bmp", 2, 3, 32, 32, 0, 0));

        Assert.NotNull(image);
        var bitmap = Assert.IsAssignableFrom<BitmapSource>(image);
        Assert.Equal(32, bitmap.PixelWidth);
        Assert.Equal(32, bitmap.PixelHeight);
    }

    [Fact]
    public void RuntimeSettings_CombinesCharacterWindowsWithDedicatedStatsRegion()
    {
        var chat = new OcrWatchRegion("chat-window", "Chat", new ScreenRegion(1, 2, 300, 100));
        var stats = new OcrWatchRegion("character-stats", "Character Stats", new ScreenRegion(20, 30, 140, 110));
        var settings = AppRuntimeSettings.FromMap(new Dictionary<string, string>
        {
            ["server"] = "Eden",
            ["daoc.character.eden"] = "Rooy Test",
            ["daoc.ocr.windows.eden.rooy_test"] = JsonSerializer.Serialize(new[] { chat }),
            ["daoc.ocr.stats.eden.rooy_test"] = JsonSerializer.Serialize(stats)
        });

        Assert.Equal(2, settings.OcrWatchRegions.Count);
        Assert.Contains(settings.OcrWatchRegions, x => x.Key == "chat-window");
        Assert.Contains(settings.OcrWatchRegions, x => x.Key == "character-stats");
    }

    private static CharacterStatsSnapshot CreateStats(
        int dexterity,
        int intelligence,
        double castSpeed,
        double spellDamage)
    {
        return new CharacterStatsSnapshot(
            ShardType.Eden, "Rooy", null, null, dexterity, null, intelligence,
            null, null, null, castSpeed, spellDamage, DateTimeOffset.UtcNow);
    }
}
