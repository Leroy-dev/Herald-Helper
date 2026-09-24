using System.Globalization;
using System.IO;

namespace HeraldHelper.Desktop;

/// <summary>Client-side icon→spell-name resolution from the gamedata.mpk
/// tables shipped under data/client-tables/: icons.csv maps a client icon id
/// to its base icon + linked spell id, spells.csv maps spell id → name.
/// This is how bare iconIds (mini_pet_effectN, group icons) get real names
/// without the charplan catalogs.</summary>
internal static class ClientIconSpellMap
{
    private static readonly Lazy<IReadOnlyDictionary<int, string>> NamesLazy =
        new(Load);

    /// <summary>Real spell name for a client icon id, or null when the icon
    /// has no spell link (frames, borders, generic markers).</summary>
    public static string? NameForIcon(int iconId)
    {
        return NamesLazy.Value.TryGetValue(iconId, out var name) ? name : null;
    }

    private static IReadOnlyDictionary<int, string> Load()
    {
        var root = FindTableRoot();
        if (root is null)
        {
            return new Dictionary<int, string>();
        }

        var spellIcons = new Dictionary<int, string>();
        var spellNames = new Dictionary<int, string>();
        var spellsPath = Path.Combine(root, "spells.csv");
        if (File.Exists(spellsPath))
        {
            foreach (var line in File.ReadLines(spellsPath).Skip(3))
            {
                var fields = line.Split(',');
                if (fields.Length > 2 &&
                    int.TryParse(fields[0], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var spellId) &&
                    !string.IsNullOrWhiteSpace(fields[1]))
                {
                    spellNames.TryAdd(spellId, fields[1].Trim());
                    // Column 2 is the spell's own icon id — the direct map.
                    if (int.TryParse(fields[2], NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var iconId) &&
                        iconId > 0)
                    {
                        spellIcons.TryAdd(iconId, fields[1].Trim());
                    }
                }
            }
        }

        var result = new Dictionary<int, string>(spellIcons);
        var iconsPath = Path.Combine(root, "icons.csv");
        if (File.Exists(iconsPath))
        {
            foreach (var line in File.ReadLines(iconsPath).Skip(2))
            {
                var fields = line.Split(',');
                if (fields.Length > 10 &&
                    int.TryParse(fields[0], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var iconId) &&
                    int.TryParse(fields[10], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var spellId) &&
                    spellId > 0 &&
                    spellNames.TryGetValue(spellId, out var name))
                {
                    result.TryAdd(iconId, name);
                }
            }
        }

        return result;
    }

    private static string? FindTableRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", "client-tables");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
