using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Infrastructure.Configuration;

namespace HeraldHelper.Desktop.Services;

/// <summary>
/// One-time settings-key renames so the read path never needs legacy
/// fallbacks. Runs at startup after the DB schema migrations; idempotent.
///   * `ability.profile.class.&lt;shard&gt;` (shard-wide class default from before
///     per-character keys) → `ability.profile.class.&lt;shard&gt;.default`
///   * `daoc.ocr.windows.&lt;shard&gt;.&lt;char&gt;` keys saved before character-name
///     normalization → re-keyed under the normalized segment.
/// </summary>
internal static class LegacySettingsMigrator
{
    public static int Migrate(ISettingsRepository settings)
    {
        var map = settings.LoadSettingsMap();
        var renames = new List<(string OldKey, string NewKey)>();

        foreach (var shard in Enum.GetValues<ShardType>())
        {
            var legacyClassKey = $"ability.profile.class.{shard.ToString().ToLowerInvariant()}";
            if (map.ContainsKey(legacyClassKey))
            {
                renames.Add((legacyClassKey, $"{legacyClassKey}.default"));
            }
        }

        const string ocrWindowsPrefix = "daoc.ocr.windows.";
        foreach (var key in map.Keys
                     .Where(k => k.StartsWith(ocrWindowsPrefix, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            var suffix = key[ocrWindowsPrefix.Length..]; // "<shard>.<character>"
            var dot = suffix.IndexOf('.');
            if (dot < 0)
            {
                continue;
            }

            var characterSegment = suffix[(dot + 1)..];
            var normalized = CharacterSettingsKeys.NormalizeSegment(characterSegment);
            if (!string.Equals(characterSegment, normalized, StringComparison.Ordinal))
            {
                renames.Add((key, $"{ocrWindowsPrefix}{suffix[..(dot + 1)]}{normalized}"));
            }
        }

        if (renames.Count == 0)
        {
            return 0;
        }

        var applied = 0;
        foreach (var (oldKey, newKey) in renames)
        {
            if (!map.TryGetValue(oldKey, out var value))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(value) && !map.ContainsKey(newKey))
            {
                map[newKey] = value;
            }

            map.Remove(oldKey);
            applied++;
        }

        if (applied > 0)
        {
            settings.SaveSettings(map.Select(x => new ConfigEntry { Key = x.Key, Value = x.Value }));
        }

        return applied;
    }
}
