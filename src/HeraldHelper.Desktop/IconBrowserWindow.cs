using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HeraldHelper.Domain.Models;
using Binding = System.Windows.Data.Binding;
using Button = System.Windows.Controls.Button;
using DataGrid = System.Windows.Controls.DataGrid;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace HeraldHelper.Desktop;

internal sealed class IconBrowserWindow : Window
{
    private readonly ObservableCollection<IconRow> _rows = [];
    private readonly ICollectionView _view;
    private readonly TextBox _searchBox;
    private readonly DataGrid _grid;
    private readonly bool _useBlackthornIcons;

    public IconBrowserWindow(IconSpriteRef? selected, bool useBlackthornIcons = false)
    {
        _useBlackthornIcons = useBlackthornIcons;
        Title = "Choose Icon";
        Width = 920;
        Height = 680;
        MinWidth = 760;
        MinHeight = 520;
        Background = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsWindowBackgroundBrush"];
        Foreground = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsTextBrush"];

        _searchBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(10, 6, 10, 6),
            MinWidth = 280,
            ToolTip = "Search by icon id, sprite sheet, mapping type, or class",
            Background = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsPanelAltBrush"],
            Foreground = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsTextBrush"],
            BorderBrush = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsAccentBrush"]
        };

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            RowHeight = 44,
            Background = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsPanelBrush"],
            BorderBrush = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsBorderBrush"],
            RowBackground = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsPanelBrush"],
            AlternatingRowBackground = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsPanelAltBrush"],
            HorizontalGridLinesBrush = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsBorderBrush"],
            VerticalGridLinesBrush = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsBorderBrush"],
            SelectionMode = DataGridSelectionMode.Single
        };
        _grid.MouseDoubleClick += (_, _) =>
        {
            if (_grid.SelectedItem is IconRow row)
            {
                SelectedIcon = row.Icon;
                DialogResult = true;
                Close();
            }
        };

        var iconTemplate = new DataTemplate();
        var iconFactory = new FrameworkElementFactory(typeof(Image));
        iconFactory.SetBinding(Image.SourceProperty, new Binding(nameof(IconRow.Image)));
        iconFactory.SetValue(Image.WidthProperty, 32.0);
        iconFactory.SetValue(Image.HeightProperty, 32.0);
        iconTemplate.VisualTree = iconFactory;

        _grid.Columns.Add(new DataGridTemplateColumn { Header = "", Width = 46, CellTemplate = iconTemplate });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Icon Id", Binding = new Binding(nameof(IconRow.IconId)), Width = 90 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Sheet", Binding = new Binding(nameof(IconRow.SpriteSheet)), Width = 150 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Class", Binding = new Binding(nameof(IconRow.SpriteClass)), Width = 80 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Mapping", Binding = new Binding(nameof(IconRow.MappingType)), Width = 120 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Coords", Binding = new Binding(nameof(IconRow.Coordinates)), Width = 110 });

        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = FilterRow;
        _grid.ItemsSource = _view;
        _searchBox.TextChanged += (_, _) => _view.Refresh();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var clearButton = new Button { Content = "Clear Icon", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        clearButton.Click += (_, _) =>
        {
            SelectedIcon = null;
            DialogResult = true;
            Close();
        };
        var cancelButton = new Button { Content = "Cancel", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        cancelButton.Click += (_, _) => Close();
        var chooseButton = new Button { Content = "Choose", Padding = new Thickness(12, 6, 12, 6) };
        chooseButton.Click += (_, _) =>
        {
            if (_grid.SelectedItem is not IconRow row)
            {
                return;
            }

            SelectedIcon = row.Icon;
            DialogResult = true;
            Close();
        };
        buttons.Children.Add(clearButton);
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(chooseButton);

        var root = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(_searchBox, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(_searchBox);
        root.Children.Add(buttons);
        root.Children.Add(_grid);
        Content = root;

        LoadRows(selected);
    }

    public IconSpriteRef? SelectedIcon { get; private set; }

    private void LoadRows(IconSpriteRef? selected)
    {
        var loader = new IconImageLoader();
        IconRow? selectedRow = null;
        var entries = _useBlackthornIcons ? IconCatalog.LoadBlackthorn() : IconCatalog.Load();
        foreach (var entry in entries)
        {
            var row = new IconRow
            {
                IconId = entry.IconId,
                SpriteSheet = entry.SpriteSheet,
                SpriteClass = entry.SpriteClass,
                MappingType = entry.MappingType,
                Coordinates = $"{entry.Icon.X},{entry.Icon.Y}",
                SearchText = $"{entry.IconId} {entry.SpriteSheet} {entry.SpriteClass} {entry.MappingType}".ToLowerInvariant(),
                Icon = entry.Icon,
                Image = loader.Load(entry.Icon)
            };
            _rows.Add(row);

            if (selected is not null &&
                selected.SpriteSheet == entry.Icon.SpriteSheet &&
                selected.X == entry.Icon.X &&
                selected.Y == entry.Icon.Y &&
                selected.Width == entry.Icon.Width &&
                selected.Height == entry.Icon.Height &&
                selected.BorderIndex == entry.Icon.BorderIndex &&
                selected.SpellBadgeIndex == entry.Icon.SpellBadgeIndex)
            {
                selectedRow = row;
            }
        }

        if (selectedRow is not null)
        {
            _grid.SelectedItem = selectedRow;
            _grid.ScrollIntoView(selectedRow);
        }
    }

    private bool FilterRow(object obj)
    {
        if (obj is not IconRow row)
        {
            return false;
        }

        var query = _searchBox.Text.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(query) || row.SearchText.Contains(query, StringComparison.Ordinal);
    }

    private sealed class IconRow
    {
        public required int IconId { get; init; }
        public required string SpriteSheet { get; init; }
        public required int SpriteClass { get; init; }
        public required string MappingType { get; init; }
        public required string Coordinates { get; init; }
        public required string SearchText { get; init; }
        public required IconSpriteRef Icon { get; init; }
        public required System.Windows.Media.ImageSource? Image { get; init; }
    }
}
