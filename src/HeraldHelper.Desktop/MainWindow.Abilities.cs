using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HeraldHelper.Application.Services;
using HeraldHelper.Desktop.Controllers;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Desktop.Views;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Auth;
using HeraldHelper.Infrastructure.Capture;
using HeraldHelper.Infrastructure.Composition;
using HeraldHelper.Infrastructure.Configuration;
using HeraldHelper.Infrastructure.Overlay;
using HeraldHelper.Infrastructure.Parsing;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace HeraldHelper.Desktop;

public partial class MainWindow : Window
{
    internal void MarkAbilitiesDirty()
    {
        _abilityProfileController.MarkDirty();
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary + "  (unsaved)";
    }

    /// <summary>Returns false when the user chose Cancel — edits stay loaded.</summary>
    private bool ConfirmDiscardAbilityEdits()
    {
        if (!_abilityProfileController.IsDirty)
        {
            return true;
        }

        var choice = System.Windows.MessageBox.Show(
            this,
            "The ability profile has unsaved changes.\n\nSave before continuing?",
            "Unsaved Abilities",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        switch (choice)
        {
            case MessageBoxResult.Yes:
                _abilityProfileController.Save();
                return true;
            case MessageBoxResult.No:
                return true;
            default:
                return false;
        }
    }

    internal void ReloadAbilities_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardAbilityEdits())
        {
            return;
        }
        ReloadEditorData();
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
        OutputBox.Text = "Abilities reloaded.";
    }

    internal void ReloadAbilityCatalogs_Click(object sender, RoutedEventArgs e)
    {
        AbilityProfileCatalog.Refresh();
        _abilityIconIndex.Invalidate();
        _abilityProfileController.RefreshClasses(_abilityProfileController.Shard);
        AbilityProfileServerCombo.SelectedItem = _abilityProfileController.Shard;
        AbilityProfileClassCombo.ItemsSource = _abilityProfileController.ClassItems;
        AbilityProfileClassCombo.SelectedItem = _abilityProfileController.Class;
        AbilityProfileClassCombo.IsEnabled = _abilityProfileController.IsClassEnabled;
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        OutputBox.Text = "Local Eden and Blackthorn catalogs reloaded.";
    }

    internal async void UpdateCatalogsOnline_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = System.Windows.MessageBox.Show(
            this,
            "Download and validate fresh Eden and Blackthorn catalogs? Existing snapshots are replaced only after validation succeeds.",
            "Update Catalogs",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            OutputBox.Text = "Updating catalogs...";
            var progress = new Progress<string>(message => OutputBox.Text = message);
            var result = await _services.GetRequiredService<CatalogUpdateService>().UpdateAsync(CancellationToken.None, progress);
            _edenBrowserWindow?.Close();
            // The catalog caches parse ~30MB of class JSON on first access —
            // warm them off-thread so the runtime loop and UI never stall.
            var selectedClass = _abilityProfileController.Class;
            await Task.Run(() =>
            {
                AbilityProfileCatalog.Refresh();
                _abilityIconIndex.Invalidate();
                AbilityProfileCatalog.GetClasses(ShardType.Eden);
                AbilityProfileCatalog.GetClasses(ShardType.Blackthorn);
                AbilityProfileCatalog.GetProfile(ShardType.Eden, selectedClass);
                AbilityProfileCatalog.GetProfile(ShardType.Blackthorn, selectedClass);
            });
            RebuildRuntimeFromFiles();
            _abilityProfileController.ReloadRows();
            AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
            OutputBox.Text = result;
        }
        catch (Exception ex)
        {
            OutputBox.Text = $"Catalog update failed; previous snapshots were kept. {ex.Message}";
        }
    }

    internal void SaveAbilities_Click(object sender, RoutedEventArgs e)
    {
        _abilityProfileController.Save();
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
        OutputBox.Text = _abilityProfileController.GetSaveMessage();
    }

    internal void AddAbility_Click(object sender, RoutedEventArgs e)
    {
        _abilityProfileController.Add();
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
    }

    internal void AddAbilityFromCatalog_Click(object sender, RoutedEventArgs e)
    {
        var shard = _abilityProfileController.Shard;
        if (shard is not (ShardType.Eden or ShardType.Blackthorn))
        {
            OutputBox.Text = "Pick an Eden or Blackthorn ability profile first — catalog picking needs a shard.";
            return;
        }

        var entries = _abilityIconIndex.GetEntries(shard);
        if (entries.Count == 0)
        {
            OutputBox.Text = $"No {shard} catalog loaded — use 'Update Catalogs Online' first.";
            return;
        }

        var picked = AbilityCatalogPickerWindow.Pick(this, entries, _abilityProfileController.Class);
        if (picked is null)
        {
            return;
        }

        _abilityProfileController.AddFromCatalog(picked);
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
        OutputBox.Text = $"Added {picked.Name} — confirm duration and effect type, then save.";
    }

    internal void RemoveAbility_Click(object sender, RoutedEventArgs e)
    {
        if (AbilitiesGrid.SelectedItem is not AbilityEditorRow selected)
        {
            return;
        }

        _abilityProfileController.Remove(selected);
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
    }

    internal void AbilityProfileServerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBindingControls || AbilityProfileServerCombo.SelectedItem is not ShardType shard)
        {
            return;
        }

        if (!ConfirmDiscardAbilityEdits())
        {
            _isBindingControls = true;
            try { AbilityProfileServerCombo.SelectedItem = _abilityProfileController.Shard; }
            finally { _isBindingControls = false; }
            return;
        }

        _abilityProfileController.RefreshClasses(shard);
        AbilityProfileServerCombo.SelectedItem = _abilityProfileController.Shard;
        AbilityProfileClassCombo.ItemsSource = _abilityProfileController.ClassItems;
        AbilityProfileClassCombo.SelectedItem = _abilityProfileController.Class;
        AbilityProfileClassCombo.IsEnabled = _abilityProfileController.IsClassEnabled;
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
        _abilityProfileController.ReloadRows();
    }

    internal void AbilityTestLine_Click(object sender, RoutedEventArgs e)
    {
        var text = AbilitiesView?.AbilityTestText?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var abilities = _abilityProfileController.AbilityEntries
            .Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.AbilityName))
            .Select(row => new AbilityDefinition(
                row.AbilityName.Trim(),
                row.SkillCode.Trim().ToLowerInvariant(),
                Math.Max(1, row.DurationSeconds),
                AbilitiesChatEventParser.ParseEffectTypeCode(row.EffectType),
                row.Aliases
                    .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();

        var parser = new AbilitiesChatEventParser(abilities);
        var result = parser.Parse(text, fallbackTargetName: "TestTarget");

        var parts = new List<string>();
        if (result.TargetEvent is { } target)
        {
            parts.Add($"target={target.Name}");
        }
        if (result.CastEvent is { } cast)
        {
            parts.Add($"cast={cast.EventType}:{cast.SpellName}");
        }
        parts.AddRange(result.AbilityHits.Select(hit =>
        {
            if (!hit.LandedSuccessfully)
            {
                return $"{hit.AbilityName} → {hit.TargetName} [{hit.EffectType} {hit.BaseDurationSeconds}s] (failed)";
            }

            var targetClass = TryGetCachedTargetClass(hit.TargetName);
            var immunity = CcImmunityTracker.PreviewImmunitySeconds(hit, targetClass, _resistPercent);
            var classNote = targetClass is null ? string.Empty : $" ({targetClass})";
            return $"{hit.AbilityName} → {hit.TargetName}{classNote} [{hit.EffectType} {hit.BaseDurationSeconds}s → imm {immunity}s]";
        }));

        if (parts.Count == 0)
        {
            // Check disabled rows too — "no match because it's off" is the most
            // common profile mistake and otherwise invisible.
            var disabledParser = new AbilitiesChatEventParser(_abilityProfileController.AbilityEntries
                .Where(x => !x.IsEnabled && !string.IsNullOrWhiteSpace(x.AbilityName))
                .Select(row => new AbilityDefinition(
                    row.AbilityName.Trim(),
                    row.SkillCode.Trim().ToLowerInvariant(),
                    Math.Max(1, row.DurationSeconds),
                    AbilitiesChatEventParser.ParseEffectTypeCode(row.EffectType)))
                .ToList());
            var disabledResult = disabledParser.Parse(text, fallbackTargetName: "TestTarget");
            AbilitiesView!.AbilityTestResult.Text = disabledResult.AbilityHits.Count > 0
                ? $"matches only DISABLED abilit{(disabledResult.AbilityHits.Count == 1 ? "y" : "ies")}: {string.Join(", ", disabledResult.AbilityHits.Select(h => h.AbilityName).Distinct())}"
                : "no match — the line names none of the enabled abilities";
            return;
        }

        AbilitiesView!.AbilityTestResult.Text = string.Join("   ", parts);
    }

    /// <summary>The immunity preview is only as good as the det-class lookup —
    /// pull the cached herald profile when the name is a known player.</summary>
    private string? TryGetCachedTargetClass(string targetName)
    {
        try
        {
            var profile = _store.Load(_shardType, targetName);
            return profile?.Class;
        }
        catch
        {
            return null;
        }
    }

    internal void AbilityFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        _abilitiesView?.Refresh();
    }

    private bool MatchesAbilityFilter(object item)
    {
        if (item is not AbilityEditorRow row)
        {
            return false;
        }

        var fragment = AbilitiesView?.AbilityFilterText?.Text;
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return true;
        }

        return row.AbilityName.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
               row.Category.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
               row.Aliases.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
               row.EffectType.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
               row.SkillCode.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
               (row.Level?.ToString().Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    internal void AbilityProfileClassCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBindingControls || _abilityProfileController.Shard == ShardType.Default ||
            AbilityProfileClassCombo.SelectedItem is not string className)
        {
            return;
        }

        if (!ConfirmDiscardAbilityEdits())
        {
            _isBindingControls = true;
            try { AbilityProfileClassCombo.SelectedItem = _abilityProfileController.Class; }
            finally { _isBindingControls = false; }
            return;
        }

        _abilityProfileController.SelectClass(className);
        ReloadEditorData();
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
        if (_abilityProfileController.Shard == _shardType)
        {
            RebuildRuntimeFromFiles();
        }

        OutputBox.Text = $"Active ability profile: {_abilityProfileController.Shard} / {_abilityProfileController.Character} / {className}.";
    }

    private async void RefreshAuthCurrent_Click(object sender, RoutedEventArgs e)
    {
        var ok = await _authController.RefreshCurrentAsync(_shardType);
        UpdateAuthSessionColumn(_shardType, ok);
    }

    private async void RefreshAuthAll_Click(object sender, RoutedEventArgs e)
    {
        var refreshed = await _authController.RefreshAllAsync();
        foreach (var shard in Enum.GetValues<ShardType>().Where(s => s != ShardType.Default))
        {
            UpdateAuthSessionColumn(shard, refreshed.Contains(shard.ToString()));
        }
    }

    private void UpdateAuthSessionColumn(ShardType shard, bool ok)
    {
        var row = _shardAuthRows.FirstOrDefault(r => r.Shard == shard);
        if (row is null)
        {
            return;
        }

        row.Session = ok
            ? $"ok {DateTime.Now:HH:mm}"
            : "failed — sign in via Browser";
        ConfigView?.ShardAuthGrid?.Items.Refresh();
    }

}
