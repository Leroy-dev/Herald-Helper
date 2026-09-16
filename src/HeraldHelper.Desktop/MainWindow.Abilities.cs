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
    internal void ReloadAbilities_Click(object sender, RoutedEventArgs e)
    {
        ReloadEditorData();
        OutputBox.Text = "Abilities reloaded.";
    }

    internal void ReloadAbilityCatalogs_Click(object sender, RoutedEventArgs e)
    {
        AbilityProfileCatalog.Refresh();
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
        OutputBox.Text = _abilityProfileController.GetSaveMessage();
    }

    internal void AddAbility_Click(object sender, RoutedEventArgs e)
    {
        _abilityProfileController.Add();
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
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
            $"{hit.AbilityName} → {hit.TargetName} [{hit.EffectType} {hit.BaseDurationSeconds}s]{(hit.LandedSuccessfully ? string.Empty : " (failed)")}"));

        AbilitiesView!.AbilityTestResult.Text = parts.Count == 0
            ? "no match — the line names none of the enabled abilities"
            : string.Join("   ", parts);
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

        _abilityProfileController.SelectClass(className);
        ReloadEditorData();
        if (_abilityProfileController.Shard == _shardType)
        {
            RebuildRuntimeFromFiles();
        }

        OutputBox.Text = $"Active ability profile: {_abilityProfileController.Shard} / {_abilityProfileController.Character} / {className}.";
    }

    private async void RefreshAuthCurrent_Click(object sender, RoutedEventArgs e)
    {
        await _authController.RefreshCurrentAsync(_shardType);
    }

    private async void RefreshAuthAll_Click(object sender, RoutedEventArgs e)
    {
        await _authController.RefreshAllAsync();
    }

}
