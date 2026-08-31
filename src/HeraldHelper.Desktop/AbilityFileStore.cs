using System.Text;
using System.IO;

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

            var parts = line.Split('#');
            if (parts.Length != 4)
            {
                continue;
            }

            if (!int.TryParse(parts[2].Trim(), out var seconds))
            {
                continue;
            }

            rows.Add(new AbilityEditorRow
            {
                AbilityName = parts[0].Trim(),
                SkillCode = parts[1].Trim().ToLowerInvariant(),
                DurationSeconds = seconds,
                EffectType = parts[3].Trim().ToLowerInvariant()
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
