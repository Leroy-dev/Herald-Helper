using System.Linq;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Infrastructure.Auth;

namespace HeraldHelper.Desktop.Controllers;

internal sealed class SettingsController
{
    private readonly ISettingsRepository _settingsRepository;

    public SettingsController(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public IReadOnlyDictionary<string, string> LoadMap()
    {
        return _settingsRepository.LoadSettingsMap();
    }

    public List<ConfigEntry> LoadEntries()
    {
        return _settingsRepository.LoadConfigEntries();
    }

    public void Save(IEnumerable<ConfigEntry> updates)
    {
        var map = LoadMap().ToDictionary(StringComparer.OrdinalIgnoreCase);
        foreach (var update in updates)
        {
            map[update.Key] = update.Value;
        }

        _settingsRepository.SaveSettings(map.Select(kvp => new ConfigEntry { Key = kvp.Key, Value = kvp.Value }));
    }

    public void Replace(IEnumerable<ConfigEntry> entries)
    {
        _settingsRepository.SaveSettings(entries);
    }

    public void EnsureDefaultAuthSettings()
    {
        var current = LoadMap().ToDictionary(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var entry in ShardAuthProfileResolver.DefaultSettings())
        {
            if (current.ContainsKey(entry.Key))
            {
                continue;
            }

            current[entry.Key] = entry.Value;
            changed = true;
        }

        if (current.TryGetValue("auth.eden.cookieNames", out var cookieNamesRaw))
        {
            var normalized = cookieNamesRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !x.Equals("POWSESS", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count == 0)
            {
                normalized.AddRange(["eden_daoc_u", "eden_daoc_k", "eden_daoc_sid"]);
            }

            var normalizedCsv = string.Join(",", normalized);
            if (!string.Equals(cookieNamesRaw, normalizedCsv, StringComparison.Ordinal))
            {
                current["auth.eden.cookieNames"] = normalizedCsv;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        _settingsRepository.SaveSettings(current.Select(x => new ConfigEntry { Key = x.Key, Value = x.Value }));
    }
}
