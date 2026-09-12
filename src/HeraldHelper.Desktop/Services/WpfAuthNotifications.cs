using Microsoft.Extensions.DependencyInjection;

namespace HeraldHelper.Desktop.Services;

internal sealed class WpfAuthNotifications(IServiceProvider services) : IAuthNotifications
{
    public void Log(string message)
    {
        services.GetRequiredService<MainWindow>().OutputBox.Text = message;
    }

    public void OnRefreshed()
    {
        var window = services.GetRequiredService<MainWindow>();
        window.ReloadEditorData();
        window.RebuildRuntimeFromFiles();
        window.ReloadOverlaySettingsFromStore();
    }
}
