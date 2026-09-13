using System.Windows;
using System.Windows.Controls;

namespace HeraldHelper.Desktop.Views;

public partial class HeraldView : System.Windows.Controls.UserControl
{
    private HeraldHelper.Desktop.MainWindow? Main =>
        Window.GetWindow(this) as HeraldHelper.Desktop.MainWindow;

    public HeraldView()
    {
        InitializeComponent();
    }

    private void HeraldSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        Main?.RefreshHeraldResults();
    }

    private void HeraldShard_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Main?.RefreshHeraldResults();
    }
}
