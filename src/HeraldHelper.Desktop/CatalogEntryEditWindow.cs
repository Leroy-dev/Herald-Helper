using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HeraldHelper.Domain.Models;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using Image = System.Windows.Controls.Image;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace HeraldHelper.Desktop;

internal sealed class CatalogEntryEditWindow : Window
{
    private readonly CatalogBrowserEntry _baseEntry;
    private readonly TextBox _nameBox;
    private readonly TextBox _typeBox;
    private readonly TextBox _classBox;
    private readonly TextBox _categoryBox;
    private readonly TextBox _levelBox;
    private readonly TextBox _castTimeBox;
    private readonly TextBox _summaryBox;
    private readonly TextBox _detailsBox;
    private readonly Image _iconPreview;
    private readonly IconImageLoader _iconLoader = new();

    public CatalogEntryEditWindow(CatalogBrowserEntry entry)
    {
        _baseEntry = entry;
        CurrentIcon = entry.Icon;

        Title = $"Edit {entry.Name}";
        Width = 720;
        Height = 760;
        MinWidth = 620;
        MinHeight = 640;
        Background = (Brush?)System.Windows.Application.Current.Resources["VsWindowBackgroundBrush"];
        Foreground = (Brush?)System.Windows.Application.Current.Resources["VsTextBrush"];

        _nameBox = CreateTextBox(entry.Name);
        _typeBox = CreateTextBox(entry.EntryType);
        _classBox = CreateTextBox(entry.ClassName);
        _categoryBox = CreateTextBox(entry.Category);
        _levelBox = CreateTextBox(entry.Level?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        _castTimeBox = CreateTextBox(entry.CastTimeSeconds?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty);
        _summaryBox = CreateTextBox(entry.Summary);
        _detailsBox = CreateTextBox(entry.Details, acceptsReturn: true, minHeight: 220);
        _iconPreview = new Image
        {
            Width = 48,
            Height = 48,
            Margin = new Thickness(0, 0, 12, 0),
            Stretch = Stretch.UniformToFill,
            Source = _iconLoader.Load(CurrentIcon)
        };
        RenderOptions.SetBitmapScalingMode(_iconPreview, BitmapScalingMode.NearestNeighbor);

        var form = new StackPanel
        {
            Margin = new Thickness(16)
        };
        form.Children.Add(MakeField("Name", _nameBox));
        form.Children.Add(MakeField("Type", _typeBox));
        form.Children.Add(MakeField("Class", _classBox));
        form.Children.Add(MakeField("Category", _categoryBox));
        form.Children.Add(MakeField("Level", _levelBox));
        form.Children.Add(MakeField("Cast Time (seconds)", _castTimeBox));
        form.Children.Add(MakeField("Summary", _summaryBox));
        form.Children.Add(MakeField("Details", _detailsBox));

        var iconRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconRow.Children.Add(_iconPreview);
        var chooseIconButton = new Button { Content = "Choose Icon", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        chooseIconButton.Click += (_, _) => ChooseIcon();
        var clearIconButton = new Button { Content = "Clear Icon", Padding = new Thickness(12, 6, 12, 6) };
        clearIconButton.Click += (_, _) =>
        {
            CurrentIcon = null;
            RefreshIconPreview();
        };
        iconRow.Children.Add(chooseIconButton);
        iconRow.Children.Add(clearIconButton);
        form.Children.Add(iconRow);

        var note = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            Foreground = (Brush?)System.Windows.Application.Current.Resources["VsMutedTextBrush"],
            Text = "Saving creates a local override. Reset removes it and falls back to the selected server source data."
        };
        form.Children.Add(note);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var resetButton = new Button { Content = "Reset Override", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        resetButton.Click += (_, _) =>
        {
            ResetRequested = true;
            DialogResult = true;
            Close();
        };
        var cancelButton = new Button { Content = "Cancel", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        cancelButton.Click += (_, _) => Close();
        var saveButton = new Button { Content = "Save", Padding = new Thickness(12, 6, 12, 6) };
        saveButton.Click += (_, _) => Save();
        buttonRow.Children.Add(resetButton);
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(saveButton);
        form.Children.Add(buttonRow);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = form
        };
    }

    public CatalogEntryOverride? EntryOverride { get; private set; }

    public bool ResetRequested { get; private set; }

    private IconSpriteRef? CurrentIcon { get; set; }

    private void ChooseIcon()
    {
        var useBlackthornIcons = _baseEntry.EntryKey.StartsWith("blackthorn|", StringComparison.OrdinalIgnoreCase);
        var dialog = new IconBrowserWindow(CurrentIcon, useBlackthornIcons)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            CurrentIcon = dialog.SelectedIcon;
            RefreshIconPreview();
        }
    }

    private void RefreshIconPreview()
    {
        _iconPreview.Source = _iconLoader.Load(CurrentIcon);
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show(this, "Name is required.", "Spell Browser", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int? level = null;
        if (!string.IsNullOrWhiteSpace(_levelBox.Text))
        {
            if (!int.TryParse(_levelBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLevel))
            {
                MessageBox.Show(this, "Level must be an integer.", "Spell Browser", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            level = parsedLevel;
        }

        double? castTime = null;
        if (!string.IsNullOrWhiteSpace(_castTimeBox.Text))
        {
            if (!double.TryParse(_castTimeBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedCastTime))
            {
                MessageBox.Show(this, "Cast time must be a number in seconds.", "Spell Browser", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            castTime = parsedCastTime;
        }

        EntryOverride = new CatalogEntryOverride(
            _baseEntry.EntryKey,
            _nameBox.Text.Trim(),
            _typeBox.Text.Trim(),
            _classBox.Text.Trim(),
            _categoryBox.Text.Trim(),
            level,
            castTime,
            _summaryBox.Text.Trim(),
            _detailsBox.Text.Trim(),
            CurrentIcon);

        DialogResult = true;
        Close();
    }

    private static FrameworkElement MakeField(string label, Control control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (Brush?)System.Windows.Application.Current.Resources["VsTextBrush"]
        });
        panel.Children.Add(control);
        return panel;
    }

    private static TextBox CreateTextBox(string text, bool acceptsReturn = false, double minHeight = 0)
    {
        return new TextBox
        {
            Text = text,
            Padding = new Thickness(10, 6, 10, 6),
            AcceptsReturn = acceptsReturn,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = acceptsReturn ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            MinHeight = minHeight,
            Background = (Brush?)System.Windows.Application.Current.Resources["VsPanelAltBrush"],
            Foreground = (Brush?)System.Windows.Application.Current.Resources["VsTextBrush"],
            BorderBrush = (Brush?)System.Windows.Application.Current.Resources["VsBorderBrush"]
        };
    }
}
