using System.Globalization;
using System.Windows.Data;
using WpfBinding = System.Windows.Data.Binding;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace HeraldHelper.Desktop;

/// <summary>Turns a hex color text (#RGB, #RRGGBB, #AARRGGBB) into a frozen
/// brush — used for the live swatch next to color inputs. Invalid text keeps
/// the previous brush (Binding.DoNothing) instead of blanking the swatch.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return WpfBinding.DoNothing;
        }

        try
        {
            var parsed = WpfColorConverter.ConvertFromString(text.Trim());
            if (parsed is WpfColor color)
            {
                var brush = new WpfSolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // fall through — invalid text leaves the swatch unchanged
        }

        return WpfBinding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return WpfBinding.DoNothing;
    }
}
