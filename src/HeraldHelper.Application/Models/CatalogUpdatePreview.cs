namespace HeraldHelper.Application.Models;

public sealed class CatalogUpdatePreview
{
    public string Catalog { get; set; } = string.Empty;
    public string? BeforeVersion { get; set; }
    public string? AfterVersion { get; set; }
    public List<string> Additions { get; set; } = [];
    public List<string> Removals { get; set; } = [];
    public List<string> Conflicts { get; set; } = [];
    public List<string> SchemaChanges { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string Summary { get; set; } = string.Empty;

    public bool HasChanges =>
        Additions.Count > 0
        || Removals.Count > 0
        || Conflicts.Count > 0
        || SchemaChanges.Count > 0
        || Warnings.Count > 0
        || !string.Equals(BeforeVersion, AfterVersion, StringComparison.OrdinalIgnoreCase);
}
