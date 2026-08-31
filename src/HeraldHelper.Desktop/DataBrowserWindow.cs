using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HeraldHelper.Domain.Enums;
using Binding = System.Windows.Data.Binding;
using DataGrid = System.Windows.Controls.DataGrid;
using DataGridCell = System.Windows.Controls.DataGridCell;
using DataGridColumnHeader = System.Windows.Controls.Primitives.DataGridColumnHeader;
using Image = System.Windows.Controls.Image;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfControl = System.Windows.Controls.Control;
using WpfComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;

namespace HeraldHelper.Desktop;

public sealed class DataBrowserWindow : Window
{
    private readonly AppDataStore _store;
    private readonly ObservableCollection<CatalogBrowserRow> _rows = [];
    private readonly ICollectionView _view;
    private readonly TextBox _searchBox;
    private readonly TextBlock _countText;
    private readonly WpfComboBox _serverCombo;
    private ShardType _server;
    private IReadOnlyList<CatalogBrowserEntry> _entries = [];

    public DataBrowserWindow(AppDataStore store, ShardType initialServer = ShardType.Eden)
    {
        _store = store;
        _server = initialServer == ShardType.Blackthorn ? ShardType.Blackthorn : ShardType.Eden;
        Title = "DAoC Spell Browser";
        Width = 1180;
        Height = 760;
        MinWidth = 980;
        MinHeight = 620;
        Background = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsWindowBackgroundBrush"];
        Foreground = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsTextBrush"];

        _searchBox = new TextBox
        {
            MinWidth = 300,
            Margin = new Thickness(0, 0, 12, 0),
            Padding = new Thickness(10, 6, 10, 6),
            ToolTip = "Search by name, class, category, type, or details",
            Background = (MediaBrush?)System.Windows.Application.Current.Resources["VsPanelAltBrush"],
            Foreground = (MediaBrush?)System.Windows.Application.Current.Resources["VsTextBrush"],
            BorderBrush = (MediaBrush?)System.Windows.Application.Current.Resources["VsAccentBrush"],
            CaretBrush = (MediaBrush?)System.Windows.Application.Current.Resources["VsTextBrush"]
        };
        _searchBox.TextChanged += (_, _) => RefreshFilter();

        _countText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (MediaBrush?)System.Windows.Application.Current.Resources["VsMutedTextBrush"]
        };

        _serverCombo = new WpfComboBox
        {
            Width = 140,
            Margin = new Thickness(0, 0, 16, 0),
            Padding = new Thickness(8, 5, 8, 5),
            ItemsSource = new[] { ShardType.Eden, ShardType.Blackthorn },
            SelectedItem = _server,
            Background = (MediaBrush?)System.Windows.Application.Current.Resources["VsPanelAltBrush"],
            Foreground = (MediaBrush?)System.Windows.Application.Current.Resources["VsTextBrush"]
        };
        _serverCombo.SelectionChanged += (_, _) => ChangeServer();

