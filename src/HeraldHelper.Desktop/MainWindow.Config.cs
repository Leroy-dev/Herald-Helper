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
            new ConfigEntry { Key = CharacterSettingsKeys.SelectedCharacter(shard), Value = profile.CharacterName }
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
        RefreshSetupChecklist();
        OutputBox.Text = "Config reloaded.";
    }

    internal void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        _settingsController.Replace(_cfgEntries.Where(x => !string.IsNullOrWhiteSpace(x.Key)));
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        ReloadOverlaySettingsFromStore();
        _authController.ConfigureTimer();
        RefreshSetupChecklist();
        OutputBox.Text = "Config saved to database and runtime refreshed.";
    }

    internal void CustomUiFolderBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a UI package folder (game folder, ui\\custom, or a package folder)"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (ConfigView?.CustomUiFolderText is not null)
        {
            ConfigView.CustomUiFolderText.Text = dialog.FolderName;
        }
        SaveCustomUiFolder(dialog.FolderName);
    }

    internal void CustomUiFolderClear_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigView?.CustomUiFolderText is not null)
        {
            ConfigView.CustomUiFolderText.Text = string.Empty;
        }
        SaveCustomUiFolder(string.Empty);
    }

    internal void CustomUiFolder_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isBindingControls)
        {
            return;
        }

        var text = ConfigView?.CustomUiFolderText?.Text.Trim() ?? string.Empty;
        if (!string.Equals(ReadCustomUiFolder() ?? string.Empty, text, StringComparison.OrdinalIgnoreCase))
        {
            SaveCustomUiFolder(text);
        }
    }

    private string? ReadCustomUiFolder() =>
        _cfgEntries
            .FirstOrDefault(entry => entry.Key.Equals("customUiFolder", StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private void SaveCustomUiFolder(string folder)
    {
        _settingsController.Save([
            new ConfigEntry { Key = "customUiFolder", Value = folder }
        ]);
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        OutputBox.Text = string.IsNullOrWhiteSpace(folder)
            ? "Custom UI folder cleared — OCR auto-detects from the launcher."
            : $"Custom UI folder set: {folder} — {DescribeCustomUiPath(folder)}";
    }

    /// <summary>Echoes what the picked path resolves to so a wrong folder is
    /// obvious — the resolver accepts a package dir, a ui root, a game root,
    /// or a uimain.xml file.</summary>
    private static string DescribeCustomUiPath(string folder)
    {
        var full = Path.GetFullPath(folder.Trim().Trim('"'));

        if (File.Exists(full) && full.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(full).Equals("uimain.xml", StringComparison.OrdinalIgnoreCase)
                ? "manifest picked"
                : "warning — picked file isn't uimain.xml";
        }

        if (!Directory.Exists(full))
        {
            return "warning — folder doesn't exist";
        }

        if (File.Exists(Path.Combine(full, "uimain.xml")))
        {
            return "package dir (uimain.xml found)";
        }

        var uiRoot = Directory.Exists(Path.Combine(full, "ui"))
            ? Path.Combine(full, "ui")
            : null;
        var packages = 0;
        try
        {
            if ((uiRoot ?? full) is { } root && Directory.Exists(root))
            {
                packages = Directory.EnumerateDirectories(root)
                    .Count(d => File.Exists(Path.Combine(d, "uimain.xml")));
            }
        }
        catch (Exception)
        {
            return "warning — couldn't list that folder";
        }
        return packages > 0
            ? $"{packages} package(s) found under {(uiRoot is null ? "folder" : "ui\\")}"
            : "warning — no uimain.xml found under that folder";
    }

    private void ReloadShardAuthRows()
    {
        _shardAuthRows.Clear();
        var settings = _settingsController.LoadMap();
        var defaults = ShardAuthProfileResolver.DefaultSettings()
            .Where(e => !string.IsNullOrWhiteSpace(e.Value))
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);

        string Read(string key)
        {
            if (settings.TryGetValue(key, out var value) && value is not null)
            {
                return value;
            }
            return defaults.TryGetValue(key, out var fallback) ? fallback : string.Empty;
        }

        foreach (var shard in Enum.GetValues<ShardType>().Where(s => s != ShardType.Default))
        {
            var key = shard.ToString().ToLowerInvariant();
            _shardAuthRows.Add(new ShardAuthConfigRow
            {
                Shard = shard,
                Enabled = Read($"auth.{key}.enabled").Equals("true", StringComparison.OrdinalIgnoreCase),
                HubUrl = Read($"auth.{key}.hubUrl"),
                Domain = Read($"auth.{key}.domain"),
                CookieNames = Read($"auth.{key}.cookieNames"),
                RequiredCookies = Read($"auth.{key}.requiredCookies"),
                ValidateUrl = Read($"auth.{key}.validateUrl"),
                Session = string.IsNullOrWhiteSpace(Read($"auth.{key}.cookieHeader"))
                    ? "no session"
                    : "cookies stored"
            });
        }
    }

    internal void SaveShardAuth_Click(object sender, RoutedEventArgs e)
    {
        var updates = new List<ConfigEntry>();
        foreach (var row in _shardAuthRows)
        {
            var key = row.Shard.ToString().ToLowerInvariant();
            updates.Add(new ConfigEntry { Key = $"auth.{key}.enabled", Value = row.Enabled.ToString().ToLowerInvariant() });
            updates.Add(new ConfigEntry { Key = $"auth.{key}.hubUrl", Value = row.HubUrl.Trim() });
            updates.Add(new ConfigEntry { Key = $"auth.{key}.domain", Value = row.Domain.Trim() });
            updates.Add(new ConfigEntry { Key = $"auth.{key}.cookieNames", Value = row.CookieNames.Trim() });
            updates.Add(new ConfigEntry { Key = $"auth.{key}.requiredCookies", Value = row.RequiredCookies.Trim() });
            updates.Add(new ConfigEntry { Key = $"auth.{key}.validateUrl", Value = row.ValidateUrl.Trim() });
        }

        _settingsController.Save(updates);
        _authController.ConfigureTimer();
        ReloadEditorData();
        OutputBox.Text = "Shard auth settings saved.";
    }

    internal void ConservativeMode_Click(object sender, RoutedEventArgs e)
    {
        if (_isBindingControls || ConfigView?.ConservativeModeCheckbox is not { } checkbox)
        {
            return;
        }

        var enabled = checkbox.IsChecked == true;
        _settingsController.Save([
            new ConfigEntry { Key = "conservativeMode", Value = enabled ? "true" : "false" }
        ]);
        RebuildRuntimeFromFiles();
        RefreshSetupChecklist();
        OutputBox.Text = enabled
            ? "Conservative mode on — memory chat read, scrollback and live stats disabled."
            : "Conservative mode off — memory sources honored again.";
    }

    /// <summary>Re-runs the setup checklist — called on view switch, reload,
    /// save and conservative-mode toggles so the ✓/✗ states stay honest.</summary>
    internal void RefreshSetupChecklist()
    {
        if (ConfigView?.SetupChecklistList is null)
        {
            return;
        }

        var map = _settingsController.LoadMap();
        var runtime = AppRuntimeSettings.FromMap(map);
        var overlay = _overlaySettingsController.Load();
        var overlayElements = new[]
        {
            overlay.ShowTarget, overlay.ShowTimers, overlay.ShowCastBar, overlay.ShowResists,
            overlay.ShowGroup, overlay.ShowSelfCc, overlay.ShowPeel, overlay.ShowWorld
        }.Count(x => x);

        var catalogRoot = runtime.ShardType switch
        {
            ShardType.Eden => "eden-charplan",
            ShardType.Blackthorn => "blackthorn-charplan",
            _ => null
        };

        ConfigView.SetupChecklistList.ItemsSource = SetupChecklistBuilder.Build(new SetupChecklistInput(
            ConservativeMode: runtime.ConservativeMode,
            ChatMemReadEnabled: runtime.ChatMemReadEnabled,
            StatsMemReadEnabled: runtime.StatsMemReadEnabled,
            IsElevated: IsElevated(),
            HasChatRegion: runtime.ChatRegion is not null,
            HasWatchRegions: runtime.OcrWatchRegions.Count > 0,
            ChatLogAvailable: runtime.ChatLogCaptureEnabled,
            RelayAvailable: runtime.BlackthornRelayEnabled,
            CustomUiFolderResolved: CustomUiFolderResolved(runtime),
            CatalogsPresent: catalogRoot is not null &&
                             File.Exists(Path.Combine(AppContext.BaseDirectory, "data", catalogRoot, "generated", "manifest.json")),
            CharacterSelected: map.TryGetValue(CharacterSettingsKeys.SelectedCharacter(runtime.ShardType), out var character) &&
                               !string.IsNullOrWhiteSpace(character),
            OcrEngineAvailable: ProbeWindowsOcr(),
            OverlayElementsEnabled: overlayElements));
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static bool ProbeWindowsOcr()
    {
        try
        {
            return Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages() is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Explicit folder → must resolve to a real package; empty → the
    /// launcher for the current shard must have left its marker.</summary>
    private static bool CustomUiFolderResolved(AppRuntimeSettings runtime)
    {
        if (!string.IsNullOrWhiteSpace(runtime.CustomUiFolder))
        {
            return !DescribeCustomUiPath(runtime.CustomUiFolder).Contains("warning", StringComparison.OrdinalIgnoreCase);
        }

        return runtime.ShardType switch
        {
            ShardType.Eden => File.Exists(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "eden-launcher", "config.json")),
            ShardType.Blackthorn => File.Exists(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "com.blackthorn", "logs", "Blackthorn Launcher.log")),
            _ => false
        };
    }

    internal void ConfigFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        _configView?.Refresh();
    }

    private bool MatchesConfigFilter(object item)
    {
        if (item is not ConfigEntry entry)
        {
            return false;
        }

        var fragment = ConfigView?.ConfigFilterText?.Text;
        return string.IsNullOrWhiteSpace(fragment) ||
               entry.Key.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
               (entry.Value?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false);
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
        RefreshSetupChecklist();
        OutputBox.Text = "Backup imported and runtime refreshed.";
    }

}
