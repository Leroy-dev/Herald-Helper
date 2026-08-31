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
    private GameLoopOrchestrator _orchestrator = null!;
    private DebugOverlayRenderer _overlay = null!;
    private DesktopOverlayRenderer _liveOverlay = null!;
    private ScreenCaptureOcrService _capture = null!;
    private readonly AppDataStore _store;
    private readonly SettingsController _settingsController;
    private readonly OverlaySettingsController _overlaySettingsController;
    private readonly RuntimeController _runtimeController;
    private readonly AuthController _authController;
    private readonly ResponseDiagnosticsBuffer _responseDiagnostics;
    private readonly IShardAuthRefreshService _authRefreshService;
    private readonly HttpClient _httpClient;
    private readonly DispatcherTimer _loopTimer;
    private readonly PaletteHelper _paletteHelper = new();
    private bool _tickInProgress;
    private bool _isBindingControls;
    private bool _isDarkTheme = true;
    private ScreenRegion? _chatRegion;
    private ShardType _shardType;
    private int _resistPercent;
    private OcrEngineMode _ocrEngineMode;
    private ShardType _abilityProfileShard = ShardType.Default;
    private string? _abilityProfileClass;
    private string _abilityProfileCharacter = string.Empty;
    private readonly ObservableCollection<ConfigEntry> _cfgEntries = [];
    private readonly ObservableCollection<AbilityEditorRow> _abilityEntries = [];
    private readonly DaocCharacterDiscoveryService _daocCharacterDiscovery = new();
    private readonly Dictionary<ShardType, WpfComboBox> _daocCharacterSelectors = new();
    private readonly Dictionary<ShardType, System.Windows.Controls.Button> _daocWindowButtons = new();
    private readonly Dictionary<ShardType, TextBlock> _daocCharacterSummaryBlocks = new();
    private IReadOnlyDictionary<ShardType, IReadOnlyList<DaocCharacterProfile>> _daocCharacterProfiles = new Dictionary<ShardType, IReadOnlyList<DaocCharacterProfile>>();
    private AppRuntimeSettings _runtimeSettings = new(null, ShardType.Default, 0, OcrEngineMode.Adaptive, [], true, true, true, true);
    private OverlaySnapshot? _lastOverlaySnapshot;
    private DataBrowserWindow? _edenBrowserWindow;

    public MainWindow()
    {
        InitializeComponent();
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeraldHelper",
            "heraldhelper.db");
        _store = new AppDataStore(dbPath);
        _store.Initialize();
        _settingsController = new SettingsController(_store);
        _overlaySettingsController = new OverlaySettingsController(_settingsController);
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        _responseDiagnostics = new ResponseDiagnosticsBuffer();
        _responseDiagnostics.LineAdded += OnResponseDiagnosticLineAdded;
        _liveOverlay = new DesktopOverlayRenderer(() => _settingsController.LoadMap());

        var legacyCfgPath = FindFilePath("cfg.ini");
        var legacyAbilitiesPath = FindFilePath("abilities.txt");
        LegacyTextImporter.ImportIfNeeded(_store, legacyCfgPath, legacyAbilitiesPath);
        _settingsController.EnsureDefaultAuthSettings();

        var profilesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeraldHelper",
            "browser-profiles");
        _authRefreshService = new PlaywrightShardAuthRefreshService(
            shard => ShardAuthProfileResolver.Resolve(_settingsController.LoadMap(), shard),
            OnAuthRefreshed,
            profilesRoot);

        _runtimeController = new RuntimeController(
            _store,
            _store,
            _store,
            _store,
            _store,
            _store,
            _httpClient,
            _authRefreshService,
            _liveOverlay,
            _responseDiagnostics);

        _authController = new AuthController(
            _settingsController,
            _authRefreshService,
            TimeSpan.FromMinutes(25),
            text => OutputBox.Text = text,
            () =>
            {
                ReloadEditorData();
                RebuildRuntimeFromFiles();
                ReloadOverlaySettingsFromStore();
                return;
            });

        _loopTimer = new DispatcherTimer();
        _loopTimer.Interval = TimeSpan.FromMilliseconds(350);
        _loopTimer.Tick += async (_, _) => await TickOnceAsync();
        _authController.ConfigureTimer();

        RebuildRuntimeFromFiles();
        ConfigGrid.ItemsSource = _cfgEntries;
        AbilitiesGrid.ItemsSource = _abilityEntries;
        InitializeAbilityProfileControls();
        ReloadEditorData();
        LoadDaocCharacterProfiles();
        RepairInvalidSavedCustomWindowRegions();
        BuildDaocCharacterSelectorUi();
        ReloadOverlaySettingsFromStore();
        LoadThemeSetting();
        OutputBox.Text = "Ready.";
    }

    protected override void OnClosed(EventArgs e)
    {
        _loopTimer.Stop();
        _authController.AuthRefreshTimer.Stop();
        _responseDiagnostics.LineAdded -= OnResponseDiagnosticLineAdded;
        _orchestrator?.Dispose();
        _liveOverlay?.Dispose();
        _httpClient.Dispose();
        base.OnClosed(e);
    }

    private void RebuildRuntimeFromFiles()
    {
        _runtimeController.Rebuild(
            _settingsController.LoadMap(),
            () => Dispatcher.BeginInvoke(UpdateRegionText));
        _orchestrator = _runtimeController.Orchestrator!;
        _overlay = _runtimeController.DebugOverlay!;
        _runtimeSettings = _runtimeController.RuntimeSettings;
        _capture = _runtimeController.Capture!;
        _chatRegion = _runtimeSettings.ChatRegion;
        _shardType = _runtimeSettings.ShardType;
        _resistPercent = _runtimeSettings.ResistPercent;
        _ocrEngineMode = _runtimeSettings.OcrEngineMode;
        BindControlsFromSettings();
        UpdateRegionText();
    }

    private void BindControlsFromSettings()
    {
        _isBindingControls = true;
        try
        {
            ShardCombo.ItemsSource = Enum.GetValues(typeof(ShardType));
            ShardCombo.SelectedItem = _shardType;
            OcrEngineCombo.ItemsSource = Enum.GetValues(typeof(OcrEngineMode));
            OcrEngineCombo.SelectedItem = _ocrEngineMode;
            ResistText.Text = _resistPercent.ToString();
        }
        finally
        {
            _isBindingControls = false;
        }
    }

    private void ReloadEditorData()
    {
        _cfgEntries.Clear();
        foreach (var entry in _settingsController.LoadEntries())
        {
            _cfgEntries.Add(entry);
        }

        ReloadAbilityEditorRows();

        RefreshDaocCharacterSelectors();
    }

    private void InitializeAbilityProfileControls()
    {
        _isBindingControls = true;
        try
        {
            AbilityProfileServerCombo.ItemsSource = new[]
            {
                ShardType.Default,
                ShardType.Eden,
                ShardType.Blackthorn
            };
            _abilityProfileShard = SupportsAbilityProfiles(_shardType) ? _shardType : ShardType.Default;
            AbilityProfileServerCombo.SelectedItem = _abilityProfileShard;
            RefreshAbilityProfileClasses();
        }
        finally
        {
            _isBindingControls = false;
        }
    }

    private void RefreshAbilityProfileClasses()
    {
        if (_abilityProfileShard == ShardType.Default)
        {
            AbilityProfileClassCombo.ItemsSource = Array.Empty<string>();
            AbilityProfileClassCombo.SelectedItem = null;
            AbilityProfileClassCombo.IsEnabled = false;
            _abilityProfileClass = null;
            _abilityProfileCharacter = string.Empty;
            return;
        }

        var classes = AbilityProfileCatalog.GetClasses(_abilityProfileShard);
        var settings = _settingsController.LoadMap();
        _abilityProfileCharacter = ReadOrDefault(
            settings,
            $"daoc.character.{_abilityProfileShard.ToString().ToLowerInvariant()}",
            string.Empty);
        var selectedClass = ReadOrDefault(
            settings,
            AbilityProfileClassSettingKey(_abilityProfileShard, _abilityProfileCharacter),
            ReadOrDefault(settings, LegacyAbilityProfileClassSettingKey(_abilityProfileShard), string.Empty));
        _abilityProfileClass = classes.FirstOrDefault(x =>
            string.Equals(x, selectedClass, StringComparison.OrdinalIgnoreCase));
        AbilityProfileClassCombo.ItemsSource = classes;
        AbilityProfileClassCombo.SelectedItem = _abilityProfileClass;
        AbilityProfileClassCombo.IsEnabled = true;
    }

    private void ReloadAbilityEditorRows()
    {
        _abilityEntries.Clear();
        var entries = _abilityProfileShard != ShardType.Default && !string.IsNullOrWhiteSpace(_abilityProfileClass)
            ? _store.LoadAbilityProfile(_abilityProfileShard, _abilityProfileCharacter, _abilityProfileClass)
            : _store.LoadAbilities();
        foreach (var entry in entries)
        {
            _abilityEntries.Add(entry);
        }

        AbilityProfileSummaryText.Text = _abilityProfileShard == ShardType.Default
            ? $"Manual fallback: {_abilityEntries.Count} entries"
            : string.IsNullOrWhiteSpace(_abilityProfileClass)
                ? "Choose a class to activate its profile."
                : $"{_abilityProfileShard} / {DisplayProfileCharacter(_abilityProfileCharacter)} / {_abilityProfileClass}: {_abilityEntries.Count(x => x.IsEnabled)} of {_abilityEntries.Count} enabled";
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

    private static string NormalizeSettingSegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().ToLowerInvariant().Select(x => char.IsLetterOrDigit(x) ? x : '_').ToArray());
    }

    private static string LegacyAbilityProfileClassSettingKey(ShardType shard)
    {
        return $"ability.profile.class.{shard.ToString().ToLowerInvariant()}";
    }

    private static string DisplayProfileCharacter(string characterName)
    {
        return string.IsNullOrWhiteSpace(characterName) ? "Default character" : characterName;
    }

    private void LoadDaocCharacterProfiles()
    {
        _daocCharacterProfiles = _daocCharacterDiscovery.Discover();
    }

    private void RepairInvalidSavedCustomWindowRegions()
    {
        var settings = _settingsController.LoadMap();
        var updates = new List<ConfigEntry>();
        foreach (var (shard, profiles) in _daocCharacterProfiles)
        {
            foreach (var profile in profiles)
            {
                var prefix = $"daoc.ocr.windows.{shard.ToString().ToLowerInvariant()}.";
                var normalizedKey = prefix + NormalizeSettingSegment(profile.CharacterName);
                var legacyKey = prefix + profile.CharacterName.Trim().ToLowerInvariant();
                if (!settings.TryGetValue(normalizedKey, out var raw) &&
                    !settings.TryGetValue(legacyKey, out raw))
                {
                    continue;
                }
                try
                {
                    var saved = JsonSerializer.Deserialize<List<OcrWatchRegion>>(raw) ?? [];
                    var discovered = profile.Windows.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
                    var changed = false;
                    for (var index = 0; index < saved.Count; index++)
                    {
                        var current = saved[index];
                        if (!current.Key.StartsWith("Custom", StringComparison.OrdinalIgnoreCase) ||
                            !discovered.TryGetValue(current.Key, out var corrected))
                        {
                            continue;
                        }

                        var hasInvalidSize = current.Region.Width <= 1 || current.Region.Height <= 1;
                        var hasStaleConfirmedSize = !corrected.SizeIsEstimated &&
                            (current.Region.Width != corrected.Region.Width || current.Region.Height != corrected.Region.Height);
                        if (!hasInvalidSize && !hasStaleConfirmedSize)
                        {
                            continue;
                        }

                        // The INI owns position, while the active UI XML owns custom-window size.
                        var correctedRegion = new ScreenRegion(
                            current.Region.X,
                            current.Region.Y,
                            corrected.Region.Width,
                            corrected.Region.Height);
                        saved[index] = new OcrWatchRegion(current.Key, corrected.Label, correctedRegion);
                        changed = true;
                    }
                    if (changed)
                    {
                        updates.Add(new ConfigEntry
                        {
                            Key = normalizedKey,
                            Value = JsonSerializer.Serialize(saved)
                        });
                    }
                }
                catch
                {
                }
            }
        }
        if (updates.Count == 0)
        {
            return;
        }
        _settingsController.Save(updates);
        RebuildRuntimeFromFiles();
        _responseDiagnostics.Log($"[OCR] repaired {updates.Count} saved custom-window configuration(s) from active UI XML.");
    }

    private void BuildDaocCharacterSelectorUi()
    {
        DaocCharacterPanel.Children.Clear();
        _daocCharacterSelectors.Clear();
        _daocCharacterSummaryBlocks.Clear();

        foreach (var shard in Enum.GetValues<ShardType>())
        {
            if (shard == ShardType.Default)
            {
                continue;
            }

            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = shard.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var combo = new WpfComboBox
            {
                Margin = new Thickness(0, 0, 8, 0),
                DisplayMemberPath = nameof(DaocCharacterProfile.DisplayLabel),
                IsEnabled = true
            };
            combo.SelectionChanged += DaocCharacterSelector_SelectionChanged;
            combo.Tag = shard;
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);

            var windowButton = new System.Windows.Controls.Button
            {
                Content = "OCR Fenster wählen",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(10, 5, 10, 5),
                IsEnabled = false
            };
            windowButton.Click += CharacterOcrWindows_Click;
            windowButton.Tag = shard;
            Grid.SetColumn(windowButton, 2);
            row.Children.Add(windowButton);

            var summary = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsMutedTextBrush"]
            };
            Grid.SetColumn(summary, 3);
            row.Children.Add(summary);

            _daocCharacterSelectors[shard] = combo;
            _daocWindowButtons[shard] = windowButton;
            _daocCharacterSummaryBlocks[shard] = summary;
            DaocCharacterPanel.Children.Add(row);
        }

        RefreshDaocCharacterSelectors();
    }

    private void RefreshDaocCharacterSelectors()
    {
        if (DaocCharacterPanel is null)
        {
            return;
        }

        _isBindingControls = true;
        try
        {
            var settings = _settingsController.LoadMap();
            foreach (var shard in Enum.GetValues<ShardType>())
            {
                if (!_daocCharacterSelectors.TryGetValue(shard, out var combo))
                {
                    continue;
                }

                var profiles = _daocCharacterProfiles.TryGetValue(shard, out var list)
                    ? list
                    : [];
                combo.ItemsSource = profiles;
                var selectedName = ReadOrDefault(settings, $"daoc.character.{shard.ToString().ToLowerInvariant()}", string.Empty);
                var selectedProfile = profiles.FirstOrDefault(x => string.Equals(x.CharacterName, selectedName, StringComparison.OrdinalIgnoreCase));
                combo.SelectedItem = selectedProfile;
                if (_daocWindowButtons.TryGetValue(shard, out var windowButton))
                {
                    windowButton.IsEnabled = selectedProfile is not null;
                }

                if (_daocCharacterSummaryBlocks.TryGetValue(shard, out var summary))
                {
                    if (selectedProfile is null)
                    {
                        summary.Text = profiles.Count == 0
                            ? "No .ini files found in the shard folder."
                            : "Choose a character config from this shard.";
                    }
                    else
                    {
                        var ocrCount = LoadSelectedOcrWindows(shard, selectedProfile.CharacterName).Count;
                        summary.Text = $"{selectedProfile.SummaryText} | OCR Windows: {ocrCount}";
                    }
                }
            }
        }
        finally
        {
            _isBindingControls = false;
        }
    }

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

        if (_abilityProfileShard == shard)
        {
            SelectAbilityProfileShard(shard);
            ReloadAbilityEditorRows();
        }

        if (_shardType == shard)
        {
            RebuildRuntimeFromFiles();
        }

        if (_daocCharacterSummaryBlocks.TryGetValue(shard, out var summary))
        {
            var ocrCount = LoadSelectedOcrWindows(shard, profile.CharacterName).Count;
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

        var current = LoadSelectedOcrWindows(shard, profile.CharacterName);
        var selected = DaocWindowSelectionWindow.Pick(this, profile, current);
        if (selected is null)
        {
            OutputBox.Text = $"OCR window selection cancelled for {profile.CharacterName}.";
            return;
        }

        SaveSelectedOcrWindows(shard, profile.CharacterName, selected);
        _responseDiagnostics.Log($"[OCR] saved {selected.Count} windows for {profile.CharacterName} on {shard}.");
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        OutputBox.Text = $"OCR windows saved for {profile.CharacterName} ({selected.Count} windows).";
    }

    private async void RunTick_Click(object sender, RoutedEventArgs e)
    {
        await TickOnceAsync();
    }

    private async Task TickOnceAsync()
    {
        if (_tickInProgress)
        {
            return;
        }

        if (_chatRegion is null && _runtimeSettings.OcrWatchRegions.Count == 0)
        {
            OutputBox.Text = "Select chat area first (drag selection).";
            return;
        }

        _tickInProgress = true;
        try
        {
            await _orchestrator.TickAsync(_chatRegion ?? new ScreenRegion(0, 0, 1, 1), _shardType, _resistPercent, DateTimeOffset.UtcNow, CancellationToken.None);
            OutputBox.Text = _overlay.LastRendered;
            _lastOverlaySnapshot = _overlay.LastSnapshot;
            RefreshDiagnostics();
        }
        catch (Exception ex)
        {
            OutputBox.Text = ex.Message;
        }
        finally
        {
            _tickInProgress = false;
        }
    }

    private void ToggleLoop_Click(object sender, RoutedEventArgs e)
    {
        if (_loopTimer.IsEnabled)
        {
            _loopTimer.Stop();
            ToggleLoopButton.Content = "Start Loop";
            return;
        }

        _loopTimer.Start();
        ToggleLoopButton.Content = "Stop Loop";
    }

    private void OpenSpellBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (_edenBrowserWindow is not null)
        {
            if (!_edenBrowserWindow.IsVisible)
            {
                _edenBrowserWindow.Show();
            }

            _edenBrowserWindow.Activate();
            return;
        }

        _edenBrowserWindow = new DataBrowserWindow(_store, _shardType)
        {
            Owner = this
        };
        _edenBrowserWindow.Closed += (_, _) => _edenBrowserWindow = null;
        _edenBrowserWindow.Show();
        _edenBrowserWindow.Activate();
    }

    private async void OpenEdenBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            OutputBox.Text = "Opening Eden Playwright browser...";
            await _authRefreshService.OpenBrowserAsync(ShardType.Eden, CancellationToken.None);
            OutputBox.Text = "Eden Playwright browser closed.";
        }
        catch (Exception ex)
        {
            OutputBox.Text = $"Could not open Eden Playwright browser: {ex.Message}";
        }
    }

    private void SelectChatArea_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        try
        {
            var region = RegionSelectionWindow.Select(this);
            if (region is not null)
            {
                _chatRegion = region;
                UpdateRegionText();
                _settingsController.Save([
                        new ConfigEntry { Key = "MX", Value = region.X.ToString() },
                        new ConfigEntry { Key = "MY", Value = region.Y.ToString() },
                        new ConfigEntry { Key = "w", Value = region.Width.ToString() },
                        new ConfigEntry { Key = "h", Value = region.Height.ToString() }
                    ]);
                ReloadEditorData();
                OutputBox.Text = "Chat area saved to database.";
            }
        }
        finally
        {
            Show();
            Activate();
        }
    }

    private void SelectStatsArea_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsController.LoadMap();
        var character = ReadOrDefault(
            settings,
            $"daoc.character.{_shardType.ToString().ToLowerInvariant()}",
            string.Empty);
        if (_shardType == ShardType.Default || string.IsNullOrWhiteSpace(character))
        {
            OutputBox.Text = "Choose a server and DAoC character before selecting the stats area.";
            return;
        }
        Hide();
        try
        {
            var selected = RegionSelectionWindow.Select(this);
            if (selected is null)
            {
                OutputBox.Text = "Stats area selection cancelled.";
                return;
            }
            var region = new OcrWatchRegion("character-stats", "Character Stats", selected);
            var key = $"daoc.ocr.stats.{_shardType.ToString().ToLowerInvariant()}.{NormalizeSettingSegment(character)}";
            _settingsController.Save([
                new ConfigEntry { Key = key, Value = JsonSerializer.Serialize(region) }
            ]);
            RebuildRuntimeFromFiles();
            OutputBox.Text = $"Stats OCR area saved for {character}. Keep the status window visible when values change.";
        }
        finally
        {
            Show();
            Activate();
        }
    }

    private void ShardCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBindingControls || ShardCombo.SelectedItem is not ShardType selected)
        {
            return;
        }

        _shardType = selected;
        _settingsController.Save([
            new ConfigEntry { Key = "server", Value = _shardType.ToString().ToLowerInvariant() }
        ]);
        SelectAbilityProfileShard(SupportsAbilityProfiles(selected) ? selected : ShardType.Default);
        ReloadEditorData();
        UpdateRegionText();
        RebuildRuntimeFromFiles();
    }

    private void ResistText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isBindingControls || !int.TryParse(ResistText.Text, out var parsed))
        {
            return;
        }

        _resistPercent = Math.Clamp(parsed, 0, 60);
        if (_resistPercent.ToString() != ResistText.Text)
        {
            ResistText.Text = _resistPercent.ToString();
            ResistText.CaretIndex = ResistText.Text.Length;
        }

        _settingsController.Save([
            new ConfigEntry { Key = "resis", Value = _resistPercent.ToString() }
        ]);
        ReloadEditorData();
        UpdateRegionText();
    }

    private void OcrEngineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBindingControls || OcrEngineCombo.SelectedItem is not OcrEngineMode selected)
        {
            return;
        }

        _ocrEngineMode = selected;
        _settingsController.Save([
            new ConfigEntry { Key = "ocrEngine", Value = _ocrEngineMode.ToString().ToLowerInvariant() }
        ]);
        ReloadEditorData();
        RebuildRuntimeFromFiles();
    }

    private void LoopMsText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loopTimer is null)
        {
            return;
        }

        if (!int.TryParse(LoopMsText.Text, out var ms))
        {
            return;
        }

        ms = Math.Clamp(ms, 100, 5000);
        _loopTimer.Interval = TimeSpan.FromMilliseconds(ms);
    }

    private void ReloadConfig_Click(object sender, RoutedEventArgs e)
    {
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        ReloadOverlaySettingsFromStore();
        _authController.ConfigureTimer();
        OutputBox.Text = "Config reloaded.";
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        _settingsController.Replace(_cfgEntries.Where(x => !string.IsNullOrWhiteSpace(x.Key)));
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        ReloadOverlaySettingsFromStore();
        _authController.ConfigureTimer();
        OutputBox.Text = "Config saved to database and runtime refreshed.";
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
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

    private void ImportJson_Click(object sender, RoutedEventArgs e)
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

    private void ReloadAbilities_Click(object sender, RoutedEventArgs e)
    {
        ReloadEditorData();
        OutputBox.Text = "Abilities reloaded.";
    }

    private void ReloadAbilityCatalogs_Click(object sender, RoutedEventArgs e)
    {
        AbilityProfileCatalog.Refresh();
        SelectAbilityProfileShard(_abilityProfileShard);
        ReloadEditorData();
        RebuildRuntimeFromFiles();
        OutputBox.Text = "Local Eden and Blackthorn catalogs reloaded.";
    }

    private async void UpdateCatalogsOnline_Click(object sender, RoutedEventArgs e)
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
            var result = await new CatalogUpdateService().UpdateAsync(CancellationToken.None);
            _edenBrowserWindow?.Close();
            AbilityProfileCatalog.Refresh();
            RebuildRuntimeFromFiles();
            ReloadAbilityEditorRows();
            OutputBox.Text = result;
        }
        catch (Exception ex)
        {
            OutputBox.Text = $"Catalog update failed; previous snapshots were kept. {ex.Message}";
        }
    }

    private void SaveAbilities_Click(object sender, RoutedEventArgs e)
    {
        if (_abilityProfileShard != ShardType.Default && !string.IsNullOrWhiteSpace(_abilityProfileClass))
        {
            _store.SaveAbilityProfile(_abilityProfileShard, _abilityProfileCharacter, _abilityProfileClass, _abilityEntries);
        }
        else
        {
            _store.SaveAbilities(_abilityEntries);
        }

        ReloadEditorData();
        RebuildRuntimeFromFiles();
        OutputBox.Text = _abilityProfileShard == ShardType.Default
            ? "Manual abilities saved and parser refreshed."
            : $"Ability profile saved for {_abilityProfileShard} / {_abilityProfileClass} and parser refreshed.";
    }

    private void AddAbility_Click(object sender, RoutedEventArgs e)
    {
        _abilityEntries.Add(new AbilityEditorRow
        {
            IsEnabled = true,
            Server = _abilityProfileShard == ShardType.Default
                ? string.Empty
                : _abilityProfileShard.ToString().ToLowerInvariant(),
            ClassName = _abilityProfileClass ?? string.Empty,
            CharacterName = _abilityProfileCharacter,
            SourceAbilityName = string.Empty,
            SourceEffectType = "s",
            Category = _abilityProfileShard == ShardType.Default ? string.Empty : "Custom",
            IsCustom = _abilityProfileShard != ShardType.Default
        });
    }

    private void RemoveAbility_Click(object sender, RoutedEventArgs e)
    {
        if (AbilitiesGrid.SelectedItem is not AbilityEditorRow selected)
        {
            return;
        }

        if (_abilityProfileShard != ShardType.Default && !selected.IsCustom)
        {
            selected.IsEnabled = false;
            AbilitiesGrid.Items.Refresh();
            AbilityProfileSummaryText.Text = $"{_abilityProfileShard} / {DisplayProfileCharacter(_abilityProfileCharacter)} / {_abilityProfileClass}: {_abilityEntries.Count(x => x.IsEnabled)} of {_abilityEntries.Count} enabled";
            return;
        }

        _abilityEntries.Remove(selected);
    }

    private void AbilityProfileServerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBindingControls || AbilityProfileServerCombo.SelectedItem is not ShardType shard)
        {
            return;
        }

        SelectAbilityProfileShard(shard);
        ReloadAbilityEditorRows();
    }

    private void AbilityProfileClassCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isBindingControls || _abilityProfileShard == ShardType.Default ||
            AbilityProfileClassCombo.SelectedItem is not string className)
        {
            return;
        }

        _abilityProfileClass = className;
        _settingsController.Save([
            new ConfigEntry
            {
                Key = AbilityProfileClassSettingKey(_abilityProfileShard, _abilityProfileCharacter),
                Value = className
            }
        ]);
        ReloadEditorData();
        if (_abilityProfileShard == _shardType)
        {
            RebuildRuntimeFromFiles();
        }

        OutputBox.Text = $"Active ability profile: {_abilityProfileShard} / {DisplayProfileCharacter(_abilityProfileCharacter)} / {className}.";
    }

    private void SelectAbilityProfileShard(ShardType shard)
    {
        var previousBinding = _isBindingControls;
        _isBindingControls = true;
        try
        {
            _abilityProfileShard = SupportsAbilityProfiles(shard) ? shard : ShardType.Default;
            AbilityProfileServerCombo.SelectedItem = _abilityProfileShard;
            RefreshAbilityProfileClasses();
        }
        finally
        {
            _isBindingControls = previousBinding;
        }
    }

    private async void RefreshAuthCurrent_Click(object sender, RoutedEventArgs e)
    {
        await _authController.RefreshCurrentAsync(_shardType);
    }

    private async void RefreshAuthAll_Click(object sender, RoutedEventArgs e)
    {
        await _authController.RefreshAllAsync();
    }

    private void RefreshDiagnostics()
    {
        var snapshot = _overlay.LastSnapshot;
        var raw = snapshot?.RawOcrText ?? string.Empty;
        if (raw.Length > 700)
        {
            raw = raw[..700] + " ...";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Engine: {_capture.LastOcrEngineName}");
        sb.AppendLine($"OCR time: {_capture.LastOcrDurationMs} ms");
        sb.AppendLine($"OCR chars: {_capture.LastOcrTextLength}");
        sb.AppendLine($"Target class: {snapshot?.Target?.Class ?? "Unknown"}");
        sb.AppendLine("OCR preview:");
        sb.AppendLine(raw);
        DiagnosticsBox.Text = sb.ToString();
    }

    private void ClearResponseDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        _responseDiagnostics.Clear();
        ResponseDiagnosticsBox.Clear();
    }

    private void UpdateRegionText()
    {
        var modeLabel = _shardType == ShardType.Default ? "Default mode" : $"Shard: {_shardType}";
        var statsLabel = BuildCharacterStatsLabel();
        if (_chatRegion is null)
        {
            RegionText.Text = _shardType == ShardType.Default
                ? $"{modeLabel} | OCR: {_ocrEngineMode} | Resist: {_resistPercent}% | Region: not set | Independent mode"
                : $"Shard: {_shardType} | OCR: {_ocrEngineMode} | Resist: {_resistPercent}% | Region: not set | OCR windows: {_runtimeSettings.OcrWatchRegions.Count} | {statsLabel}";
            return;
        }

        RegionText.Text = _shardType == ShardType.Default
            ? $"{modeLabel} | OCR: {_ocrEngineMode} | Resist: {_resistPercent}% | Region: X={_chatRegion.X}, Y={_chatRegion.Y}, W={_chatRegion.Width}, H={_chatRegion.Height} | Independent mode"
            : $"Shard: {_shardType} | OCR: {_ocrEngineMode} | Resist: {_resistPercent}% | Region: X={_chatRegion.X}, Y={_chatRegion.Y}, W={_chatRegion.Width}, H={_chatRegion.Height} | OCR windows: {_runtimeSettings.OcrWatchRegions.Count} | {statsLabel}";
    }

    private string BuildCharacterStatsLabel()
    {
        if (_shardType == ShardType.Default)
        {
            return "Stats: inactive";
        }

        var map = _settingsController.LoadMap();
        var character = ReadOrDefault(map, $"daoc.character.{_shardType.ToString().ToLowerInvariant()}", string.Empty);
        var stats = _store.LoadCharacterStats(_shardType, character);
        return stats?.Dexterity is null
            ? "Stats: awaiting OCR"
            : $"Stats: Dex {stats.Dexterity} | Cast {stats.CastingSpeedPercent:0.##}% | Dmg {stats.SpellDamagePercent:0.##}%";
    }

    private IReadOnlyList<OcrWatchRegion> LoadSelectedOcrWindows(ShardType shard, string characterName)
    {
        var settings = _settingsController.LoadMap();
        var prefix = $"daoc.ocr.windows.{shard.ToString().ToLowerInvariant()}.";
        var key = prefix + NormalizeSettingSegment(characterName);
        var legacyKey = prefix + characterName.Trim().ToLowerInvariant();
        if (!settings.TryGetValue(key, out var raw))
        {
            settings.TryGetValue(legacyKey, out raw);
        }
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<OcrWatchRegion>>(raw) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveSelectedOcrWindows(ShardType shard, string characterName, IReadOnlyCollection<DaocWindowDefinition> selected)
    {
        var windows = selected.Select(x => new OcrWatchRegion(x.Key, x.Label, x.Region)).ToList();
        var raw = JsonSerializer.Serialize(windows);
        _settingsController.Save([
            new ConfigEntry
            {
                Key = $"daoc.ocr.windows.{shard.ToString().ToLowerInvariant()}.{NormalizeSettingSegment(characterName)}",
                Value = raw
            }
        ]);
    }

    private static string FindFilePath(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path))
            {
                return path;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), fileName);
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(!_isDarkTheme);
        _settingsController.Save([
            new ConfigEntry { Key = "ui.theme", Value = _isDarkTheme ? "dark" : "light" }
        ]);
        ReloadEditorData();
    }

    private void LoadThemeSetting()
    {
        var settings = _settingsController.LoadMap();
        var theme = settings.TryGetValue("ui.theme", out var themeRaw) ? themeRaw : "dark";
        ApplyTheme(!string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyTheme(bool dark)
    {
        _isDarkTheme = dark;
        var theme = _paletteHelper.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        _paletteHelper.SetTheme(theme);
        ApplyVsCodePalette(dark);
        ThemeToggleButton.Content = dark ? "Switch to Light" : "Switch to Dark";
    }

    private static void ApplyVsCodePalette(bool dark)
    {
        var resources = System.Windows.Application.Current.Resources;
        if (dark)
        {
            resources["VsWindowBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
            resources["VsTitleBarBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x26));
            resources["VsPanelBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x26));
            resources["VsPanelAltBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30));
            resources["VsBorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x42));
            resources["VsTextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD4, 0xD4, 0xD4));
            resources["VsMutedTextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9D, 0xA1, 0xA6));
            resources["VsAccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC));
            return;
        }

        resources["VsWindowBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3));
        resources["VsTitleBarBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE7, 0xE7, 0xE7));
        resources["VsPanelBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
        resources["VsPanelAltBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0xF8, 0xF8));
        resources["VsBorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD4, 0xD4, 0xD4));
        resources["VsTextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
        resources["VsMutedTextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x5C, 0x63, 0x6A));
        resources["VsAccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x6B, 0xC1));
    }

    private void OnResponseDiagnosticLineAdded(string line)
    {
        Dispatcher.Invoke(() =>
        {
            if (ResponseDiagnosticsBox.Text.Length > 80_000)
            {
                ResponseDiagnosticsBox.Text = string.Join(Environment.NewLine, _responseDiagnostics.Snapshot());
            }
            else
            {
                if (ResponseDiagnosticsBox.Text.Length > 0)
                {
                    ResponseDiagnosticsBox.AppendText(Environment.NewLine);
                }

                ResponseDiagnosticsBox.AppendText(line);
            }

            ResponseDiagnosticsBox.ScrollToEnd();
        });
    }

    private void OnAuthRefreshed(ShardType shard, ShardAuthBundle bundle)
    {
        _authController.OnRefreshed(shard, bundle);
    }

    private void ReloadOverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        ReloadOverlaySettingsFromStore();
        OutputBox.Text = "Overlay settings reloaded.";
    }

    private void SaveOverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new OverlaySettings(
            NormalizeIntText(OverlayXText.Text, 1200),
            NormalizeIntText(OverlayYText.Text, 900),
            NormalizeIntText(OverlayTimerXText.Text, 1580),
            NormalizeIntText(OverlayTimerYText.Text, 900),
            NormalizeIntText(OverlayCastXText.Text, 1200),
            NormalizeIntText(OverlayCastYText.Text, 986),
            NormalizeIntText(OverlayFontSizeText.Text, 20),
            NormalizeIntText(OverlayTimerSizeText.Text, 20),
            NormalizeColorText(TargetColorText.Text, "#FFFFFF"),
            NormalizeColorText(TimerColorText.Text, "#FFFFFF"),
            NormalizeColorText(OutlineColorText.Text, "#000000"),
            ShowTargetCheckbox?.IsChecked ?? true,
            ShowTimersCheckbox?.IsChecked ?? true,
            ShowCastBarCheckbox?.IsChecked ?? true,
            DynamicCastSpeedCheckbox?.IsChecked ?? false,
            EstimatedSpellDamageCheckbox?.IsChecked ?? false,
            OcrReplayCheckbox?.IsChecked ?? false);

        _settingsController.Save(_overlaySettingsController.Save(settings));
        SaveCurrentCharacterStatBonuses();
        ReloadEditorData();
        _liveOverlay?.ClearPreview();
        ReloadOverlaySettingsFromStore();
        RenderLiveOverlayPreview();
        OutputBox.Text = "Overlay settings saved.";
    }

    private void PickTargetColor_Click(object sender, RoutedEventArgs e)
    {
        PickColorLive(TargetColorText, "#FFFFFF");
    }

    private void PickTimerColor_Click(object sender, RoutedEventArgs e)
    {
        PickColorLive(TimerColorText, "#FFFFFF");
    }

    private void PickOutlineColor_Click(object sender, RoutedEventArgs e)
    {
        PickColorLive(OutlineColorText, "#000000");
    }

    private void PickTargetOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickOverlayPosition(isTimerOverlay: false);
    }

    private void PickTimerOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickOverlayPosition(isTimerOverlay: true);
    }

    private void PickCastOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        PickCastOverlayPosition();
    }

    private void PickTargetSize_Click(object sender, RoutedEventArgs e)
    {
        PickSizeLive(OverlayFontSizeText, "Target", isTimerOverlay: false);
    }

    private void PickTimerSize_Click(object sender, RoutedEventArgs e)
    {
        PickSizeLive(OverlayTimerSizeText, "Timers", isTimerOverlay: true);
    }

    private void PickOverlayPosition(bool isTimerOverlay)
    {
        var xText = isTimerOverlay ? OverlayTimerXText : OverlayXText;
        var yText = isTimerOverlay ? OverlayTimerYText : OverlayYText;
        var originalX = xText.Text;
        var originalY = yText.Text;

        var selected = OverlayCursorPickerWindow.Pick(this, (x, y) =>
        {
            xText.Text = x.ToString();
            yText.Text = y.ToString();
            _liveOverlay?.SetPreviewPosition(isTimerOverlay, x, y);
            RenderLiveOverlayPreview();
        });

        if (selected is null)
        {
            xText.Text = originalX;
            yText.Text = originalY;
            _liveOverlay?.ClearPreview();
            RenderLiveOverlayPreview();
            return;
        }

        xText.Text = selected.Value.X.ToString();
        yText.Text = selected.Value.Y.ToString();
        SaveOverlaySettings_Click(this, new RoutedEventArgs());
    }

    private void PickCastOverlayPosition()
    {
        var originalX = OverlayCastXText.Text;
        var originalY = OverlayCastYText.Text;

        var selected = OverlayCursorPickerWindow.Pick(this, (x, y) =>
        {
            OverlayCastXText.Text = x.ToString();
            OverlayCastYText.Text = y.ToString();
            _liveOverlay?.SetPreviewCastBarPosition(x, y);
            RenderLiveOverlayPreview();
        });

        if (selected is null)
        {
            OverlayCastXText.Text = originalX;
            OverlayCastYText.Text = originalY;
            _liveOverlay?.ClearPreview();
            RenderLiveOverlayPreview();
            return;
        }

        OverlayCastXText.Text = selected.Value.X.ToString();
        OverlayCastYText.Text = selected.Value.Y.ToString();
        SaveOverlaySettings_Click(this, new RoutedEventArgs());
    }

    private void PickSizeLive(System.Windows.Controls.TextBox targetBox, string label, bool isTimerOverlay)
    {
        var original = NormalizeIntText(targetBox.Text, 20);
        targetBox.Text = original.ToString();

        var selected = LiveSizePickerWindow.Pick(this, label, original, size =>
        {
            targetBox.Text = size.ToString();
            _liveOverlay?.SetPreviewFontSize(isTimerOverlay, size);
            RenderLiveOverlayPreview();
        });

        if (selected is null)
        {
            targetBox.Text = original.ToString();
            _liveOverlay?.SetPreviewFontSize(isTimerOverlay, original);
            RenderLiveOverlayPreview();
            return;
        }

        targetBox.Text = selected.Value.ToString();
        SaveOverlaySettings_Click(this, new RoutedEventArgs());
    }

    private void ReloadOverlaySettingsFromStore()
    {
        var settings = _overlaySettingsController.Load();
        OverlayXText.Text = settings.X.ToString();
        OverlayYText.Text = settings.Y.ToString();
        OverlayTimerXText.Text = settings.TimerX.ToString();
        OverlayTimerYText.Text = settings.TimerY.ToString();
        OverlayCastXText.Text = settings.CastX.ToString();
        OverlayCastYText.Text = settings.CastY.ToString();
        OverlayFontSizeText.Text = settings.FontSize.ToString();
        OverlayTimerSizeText.Text = settings.TimerSize.ToString();
        TargetColorText.Text = NormalizeColorText(settings.TargetColor, "#FFFFFF");
        TimerColorText.Text = NormalizeColorText(settings.TimerColor, "#FFFFFF");
        OutlineColorText.Text = NormalizeColorText(settings.OutlineColor, "#000000");

        var map = _settingsController.LoadMap();
        var character = ReadOrDefault(map, $"daoc.character.{_shardType.ToString().ToLowerInvariant()}", string.Empty);
        var stats = _store.LoadCharacterStats(_shardType, character);
        CastingSpeedBonusText.Text = (stats?.CastingSpeedPercent ?? 0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        SpellDamageBonusText.Text = (stats?.SpellDamagePercent ?? 0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        BindToggle(ShowTargetCheckbox, settings.ShowTarget, OverlayVisibilityChanged);
        BindToggle(ShowTimersCheckbox, settings.ShowTimers, OverlayVisibilityChanged);
        BindToggle(ShowCastBarCheckbox, settings.ShowCastBar, OverlayVisibilityChanged);
        BindToggle(DynamicCastSpeedCheckbox, settings.DynamicCastSpeedEnabled, OverlayVisibilityChanged);
        BindToggle(EstimatedSpellDamageCheckbox, settings.EstimatedSpellDamageEnabled, OverlayVisibilityChanged);
        BindToggle(OcrReplayCheckbox, settings.OcrReplayEnabled, OverlayVisibilityChanged);
    }

    private void BindToggle(System.Windows.Controls.CheckBox? checkBox, bool value, RoutedEventHandler? handler = null)
    {
        if (checkBox is null)
        {
            return;
        }

        checkBox.IsChecked = value;
        if (handler is null)
        {
            return;
        }

        checkBox.Checked -= handler;
        checkBox.Unchecked -= handler;
        checkBox.Checked += handler;
        checkBox.Unchecked += handler;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> map, string key, bool fallback)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" => true,
            "0" or "false" or "no" => false,
            _ => fallback
        };
    }

    private void OverlayVisibilityChanged(object sender, RoutedEventArgs e)
    {
        _settingsController.Save(_overlaySettingsController.SaveVisibility(
            ShowTargetCheckbox?.IsChecked ?? true,
            ShowTimersCheckbox?.IsChecked ?? true,
            ShowCastBarCheckbox?.IsChecked ?? true,
            DynamicCastSpeedCheckbox?.IsChecked ?? false,
            EstimatedSpellDamageCheckbox?.IsChecked ?? false,
            OcrReplayCheckbox?.IsChecked ?? false));
        RebuildRuntimeFromFiles();
    }

    private void SaveCurrentCharacterStatBonuses()
    {
        var map = _settingsController.LoadMap();
        var character = ReadOrDefault(map, $"daoc.character.{_shardType.ToString().ToLowerInvariant()}", string.Empty);
        if (string.IsNullOrWhiteSpace(character))
        {
            return;
        }
        var current = _store.LoadCharacterStats(_shardType, character);
        var castSpeed = NormalizePercentText(CastingSpeedBonusText.Text);
        var spellDamage = NormalizePercentText(SpellDamageBonusText.Text);
        _store.SaveCharacterStats(current is null
            ? new CharacterStatsSnapshot(
                _shardType, character, null, null, null, null, null, null, null, null,
                castSpeed, spellDamage, DateTimeOffset.UtcNow)
            : current with
            {
                CastingSpeedPercent = castSpeed,
                SpellDamagePercent = spellDamage,
                UpdatedUtc = DateTimeOffset.UtcNow
            });
    }

    private static double NormalizePercentText(string? raw)
    {
        return double.TryParse(
            raw?.Trim().Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? Math.Clamp(value, 0, 25)
            : 0;
    }

    private static int NormalizeIntText(string raw, int fallback)
    {
        return int.TryParse(raw?.Trim(), out var parsed) ? parsed : fallback;
    }

    private static string NormalizeColorText(string? raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            var color = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(raw.Trim());
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch
        {
            return fallback;
        }
    }

    private void PickColorLive(System.Windows.Controls.TextBox targetBox, string fallback)
    {
        var original = NormalizeColorText(targetBox.Text, fallback);
        targetBox.Text = original;

        var selected = LiveColorPickerWindow.Pick(this, original, hex =>
        {
            targetBox.Text = NormalizeColorText(hex, fallback);
            ApplyOverlayColorPreviewFromInputs();
            RenderLiveOverlayPreview();
        });

        if (selected is null)
        {
            targetBox.Text = original;
        }
        else
        {
            targetBox.Text = NormalizeColorText(selected, fallback);
        }

        ApplyOverlayColorPreviewFromInputs();
        RenderLiveOverlayPreview();
    }

    private static string ReadOrDefault(IReadOnlyDictionary<string, string> map, string key, string fallback)
    {
        return map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private void ApplyOverlayColorPreviewFromInputs()
    {
        var target = TryParseColor(TargetColorText.Text);
        var timer = TryParseColor(TimerColorText.Text);
        var outline = TryParseColor(OutlineColorText.Text);
        _liveOverlay?.SetPreviewColors(target, timer, outline);
    }

    private static MediaColor? TryParseColor(string raw)
    {
        try
        {
            var converted = System.Windows.Media.ColorConverter.ConvertFromString(raw);
            return converted is MediaColor c ? c : null;
        }
        catch
        {
            return null;
        }
    }

    private void RenderLiveOverlayPreview()
    {
        if (_liveOverlay is null)
        {
            return;
        }

        var snapshot = _overlay?.LastSnapshot ?? _lastOverlaySnapshot;
        if (snapshot is null)
        {
            return;
        }

        _ = _liveOverlay.RenderAsync(snapshot, CancellationToken.None);
    }
}
