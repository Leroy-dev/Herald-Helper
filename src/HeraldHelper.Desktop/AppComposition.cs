using System.IO;
using System.Net.Http;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Controllers;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Desktop.Services;
using HeraldHelper.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace HeraldHelper.Desktop;

internal static class AppServiceProvider
{
    public static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<AppDataStore>(sp =>
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HeraldHelper",
                "heraldhelper.db");
            var store = new AppDataStore(dbPath);
            store.Initialize();
            return store;
        });

        services.AddSingleton<ISettingsRepository>(sp => sp.GetRequiredService<AppDataStore>());
        services.AddSingleton<ICharacterStatsRepository>(sp => sp.GetRequiredService<AppDataStore>());
        services.AddSingleton<IAbilityRepository>(sp => sp.GetRequiredService<AppDataStore>());
        services.AddSingleton<ICatalogOverrideRepository>(sp => sp.GetRequiredService<AppDataStore>());
        services.AddSingleton<IAbilityProfileRepository>(sp => sp.GetRequiredService<AppDataStore>());
        services.AddSingleton<IBackupRepository>(sp => sp.GetRequiredService<AppDataStore>());
        services.AddSingleton<ITargetProfileCache>(sp => sp.GetRequiredService<AppDataStore>());

        services.AddSingleton<SettingsController>();
        services.AddSingleton<IWritableSettings<HeraldHelperSettings>, HeraldHelperSettingsService>();
        services.AddSingleton<OverlaySettingsController>();

        services.AddSingleton<ThemeController>();
        services.AddSingleton<AbilityProfileController>();
        services.AddSingleton<DaocCharacterController>();

        services.AddSingleton<IResponseDiagnostics>(sp => sp.GetRequiredService<ResponseDiagnosticsBuffer>());
        services.AddSingleton<ResponseDiagnosticsBuffer>();

        services.AddSingleton<HttpClient>(sp =>
            new HttpClient { Timeout = TimeSpan.FromSeconds(8) });

        services.AddSingleton<DesktopOverlayRenderer>(sp =>
            new DesktopOverlayRenderer(
                () => sp.GetRequiredService<OverlaySettingsController>().Load(),
                () => sp.GetRequiredService<SettingsController>().LoadMap()));
        services.AddSingleton<IOverlayRenderer>(sp => sp.GetRequiredService<DesktopOverlayRenderer>());

        services.AddSingleton<IShardAuthRefreshService>(sp =>
        {
            var settings = sp.GetRequiredService<SettingsController>();
            var profilesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HeraldHelper",
                "browser-profiles");
            return new PlaywrightShardAuthRefreshService(
                shard => ShardAuthProfileResolver.Resolve(settings.LoadMap(), shard),
                (shard, bundle) => sp.GetRequiredService<AuthController>().OnRefreshed(shard, bundle),
                profilesRoot);
        });

        services.AddSingleton<ITargetProfileRepository, LocalTargetProfileRepository>();
        services.AddSingleton<IOnlineTargetProfileClient, RestOnlineTargetProfileClient>();
        services.AddSingleton<IOnlineSyncService, OnlineSyncService>();

        services.AddSingleton<RuntimeController>();

        services.AddSingleton<AuthController>(sp =>
        {
            var settings = sp.GetRequiredService<SettingsController>();
            var refresh = sp.GetRequiredService<IShardAuthRefreshService>();
            var liveSettings = sp.GetRequiredService<IWritableSettings<HeraldHelperSettings>>();
            return new AuthController(
                settings,
                liveSettings,
                refresh,
                text =>
                {
                    var window = sp.GetRequiredService<MainWindow>();
                    window.OutputBox.Text = text;
                },
                () =>
                {
                    var window = sp.GetRequiredService<MainWindow>();
                    window.ReloadEditorData();
                    window.RebuildRuntimeFromFiles();
                    window.ReloadOverlaySettingsFromStore();
                });
        });

        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
