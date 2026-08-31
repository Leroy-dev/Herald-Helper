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
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace HeraldHelper.Desktop;

public partial class MainWindow : Window
{
    private void DaocCharacterSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBindingControls || sender is not WpfComboBox combo || combo.Tag is not ShardType shard)
        {
            return;
        }

        if (combo.SelectedItem is not DaocCharacterProfile profile)
        {
            return;
        }

        _settingsController.Save([
            new ConfigEntry { Key = $"daoc.character.{shard.ToString().ToLowerInvariant()}", Value = profile.CharacterName }
        ]);

        if (_abilityProfileController.Shard == shard)
        {
            _abilityProfileController.RefreshClasses(shard);
            AbilityProfileServerCombo.SelectedItem = _abilityProfileController.Shard;
            AbilityProfileClassCombo.ItemsSource = _abilityProfileController.ClassItems;
            AbilityProfileClassCombo.SelectedItem = _abilityProfileController.Class;
            AbilityProfileClassCombo.IsEnabled = _abilityProfileController.IsClassEnabled;
            AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
            _abilityProfileController.ReloadRows();
        }

        if (_shardType == shard)
        {
            RebuildRuntimeFromFiles();
        }

        if (_daocCharacterSummaryBlocks.TryGetValue(shard, out var summary))
        {
            var ocrCount = _daocCharacterController.LoadOcrWindows(shard, profile.CharacterName).Count;
            summary.Text = $"{profile.SummaryText} | OCR Windows: {ocrCount}";
        }

        if (_daocWindowButtons.TryGetValue(shard, out var windowButton))
        {
            windowButton.IsEnabled = true;
        }
    }

    private void CharacterOcrWindows_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not ShardType shard)
        {
            return;
        }

        if (!_daocCharacterSelectors.TryGetValue(shard, out var selector) || selector.SelectedItem is not DaocCharacterProfile profile)
        {
            OutputBox.Text = $"Choose a character for {shard} first.";
            return;
        }

        var current = _daocCharacterController.LoadOcrWindows(shard, profile.CharacterName);
        var selected = DaocWindowSelectionWindow.Pick(this, profile, current);
        if (selected is null)
        {
            OutputBox.Text = $"OCR window selection cancelled for {profile.CharacterName}.";
            return;
        }

        _daocCharacterController.SaveOcrWindows(shard, profile.CharacterName, selected);
        _responseDiagnostics.Log($"[OCR] saved {selected.Count} windows for {profile.CharacterName} on {shard}.");
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        OutputBox.Text = $"OCR windows saved for {profile.CharacterName} ({selected.Count} windows).";
    }

    internal void ReloadConfig_Click(object sender, RoutedEventArgs e)
    {
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        ReloadOverlaySettingsFromStore();
        _authController.ConfigureTimer();
        OutputBox.Text = "Config reloaded.";
    }

    internal void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        _settingsController.Replace(_cfgEntries.Where(x => !string.IsNullOrWhiteSpace(x.Key)));
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        ReloadOverlaySettingsFromStore();
        _authController.ConfigureTimer();
        OutputBox.Text = "Config saved to database and runtime refreshed.";
    }

    internal void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export HeraldHelper Data",
            Filter = "JSON files (*.json)|*.json",
            FileName = $"heraldhelper-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _store.ExportJson(dialog.FileName);
        OutputBox.Text = $"Backup exported: {dialog.FileName}";
    }

    internal void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import HeraldHelper Data",
            Filter = "JSON files (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _store.ImportJson(dialog.FileName, replaceExisting: true);
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        ReloadOverlaySettingsFromStore();
        _authController.ConfigureTimer();
        OutputBox.Text = "Backup imported and runtime refreshed.";
    }

}
