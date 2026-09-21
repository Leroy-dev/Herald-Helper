using System.Net.Http;
using System.Windows.Threading;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Desktop.Services;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Infrastructure.Auth;

namespace HeraldHelper.Desktop.Controllers;

internal sealed class AuthController
{
    private readonly SettingsController _settingsController;
    private readonly IWritableSettings<HeraldHelperSettings> _writableSettings;
    private readonly IShardAuthRefreshService _authRefreshService;
    private readonly DispatcherTimer _authRefreshTimer;
    private readonly IAuthNotifications _notifications;

    public AuthController(
        SettingsController settingsController,
        IWritableSettings<HeraldHelperSettings> writableSettings,
        IShardAuthRefreshService authRefreshService,
        IAuthNotifications notifications)
    {
        _settingsController = settingsController;
        _writableSettings = writableSettings;
        _authRefreshService = authRefreshService;
        _notifications = notifications;
        _authRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(writableSettings.Value.Auth.AutoRefreshMinutes) };
        _authRefreshTimer.Tick += async (_, _) =>
        {
            try
            {
                await RefreshEnabledShardAuthAsync();
            }
            catch (Exception ex)
            {
                // Keep UI alive even if auth refresh fails — but surface it.
                _notifications.Log($"Scheduled auth refresh failed: {ex.Message}");
            }
        };
    }

    public DispatcherTimer AuthRefreshTimer => _authRefreshTimer;

    public void ConfigureTimer()
    {
        var auth = _writableSettings.Value.Auth;
        var minutes = Math.Clamp(auth.AutoRefreshMinutes, 5, 240);

        _authRefreshTimer.Interval = TimeSpan.FromMinutes(minutes);
        if (auth.AutoRefreshEnabled)
        {
            _authRefreshTimer.Start();
        }
        else
        {
            _authRefreshTimer.Stop();
        }
    }

    public async Task<bool> RefreshCurrentAsync(ShardType shard)
    {
        try
        {
            _notifications.Log($"Refreshing auth for {shard}...");
            var bundle = await _authRefreshService.RefreshAsync(shard, CancellationToken.None);
            if (bundle is null)
            {
                _notifications.Log($"No valid auth captured for {shard}. Use the Browser button to sign in, then refresh again.");
                return false;
            }

            _notifications.OnRefreshed();
            _notifications.Log($"Auth refreshed for {shard}.");
            return true;
        }
        catch (Exception ex)
        {
            _notifications.Log($"Auth refresh failed for {shard}: {ex.Message}");
            return false;
        }
    }

    public async Task<IReadOnlyCollection<string>> RefreshAllAsync()
    {
        try
        {
            var refreshed = await RefreshEnabledShardAuthAsync();

            _notifications.OnRefreshed();
            _notifications.Log(refreshed.Count == 0
                ? "No enabled shard auth profiles."
                : $"Auth refreshed: {string.Join(", ", refreshed)}");
            return refreshed;
        }
        catch (Exception ex)
        {
            _notifications.Log($"Auth refresh failed: {ex.Message}");
            return [];
        }
    }

    private async Task<List<string>> RefreshEnabledShardAuthAsync()
    {
        var refreshed = new List<string>();
        foreach (var shard in Enum.GetValues<ShardType>())
        {
            var bundle = await _authRefreshService.RefreshAsync(shard, CancellationToken.None);
            if (bundle is not null)
            {
                refreshed.Add(shard.ToString());
            }
        }

        return refreshed;
    }

    public void OnRefreshed(ShardType shard, ShardAuthBundle bundle)
    {
        var shardKey = shard.ToString().ToLowerInvariant();
        var updates = new List<ConfigEntry>
        {
            new() { Key = $"auth.{shardKey}.cookieHeader", Value = bundle.CookieHeader ?? string.Empty },
            new() { Key = $"auth.{shardKey}.userAgent", Value = bundle.UserAgent ?? string.Empty }
        };

        if (shard == ShardType.Eden)
        {
            updates.Add(new ConfigEntry { Key = "edenHeraldCookie", Value = bundle.CookieHeader ?? string.Empty });
            updates.Add(new ConfigEntry { Key = "edenHeraldUserAgent", Value = bundle.UserAgent ?? string.Empty });
        }

        _settingsController.Save(updates);
    }
}
