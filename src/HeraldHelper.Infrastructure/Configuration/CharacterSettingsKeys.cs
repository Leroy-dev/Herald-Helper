using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Configuration;

public static class CharacterSettingsKeys
{
    public static string SelectedCharacter(ShardType shard)
    {
        return $"daoc.character.{ShardSegment(shard)}";
    }

    public static string AbilityProfileClass(ShardType shard, string characterName)
    {
        var characterKey = NormalizeSegment(characterName);
        if (string.IsNullOrWhiteSpace(characterKey))
        {
            characterKey = "default";
        }

        return $"ability.profile.class.{ShardSegment(shard)}.{characterKey}";
    }

    public static string LegacyAbilityProfileClass(ShardType shard)
    {
        return $"ability.profile.class.{ShardSegment(shard)}";
    }

    public static string OcrWindows(ShardType shard, string characterName)
    {
        return $"daoc.ocr.windows.{ShardSegment(shard)}.{NormalizeSegment(characterName)}";
    }

    public static string LegacyOcrWindows(ShardType shard, string characterName)
    {
        return $"daoc.ocr.windows.{ShardSegment(shard)}.{LegacyNormalizeSegment(characterName)}";
    }

    public static string OcrStats(ShardType shard, string characterName)
    {
        return $"daoc.ocr.stats.{ShardSegment(shard)}.{NormalizeSegment(characterName)}";
    }

    public static string CharacterLevel(ShardType shard, string characterName)
    {
        return $"character.level.{ShardSegment(shard)}.{NormalizeSegment(characterName)}";
    }

    public static string NormalizeSegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().ToLowerInvariant().Select(x => char.IsLetterOrDigit(x) ? x : '_').ToArray());
    }

    public static string LegacyNormalizeSegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string ShardSegment(ShardType shard)
    {
        return shard.ToString().ToLowerInvariant();
    }
}
