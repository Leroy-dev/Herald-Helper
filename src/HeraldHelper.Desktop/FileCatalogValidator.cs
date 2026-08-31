using System.IO;
using System.Text.Json;

namespace HeraldHelper.Desktop;

internal sealed class FileCatalogValidator : ICatalogValidator
{
    public void Validate(string root, string catalog)
    {
        var catalogRoot = Path.Combine(root, "data", catalog);
        var manifestPath = Path.Combine(catalogRoot, "generated", "manifest.json");
        var indexPath = Path.Combine(catalogRoot, "generated", "classes", "index.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        var classCount = manifest.RootElement.GetProperty("classCount").GetInt32();
        if (classCount <= 0 || index.RootElement.ValueKind != JsonValueKind.Array ||
            index.RootElement.GetArrayLength() != classCount)
        {
            throw new InvalidDataException($"{catalog} class index does not match its manifest.");
        }

        foreach (var entry in index.RootElement.EnumerateArray())
        {
            var slug = entry.GetProperty("slug").GetString();
            if (string.IsNullOrWhiteSpace(slug) ||
                !File.Exists(Path.Combine(catalogRoot, "generated", "classes", slug + ".json")))
            {
                throw new InvalidDataException($"{catalog} is missing the generated class file for '{slug}'.");
            }
        }
    }
}
