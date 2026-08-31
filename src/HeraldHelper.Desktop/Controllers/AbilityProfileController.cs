using System.Collections.ObjectModel;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Desktop.Controllers;

internal sealed class AbilityProfileController
{
    private readonly IAbilityProfileRepository _abilityProfileRepository;
    private readonly IAbilityRepository _abilityRepository;
    private readonly SettingsController _settingsController;
    private readonly ObservableCollection<AbilityEditorRow> _abilityEntries = [];

    public AbilityProfileController(
        IAbilityProfileRepository abilityProfileRepository,
        IAbilityRepository abilityRepository,
        SettingsController settingsController)
    {
        _abilityProfileRepository = abilityProfileRepository;
        _abilityRepository = abilityRepository;
        _settingsController = settingsController;
    }

    public ObservableCollection<AbilityEditorRow> AbilityEntries => _abilityEntries;

    public ShardType[] ServerItems { get; } = [ShardType.Default, ShardType.Eden, ShardType.Blackthorn];

    public ShardType Shard { get; private set; } = ShardType.Default;

    public string? Class { get; private set; }

    public string Character { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public bool IsClassEnabled { get; private set; }

    public IReadOnlyList<string> ClassItems { get; private set; } = Array.Empty<string>();

    public ShardType GetInitialShard(ShardType activeShard)
    {
        return SupportsAbilityProfiles(activeShard) ? activeShard : ShardType.Default;
    }

    public void RefreshClasses(ShardType shard)
    {
        Shard = SupportsAbilityProfiles(shard) ? shard : ShardType.Default;
        if (Shard == ShardType.Default)
        {
            Class = null;
            Character = string.Empty;
            IsClassEnabled = false;
            ClassItems = Array.Empty<string>();
            UpdateSummary();
            return;
        }

        var classes = AbilityProfileCatalog.GetClasses(Shard);
        var settings = _settingsController.LoadMap();
        Character = ReadOrDefault(
            settings,
            $"daoc.character.{Shard.ToString().ToLowerInvariant()}",
            string.Empty);
        var selectedClass = ReadOrDefault(
            settings,
            AbilityProfileClassSettingKey(Shard, Character),
            ReadOrDefault(settings, LegacyAbilityProfileClassSettingKey(Shard), string.Empty));
        Class = classes.FirstOrDefault(x =>
            string.Equals(x, selectedClass, StringComparison.OrdinalIgnoreCase));
        IsClassEnabled = true;
        ClassItems = classes;
    }

    public void SelectClass(string className)
    {
        Class = className;
        _settingsController.Save([
            new ConfigEntry
            {
                Key = AbilityProfileClassSettingKey(Shard, Character),
                Value = className
            }
        ]);
    }

    public void ReloadRows()
    {
        _abilityEntries.Clear();
        var entries = Shard != ShardType.Default && !string.IsNullOrWhiteSpace(Class)
            ? _abilityProfileRepository.LoadAbilityProfile(Shard, Character, Class!)
            : _abilityRepository.LoadAbilities();
        foreach (var entry in entries)
        {
            _abilityEntries.Add(entry);
        }

        UpdateSummary();
    }

    public void Save()
    {
        if (Shard != ShardType.Default && !string.IsNullOrWhiteSpace(Class))
        {
            _abilityProfileRepository.SaveAbilityProfile(Shard, Character, Class, _abilityEntries);
        }
        else
        {
            _abilityRepository.SaveAbilities(_abilityEntries);
        }
    }

    public AbilityEditorRow Add()
    {
        var row = new AbilityEditorRow
        {
            IsEnabled = true,
            Server = Shard == ShardType.Default
                ? string.Empty
                : Shard.ToString().ToLowerInvariant(),
            ClassName = Class ?? string.Empty,
            CharacterName = Character,
            SourceAbilityName = string.Empty,
            SourceEffectType = "s",
            Category = Shard == ShardType.Default ? string.Empty : "Custom",
            IsCustom = Shard != ShardType.Default
        };

        _abilityEntries.Add(row);
        UpdateSummary();
        return row;
    }

    public bool Remove(AbilityEditorRow row)
    {
        if (Shard != ShardType.Default && !row.IsCustom)
        {
            row.IsEnabled = false;
            UpdateSummary();
            return false;
        }

        _abilityEntries.Remove(row);
        UpdateSummary();
        return true;
    }

    public string GetSaveMessage()
    {
        return Shard == ShardType.Default
            ? "Manual abilities saved and parser refreshed."
            : $"Ability profile saved for {Shard} / {Class} and parser refreshed.";
    }

    public void UpdateSummary()
    {
        Summary = Shard == ShardType.Default
            ? $"Manual fallback: {_abilityEntries.Count} entries"
            : string.IsNullOrWhiteSpace(Class)
                ? "Choose a class to activate its profile."
                : $"{Shard} / {DisplayProfileCharacter(Character)} / {Class}: {_abilityEntries.Count(x => x.IsEnabled)} of {_abilityEntries.Count} enabled";
    }

    private static string ReadOrDefault(IReadOnlyDictionary<string, string> map, string key, string fallback)
    {
        return map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private static bool SupportsAbilityProfiles(ShardType shard)
    {
        return shard is ShardType.Eden or ShardType.Blackthorn;
    }

    private static string AbilityProfileClassSettingKey(ShardType shard, string characterName)
    {
        var characterKey = NormalizeSettingSegment(characterName);
        if (string.IsNullOrWhiteSpace(characterKey))
        {
            characterKey = "default";
        }

        return $"ability.profile.class.{shard.ToString().ToLowerInvariant()}.{characterKey}";
    }

    private static string LegacyAbilityProfileClassSettingKey(ShardType shard)
    {
        return $"ability.profile.class.{shard.ToString().ToLowerInvariant()}";
    }

    private static string NormalizeSettingSegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().ToLowerInvariant().Select(x => char.IsLetterOrDigit(x) ? x : '_').ToArray());
    }

    private static string DisplayProfileCharacter(string characterName)
    {
        return string.IsNullOrWhiteSpace(characterName) ? "Default character" : characterName;
    }
}
