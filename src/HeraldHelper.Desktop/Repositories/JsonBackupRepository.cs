using System.IO;
using System.Text;
using System.Text.Json;
using HeraldHelper.Desktop;
using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Desktop.Repositories;

internal sealed class JsonBackupRepository : IBackupRepository
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IAbilityRepository _abilitiesRepository;
    private readonly ICatalogOverrideRepository _catalogOverrideRepository;
    private readonly IAbilityProfileRepository _abilityProfileRepository;
    private readonly ICharacterStatsRepository _characterStatsRepository;
    private readonly Func<int> _getDatabaseMigrationVersion;

    public JsonBackupRepository(
        ISettingsRepository settingsRepository,
        IAbilityRepository abilitiesRepository,
        ICatalogOverrideRepository catalogOverrideRepository,
        IAbilityProfileRepository abilityProfileRepository,
        ICharacterStatsRepository characterStatsRepository,
        Func<int> getDatabaseMigrationVersion)
    {
        _settingsRepository = settingsRepository;
        _abilitiesRepository = abilitiesRepository;
        _catalogOverrideRepository = catalogOverrideRepository;
        _abilityProfileRepository = abilityProfileRepository;
        _characterStatsRepository = characterStatsRepository;
        _getDatabaseMigrationVersion = getDatabaseMigrationVersion;
    }

    public void ExportJson(string outputPath)
    {
        var payload = new BackupPayload
        {
            BackupFormatVersion = AppDataStore.CurrentBackupFormatVersion,
            DatabaseMigrationVersion = _getDatabaseMigrationVersion(),
            ExportedUtc = DateTimeOffset.UtcNow,
            SecretsExcluded = true,
            Settings = _settingsRepository.LoadConfigEntries().Where(x => !SqliteSettingsRepository.IsSensitiveKey(x.Key)).ToList(),
            Abilities = _abilitiesRepository.LoadAbilities(),
            CatalogEntryOverrides = _catalogOverrideRepository.LoadCatalogEntryOverrides().Values.ToList(),
            AbilityProfileOverrides = _abilityProfileRepository.LoadAbilityProfileOverrides(),
            CharacterStats = _characterStatsRepository.LoadAllCharacterStats()
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    public void ImportJson(string inputPath, bool replaceExisting = true)
    {
        var json = File.ReadAllText(inputPath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Selected backup file is empty.");
        }

        BackupPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<BackupPayload>(json) ?? new BackupPayload();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Selected backup file is not valid HeraldHelper JSON.", ex);
        }

        var backupFormatVersion = payload.ResolvedBackupFormatVersion;
        var databaseMigrationVersion = payload.ResolvedDatabaseMigrationVersion;

        if (backupFormatVersion > AppDataStore.CurrentBackupFormatVersion)
        {
            throw new InvalidOperationException(
                $"Backup format version {backupFormatVersion} is newer than the supported version {AppDataStore.CurrentBackupFormatVersion}.");
        }

        if (databaseMigrationVersion > AppDataStore.CurrentDatabaseMigrationVersion)
        {
            throw new InvalidOperationException(
                $"Database migration version {databaseMigrationVersion} is newer than the supported version {AppDataStore.CurrentDatabaseMigrationVersion}.");
        }

        if (replaceExisting)
        {
            var restoredSettings = payload.Settings;
            if (payload.SecretsExcluded)
            {
                restoredSettings = _settingsRepository.LoadConfigEntries()
                    .Where(x => SqliteSettingsRepository.IsSensitiveKey(x.Key))
                    .Concat(payload.Settings.Where(x => !SqliteSettingsRepository.IsSensitiveKey(x.Key)))
                    .ToList();
            }

            _settingsRepository.SaveSettings(restoredSettings);
            _abilitiesRepository.SaveAbilities(payload.Abilities);
            if (payload.CatalogEntryOverrides is not null)
            {
                _catalogOverrideRepository.DeleteAllCatalogEntryOverrides();
                foreach (var entryOverride in payload.CatalogEntryOverrides)
                {
                    _catalogOverrideRepository.SaveCatalogEntryOverride(entryOverride);
                }
            }

            if (payload.AbilityProfileOverrides is not null)
            {
                _abilityProfileRepository.DeleteAllAbilityProfiles();
                _abilityProfileRepository.RestoreAbilityProfileOverrides(payload.AbilityProfileOverrides);
            }
            else if (payload.AbilityProfiles is not null)
            {
                _abilityProfileRepository.DeleteAllAbilityProfiles();
                RestoreLegacyAbilityProfiles(payload.AbilityProfiles);
            }

            if (payload.CharacterStats is not null)
            {
                _characterStatsRepository.DeleteAllCharacterStats();
                foreach (var stats in payload.CharacterStats)
                {
                    _characterStatsRepository.SaveCharacterStats(stats);
                }
            }

            return;
        }

        var mergedSettings = _settingsRepository.LoadConfigEntries()
            .Concat(payload.Settings)
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();
        _settingsRepository.SaveSettings(mergedSettings);

        var mergedAbilities = _abilitiesRepository.LoadAbilities()
            .Concat(payload.Abilities)
            .ToList();
        _abilitiesRepository.SaveAbilities(mergedAbilities);

        if (payload.CatalogEntryOverrides is not null)
        {
            foreach (var entryOverride in payload.CatalogEntryOverrides)
            {
                _catalogOverrideRepository.SaveCatalogEntryOverride(entryOverride);
            }
        }

        if (payload.AbilityProfileOverrides is not null)
        {
            _abilityProfileRepository.RestoreAbilityProfileOverrides(payload.AbilityProfileOverrides);
        }
        else if (payload.AbilityProfiles is not null)
        {
            RestoreLegacyAbilityProfiles(payload.AbilityProfiles);
        }

        if (payload.CharacterStats is not null)
        {
            foreach (var stats in payload.CharacterStats)
            {
                _characterStatsRepository.SaveCharacterStats(stats);
            }
        }
    }

    private void RestoreLegacyAbilityProfiles(IEnumerable<AbilityEditorRow> rows)
    {
        foreach (var group in rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Server) && !string.IsNullOrWhiteSpace(x.ClassName))
            .GroupBy(x => $"{x.Server}|{x.ClassName}", StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            if (Enum.TryParse<ShardType>(first.Server, true, out var shard))
            {
                _abilityProfileRepository.SaveAbilityProfile(shard, first.CharacterName, first.ClassName, group);
            }
        }
    }
}
