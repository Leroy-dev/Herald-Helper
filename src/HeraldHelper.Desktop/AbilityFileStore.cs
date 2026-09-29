using System.IO;
using System.Text;

namespace HeraldHelper.Desktop;

public static class AbilityFileStore
{
    public static List<AbilityEditorRow> Load(string path)
    {
        var rows = new List<AbilityEditorRow>();
        if (!File.Exists(path))
        {
            return rows;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // name#skill#seconds#effect — split from the RIGHT so a '#' inside
            // the ability name survives (plain Split('#') silently drops it).
            var i3 = line.LastIndexOf('#');
            var i2 = i3 > 0 ? line.LastIndexOf('#', i3 - 1) : -1;
            var i1 = i2 > 0 ? line.LastIndexOf('#', i2 - 1) : -1;
            if (i1 <= 0)
            {
                continue;
            }

            if (!int.TryParse(line[(i2 + 1)..i3].Trim(), out var seconds))
            {
                continue;
            }

            rows.Add(new AbilityEditorRow
            {
                AbilityName = line[..i1].Trim(),
                SkillCode = line[(i1 + 1)..i2].Trim().ToLowerInvariant(),
                DurationSeconds = seconds,
                EffectType = line[(i3 + 1)..].Trim().ToLowerInvariant()
            });
        }

        return rows;
    }

    public static void Save(string path, IEnumerable<AbilityEditorRow> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.AbilityName))
            {
                continue;
            }

            var skill = NormalizeCode(row.SkillCode, "s");
            var effect = NormalizeCode(row.EffectType, "s");
            var seconds = Math.Max(1, row.DurationSeconds);
            sb.Append(row.AbilityName.Trim())
                .Append('#')
                .Append(skill)
                .Append('#')
                .Append(seconds)
                .Append('#')
                .Append(effect)
                .AppendLine();
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static string NormalizeCode(string value, string fallback)
    {
        var v = value.Trim().ToLowerInvariant();
        return v is "m" or "s" or "r" ? v : fallback;
    }
}
