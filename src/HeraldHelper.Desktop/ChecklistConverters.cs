using System.Globalization;
using System.Windows.Data;
using WpfBinding = System.Windows.Data.Binding;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace HeraldHelper.Desktop;

/// <summary>bool → "✓"/"✗" for the setup checklist.</summary>
public sealed class ChecklistGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "✓" : "✗";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        WpfBinding.DoNothing;
}

/// <summary>bool → green/red brush for the setup checklist glyph.</summary>
public sealed class ChecklistGlyphColorConverter : IValueConverter
{
    private static readonly WpfSolidColorBrush OkBrush = new(WpfColor.FromRgb(0x45, 0xB9, 0x7C));
    private static readonly WpfSolidColorBrush FailBrush = new(WpfColor.FromRgb(0xE0, 0x5D, 0x65));

    static ChecklistGlyphColorConverter()
    {
        OkBrush.Freeze();
        FailBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? OkBrush : FailBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        WpfBinding.DoNothing;
}