        var toolbar = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 10)
        };
        toolbar.Children.Add(new TextBlock
        {
            Text = "Server",
            Margin = new Thickness(0, 6, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (MediaBrush?)System.Windows.Application.Current.Resources["VsTextBrush"]
        });
        toolbar.Children.Add(_serverCombo);
        toolbar.Children.Add(new TextBlock
        {
            Text = "Search",
            Margin = new Thickness(0, 6, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (MediaBrush?)System.Windows.Application.Current.Resources["VsTextBrush"]
        });
        toolbar.Children.Add(_searchBox);
        toolbar.Children.Add(_countText);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowHeight = 48,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            Background = (MediaBrush?)System.Windows.Application.Current.Resources["VsPanelBrush"],
            BorderBrush = (MediaBrush?)System.Windows.Application.Current.Resources["VsBorderBrush"],
            Foreground = (MediaBrush?)System.Windows.Application.Current.Resources["VsTextBrush"],
            RowBackground = (MediaBrush?)System.Windows.Application.Current.Resources["VsPanelBrush"],
            AlternatingRowBackground = (MediaBrush?)System.Windows.Application.Current.Resources["VsPanelAltBrush"],
            HorizontalGridLinesBrush = (MediaBrush?)System.Windows.Application.Current.Resources["VsBorderBrush"],
            VerticalGridLinesBrush = (MediaBrush?)System.Windows.Application.Current.Resources["VsBorderBrush"],
            EnableRowVirtualization = true,
            EnableColumnVirtualization = true
        };
        VirtualizingPanel.SetIsVirtualizing(grid, true);
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
        grid.MouseDoubleClick += (_, _) => OpenSelectedEntryEditor(grid);

        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(WpfControl.BackgroundProperty, (MediaBrush?)System.Windows.Application.Current.Resources["VsTitleBarBrush"]));
        headerStyle.Setters.Add(new Setter(WpfControl.ForegroundProperty, (MediaBrush?)System.Windows.Application.Current.Resources["VsTextBrush"]));
        headerStyle.Setters.Add(new Setter(WpfControl.BorderBrushProperty, (MediaBrush?)System.Windows.Application.Current.Resources["VsBorderBrush"]));
        headerStyle.Setters.Add(new Setter(WpfControl.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        headerStyle.Setters.Add(new Setter(WpfControl.PaddingProperty, new Thickness(8, 6, 8, 6)));
        headerStyle.Setters.Add(new Setter(WpfControl.FontWeightProperty, FontWeights.SemiBold));
        grid.ColumnHeaderStyle = headerStyle;

        var iconTemplate = new DataTemplate();
        var iconFactory = new FrameworkElementFactory(typeof(Image));
        iconFactory.SetBinding(Image.SourceProperty, new Binding(nameof(CatalogBrowserRow.IconImage)));
        iconFactory.SetValue(Image.WidthProperty, 32.0);
        iconFactory.SetValue(Image.HeightProperty, 32.0);
        iconFactory.SetValue(Image.MarginProperty, new Thickness(4, 2, 4, 2));
        iconFactory.SetValue(Image.VerticalAlignmentProperty, VerticalAlignment.Center);
        iconTemplate.VisualTree = iconFactory;

        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "",
            Width = 42,
            CellTemplate = iconTemplate
        });
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(CatalogBrowserRow.Name)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new Binding(nameof(CatalogBrowserRow.EntryType)), Width = 130 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Class", Binding = new Binding(nameof(CatalogBrowserRow.ClassName)), Width = 140 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Category", Binding = new Binding(nameof(CatalogBrowserRow.Category)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cast", Binding = new Binding(nameof(CatalogBrowserRow.CastTimeDisplay)), Width = 80 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Summary", Binding = new Binding(nameof(CatalogBrowserRow.Summary)), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(ToolTipProperty, new Binding(nameof(CatalogBrowserRow.Details))));
        rowStyle.Setters.Add(new Setter(WpfControl.ForegroundProperty, (MediaBrush?)System.Windows.Application.Current.Resources["VsTextBrush"]));
        rowStyle.Setters.Add(new Setter(WpfControl.BackgroundProperty, (MediaBrush?)System.Windows.Application.Current.Resources["VsPanelBrush"]));
        rowStyle.Setters.Add(new Setter(WpfControl.BorderBrushProperty, (MediaBrush?)System.Windows.Application.Current.Resources["VsBorderBrush"]));
        var hoverTrigger = new Trigger
        {
            Property = DataGridRow.IsMouseOverProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new Setter(WpfControl.BackgroundProperty, (MediaBrush?)System.Windows.Application.Current.Resources["VsPanelAltBrush"]));
        rowStyle.Triggers.Add(hoverTrigger);
        grid.RowStyle = rowStyle;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(WpfControl.PaddingProperty, new Thickness(8, 4, 8, 4)));
        cellStyle.Setters.Add(new Setter(WpfControl.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(WpfControl.BackgroundProperty, MediaBrushes.Transparent));
        cellStyle.Setters.Add(new Setter(WpfControl.ForegroundProperty, (MediaBrush?)System.Windows.Application.Current.Resources["VsTextBrush"]));
        var selectedTrigger = new Trigger
        {
            Property = DataGridCell.IsSelectedProperty,
            Value = true
        };
        selectedTrigger.Setters.Add(new Setter(WpfControl.BackgroundProperty, (MediaBrush?)System.Windows.Application.Current.Resources["VsAccentBrush"]));
        selectedTrigger.Setters.Add(new Setter(WpfControl.ForegroundProperty, MediaBrushes.White));
        cellStyle.Triggers.Add(selectedTrigger);
        grid.CellStyle = cellStyle;

        _view = CollectionViewSource.GetDefaultView(_rows);
        grid.ItemsSource = _view;

        var root = new DockPanel
        {
            Margin = new Thickness(14)
        };
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(grid);
        Content = root;

        LoadRows();
    }

    private void LoadRows()
    {
        _rows.Clear();
        _entries = LoadEntries();
        foreach (var entry in _entries)
        {
            _rows.Add(new CatalogBrowserRow
            {
                EntryKey = entry.EntryKey,
                Name = entry.Name,
                EntryType = entry.EntryType,
                ClassName = entry.ClassName,
                Category = entry.Category,
                Summary = entry.Summary,
                Details = entry.Details,
                CastTimeDisplay = entry.CastTimeSeconds is null ? string.Empty : $"{entry.CastTimeSeconds:0.#}s",
                SearchText = $"{entry.Name} {entry.EntryType} {entry.ClassName} {entry.Category} {entry.Summary} {entry.Details}".ToLowerInvariant(),
                Icon = entry.Icon
            });
        }

        UpdateCount();
    }

    private void OpenSelectedEntryEditor(DataGrid grid)
    {
        if (grid.SelectedItem is not CatalogBrowserRow selected)
        {
            return;
        }

        var currentEntry = _entries
            .FirstOrDefault(x => string.Equals(x.EntryKey, selected.EntryKey, StringComparison.OrdinalIgnoreCase));
        if (currentEntry is null)
        {
            return;
        }

        var dialog = new CatalogEntryEditWindow(currentEntry)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.ResetRequested)
        {
            _store.DeleteCatalogEntryOverride(currentEntry.EntryKey);
        }
        else if (dialog.EntryOverride is not null)
        {
            _store.SaveCatalogEntryOverride(dialog.EntryOverride);
        }

        LoadRows();
        _view.Refresh();
    }

    private IReadOnlyList<CatalogBrowserEntry> LoadEntries()
    {
        var overrides = _store.LoadCatalogEntryOverrides();
        return _server == ShardType.Blackthorn
            ? BlackthornDataBrowserCatalog.Load(overrides)
            : EdenDataBrowserCatalog.Load(overrides);
    }

    private void ChangeServer()
    {
        if (_serverCombo.SelectedItem is not ShardType selected || selected == _server)
        {
            return;
        }

        _server = selected;
        Title = $"{_server} Spell Browser";
        LoadRows();
        RefreshFilter();
    }

    private void RefreshFilter()
    {
        _view.Filter = MatchesSearch;
        _view.Refresh();
        UpdateCount();
    }

    private bool MatchesSearch(object obj)
    {
        if (obj is not CatalogBrowserRow row)
        {
            return false;
        }

        var query = _searchBox.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return row.SearchText.Contains(query, StringComparison.Ordinal);
    }

    private void UpdateCount()
    {
        _countText.Text = $"{_view.Cast<object>().Count()} entries";
    }
}
