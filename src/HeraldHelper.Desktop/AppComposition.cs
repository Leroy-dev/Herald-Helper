using System.IO;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Controllers;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Desktop.Repositories;
using HeraldHelper.Desktop.Services;
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

        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
