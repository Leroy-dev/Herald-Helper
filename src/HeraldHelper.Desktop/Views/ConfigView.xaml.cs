using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HeraldHelper.Desktop.Views;

public partial class ConfigView : System.Windows.Controls.UserControl
{
    private HeraldHelper.Desktop.MainWindow? Main =>
        Window.GetWindow(this) as HeraldHelper.Desktop.MainWindow;

    public ConfigView()
    {
        InitializeComponent();
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        Main?.ExportJson_Click(sender, e);
    }
    private void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        Main?.ImportJson_Click(sender, e);
    }
    private void ReloadConfig_Click(object sender, RoutedEventArgs e)
    {
        Main?.ReloadConfig_Click(sender, e);
    }
    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        Main?.SaveConfig_Click(sender, e);
    }
}
