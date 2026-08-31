using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HeraldHelper.Desktop.Views;

public partial class LiveView : System.Windows.Controls.UserControl
{
    private HeraldHelper.Desktop.MainWindow? Main =>
        Window.GetWindow(this) as HeraldHelper.Desktop.MainWindow;

    public LiveView()
    {
        InitializeComponent();
    }

    private void ClearResponseDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        Main?.ClearResponseDiagnostics_Click(sender, e);
    }
}
