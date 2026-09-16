using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HeraldHelper.Desktop.Controllers;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Desktop.Services;
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
    private DesktopOverlayRenderer _liveOverlay = null!;
    private System.Windows.Data.ListCollectionView? _abilitiesView;
    private System.Windows.Data.ListCollectionView? _configView;
    private TimeSpan _loopInterval = TimeSpan.FromMilliseconds(350);
    private readonly IServiceProvider _services;
    private readonly IWritableSettings<HeraldHelperSettings> _writableSettings;
    private readonly AppDataStore _store;
    private readonly SettingsController _settingsController;
    private readonly OverlaySettingsController _overlaySettingsController;
    private readonly RuntimeController _runtimeController;
    private readonly AuthController _authController;
    private readonly ResponseDiagnosticsBuffer _responseDiagnostics;
    private readonly IShardAuthRefreshService _authRefreshService;
    private bool _isBindingControls;
    private ScreenRegion? _chatRegion;
    private ShardType _shardType;
    private int _resistPercent;
    private OcrEngineMode _ocrEngineMode;
    private readonly ObservableCollection<ConfigEntry> _cfgEntries = [];
    private readonly AbilityProfileController _abilityProfileController;
    private readonly DaocCharacterController _daocCharacterController;
    private readonly ThemeController _themeController;
    private readonly Dictionary<ShardType, WpfComboBox> _daocCharacterSelectors = new();
    private readonly Dictionary<ShardType, System.Windows.Controls.Button> _daocWindowButtons = new();
    private readonly Dictionary<ShardType, TextBlock> _daocCharacterSummaryBlocks = new();
    private OverlaySnapshot? _lastOverlaySnapshot;
    private DataBrowserWindow? _edenBrowserWindow;

    internal System.Windows.Controls.TextBox OutputBox => LiveView!.OutputBox;
    private System.Windows.Controls.TextBox DiagnosticsBox => LiveView!.DiagnosticsBox;
    private System.Windows.Controls.TextBox ResponseDiagnosticsBox => LiveView!.ResponseDiagnosticsBox;
    private System.Windows.Controls.StackPanel DaocCharacterPanel => ConfigView!.DaocCharacterPanel;
    private System.Windows.Controls.DataGrid ConfigGrid => ConfigView!.ConfigGrid;
    private System.Windows.Controls.ComboBox AbilityProfileServerCombo => AbilitiesView!.AbilityProfileServerCombo;
    private System.Windows.Controls.ComboBox AbilityProfileClassCombo => AbilitiesView!.AbilityProfileClassCombo;
    private System.Windows.Controls.TextBlock AbilityProfileSummaryText => AbilitiesView!.AbilityProfileSummaryText;
    private System.Windows.Controls.DataGrid AbilitiesGrid => AbilitiesView!.AbilitiesGrid;
    private System.Windows.Controls.CheckBox ShowTargetCheckbox => OverlayView!.ShowTargetCheckbox;
    private System.Windows.Controls.CheckBox ShowTimersCheckbox => OverlayView!.ShowTimersCheckbox;
    private System.Windows.Controls.CheckBox ShowCastBarCheckbox => OverlayView!.ShowCastBarCheckbox;
    private System.Windows.Controls.CheckBox DynamicCastSpeedCheckbox => OverlayView!.DynamicCastSpeedCheckbox;
    private System.Windows.Controls.CheckBox EstimatedSpellDamageCheckbox => OverlayView!.EstimatedSpellDamageCheckbox;
    private System.Windows.Controls.CheckBox OcrReplayCheckbox => OverlayView!.OcrReplayCheckbox;
    private System.Windows.Controls.TextBox CastingSpeedBonusText => OverlayView!.CastingSpeedBonusText;
    private System.Windows.Controls.TextBox SpellDamageBonusText => OverlayView!.SpellDamageBonusText;
    private System.Windows.Controls.TextBox OverlayXText => OverlayView!.OverlayXText;
    private System.Windows.Controls.TextBox OverlayYText => OverlayView!.OverlayYText;
    private System.Windows.Controls.TextBox OverlayTimerXText => OverlayView!.OverlayTimerXText;
    private System.Windows.Controls.TextBox OverlayTimerYText => OverlayView!.OverlayTimerYText;
    private System.Windows.Controls.TextBox OverlayFontSizeText => OverlayView!.OverlayFontSizeText;
    private System.Windows.Controls.TextBox OverlayTimerSizeText => OverlayView!.OverlayTimerSizeText;
    private System.Windows.Controls.TextBox OverlayCastXText => OverlayView!.OverlayCastXText;
    private System.Windows.Controls.TextBox OverlayCastYText => OverlayView!.OverlayCastYText;
    private System.Windows.Controls.TextBox TargetColorText => OverlayView!.TargetColorText;
    private System.Windows.Controls.CheckBox UseRealmColorsCheckbox => OverlayView!.UseRealmColorsCheckbox;
    private System.Windows.Controls.TextBox TimerColorText => OverlayView!.TimerColorText;
    private System.Windows.Controls.TextBox OutlineColorText => OverlayView!.OutlineColorText;
    private System.Windows.Controls.ComboBox OverlayTargetFontCombo => OverlayView!.OverlayTargetFontCombo;
    private System.Windows.Controls.ComboBox OverlayTimerFontCombo => OverlayView!.OverlayTimerFontCombo;
    private System.Windows.Controls.ComboBox OverlayCastbarFontCombo => OverlayView!.OverlayCastbarFontCombo;

    public static readonly System.Windows.Input.RoutedUICommand SelectLiveViewCommand = new(
        "Live", "SelectLiveView", typeof(MainWindow),
        new System.Windows.Input.InputGestureCollection { new System.Windows.Input.KeyGesture(System.Windows.Input.Key.D1, System.Windows.Input.ModifierKeys.Alt) });

    public static readonly System.Windows.Input.RoutedUICommand SelectConfigViewCommand = new(
        "Config", "SelectConfigView", typeof(MainWindow),
        new System.Windows.Input.InputGestureCollection { new System.Windows.Input.KeyGesture(System.Windows.Input.Key.D2, System.Windows.Input.ModifierKeys.Alt) });

    public static readonly System.Windows.Input.RoutedUICommand SelectAbilitiesViewCommand = new(
        "Abilities", "SelectAbilitiesView", typeof(MainWindow),
        new System.Windows.Input.InputGestureCollection { new System.Windows.Input.KeyGesture(System.Windows.Input.Key.D3, System.Windows.Input.ModifierKeys.Alt) });

    public static readonly System.Windows.Input.RoutedUICommand SelectOverlayViewCommand = new(
        "Overlay", "SelectOverlayView", typeof(MainWindow),
        new System.Windows.Input.InputGestureCollection { new System.Windows.Input.KeyGesture(System.Windows.Input.Key.D4, System.Windows.Input.ModifierKeys.Alt) });

    public static readonly System.Windows.Input.RoutedUICommand SelectHeraldViewCommand = new(
        "Herald", "SelectHeraldView", typeof(MainWindow),
        new System.Windows.Input.InputGestureCollection { new System.Windows.Input.KeyGesture(System.Windows.Input.Key.D5, System.Windows.Input.ModifierKeys.Alt) });

    public static readonly System.Windows.Input.RoutedUICommand ToggleLoopCommand = new(
        "Toggle Loop", "ToggleLoop", typeof(MainWindow),
        new System.Windows.Input.InputGestureCollection { new System.Windows.Input.KeyGesture(System.Windows.Input.Key.F6) });

    public static readonly System.Windows.Input.RoutedUICommand RunTickCommand = new(
        "Run Tick", "RunTick", typeof(MainWindow),
        new System.Windows.Input.InputGestureCollection { new System.Windows.Input.KeyGesture(System.Windows.Input.Key.F5) });

    public static readonly System.Windows.Input.RoutedUICommand SaveCommand = new(
        "Save", "Save", typeof(MainWindow),
        new System.Windows.Input.InputGestureCollection { new System.Windows.Input.KeyGesture(System.Windows.Input.Key.S, System.Windows.Input.ModifierKeys.Control) });

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        BindViewCommands();

        var fontFamilies = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        OverlayTargetFontCombo.ItemsSource = fontFamilies;
        OverlayTimerFontCombo.ItemsSource = fontFamilies;
        OverlayCastbarFontCombo.ItemsSource = fontFamilies;

        _services = services;
        _store = services.GetRequiredService<AppDataStore>();
        _settingsController = services.GetRequiredService<SettingsController>();
        _writableSettings = services.GetRequiredService<IWritableSettings<HeraldHelperSettings>>();
        _writableSettings.Load();
        _overlaySettingsController = services.GetRequiredService<OverlaySettingsController>();
        _abilityProfileController = services.GetRequiredService<AbilityProfileController>();
        _daocCharacterController = services.GetRequiredService<DaocCharacterController>();
        _themeController = services.GetRequiredService<ThemeController>();
        _responseDiagnostics = services.GetRequiredService<ResponseDiagnosticsBuffer>();
        _responseDiagnostics.LineAdded += OnResponseDiagnosticLineAdded;
        _liveOverlay = services.GetRequiredService<DesktopOverlayRenderer>();
        _liveOverlay.Rendered += (_, state) => LiveView?.UpdateMirror(state);
        _authRefreshService = services.GetRequiredService<IShardAuthRefreshService>();
        _runtimeController = services.GetRequiredService<RuntimeController>();
        _runtimeController.ConfigureLoopInput(
            () => new LoopTickInput(_chatRegion, _shardType, _resistPercent),
            _loopInterval);
        _runtimeController.TickCompleted += OnLoopTickCompleted;
        _runtimeController.TickFailed += message =>
        {
            OutputBox.Text = message;
            LoopStatusText.Text = $"Tick failed {DateTime.Now:HH:mm:ss}";
        };
        _authController = services.GetRequiredService<AuthController>();

        var legacyCfgPath = FindFilePath("cfg.ini");
        var legacyAbilitiesPath = FindFilePath("abilities.txt");
        LegacyTextImporter.ImportIfNeeded(_store, legacyCfgPath, legacyAbilitiesPath);
        _settingsController.EnsureDefaultAuthSettings();

        _authController.ConfigureTimer();

        RebuildRuntimeFromFiles();
        _configView = new System.Windows.Data.ListCollectionView(_cfgEntries)
        {
            Filter = MatchesConfigFilter
        };
        ConfigGrid.ItemsSource = _configView;
        _abilitiesView = new System.Windows.Data.ListCollectionView(_abilityProfileController.AbilityEntries)
        {
            Filter = MatchesAbilityFilter
        };
        AbilitiesGrid.ItemsSource = _abilitiesView;
        InitializeAbilityProfileControls();
        ReloadEditorData();
        LoadDaocCharacterProfiles();
        RepairInvalidSavedCustomWindowRegions();
        BuildDaocCharacterSelectorUi();
        ReloadOverlaySettingsFromStore();
        _themeController.Initialize();
        ThemeToggleButton.Content = _themeController.ToggleButtonContent;
        OutputBox.Text = "Ready.";
    }

    protected override void OnClosed(EventArgs e)
    {
        _runtimeController?.Dispose();
        _authController?.AuthRefreshTimer?.Stop();
        if (_responseDiagnostics is not null)
        {
            _responseDiagnostics.LineAdded -= OnResponseDiagnosticLineAdded;
        }
        _liveOverlay?.Dispose();
        base.OnClosed(e);
    }

    private void BindViewCommands()
    {
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SelectLiveViewCommand, (_, _) => SelectView(0)));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SelectConfigViewCommand, (_, _) => SelectView(1)));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SelectAbilitiesViewCommand, (_, _) => SelectView(2)));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SelectOverlayViewCommand, (_, _) => SelectView(3)));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SelectHeraldViewCommand, (_, _) => SelectView(4)));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(ToggleLoopCommand, (_, _) => ToggleLoop_Click(this, new System.Windows.RoutedEventArgs())));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(RunTickCommand, (_, _) => RunTick_Click(this, new System.Windows.RoutedEventArgs())));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(SaveCommand, (_, _) => SaveCurrentView()));
    }

    private void SelectView(int index)
    {
        if (_isBindingControls || SidebarList is null)
        {
            return;
        }

        SidebarList.SelectedIndex = index;
    }

    private void SaveCurrentView()
    {
        if (_isBindingControls)
        {
            return;
        }

        if (ConfigView.Visibility == System.Windows.Visibility.Visible)
        {
            SaveConfig_Click(this, new System.Windows.RoutedEventArgs());
        }
        else if (AbilitiesView.Visibility == System.Windows.Visibility.Visible)
        {
            SaveAbilities_Click(this, new System.Windows.RoutedEventArgs());
        }
        else if (OverlayView.Visibility == System.Windows.Visibility.Visible)
        {
            SaveOverlaySettings_Click(this, new System.Windows.RoutedEventArgs());
        }
    }

    internal void RebuildRuntimeFromFiles()
    {
        _runtimeController.Rebuild(
            _settingsController.LoadMap(),
            () => Dispatcher.BeginInvoke(UpdateRegionText));
        _chatRegion = _runtimeController.RuntimeSettings?.ChatRegion;
        _shardType = _runtimeController.RuntimeSettings?.ShardType ?? ShardType.Default;
        _resistPercent = _runtimeController.RuntimeSettings?.ResistPercent ?? 0;
        _ocrEngineMode = _runtimeController.RuntimeSettings?.OcrEngineMode ?? OcrEngineMode.Adaptive;
        BindControlsFromSettings();
        UpdateRegionText();
    }

    private void OnLoopTickCompleted(LoopTickResult tick)
    {
        // A UI hiccup here must not surface as a capture failure in the loop.
        try
        {
            OutputBox.Text = tick.Output;
            _lastOverlaySnapshot = tick.Snapshot;
            DiagnosticsBox.Text = tick.DiagnosticsText;
            LiveView?.UpdateClientState(tick.AdapterValues);
        }
        catch (Exception ex)
        {
            _responseDiagnostics.Log($"[UI] tick mirror failed: {ex.Message}");
        }
        LoopStatusText.Text = $"Last tick {DateTime.Now:HH:mm:ss}";
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

    internal void ReloadEditorData()
    {
        _cfgEntries.Clear();
        foreach (var entry in _settingsController.LoadEntries())
        {
            _cfgEntries.Add(entry);
        }

        _abilityProfileController.ReloadRows();
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;

        if (ConfigView?.CustomUiFolderText is not null)
        {
            ConfigView.CustomUiFolderText.Text = ReadCustomUiFolder() ?? string.Empty;
        }

        RefreshDaocCharacterSelectors();
    }

    private void InitializeAbilityProfileControls()
    {
        _isBindingControls = true;
        try
        {
            AbilityProfileServerCombo.ItemsSource = _abilityProfileController.ServerItems;
            var initialShard = _abilityProfileController.GetInitialShard(_shardType);
            _abilityProfileController.RefreshClasses(initialShard);
            AbilityProfileServerCombo.SelectedItem = _abilityProfileController.Shard;
            AbilityProfileClassCombo.ItemsSource = _abilityProfileController.ClassItems;
            AbilityProfileClassCombo.SelectedItem = _abilityProfileController.Class;
            AbilityProfileClassCombo.IsEnabled = _abilityProfileController.IsClassEnabled;
            AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
            _abilityProfileController.ReloadRows();
        }
        finally
        {
            _isBindingControls = false;
        }
    }

    private void LoadDaocCharacterProfiles()
    {
        _daocCharacterController.LoadProfiles();
    }

    private void RepairInvalidSavedCustomWindowRegions()
    {
        var settings = _settingsController.LoadMap();
        var updates = new List<ConfigEntry>();
        foreach (var (shard, profiles) in _daocCharacterController.Profiles)
        {
            foreach (var profile in profiles)
            {
                var normalizedKey = CharacterSettingsKeys.OcrWindows(shard, profile.CharacterName);
                var legacyKey = CharacterSettingsKeys.LegacyOcrWindows(shard, profile.CharacterName);
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
                catch (Exception ex)
                {
                    _responseDiagnostics?.Log(
                        $"[OCR] skipped unreadable saved window config for {shard}/{profile.CharacterName}: {ex.Message}");
                }
            }
        }
        if (updates.Count == 0)
        {
            return;
        }
        _settingsController.Save(updates);
        RebuildRuntimeFromFiles();
        _responseDiagnostics?.Log($"[OCR] repaired {updates.Count} saved custom-window configuration(s) from active UI XML.");
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

                var profiles = _daocCharacterController.GetProfiles(shard);
                combo.ItemsSource = profiles;
                var selectedName = ReadOrDefault(settings, CharacterSettingsKeys.SelectedCharacter(shard), string.Empty);
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
                        var ocrCount = _daocCharacterController.LoadOcrWindows(shard, selectedProfile.CharacterName).Count;
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

    private async void RunTick_Click(object sender, RoutedEventArgs e)
    {
        await _runtimeController.TickOnceAsync();
    }

    private void ToggleLoop_Click(object sender, RoutedEventArgs e)
    {
        if (_runtimeController.IsRunning)
        {
            _runtimeController.Stop();
            UpdateLoopButton();
            LoopStatusText.Text = "Loop stopped";
            return;
        }

        _runtimeController.Start();
        UpdateLoopButton();
        LoopStatusText.Text = $"Loop running · {_loopInterval}ms";
    }

    /// <summary>Reflect the loop's real state — rebuilds can swap the loop
    /// instance underneath the button.</summary>
    private void UpdateLoopButton()
    {
        var running = _runtimeController.IsRunning;
        ToggleLoopIcon.Kind = running
            ? MaterialDesignThemes.Wpf.PackIconKind.Stop
            : MaterialDesignThemes.Wpf.PackIconKind.Run;
        ToggleLoopText.Text = running ? "Stop" : "Start";
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
            CharacterSettingsKeys.SelectedCharacter(_shardType),
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
            var key = CharacterSettingsKeys.OcrStats(_shardType, character);
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
        _abilityProfileController.RefreshClasses(selected);
        AbilityProfileServerCombo.SelectedItem = _abilityProfileController.Shard;
        AbilityProfileClassCombo.ItemsSource = _abilityProfileController.ClassItems;
        AbilityProfileClassCombo.SelectedItem = _abilityProfileController.Class;
        AbilityProfileClassCombo.IsEnabled = _abilityProfileController.IsClassEnabled;
        AbilityProfileSummaryText.Text = _abilityProfileController.Summary;
        _abilityProfileController.ReloadRows();
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
        if (!int.TryParse(LoopMsText.Text, out var ms))
        {
            return;
        }

        ms = Math.Clamp(ms, 100, 5000);
        _loopInterval = TimeSpan.FromMilliseconds(ms);
        if (_runtimeController is not null)
        {
            _runtimeController.Interval = _loopInterval;
        }
    }

    private void UpdateRegionText()
    {
        var modeLabel = _shardType == ShardType.Default ? "Default mode" : $"Shard: {_shardType}";
        var statsLabel = BuildCharacterStatsLabel();
        var ocrWindowCount = _runtimeController.RuntimeSettings?.OcrWatchRegions.Count ?? 0;
        if (_chatRegion is null)
        {
            RegionText.Text = _shardType == ShardType.Default
                ? $"{modeLabel} | OCR: {_ocrEngineMode} | Resist: {_resistPercent}% | Region: not set | Independent mode"
                : $"Shard: {_shardType} | OCR: {_ocrEngineMode} | Resist: {_resistPercent}% | Region: not set | OCR windows: {ocrWindowCount} | {statsLabel}";
            return;
        }

        RegionText.Text = _shardType == ShardType.Default
            ? $"{modeLabel} | OCR: {_ocrEngineMode} | Resist: {_resistPercent}% | Region: X={_chatRegion.X}, Y={_chatRegion.Y}, W={_chatRegion.Width}, H={_chatRegion.Height} | Independent mode"
            : $"Shard: {_shardType} | OCR: {_ocrEngineMode} | Resist: {_resistPercent}% | Region: X={_chatRegion.X}, Y={_chatRegion.Y}, W={_chatRegion.Width}, H={_chatRegion.Height} | OCR windows: {ocrWindowCount} | {statsLabel}";
    }

    private string BuildCharacterStatsLabel()
    {
        if (_shardType == ShardType.Default)
        {
            return "Stats: inactive";
        }

        var map = _settingsController.LoadMap();
        var character = ReadOrDefault(map, CharacterSettingsKeys.SelectedCharacter(_shardType), string.Empty);
        var stats = _store.LoadCharacterStats(_shardType, character);
        return stats?.Dexterity is null
            ? "Stats: awaiting OCR"
            : $"Stats: Dex {stats.Dexterity} | Cast {stats.CastingSpeedPercent:0.##}% | Dmg {stats.SpellDamagePercent:0.##}%";
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

    private void SidebarList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isBindingControls ||
            LiveView is null ||
            ConfigView is null ||
            AbilitiesView is null ||
            OverlayView is null ||
            HeraldView is null ||
            SidebarList?.SelectedItem is not System.Windows.Controls.ListBoxItem item)
        {
            return;
        }

        LiveView.Visibility = item.Tag is "Live" ? Visibility.Visible : Visibility.Collapsed;
        ConfigView.Visibility = item.Tag is "Config" ? Visibility.Visible : Visibility.Collapsed;
        AbilitiesView.Visibility = item.Tag is "Abilities" ? Visibility.Visible : Visibility.Collapsed;
        OverlayView.Visibility = item.Tag is "Overlay" ? Visibility.Visible : Visibility.Collapsed;
        HeraldView.Visibility = item.Tag is "Herald" ? Visibility.Visible : Visibility.Collapsed;
        UpdateLoopButton();
        if (HeraldView.Visibility == Visibility.Visible)
        {
            RefreshHeraldResults();
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _themeController.ToggleDarkLight();
        ThemeToggleButton.Content = _themeController.ToggleButtonContent;
        ReloadEditorData();
    }

    private void OpenAppearanceSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new Views.AppearanceSettingsWindow(_themeController)
        {
            Owner = this
        };
        window.ShowDialog();
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

    internal void ReloadOverlaySettingsFromStore()
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
        BindToggle(UseRealmColorsCheckbox, settings.UseRealmColors, OverlayVisibilityChanged);

        var map = _settingsController.LoadMap();
        var character = ReadOrDefault(map, CharacterSettingsKeys.SelectedCharacter(_shardType), string.Empty);
        var stats = _store.LoadCharacterStats(_shardType, character);
        CastingSpeedBonusText.Text = (stats?.CastingSpeedPercent ?? 0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        SpellDamageBonusText.Text = (stats?.SpellDamagePercent ?? 0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        BindToggle(ShowTargetCheckbox, settings.ShowTarget, OverlayVisibilityChanged);
        BindToggle(ShowTimersCheckbox, settings.ShowTimers, OverlayVisibilityChanged);
        BindToggle(ShowCastBarCheckbox, settings.ShowCastBar, OverlayVisibilityChanged);
        BindToggle(DynamicCastSpeedCheckbox, settings.DynamicCastSpeedEnabled, OverlayVisibilityChanged);
        BindToggle(EstimatedSpellDamageCheckbox, settings.EstimatedSpellDamageEnabled, OverlayVisibilityChanged);
        BindToggle(OcrReplayCheckbox, settings.OcrReplayEnabled, OverlayVisibilityChanged);

        SelectFont(OverlayTargetFontCombo, settings.TargetFontFamily);
        SelectFont(OverlayTimerFontCombo, settings.TimerFontFamily);
        SelectFont(OverlayCastbarFontCombo, settings.CastbarFontFamily);
    }

    private static void SelectFont(System.Windows.Controls.ComboBox? comboBox, string fontFamily)
    {
        if (comboBox is null)
        {
            return;
        }

        var item = comboBox.Items.OfType<string>().FirstOrDefault(x => x.Equals(fontFamily, StringComparison.OrdinalIgnoreCase));
        comboBox.SelectedItem = item ?? "Segoe UI";
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
        _overlaySettingsController.SaveVisibility(
            ShowTargetCheckbox?.IsChecked ?? true,
            ShowTimersCheckbox?.IsChecked ?? true,
            ShowCastBarCheckbox?.IsChecked ?? true,
            UseRealmColorsCheckbox?.IsChecked ?? true,
            DynamicCastSpeedCheckbox?.IsChecked ?? false,
            EstimatedSpellDamageCheckbox?.IsChecked ?? false,
            OcrReplayCheckbox?.IsChecked ?? false);
        RebuildRuntimeFromFiles();
    }

    private void SaveCurrentCharacterStatBonuses()
    {
        var map = _settingsController.LoadMap();
        var character = ReadOrDefault(map, CharacterSettingsKeys.SelectedCharacter(_shardType), string.Empty);
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

        var snapshot = _runtimeController.LastSnapshot ?? _lastOverlaySnapshot;
        if (snapshot is null)
        {
            return;
        }

        _ = _liveOverlay.RenderAsync(snapshot, CancellationToken.None);
    }
}
