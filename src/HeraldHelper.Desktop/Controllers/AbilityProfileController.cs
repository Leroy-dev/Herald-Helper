using System.Collections.ObjectModel;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Configuration;
using HeraldHelper.Infrastructure.Parsing;

namespace HeraldHelper.Desktop.Controllers;

internal sealed class AbilityProfileController
{
    private readonly IAbilityProfileRepository _abilityProfileRepository;
    private readonly IAbilityRepository _abilityRepository;
    private readonly SettingsController _settingsController;
    private readonly AbilityIconIndex _abilityIconIndex;
    private readonly IconImageLoader _iconLoader = new();
    private readonly ObservableCollection<AbilityEditorRow> _abilityEntries = [];

    public AbilityProfileController(
        IAbilityProfileRepository abilityProfileRepository,
        IAbilityRepository abilityRepository,
        SettingsController settingsController,
        AbilityIconIndex abilityIconIndex)
    {
        _abilityProfileRepository = abilityProfileRepository;
        _abilityRepository = abilityRepository;
        _settingsController = settingsController;
        _abilityIconIndex = abilityIconIndex;
    }

    public ObservableCollection<AbilityEditorRow> AbilityEntries => _abilityEntries;

    /// <summary>Grid edits or add/remove happened since the last save/reload —
    /// switching shard/class or reloading would lose them.</summary>
    public bool IsDirty { get; private set; }

    public void MarkDirty() => IsDirty = true;

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
            CharacterSettingsKeys.SelectedCharacter(Shard),
            string.Empty);
        var selectedClass = ReadOrDefault(
            settings,
            CharacterSettingsKeys.AbilityProfileClass(Shard, Character),
            ReadOrDefault(settings, CharacterSettingsKeys.DefaultAbilityProfileClass(Shard), string.Empty));
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
                Key = CharacterSettingsKeys.AbilityProfileClass(Shard, Character),
                Value = className
            }
        ]);
    }

    public void ReloadRows()
    {
        _abilityEntries.Clear();
        IsDirty = false;
        var entries = Shard != ShardType.Default && !string.IsNullOrWhiteSpace(Class)
            ? _abilityProfileRepository.LoadAbilityProfile(Shard, Character, Class!)
            : _abilityRepository.LoadAbilities();
        foreach (var entry in entries)
        {
            entry.EffectType = NormalizeEffectCode(entry.EffectType);
            ResolveRowIcon(entry);
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
        IsDirty = false;
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

        ResolveRowIcon(row);
        _abilityEntries.Add(row);
        IsDirty = true;
        UpdateSummary();
        return row;
    }

    /// <summary>Adds a catalog entry as a custom row — exact catalog name is
    /// what the parser must match, category/level carry over, duration and
    /// effect stay at their defaults for the user to confirm.</summary>
    public AbilityEditorRow AddFromCatalog(CatalogBrowserEntry entry)
    {
        var row = Add();
        row.AbilityName = entry.Name;
        row.Category = string.IsNullOrWhiteSpace(entry.Category) ? row.Category : entry.Category;
        row.Level = entry.Level;
        row.IconSource = entry.Icon is null ? null : _iconLoader.Load(entry.Icon);
        UpdateSummary();
        return row;
    }

    /// <summary>Canonical effect codes are m/s/r; older saves may carry
    /// long-form names which the grid dropdown can't display.</summary>
    private static string NormalizeEffectCode(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "m" or "mezz" or "mesmerize" or "mesmerise" => "m",
            "s" or "stun" => "s",
            "r" or "root" => "r",
            _ => "s"
        };
    }

    private void ResolveRowIcon(AbilityEditorRow row)
    {
        // Manual (Default) profiles still resolve icons — against whichever
        // shard the app is currently pointed at.
        var shard = Shard;
        if (shard == ShardType.Default &&
            _settingsController.LoadMap().TryGetValue("server", out var serverRaw) &&
            Enum.TryParse<ShardType>(serverRaw, true, out var parsed))
        {
            shard = parsed;
        }

        row.IconSource = _abilityIconIndex.Get(shard).TryGetValue(row.AbilityName, out var icon)
            ? _iconLoader.Load(icon)
            : null;
    }

    public bool Remove(AbilityEditorRow row)
    {
        if (Shard != ShardType.Default && !row.IsCustom)
        {
            row.IsEnabled = false;
            IsDirty = true;
            UpdateSummary();
            return false;
        }

        _abilityEntries.Remove(row);
        IsDirty = true;
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

    private static string DisplayProfileCharacter(string characterName)
    {
        return string.IsNullOrWhiteSpace(characterName) ? "Default character" : characterName;
    }
}
