using System.Text.Json;
using HeraldHelper.Application.Models;

namespace HeraldHelper.Application.Services;

public static class CatalogUpdatePreviewBuilder
{
    public static CatalogUpdatePreview Build(
        string catalog,
        string beforeRoot,
        string afterRoot)
    {
        var preview = new CatalogUpdatePreview { Catalog = catalog };
        var before = new CatalogSnapshot(beforeRoot);
        var after = new CatalogSnapshot(afterRoot);

        preview.BeforeVersion = before.ManifestVersion;
        preview.AfterVersion = after.ManifestVersion;

        if (!string.Equals(preview.BeforeVersion, preview.AfterVersion, StringComparison.OrdinalIgnoreCase))
        {
            preview.SchemaChanges.Add($"manifest version changed from '{preview.BeforeVersion}' to '{preview.AfterVersion}'");
        }

        CompareValue(before, after, "classCount", "Class count", preview);
        CompareValue(before, after, "uniqueIconConfigCount", "Unique icon config count", preview);

        var beforeClasses = before.ClassesById;
        var afterClasses = after.ClassesById;
        var beforeSlugs = before.ClassesBySlug;
        var afterSlugs = after.ClassesBySlug;

        foreach (var id in afterClasses.Keys.Except(beforeClasses.Keys))
        {
            preview.Additions.Add($"class: {afterClasses[id].Name} ({afterClasses[id].Slug})");
        }

        foreach (var id in beforeClasses.Keys.Except(afterClasses.Keys))
        {
            preview.Removals.Add($"class: {beforeClasses[id].Name} ({beforeClasses[id].Slug})");
        }

        foreach (var (id, beforeClass) in beforeClasses)
        {
            if (!afterClasses.TryGetValue(id, out var afterClass))
            {
                continue;
            }

            if (!string.Equals(beforeClass.Slug, afterClass.Slug, StringComparison.OrdinalIgnoreCase))
            {
                preview.Conflicts.Add($"class id {id} changed slug from '{beforeClass.Slug}' to '{afterClass.Slug}'");
            }

            if (!string.Equals(beforeClass.Name, afterClass.Name, StringComparison.OrdinalIgnoreCase))
            {
                preview.SchemaChanges.Add($"class id {id} renamed from '{beforeClass.Name}' to '{afterClass.Name}'");
            }
        }

        CompareByKey(
            before.Skills,
            after.Skills,
            x => $"skill: {x.Name} (id={x.Id}, class={x.ClassSlug})",
            preview,
            "skill");

        CompareByKey(
            before.RealmAbilities,
            after.RealmAbilities,
            x => $"realm ability: {x.Name} (id={x.Id}, class={x.ClassSlug})",
            preview,
            "realm ability");

        CompareByKey(
            before.IconConfigs,
            after.IconConfigs,
            x => $"icon config: {x.Hash[..Math.Min(16, x.Hash.Length)]}...",
            preview,
            "icon config");

        foreach (var beforeFile in before.OtherFiles)
        {
            var afterFile = beforeFile.Replace(beforeRoot, afterRoot);
            if (!File.Exists(afterFile))
            {
                preview.Removals.Add($"file: {Path.GetRelativePath(beforeRoot, beforeFile)}");
            }
        }

        foreach (var afterFile in after.OtherFiles)
        {
            var beforeFile = afterFile.Replace(afterRoot, beforeRoot);
            if (!File.Exists(beforeFile))
            {
                preview.Additions.Add($"file: {Path.GetRelativePath(afterRoot, afterFile)}");
            }
        }

        preview.Summary =
            $"{preview.Additions.Count} additions, {preview.Removals.Count} removals, " +
            $"{preview.Conflicts.Count} conflicts, {preview.SchemaChanges.Count} schema changes.";

        return preview;
    }

