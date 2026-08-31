using System.Text.Json;
using System.Text.Json.Serialization;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

internal sealed class BackupPayload
{
    [JsonPropertyName("backupFormatVersion")]
    public int BackupFormatVersion { get; set; }

    [JsonPropertyName("databaseMigrationVersion")]
    public int DatabaseMigrationVersion { get; set; }

    [JsonPropertyName("schemaVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SchemaVersion { get; set; }

    public DateTimeOffset ExportedUtc { get; set; }
    public bool SecretsExcluded { get; set; }
    public List<ConfigEntry> Settings { get; set; } = [];
    public List<AbilityEditorRow> Abilities { get; set; } = [];
    public List<CatalogEntryOverride>? CatalogEntryOverrides { get; set; }
    public List<AbilityProfileOverride>? AbilityProfileOverrides { get; set; }
    public List<CharacterStatsSnapshot>? CharacterStats { get; set; }

    // Kept for importing schema-4 backups.
    public List<AbilityEditorRow>? AbilityProfiles { get; set; }

    public int ResolvedBackupFormatVersion =>
        BackupFormatVersion > 0
            ? BackupFormatVersion
            : SchemaVersion ?? 0;

    public int ResolvedDatabaseMigrationVersion =>
        DatabaseMigrationVersion > 0
            ? DatabaseMigrationVersion
            : SchemaVersion ?? AppDataStore.CurrentDatabaseMigrationVersion;
}
