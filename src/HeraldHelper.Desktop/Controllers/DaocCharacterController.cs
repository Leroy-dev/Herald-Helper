using System.Text.Json;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop.Controllers;

internal sealed class DaocCharacterController
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly DaocCharacterDiscoveryService _discovery = new();

    public DaocCharacterController(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public IReadOnlyDictionary<ShardType, IReadOnlyList<DaocCharacterProfile>> Profiles { get; private set; } =
        new Dictionary<ShardType, IReadOnlyList<DaocCharacterProfile>>();

    public void LoadProfiles()
    {
        Profiles = _discovery.Discover();
    }

    public IReadOnlyList<DaocCharacterProfile> GetProfiles(ShardType shard)
    {
        return Profiles.TryGetValue(shard, out var list) ? list : Array.Empty<DaocCharacterProfile>();
    }

    public IReadOnlyList<OcrWatchRegion> LoadOcrWindows(ShardType shard, string characterName)
    {
        var settings = _settingsRepository.LoadSettingsMap();
        var prefix = $"daoc.ocr.windows.{shard.ToString().ToLowerInvariant()}.";
        var key = prefix + NormalizeSettingSegment(characterName);
        var legacyKey = prefix + characterName.Trim().ToLowerInvariant();
        if (!settings.TryGetValue(key, out var raw))
        {
            settings.TryGetValue(legacyKey, out raw);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<OcrWatchRegion>>(raw) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SaveOcrWindows(ShardType shard, string characterName, IReadOnlyCollection<DaocWindowDefinition> selected)
    {
        var windows = selected.Select(x => new OcrWatchRegion(x.Key, x.Label, x.Region)).ToList();
        var raw = JsonSerializer.Serialize(windows);
        var settings = _settingsRepository.LoadSettingsMap();
        var prefix = $"daoc.ocr.windows.{shard.ToString().ToLowerInvariant()}.";
        var key = prefix + NormalizeSettingSegment(characterName);

        settings[key] = raw;

        var legacyKey = prefix + characterName.Trim().ToLowerInvariant();
        if (!string.Equals(key, legacyKey, StringComparison.OrdinalIgnoreCase) && settings.ContainsKey(legacyKey))
        {
            settings[legacyKey] = string.Empty;
        }

        _settingsRepository.SaveSettings(settings.Select(x => new ConfigEntry { Key = x.Key, Value = x.Value }));
    }

    private static string NormalizeSettingSegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().ToLowerInvariant().Select(x => char.IsLetterOrDigit(x) ? x : '_').ToArray());
    }
}
