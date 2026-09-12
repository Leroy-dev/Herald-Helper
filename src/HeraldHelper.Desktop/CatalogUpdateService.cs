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
        ICatalogCrawler crawler,
        ICatalogValidator validator,
        ICatalogBackup backup,
        string? root = null)
    {
        _root = root;
        _crawler = crawler;
        _validator = validator;
        _backup = backup;
        _catalogs = ["eden-charplan", "blackthorn-charplan"];
    }

    public async Task<string> UpdateAsync(CancellationToken cancellationToken)
    {
        var root = ResolveRoot();
        var before = ReadSummary(root);
        return await ExecuteInTransactionAsync(
            root,
            "update",
            restoreOnSuccess: false,
            ctx => Task.FromResult(
                $"Catalog update completed.\nBefore: {before}\nAfter: {ReadSummary(ctx.Root)}\n\n{ctx.Output}"),
            cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogUpdatePreview>> PreviewAsync(CancellationToken cancellationToken)
    {
        var root = ResolveRoot();
        return await ExecuteInTransactionAsync(
            root,
            "preview",
            restoreOnSuccess: true,
            ctx => Task.FromResult<IReadOnlyList<CatalogUpdatePreview>>(
                _catalogs
                    .Select(catalog => CatalogUpdatePreviewBuilder.Build(
                        catalog,
                        Path.Combine(ctx.BackupRoot, catalog),
                        Path.Combine(ctx.Root, "data", catalog)))
                    .ToList()),
            cancellationToken);
    }

    private string ResolveRoot()
    {
        return _root ?? FindProjectRoot() ?? throw new InvalidOperationException("Could not locate the HeraldHelper scripts directory.");
    }

    private sealed record TransactionContext(string Root, string BackupRoot, StringBuilder Output);

    private async Task<T> ExecuteInTransactionAsync<T>(
        string root,
        string operation,
        bool restoreOnSuccess,
        Func<TransactionContext, Task<T>> complete,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var backupRoot = Path.Combine(root, "tmp", $"catalog-{operation}-backup-{Guid.NewGuid():N}");
        var deleteBackup = true;

        try
        {
            foreach (var catalog in _catalogs)
            {
                _backup.Backup(root, catalog, backupRoot);
            }

            foreach (var catalog in _catalogs)
            {
                await _crawler.RunAsync(catalog, output, cancellationToken);
            }

            foreach (var catalog in _catalogs)
            {
                _validator.Validate(root, catalog);
            }

            var result = await complete(new TransactionContext(root, backupRoot, output));

            if (restoreOnSuccess)
            {
                TryRestore(root, backupRoot);
            }

            return result;
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
                    $"Catalog {operation} failed and rollback also failed. Recovery files remain in {backupRoot}.",
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
