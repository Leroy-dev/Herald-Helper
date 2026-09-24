using System.Windows;

namespace HeraldHelper.Desktop.Views;

public partial class OverlayView : System.Windows.Controls.UserControl
{
    private HeraldHelper.Desktop.MainWindow? Main =>
        Window.GetWindow(this) as HeraldHelper.Desktop.MainWindow;

    public OverlayView()
    {
        InitializeComponent();
    }

    private void PreviewOverlay_Click(object sender, RoutedEventArgs e)
    {
        Main?.PreviewOverlay_Click(sender, e);
    }
    private void PickOutlineColor_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickOutlineColor_Click(sender, e);
    }
    private void PickTargetColor_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickTargetColor_Click(sender, e);
    }
    private void PickTimerColor_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickTimerColor_Click(sender, e);
    }
    private void ReloadOverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        Main?.ReloadOverlaySettings_Click(sender, e);
    }
}
