using System.IO;
using System.Text;
using System.Text.Json;
using HeraldHelper.Application.Models;
using HeraldHelper.Application.Services;

namespace HeraldHelper.Desktop;

internal sealed class CatalogUpdateService
{
    private readonly string? _root;
    private readonly ICatalogCrawler _crawler;
    private readonly ICatalogValidator _validator;
    private readonly ICatalogBackup _backup;
    private readonly string[] _catalogs;

    public CatalogUpdateService(
        string? root = null,
        ICatalogCrawler? crawler = null,
        ICatalogValidator? validator = null,
        ICatalogBackup? backup = null)
    {
        _root = root;
        _crawler = crawler ?? new NodeCatalogCrawler();
        _validator = validator ?? new FileCatalogValidator();
        _backup = backup ?? new FileCatalogBackup();
        _catalogs = ["eden-charplan", "blackthorn-charplan"];
    }

    public async Task<string> UpdateAsync(CancellationToken cancellationToken)
    {
        var root = _root ?? FindProjectRoot() ?? throw new InvalidOperationException("Could not locate the HeraldHelper scripts directory.");
        var before = ReadSummary(root);
        var output = new StringBuilder();
        var backupRoot = Path.Combine(root, "tmp", $"catalog-update-backup-{Guid.NewGuid():N}");
        var deleteBackup = true;

        try
        {
            foreach (var catalog in _catalogs)
            {
                _backup.Backup(root, catalog, backupRoot);
            }

            await RunCrawlersAsync(root, output, cancellationToken);

            foreach (var catalog in _catalogs)
            {
                _validator.Validate(root, catalog);
            }

            var after = ReadSummary(root);
            return $"Catalog update completed.\nBefore: {before}\nAfter: {after}\n\n{output}";
        }
        catch (Exception updateError)
        {
            try
            {
                TryRestore(root, backupRoot);
            }
            catch (Exception restoreError)
            {
                deleteBackup = false;
                throw new AggregateException(
                    $"Catalog update failed and rollback also failed. Recovery files remain in {backupRoot}.",
                    updateError,
                    restoreError);
            }
            throw;
        }
        finally
        {
            if (deleteBackup)
            {
                _backup.Delete(backupRoot);
            }
        }
    }

    public async Task<IReadOnlyList<CatalogUpdatePreview>> PreviewAsync(CancellationToken cancellationToken)
    {
        var root = _root ?? FindProjectRoot() ?? throw new InvalidOperationException("Could not locate the HeraldHelper scripts directory.");
        var previews = new List<CatalogUpdatePreview>();
        var output = new StringBuilder();
        var backupRoot = Path.Combine(root, "tmp", $"catalog-preview-backup-{Guid.NewGuid():N}");
        var deleteBackup = true;

        try
        {
            foreach (var catalog in _catalogs)
            {
                _backup.Backup(root, catalog, backupRoot);
            }

            var beforeRoots = _catalogs.ToDictionary(
                x => x,
                x => Path.Combine(backupRoot, x));

            await RunCrawlersAsync(root, output, cancellationToken);

            foreach (var catalog in _catalogs)
            {
                _validator.Validate(root, catalog);
            }

            foreach (var catalog in _catalogs)
            {
                var preview = CatalogUpdatePreviewBuilder.Build(
                    catalog,
                    Path.Combine(backupRoot, catalog),
                    Path.Combine(root, "data", catalog));
                previews.Add(preview);
            }

            TryRestore(root, backupRoot);
            return previews;
        }
        catch (Exception updateError)
        {
            try
            {
                TryRestore(root, backupRoot);
            }
            catch (Exception restoreError)
            {
                deleteBackup = false;
                throw new AggregateException(
                    $"Catalog preview failed and rollback also failed. Recovery files remain in {backupRoot}.",
                    updateError,
                    restoreError);
            }
            throw;
        }
        finally
        {
            if (deleteBackup)
            {
                _backup.Delete(backupRoot);
            }
        }
    }

    private async Task RunCrawlersAsync(string root, StringBuilder output, CancellationToken cancellationToken)
    {
        foreach (var catalog in _catalogs)
        {
            await _crawler.RunAsync(catalog, output, cancellationToken);
        }
    }

    private void TryRestore(string root, string backupRoot)
    {
        foreach (var catalog in _catalogs)
        {
            _backup.Restore(root, catalog, backupRoot);
        }
    }

    private static string? FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "scripts")) &&
                Directory.Exists(Path.Combine(current.FullName, "data")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string ReadSummary(string root)
    {
        return $"Eden {ReadManifest(root, "eden-charplan")}; Blackthorn {ReadManifest(root, "blackthorn-charplan")}";
    }

    private static string ReadManifest(string root, string catalog)
    {
        var path = Path.Combine(root, "data", catalog, "generated", "manifest.json");
        if (!File.Exists(path)) return "not installed";
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var data = document.RootElement;
        var classes = data.TryGetProperty("classCount", out var classCount) ? classCount.ToString() : "?";
        var version = data.TryGetProperty("version", out var versionValue) ? $", version {versionValue.GetString()}" : string.Empty;
        var generated = data.TryGetProperty("generatedAt", out var generatedAt) ? generatedAt.GetString() : "?";
        return $"{classes} classes{version}, {generated}";
    }
}
