using System.IO;
using System.Text;
namespace HeraldHelper.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            ShowFatal("Unhandled UI exception", args.Exception);
            args.Handled = true;
            Shutdown(-1);
        };

        try
        {
            var window = new MainWindow();
            ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            ShowFatal("Startup failed", ex);
            Shutdown(-1);
        }
    }

    private static void ShowFatal(string title, Exception ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HeraldHelper");
            Directory.CreateDirectory(dir);
            var logPath = Path.Combine(dir, "startup-error.log");
            var text = $"{DateTimeOffset.Now:O}\n{ex}\n\n";
            File.AppendAllText(logPath, text, Encoding.UTF8);
            System.Windows.MessageBox.Show($"{title}\n\n{ex.Message}\n\nDetails logged to:\n{logPath}", "HeraldHelper Error");
        }
        catch
        {
            System.Windows.MessageBox.Show($"{title}\n\n{ex}", "HeraldHelper Error");
        }
    }
}
