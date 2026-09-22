using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfApplication = System.Windows.Application;
using WpfBinding = System.Windows.Data.Binding;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfDataGrid = System.Windows.Controls.DataGrid;
using WpfDataGridHeadersVisibility = System.Windows.Controls.DataGridHeadersVisibility;
using WpfDataGridLength = System.Windows.Controls.DataGridLength;
using WpfDataGridLengthUnitType = System.Windows.Controls.DataGridLengthUnitType;
using WpfDataGridSelectionMode = System.Windows.Controls.DataGridSelectionMode;
using WpfDataGridTemplateColumn = System.Windows.Controls.DataGridTemplateColumn;
using WpfDataGridTextColumn = System.Windows.Controls.DataGridTextColumn;
using WpfImage = System.Windows.Controls.Image;
using WpfImageSource = System.Windows.Media.ImageSource;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace HeraldHelper.Desktop;

/// <summary>
/// Pick a spell/style/ability straight from the shard's charplan catalog —
/// pre-fills the exact ability name the parser needs to match plus the
/// category and level. Duration and effect type stay user-confirmed.
/// </summary>
public sealed class AbilityCatalogPickerWindow : Window
{
    private readonly WpfDataGrid _grid;
    private readonly WpfTextBox _search;
    private readonly IconImageLoader _iconLoader = new();
    private readonly List<Row> _rows;

    public CatalogBrowserEntry? SelectedEntry { get; private set; }

    private AbilityCatalogPickerWindow(IReadOnlyList<CatalogBrowserEntry> entries, string? className)
    {
        Title = string.IsNullOrWhiteSpace(className)
            ? "Add ability from catalog"
            : $"Add ability from catalog — {className}";
        Width = 720;
        Height = 560;
        MinWidth = 480;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (WpfBrush?)WpfApplication.Current.Resources["VsWindowBackgroundBrush"];
        Foreground = (WpfBrush?)WpfApplication.Current.Resources["VsTextBrush"];

        _rows = entries
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new Row(this, e))
            .ToList();

        var root = new DockPanel { Margin = new Thickness(12) };

        _search = new WpfTextBox { Margin = new Thickness(0, 0, 0, 8) };
        _search.SetResourceReference(System.Windows.Controls.Control.StyleProperty, "CompactInputTextBoxStyle");
        MaterialDesignThemes.Wpf.HintAssist.SetHint(_search, "Filter by name, type or category");
        _search.TextChanged += (_, _) => ApplyFilter();

        var buttons = new WrapPanel
        {
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        var add = new WpfButton { Content = "Add", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new WpfButton { Content = "Cancel", Padding = new Thickness(14, 6, 14, 6), IsCancel = true };
        add.Click += (_, _) => TryAccept();
        buttons.Children.Add(add);
        buttons.Children.Add(cancel);

        _grid = new WpfDataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = WpfDataGridHeadersVisibility.Column,
            SelectionMode = WpfDataGridSelectionMode.Single,
            ItemsSource = _rows
        };
        _grid.Columns.Add(new WpfDataGridTemplateColumn
        {
            Header = string.Empty,
            Width = 34,
            CellTemplate = BuildIconTemplate()
        });
        _grid.Columns.Add(new WpfDataGridTextColumn { Header = "Name", Binding = new WpfBinding(nameof(Row.Name)), Width = new WpfDataGridLength(2, WpfDataGridLengthUnitType.Star) });
        _grid.Columns.Add(new WpfDataGridTextColumn { Header = "Type", Binding = new WpfBinding(nameof(Row.EntryType)), Width = 100 });
        _grid.Columns.Add(new WpfDataGridTextColumn { Header = "Category", Binding = new WpfBinding(nameof(Row.Category)), Width = new WpfDataGridLength(1, WpfDataGridLengthUnitType.Star) });
        _grid.Columns.Add(new WpfDataGridTextColumn { Header = "Level", Binding = new WpfBinding(nameof(Row.Level)), Width = 60 });
        _grid.MouseDoubleClick += (_, _) => TryAccept();

        DockPanel.SetDock(_search, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(_search);
        root.Children.Add(buttons);
        root.Children.Add(_grid);
        Content = root;
    }

    private void TryAccept()
    {
        if (_grid.SelectedItem is Row row)
        {
            SelectedEntry = row.Entry;
            DialogResult = true;
        }
        Close();
    }

    private void ApplyFilter()
    {
        var fragment = _search.Text.Trim();
        _grid.ItemsSource = string.IsNullOrWhiteSpace(fragment)
            ? _rows
            : _rows.Where(r =>
                r.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
                r.EntryType.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
                r.Category.Contains(fragment, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private DataTemplate BuildIconTemplate()
    {
        var image = new FrameworkElementFactory(typeof(WpfImage));
        image.SetValue(WpfImage.WidthProperty, 22.0);
        image.SetValue(WpfImage.HeightProperty, 22.0);
        image.SetValue(WpfImage.MarginProperty, new Thickness(2));
        image.SetBinding(WpfImage.SourceProperty, new WpfBinding(nameof(Row.Icon)));
        return new DataTemplate { VisualTree = image };
    }

    public static CatalogBrowserEntry? Pick(
        Window owner,
        IReadOnlyList<CatalogBrowserEntry> entries,
        string? className)
    {
        var filtered = string.IsNullOrWhiteSpace(className)
            ? entries
            : entries
                .Where(e => e.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var dialog = new AbilityCatalogPickerWindow(filtered, className) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.SelectedEntry : null;
    }

    private sealed class Row
    {
        public Row(AbilityCatalogPickerWindow owner, CatalogBrowserEntry entry)
        {
            Entry = entry;
            Name = entry.Name;
            EntryType = entry.EntryType;
            Category = entry.Category;
            Level = entry.Level?.ToString() ?? string.Empty;
            Icon = entry.Icon is null ? null : owner._iconLoader.Load(entry.Icon);
        }

        public CatalogBrowserEntry Entry { get; }
        public string Name { get; }
        public string EntryType { get; }
        public string Category { get; }
        public string Level { get; }
        public WpfImageSource? Icon { get; }
    }
}
