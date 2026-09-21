using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Parsing;
using Microsoft.Data.Sqlite;

namespace HeraldHelper.Desktop;

public sealed class AppDataStore : ITargetProfileCache, ISettingsRepository, ICharacterStatsRepository, IAbilityRepository, ICatalogOverrideRepository, IAbilityProfileRepository, IBackupRepository
{
    public const int CurrentDatabaseMigrationVersion = SqliteDatabaseMigrationsRepository.CurrentDatabaseMigrationVersion;
    public const int CurrentBackupFormatVersion = 1;

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSettingsRepository _settingsRepository;
    private readonly SqliteCharacterStatsRepository _characterStatsRepository;
    private readonly SqliteAbilitiesRepository _abilitiesRepository;
    private readonly SqliteTargetProfileCache _targetProfileCache;
    private readonly SqliteCatalogOverrideRepository _catalogOverrideRepository;
    private readonly SqliteAbilityProfileRepository _abilityProfileRepository;
    private readonly JsonBackupRepository _backupRepository;
    private readonly SqliteDatabaseMigrationsRepository _migrationsRepository;

    public AppDataStore(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var connectionString = $"Data Source={databasePath}";
        _connectionFactory = new SqliteConnectionFactory(connectionString);
        _settingsRepository = new SqliteSettingsRepository(_connectionFactory);
        _characterStatsRepository = new SqliteCharacterStatsRepository(_connectionFactory);
        _abilitiesRepository = new SqliteAbilitiesRepository(_connectionFactory);
        _targetProfileCache = new SqliteTargetProfileCache(_connectionFactory);
        _catalogOverrideRepository = new SqliteCatalogOverrideRepository(_connectionFactory);
        _abilityProfileRepository = new SqliteAbilityProfileRepository(_connectionFactory);
        _migrationsRepository = new SqliteDatabaseMigrationsRepository(_connectionFactory);
        _backupRepository = new JsonBackupRepository(
            this,
            this,
            this,
            this,
            this,
            _migrationsRepository.GetDatabaseMigrationVersion);
    }

    public void Initialize()
    {
        _migrationsRepository.MigrateDatabase();
        // Key-level settings migrations run after the schema — idempotent
        // renames so readers never need legacy fallbacks.
        Services.LegacySettingsMigrator.Migrate(_settingsRepository);
    }

    public int GetDatabaseMigrationVersion()
    {
        return _migrationsRepository.GetDatabaseMigrationVersion();
    }

    public Dictionary<string, string> LoadSettingsMap()
    {
        return _settingsRepository.LoadSettingsMap();
    }

    public List<ConfigEntry> LoadConfigEntries()
    {
        return _settingsRepository.LoadConfigEntries();
    }

    public void SaveSettings(IEnumerable<ConfigEntry> entries)
    {
        _settingsRepository.SaveSettings(entries);
    }

    public List<AbilityEditorRow> LoadAbilities()
    {
        return _abilitiesRepository.LoadAbilities();
    }

    public void SaveAbilities(IEnumerable<AbilityEditorRow> rows)
    {
        _abilitiesRepository.SaveAbilities(rows);
    }

    public List<AbilityEditorRow> LoadAbilityProfile(ShardType shard, string characterName, string className)
    {
        return _abilityProfileRepository.LoadAbilityProfile(shard, characterName, className);
    }

    public void SaveAbilityProfile(
        ShardType shard,
        string characterName,
        string className,
        IEnumerable<AbilityEditorRow> rows)
    {
        _abilityProfileRepository.SaveAbilityProfile(shard, characterName, className, rows);
    }

    public List<AbilityProfileOverride> LoadAbilityProfileOverrides()
    {
        return _abilityProfileRepository.LoadAbilityProfileOverrides();
    }

    public void DeleteAllAbilityProfiles()
    {
        _abilityProfileRepository.DeleteAllAbilityProfiles();
    }

    public void RestoreAbilityProfileOverrides(IEnumerable<AbilityProfileOverride> values)
    {
        _abilityProfileRepository.RestoreAbilityProfileOverrides(values);
    }

    public CharacterStatsSnapshot? LoadCharacterStats(ShardType shard, string characterName)
    {
        return _characterStatsRepository.LoadCharacterStats(shard, characterName);
    }

    public void SaveCharacterStats(CharacterStatsSnapshot stats)
    {
        _characterStatsRepository.SaveCharacterStats(stats);
    }

    public TargetProfile? Load(ShardType shardType, string targetName)
    {
        return _targetProfileCache.Load(shardType, targetName);
    }

    public void Save(ShardType shardType, TargetProfile profile)
    {
        _targetProfileCache.Save(shardType, profile);
    }

    public void Delete(ShardType shardType, string targetName)
    {
        _targetProfileCache.Delete(shardType, targetName);
    }

    public IReadOnlyList<TargetProfileRow> SearchTargetProfiles(string? server, string? nameFragment, int limit = 200)
    {
        return _targetProfileCache.Search(server, nameFragment, limit);
    }

    public List<CharacterStatsSnapshot> LoadAllCharacterStats()
    {
        return _characterStatsRepository.LoadAllCharacterStats();
    }

    public bool IsEmpty()
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT 
              (SELECT COUNT(*) FROM settings) AS settings_count,
              (SELECT COUNT(*) FROM abilities) AS abilities_count;
            """;
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return true;
        }

        var settingsCount = reader.GetInt32(0);
        var abilitiesCount = reader.GetInt32(1);
        return settingsCount == 0 && abilitiesCount == 0;
    }

    public IReadOnlyDictionary<string, CatalogEntryOverride> LoadCatalogEntryOverrides()
    {
        return _catalogOverrideRepository.LoadCatalogEntryOverrides();
    }

    public void SaveCatalogEntryOverride(CatalogEntryOverride entryOverride)
    {
        _catalogOverrideRepository.SaveCatalogEntryOverride(entryOverride);
    }

    public void DeleteCatalogEntryOverride(string entryKey)
    {
        _catalogOverrideRepository.DeleteCatalogEntryOverride(entryKey);
    }

    public void ExportJson(string outputPath)
    {
        _backupRepository.ExportJson(outputPath);
    }

    public void ImportJson(string inputPath, bool replaceExisting = true)
    {
        _backupRepository.ImportJson(inputPath, replaceExisting);
    }

    public void DeleteAllCatalogEntryOverrides()
    {
        _catalogOverrideRepository.DeleteAllCatalogEntryOverrides();
    }

    public void DeleteAllCharacterStats()
    {
        _characterStatsRepository.DeleteAllCharacterStats();
    }


}
