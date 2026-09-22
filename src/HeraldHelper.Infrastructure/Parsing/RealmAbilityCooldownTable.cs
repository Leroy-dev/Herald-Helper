using System.Text.Json;
using System.Text.RegularExpressions;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Parsing;

/// <summary>Realm-ability name → cooldown seconds, mined from the charplan
/// catalog delve text ("Can use every: 20:00 min"). Eden delves are often
/// empty — unknown cooldowns simply don't appear, callers fall back to
/// elapsed-since-use display.</summary>
public static partial class RealmAbilityCooldownTable
{
    /// <summary>Loads the cooldown map for a class, or null when no catalog
    /// file exists. Class name is slugged (lowercase, non-alnum → _).</summary>
    public static IReadOnlyDictionary<string, int>? Load(ShardType shard, string? className, string? dataRoot = null)
    {
        if (shard is not (ShardType.Eden or ShardType.Blackthorn) || string.IsNullOrWhiteSpace(className))
        {
            return null;
        }

        var catalog = shard == ShardType.Eden ? "eden-charplan" : "blackthorn-charplan";
        var slug = NonAlnumRegex().Replace(className.Trim().ToLowerInvariant(), "_");
        var path = dataRoot is not null
            ? Path.Combine(dataRoot, catalog, "generated", "classes", $"{slug}.json")
            : FindClassFile(catalog, slug);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("realmAbilities", out var abilities) ||
                abilities.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var ability in abilities.EnumerateArray())
            {
                if (!ability.TryGetProperty("name", out var nameProp) ||
                    nameProp.ValueKind != JsonValueKind.String ||
                    !ability.TryGetProperty("delve", out var delveProp) ||
                    delveProp.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var cooldown = ParseCooldown(delveProp.GetString());
                if (cooldown is > 0)
                {
                    result[nameProp.GetString()!.Trim()] = cooldown.Value;
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Walk up from the output dir like AbilityProfileCatalog does —
    /// dev builds run from bin/… while data/ sits at the repo root.</summary>
    private static string? FindClassFile(string catalog, string slug)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName, "data", catalog, "generated", "classes", $"{slug}.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>"Can use every: 20:00 min" / "05:00 min" / "90 sec".</summary>
    internal static int? ParseCooldown(string? delve)
    {
        if (string.IsNullOrWhiteSpace(delve))
        {
            return null;
        }

        var match = CooldownRegex().Match(delve);
        if (!match.Success)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(match.Groups["mm"].Value))
        {
            var minutes = int.Parse(match.Groups["mm"].Value);
            var seconds = int.Parse(match.Groups["ss"].Value);
            return minutes * 60 + seconds;
        }

        return int.Parse(match.Groups["sec"].Value);
    }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlnumRegex();

    [GeneratedRegex(@"[Cc]an use every:\s*(?:(?<mm>\d+):(?<ss>\d+)\s*min|(?<sec>\d+)\s*sec)", RegexOptions.Compiled)]
    private static partial Regex CooldownRegex();
}
