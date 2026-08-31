using System.Text;
using System.Text.Json;
using HeraldHelper.Application.Models;
using HeraldHelper.Desktop;

namespace HeraldHelper.Tests;

public sealed class CatalogUpdateServiceTests
{
    [Fact]
    public async Task UpdateAsync_RunsCrawlerAndReturnsSummary()
    {
        var root = CreateTempProject();
        var crawler = new ModifyCatalogCrawler(root);
        var service = new CatalogUpdateService(root, crawler, new FileCatalogValidator(), new FileCatalogBackup());

        var result = await service.UpdateAsync(CancellationToken.None);

        Assert.Contains("Catalog update completed", result);
        Assert.Contains("After:", result);
        Assert.Equal("1.0.63", ReadManifestVersion(root, "eden-charplan"));
    }

    [Fact]
    public async Task PreviewAsync_RestoresOriginalAndReportsChanges()
    {
        var root = CreateTempProject();
        var crawler = new ModifyCatalogCrawler(root);
        var service = new CatalogUpdateService(root, crawler, new FileCatalogValidator(), new FileCatalogBackup());

        var previews = await service.PreviewAsync(CancellationToken.None);

        Assert.Equal("1.0.62", ReadManifestVersion(root, "eden-charplan"));
        var eden = previews.First(p => p.Catalog == "eden-charplan");
        Assert.True(eden.HasChanges);
        Assert.Contains(eden.Additions, x => x.Contains("Druid"));
        Assert.Contains(eden.SchemaChanges, x => x.Contains("version changed"));
    }

    [Fact]
    public async Task UpdateAsync_CrawlerFailure_RestoresOriginal()
    {
        var root = CreateTempProject();
        var crawler = new ThrowingCrawler("eden-charplan");
        var service = new CatalogUpdateService(root, crawler, new FileCatalogValidator(), new FileCatalogBackup());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(CancellationToken.None));

