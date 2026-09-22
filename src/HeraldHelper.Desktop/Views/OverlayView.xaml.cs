using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HeraldHelper.Desktop.Views;

public partial class OverlayView : System.Windows.Controls.UserControl
{
    private HeraldHelper.Desktop.MainWindow? Main =>
        Window.GetWindow(this) as HeraldHelper.Desktop.MainWindow;

    public OverlayView()
    {
        InitializeComponent();
    }

    private void PickCastOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickCastOverlayPosition_Click(sender, e);
    }
    private void PickResistsOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickResistsOverlayPosition_Click(sender, e);
    }
    private void PickGroupOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickGroupOverlayPosition_Click(sender, e);
    }
    private void PickSelfCcOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickSelfCcOverlayPosition_Click(sender, e);
    }
    private void PickPeelOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickPeelOverlayPosition_Click(sender, e);
    }
    private void PickWorldOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickWorldOverlayPosition_Click(sender, e);
    }
    private void PickResistsSize_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickResistsSize_Click(sender, e);
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
    private void PickTargetOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickTargetOverlayPosition_Click(sender, e);
    }
    private void PickTargetSize_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickTargetSize_Click(sender, e);
    }
    private void PickTimerColor_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickTimerColor_Click(sender, e);
    }
    private void PickTimerOverlayPosition_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickTimerOverlayPosition_Click(sender, e);
    }
    private void PickTimerSize_Click(object sender, RoutedEventArgs e)
    {
        Main?.PickTimerSize_Click(sender, e);
    }
    private void ReloadOverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        Main?.ReloadOverlaySettings_Click(sender, e);
    }
    private void SaveOverlaySettings_Click(object sender, RoutedEventArgs e)
    {
        Main?.SaveOverlaySettings_Click(sender, e);
    }
}
