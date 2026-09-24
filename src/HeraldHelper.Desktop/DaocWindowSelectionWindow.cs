using System.Windows;
using System.Windows.Controls;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace HeraldHelper.Desktop;

public sealed class DaocWindowSelectionWindow : Window
{
    private readonly DaocWindowPreviewWindow _preview = new();
    private readonly List<SelectionItem> _items = [];
    private readonly TextBlock _selectionSummary;

    public IReadOnlyList<DaocWindowDefinition> SelectedWindows => _items.Where(x => x.CheckBox.IsChecked == true).Select(x => x.Definition).ToList();

    public DaocWindowSelectionWindow(DaocCharacterProfile profile, IReadOnlyCollection<OcrWatchRegion> initiallySelected)
    {
        Title = $"OCR Windows - {profile.CharacterName}";
        Width = 760;
        Height = 620;
        MinWidth = 640;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsWindowBackgroundBrush"];
        Foreground = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsTextBrush"];

        _selectionSummary = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = (System.Windows.Media.Brush?)System.Windows.Application.Current.Resources["VsMutedTextBrush"]
        };

        var root = new DockPanel { Margin = new Thickness(14) };

        var buttons = new WrapPanel { Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var selectAll = new WpfButton { Content = "Select All", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        var clearAll = new WpfButton { Content = "Clear", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        var adjust = new WpfButton { Content = "Adjust Selected (Drag)", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        var ok = new WpfButton { Content = "OK", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        var cancel = new WpfButton { Content = "Cancel", Padding = new Thickness(12, 6, 12, 6) };

        selectAll.Click += (_, _) =>
        {
            foreach (var item in _items)
            {
                item.CheckBox.IsChecked = true;
            }
            RefreshPreview();
        };

        clearAll.Click += (_, _) =>
        {
            foreach (var item in _items)
            {
                item.CheckBox.IsChecked = false;
            }
            RefreshPreview();
        };

        adjust.Click += (_, _) => AdjustSelectedRegion();

        ok.Click += (_, _) =>
        {
            try
            {
                DialogResult = true;
            }
            catch (InvalidOperationException)
            {
                // Modal session already ended (window was hidden/shown) —
                // still close rather than crash.
            }
            Close();
        };

        cancel.Click += (_, _) =>
        {
            try
            {
                DialogResult = false;
            }
            catch (InvalidOperationException)
            {
            }
            Close();
        };

        buttons.Children.Add(selectAll);
        buttons.Children.Add(clearAll);
        buttons.Children.Add(adjust);
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var list = new StackPanel();
        foreach (var window in profile.Windows.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase))
        {
            var checkBox = new WpfCheckBox
            {
                Content = window.DisplayText,
                ToolTip = window.SizeSource is null ? null : $"Size source: {window.SizeSource}",
                IsChecked = initiallySelected.Any(x => string.Equals(x.Key, window.Key, StringComparison.OrdinalIgnoreCase)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            checkBox.Checked += (_, _) => RefreshPreview();
            checkBox.Unchecked += (_, _) => RefreshPreview();
            _items.Add(new SelectionItem(window, checkBox));
            list.Children.Add(checkBox);
        }

        var scroll = new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        DockPanel.SetDock(_selectionSummary, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(_selectionSummary);
        root.Children.Add(scroll);
        root.Children.Add(buttons);
        Content = root;

        RefreshPreview();
    }

    protected override void OnClosed(EventArgs e)
    {
        _preview.Close();
        base.OnClosed(e);
    }

    private void RefreshPreview()
    {
        var selected = SelectedWindows;
        _selectionSummary.Text = selected.Count == 0
            ? "No OCR windows selected."
            : $"Selected OCR windows: {string.Join(", ", selected.Select(x => x.Label))}";
        _preview.UpdateWindows(selected, _items.Select(x => x.Definition).ToList());
    }

    private void AdjustSelectedRegion()
    {
        var selected = _items.Where(x => x.CheckBox.IsChecked == true).ToList();
        if (selected.Count != 1)
        {
            _selectionSummary.Text = "Select exactly one OCR window before adjusting its rectangle.";
            return;
        }

        _preview.Hide();
        // This window is shown via ShowDialog — Hide() would end the modal
        // session and the follow-up Show() would reopen it modeless, after
        // which setting DialogResult on OK/Cancel throws. Stay shown but
        // invisible/inert while the drag picker owns the screen.
        Opacity = 0;
        IsHitTestVisible = false;
        try
        {
            var region = RegionSelectionWindow.Select(this);
            if (region is null)
            {
                return;
            }
            var item = selected[0];
            item.Definition = item.Definition with
            {
                Region = region,
                SizeIsEstimated = false,
                SizeSource = "Manual drag selection"
            };
            item.CheckBox.Content = item.Definition.DisplayText;
            item.CheckBox.ToolTip = $"Size source: {item.Definition.SizeSource}";
        }
        finally
        {
            Opacity = 1;
            IsHitTestVisible = true;
            Activate();
            RefreshPreview();
        }
    }

    public static IReadOnlyList<DaocWindowDefinition>? Pick(Window owner, DaocCharacterProfile profile, IReadOnlyCollection<OcrWatchRegion> initiallySelected)
    {
        var dialog = new DaocWindowSelectionWindow(profile, initiallySelected)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.SelectedWindows : null;
    }

    private sealed class SelectionItem(DaocWindowDefinition definition, WpfCheckBox checkBox)
    {
        public DaocWindowDefinition Definition { get; set; } = definition;
        public WpfCheckBox CheckBox { get; } = checkBox;
    }
}