    private static void CompareValue(
        CatalogSnapshot before,
        CatalogSnapshot after,
        string propertyName,
        string label,
        CatalogUpdatePreview preview)
    {
        var beforeValue = before.GetManifestValue(propertyName);
        var afterValue = after.GetManifestValue(propertyName);
        if (!string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
        {
            preview.SchemaChanges.Add($"{label} changed from {beforeValue} to {afterValue}");
        }
    }

    private static void CompareByKey<T>(
        IReadOnlyDictionary<string, T> before,
        IReadOnlyDictionary<string, T> after,
        Func<T, string> format,
        CatalogUpdatePreview preview,
        string label)
    {
        foreach (var key in after.Keys.Except(before.Keys))
        {
            preview.Additions.Add(format(after[key]));
        }

        foreach (var key in before.Keys.Except(after.Keys))
        {
            preview.Removals.Add(format(before[key]));
        }
    }

    private sealed class CatalogSnapshot
    {
        private readonly string _catalogRoot;

        public CatalogSnapshot(string catalogRoot)
        {
            _catalogRoot = catalogRoot;
        }

        public string? ManifestVersion => GetManifestValue("version");

        public IReadOnlyDictionary<int, ClassEntry> ClassesById => ReadClassIndex();
        public IReadOnlyDictionary<string, ClassEntry> ClassesBySlug => ReadClassIndex().Values.ToDictionary(x => x.Slug, StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, SkillEntry> Skills => ReadSkills();
        public IReadOnlyDictionary<string, RealmAbilityEntry> RealmAbilities => ReadRealmAbilities();
        public IReadOnlyDictionary<string, IconConfigEntry> IconConfigs => ReadIconConfigs();
        public IReadOnlyList<string> OtherFiles => EnumerateOtherFiles();

        public string? GetManifestValue(string propertyName)
        {
            var path = Path.Combine(_catalogRoot, "generated", "manifest.json");
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty(propertyName, out var value)
                ? value.ToString()
                : null;
        }

        private IReadOnlyDictionary<int, ClassEntry> ReadClassIndex()
        {
            var path = Path.Combine(_catalogRoot, "generated", "classes", "index.json");
            if (!File.Exists(path))
            {
                return new Dictionary<int, ClassEntry>();
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var result = new Dictionary<int, ClassEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var id = element.GetProperty("id").GetInt32();
                var name = element.GetProperty("name").GetString() ?? string.Empty;
                var slug = element.GetProperty("slug").GetString() ?? string.Empty;
                result[id] = new ClassEntry(id, name, slug);
            }

            return result;
        }

        private IReadOnlyDictionary<string, SkillEntry> ReadSkills()
        {
            var path = Path.Combine(_catalogRoot, "generated", "skills-flat.json");
            if (!File.Exists(path))
            {
                return new Dictionary<string, SkillEntry>();
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var result = new Dictionary<string, SkillEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var id = element.GetProperty("id").GetInt32();
                var name = element.GetProperty("name").GetString() ?? string.Empty;
                var classSlug = element.GetProperty("classSlug").GetString() ?? string.Empty;
                result[$"{classSlug}:{id}"] = new SkillEntry(id, name, classSlug);
            }

            return result;
        }

        private IReadOnlyDictionary<string, RealmAbilityEntry> ReadRealmAbilities()
        {
            var path = Path.Combine(_catalogRoot, "generated", "realm-abilities.json");
            if (!File.Exists(path))
            {
                return new Dictionary<string, RealmAbilityEntry>();
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var result = new Dictionary<string, RealmAbilityEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var id = element.GetProperty("id").GetInt32();
                var name = element.GetProperty("name").GetString() ?? string.Empty;
                var classSlug = element.GetProperty("classSlug").GetString() ?? string.Empty;
                result[$"{classSlug}:{id}"] = new RealmAbilityEntry(id, name, classSlug);
            }

            return result;
        }

        private IReadOnlyDictionary<string, IconConfigEntry> ReadIconConfigs()
        {
            var path = Path.Combine(_catalogRoot, "generated", "icon-configs.json");
            if (!File.Exists(path))
            {
                return new Dictionary<string, IconConfigEntry>();
            }

            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            var result = new Dictionary<string, IconConfigEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var hash = element.GetRawText();
                var stable = HashStable(hash);
                result[stable] = new IconConfigEntry(stable, hash);
            }

            return result;
        }

        private IReadOnlyList<string> EnumerateOtherFiles()
        {
            if (!Directory.Exists(_catalogRoot))
            {
                return [];
            }

            return [.. Directory.EnumerateFiles(_catalogRoot, "*", SearchOption.AllDirectories)];
        }

        private static string HashStable(string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        }
    }

    private sealed record ClassEntry(int Id, string Name, string Slug);
    private sealed record SkillEntry(int Id, string Name, string ClassSlug);
    private sealed record RealmAbilityEntry(int Id, string Name, string ClassSlug);
    private sealed record IconConfigEntry(string Hash, string Raw);
}
