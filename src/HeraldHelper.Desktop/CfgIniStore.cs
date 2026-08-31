using System.IO;
using System.Text;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

public static class CfgIniStore
{
    public static void SaveRegion(string cfgPath, ScreenRegion region)
    {
        var map = Load(cfgPath);
        map["MX"] = region.X.ToString();
        map["MY"] = region.Y.ToString();
        map["w"] = region.Width.ToString();
        map["h"] = region.Height.ToString();
        Write(cfgPath, map);
    }

    public static void SaveSetting(string cfgPath, string key, string value)
    {
        var map = Load(cfgPath);
        map[key] = value;
        Write(cfgPath, map);
    }

    public static List<ConfigEntry> LoadEntries(string cfgPath)
    {
        return Load(cfgPath)
            .Select(kvp => new ConfigEntry { Key = kvp.Key, Value = kvp.Value })
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void SaveEntries(string cfgPath, IEnumerable<ConfigEntry> entries)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            map[entry.Key.Trim()] = entry.Value?.Trim() ?? string.Empty;
        }

        Write(cfgPath, map);
    }

    private static Dictionary<string, string> Load(string cfgPath)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(cfgPath))
        {
            return map;
        }

        foreach (var line in File.ReadLines(cfgPath))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                map[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return map;
    }

    private static void Write(string cfgPath, IReadOnlyDictionary<string, string> map)
    {
        var order = new[]
        {
            "overlayX","overlayY","overlayXTimer","overlayYTimer","overlayXccTimer","overlayYccTimer",
            "overlayXResists","overlayYResists","fontSize","timerSize","ccSize","resisSize",
            "MX","MY","w","h","show","server","edenHeraldCookie","edenHeraldUserAgent","font","resis"
        };

        var sb = new StringBuilder();
        foreach (var key in order)
        {
            if (map.TryGetValue(key, out var value))
            {
                sb.Append(key).Append(':').Append(value).AppendLine();
            }
        }

        foreach (var pair in map)
        {
            if (order.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            sb.Append(pair.Key).Append(':').Append(pair.Value).AppendLine();
        }

        File.WriteAllText(cfgPath, sb.ToString());
    }
}