        Assert.Equal("1.0.62", ReadManifestVersion(root, "eden-charplan"));
    }

    [Fact]
    public async Task UpdateAsync_ValidatorFailure_RestoresOriginal()
    {
        var root = CreateTempProject();
        var crawler = new ModifyCatalogCrawler(root);
        var validator = new ThrowingValidator("eden-charplan");
        var service = new CatalogUpdateService(root, crawler, validator, new FileCatalogBackup());

        await Assert.ThrowsAsync<InvalidDataException>(() => service.UpdateAsync(CancellationToken.None));

        Assert.Equal("1.0.62", ReadManifestVersion(root, "eden-charplan"));
    }

    [Fact]
    public async Task UpdateAsync_RestoreFailure_ThrowsAggregateException()
    {
        var root = CreateTempProject();
        var crawler = new ThrowingCrawler("eden-charplan");
        var backup = new ThrowingBackup();
        var service = new CatalogUpdateService(root, crawler, new FileCatalogValidator(), backup);

        var ex = await Assert.ThrowsAsync<AggregateException>(() => service.UpdateAsync(CancellationToken.None));

        Assert.Contains(ex.InnerExceptions, e => e is InvalidOperationException);
        Assert.Contains(ex.InnerExceptions, e => e is IOException);
    }

    private static string CreateTempProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "HeraldHelper.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "data"));

        CreateCatalog(root, "eden-charplan", "1.0.62",
        [
            new ClassRecord(1, "Hero", "hero")
        ],
        skills: [],
        realmAbilities: [],
        iconConfigs: []);

        CreateCatalog(root, "blackthorn-charplan", "1.0.5",
        [
            new ClassRecord(1, "Warrior", "warrior")
        ],
        skills: [],
        realmAbilities: [],
        iconConfigs: []);

        return root;
    }

    private static void CreateCatalog(
        string root,
        string catalog,
        string version,
        IReadOnlyList<ClassRecord> classes,
        IReadOnlyList<SkillRecord> skills,
        IReadOnlyList<RealmAbilityRecord> realmAbilities,
        IReadOnlyList<object> iconConfigs)
    {
        var catalogRoot = Path.Combine(root, "data", catalog);
        var generated = Path.Combine(catalogRoot, "generated");
        var classDir = Path.Combine(generated, "classes");
        Directory.CreateDirectory(classDir);

        var index = classes.Select(c => new { id = c.Id, name = c.Name, slug = c.Slug, realm = "Albion", races = Array.Empty<string>(), specPointMultiplier = 1.5, realmAbilityCount = 0, specCount = 1, skillCount = 0 }).ToList();
        File.WriteAllText(Path.Combine(classDir, "index.json"), JsonSerializer.Serialize(index));

        foreach (var cls in classes)
        {
            File.WriteAllText(Path.Combine(classDir, $"{cls.Slug}.json"), JsonSerializer.Serialize(new
            {
                id = cls.Id,
                name = cls.Name,
                slug = cls.Slug,
                realm = "Albion",
                specs = new[]
                {
                    new { id = 1, name = "Base", @base = true, autotrain = false, skillGroups = Array.Empty<object>(), spellLines = Array.Empty<object>() }
                }
            }));
        }

        var manifest = new
        {
            source = "https://example.com",
            version,
            generatedAt = DateTimeOffset.UtcNow.ToString("O"),
            classCount = classes.Count,
            charplanClassCount = classes.Count,
            ignoredClasses = new string[] { },
            charplanClCount = 0,
            uniqueIconConfigCount = iconConfigs.Count,
            files = new
            {
                raw = new[] { "raw/charplan.json" },
                classIndex = "generated/classes/index.json",
                realmAbilities = "generated/realm-abilities.json",
                flattenedSkills = "generated/skills-flat.json",
                iconConfigs = "generated/icon-configs.json",
                spriteDir = "assets/sprites"
            }
        };
        File.WriteAllText(Path.Combine(generated, "manifest.json"), JsonSerializer.Serialize(manifest));

        File.WriteAllText(Path.Combine(generated, "skills-flat.json"), JsonSerializer.Serialize(skills));
        File.WriteAllText(Path.Combine(generated, "realm-abilities.json"), JsonSerializer.Serialize(realmAbilities));
        File.WriteAllText(Path.Combine(generated, "icon-configs.json"), JsonSerializer.Serialize(iconConfigs));
    }

    private static string? ReadManifestVersion(string root, string catalog)
    {
        var path = Path.Combine(root, "data", catalog, "generated", "manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("version").GetString();
    }

    private sealed record ClassRecord(int Id, string Name, string Slug);
    private sealed record SkillRecord(int Id, string Name, string ClassSlug);
    private sealed record RealmAbilityRecord(int Id, string Name, string ClassSlug);

    private sealed class ModifyCatalogCrawler : ICatalogCrawler
    {
        private readonly string _root;

        public ModifyCatalogCrawler(string root)
        {
            _root = root;
        }

        public Task RunAsync(string catalog, StringBuilder output, CancellationToken cancellationToken)
        {
            if (catalog != "eden-charplan")
            {
                return Task.CompletedTask;
            }

            var generated = Path.Combine(_root, "data", catalog, "generated");
            var classDir = Path.Combine(generated, "classes");

            var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(generated, "manifest.json")));
            var manifestObject = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(generated, "manifest.json")))
                ?? new Dictionary<string, object>();
            manifestObject["version"] = "1.0.63";
            manifestObject["classCount"] = 2;
            File.WriteAllText(Path.Combine(generated, "manifest.json"), JsonSerializer.Serialize(manifestObject));

            var index = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(Path.Combine(classDir, "index.json")))
                ?? [];
            index.Add(new Dictionary<string, object>
            {
                ["id"] = 2,
                ["name"] = "Druid",
                ["slug"] = "druid",
                ["realm"] = "Hibernia",
                ["races"] = new string[] { },
                ["specPointMultiplier"] = 1.5,
                ["realmAbilityCount"] = 0,
                ["specCount"] = 1,
                ["skillCount"] = 0
            });
            File.WriteAllText(Path.Combine(classDir, "index.json"), JsonSerializer.Serialize(index));

            File.WriteAllText(Path.Combine(classDir, "druid.json"), JsonSerializer.Serialize(new
            {
                id = 2,
                name = "Druid",
                slug = "druid",
                realm = "Hibernia",
                specs = new[]
                {
                    new { id = 1, name = "Base", @base = true, autotrain = false, skillGroups = Array.Empty<object>(), spellLines = Array.Empty<object>() }
                }
            }));

            var skills = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(Path.Combine(generated, "skills-flat.json")))
                ?? [];
            skills.Add(new Dictionary<string, object>
            {
                ["id"] = 100,
                ["name"] = "Blight",
                ["classSlug"] = "druid"
            });
            File.WriteAllText(Path.Combine(generated, "skills-flat.json"), JsonSerializer.Serialize(skills));

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCrawler : ICatalogCrawler
    {
        private readonly string _catalog;

        public ThrowingCrawler(string catalog)
        {
            _catalog = catalog;
        }

        public Task RunAsync(string catalog, StringBuilder output, CancellationToken cancellationToken)
        {
            if (catalog == _catalog)
            {
                throw new InvalidOperationException($"Crawler failed for {catalog}.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingValidator : ICatalogValidator
    {
        private readonly string _catalog;

        public ThrowingValidator(string catalog)
        {
            _catalog = catalog;
        }

        public void Validate(string root, string catalog)
        {
            if (catalog == _catalog)
            {
                throw new InvalidDataException($"Validation failed for {catalog}.");
            }
        }
    }

    private sealed class ThrowingBackup : ICatalogBackup
    {
        public void Backup(string root, string catalog, string backupRoot)
        {
        }

        public void Restore(string root, string catalog, string backupRoot)
        {
            throw new IOException("Restore failed.");
        }

        public void Delete(string path)
        {
        }
    }
}
