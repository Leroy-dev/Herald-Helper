using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MediaColor = System.Windows.Media.Color;

namespace HeraldHelper.Desktop;

public enum Realm
{
    Unknown,
    Albion = 1,
    Midgard = 2,
    Hibernia = 3
}

internal static class ClassRealmResolver
{
    private static readonly Lazy<IReadOnlyDictionary<string, Realm>> Map = new(BuildMap);

    public static Realm Resolve(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return Realm.Unknown;
        }

        return Map.Value.TryGetValue(className, out var realm) ? realm : Realm.Unknown;
    }

    public static MediaColor ResolveColor(Realm realm)
    {
        return realm switch
        {
            Realm.Albion => MediaColor.FromRgb(255, 40, 40),
            Realm.Midgard => MediaColor.FromRgb(40, 120, 255),
            Realm.Hibernia => MediaColor.FromRgb(40, 220, 40),
            _ => System.Windows.Media.Colors.White
        };
    }

    private static IReadOnlyDictionary<string, Realm> BuildMap()
    {
        var map = new Dictionary<string, Realm>(StringComparer.OrdinalIgnoreCase);
        AddFromDataRoot(map, "eden-charplan");
        AddFromDataRoot(map, "blackthorn-charplan");
        return map;
    }

    private static void AddFromDataRoot(Dictionary<string, Realm> map, string dataDirName)
    {
        var root = TryFindDataRoot(dataDirName);
        if (root is null)
        {
            return;
        }

        var path = Path.Combine(root, "generated", "classes", "index.json");
        if (!File.Exists(path))
        {
            return;
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (!element.TryGetProperty("name", out var nameElement)
                || nameElement.GetString() is not { } name)
            {
                continue;
            }

            if (!element.TryGetProperty("realm", out var realmElement))
            {
                continue;
            }

            int? realmInt = null;
            if (realmElement.ValueKind == JsonValueKind.Number && realmElement.TryGetInt32(out var n))
            {
                realmInt = n;
            }
            else if (realmElement.ValueKind == JsonValueKind.String)
            {
                var realmString = realmElement.GetString();
                if (int.TryParse(realmString, out n))
                {
                    realmInt = n;
                }
                else
                {
                    realmInt = realmString?.ToLowerInvariant() switch
                    {
                        "albion" => (int)Realm.Albion,
                        "midgard" => (int)Realm.Midgard,
                        "hibernia" => (int)Realm.Hibernia,
                        _ => null
                    };
                }
            }

            if (realmInt is not null && Enum.IsDefined(typeof(Realm), realmInt.Value))
            {
                map[name] = (Realm)realmInt.Value;
            }
        }
    }

    private static string? TryFindDataRoot(string dataDirName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", dataDirName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
