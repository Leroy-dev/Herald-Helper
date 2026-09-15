using System.IO;
using System.Text;
using HeraldHelper.Domain.Enums;
using Microsoft.Playwright;

namespace HeraldHelper.Infrastructure.Auth;

public sealed class PlaywrightShardAuthRefreshService : IShardAuthRefreshService
{
    static PlaywrightShardAuthRefreshService()
    {
        // Release builds ship Chromium under <app>\.playwright\package\.local-browsers
        // (PLAYWRIGHT_BROWSERS_PATH=0). Dev builds keep using the per-user
        // %LOCALAPPDATA%\ms-playwright cache.
        var bundled = Path.Combine(AppContext.BaseDirectory, ".playwright", "package", ".local-browsers");
        if (Directory.Exists(bundled) &&
            Directory.EnumerateDirectories(bundled).Any(d =>
                Path.GetFileName(d).StartsWith("chromium", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");
        }
    }

    private readonly Func<ShardType, ShardAuthProfile?> _resolveProfile;
    private readonly Action<ShardType, ShardAuthBundle> _onRefreshed;
    private readonly string _profilesRoot;

    public PlaywrightShardAuthRefreshService(
        Func<ShardType, ShardAuthProfile?> resolveProfile,
        Action<ShardType, ShardAuthBundle> onRefreshed,
        string profilesRoot)
    {
        _resolveProfile = resolveProfile;
        _onRefreshed = onRefreshed;
        _profilesRoot = profilesRoot;
    }

    public async Task<ShardAuthBundle?> RefreshAsync(ShardType shard, CancellationToken cancellationToken)
    {
        var profile = _resolveProfile(shard);
        if (profile is null)
        {
            return null;
        }

        Directory.CreateDirectory(_profilesRoot);
        var userDataDir = Path.Combine(_profilesRoot, shard.ToString().ToLowerInvariant());
        Directory.CreateDirectory(userDataDir);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = false,
                Args =
                [
                    "--disable-blink-features=AutomationControlled",
                    "--no-default-browser-check",
                    "--no-first-run"
                ]
            });

        var page = browser.Pages.FirstOrDefault() ?? await browser.NewPageAsync();
        await page.GotoAsync(profile.HubUrl, new PageGotoOptions
        {
            Timeout = 30000,
            WaitUntil = WaitUntilState.NetworkIdle
        });

        var cookieHeader = await WaitForCookieHeaderAsync(browser, profile, shard, page, cancellationToken);
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            // Do not overwrite existing auth with empty values if login is incomplete.
            return null;
        }

        var userAgent = await TryReadUserAgentAsync(page, cancellationToken);

        var bundle = new ShardAuthBundle(cookieHeader, userAgent);
        _onRefreshed(shard, bundle);
        return bundle;
    }

    public async Task OpenBrowserAsync(ShardType shard, CancellationToken cancellationToken)
    {
        var profile = _resolveProfile(shard);
        if (profile is null)
        {
            return;
        }

        Directory.CreateDirectory(_profilesRoot);
        var userDataDir = Path.Combine(_profilesRoot, shard.ToString().ToLowerInvariant());
        Directory.CreateDirectory(userDataDir);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = false,
                Args =
                [
                    "--disable-blink-features=AutomationControlled",
                    "--no-default-browser-check",
                    "--no-first-run"
                ]
            });

        var page = browser.Pages.FirstOrDefault() ?? await browser.NewPageAsync();
        await page.GotoAsync(profile.HubUrl, new PageGotoOptions
        {
            Timeout = 30000,
            WaitUntil = WaitUntilState.NetworkIdle
        });

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        browser.Close += (_, _) => tcs.TrySetResult();
        await using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        await tcs.Task;
    }

    private static string BuildCookieHeader(IReadOnlyList<BrowserContextCookiesResult> cookies, ShardAuthProfile profile)
    {
        var domainCookies = cookies
            .Where(c => !string.IsNullOrWhiteSpace(c.Domain) && c.Domain.Contains(profile.Domain, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Always include every shard-domain cookie; preferred names only influence ordering.
        var selected = new List<BrowserContextCookiesResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in profile.PreferredCookieNames)
        {
            var c = domainCookies.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (c is null)
            {
                continue;
            }

            selected.Add(c);
            seen.Add(c.Name);
        }

        foreach (var cookie in domainCookies)
        {
            if (seen.Contains(cookie.Name))
            {
                continue;
            }

            selected.Add(cookie);
        }

        var sb = new StringBuilder();
        for (var i = 0; i < selected.Count; i++)
        {
            if (i > 0)
            {
                sb.Append("; ");
            }

            sb.Append(selected[i].Name).Append('=').Append(selected[i].Value);
        }

        return sb.ToString();
    }

    private static async Task<string> WaitForCookieHeaderAsync(
        IBrowserContext browser,
        ShardAuthProfile profile,
        ShardType shard,
        IPage page,
        CancellationToken cancellationToken)
    {
        // Keep the browser open long enough for manual login; return once required cookies exist.
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < TimeSpan.FromMinutes(3))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cookies = await browser.CookiesAsync([profile.HubUrl]);
            var cookieHeader = BuildCookieHeader(cookies, profile);
            var userAgent = await TryReadUserAgentAsync(page, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cookieHeader) &&
                await IsLoginValidatedAsync(shard, cookieHeader, userAgent, cancellationToken))
            {
                return cookieHeader;
            }

            await Task.Delay(750, cancellationToken);
        }

        return string.Empty;
    }

    private static async Task<bool> IsLoginValidatedAsync(
        ShardType shard,
        string cookieHeader,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (shard != ShardType.Eden)
        {
            return true;
        }

        // eden_daoc_u and eden_daoc_sid are required to avoid persisting completely anonymous cookie sets.
        var hasBaseSession = HasCookieValue(cookieHeader, "eden_daoc_u")
            && HasCookieValue(cookieHeader, "eden_daoc_sid");
        if (!hasBaseSession)
        {
            return false;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://eden-daoc.net/herald");
            req.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                req.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            }

            using var response = await client.SendAsync(req, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return !content.Contains("The requested page", StringComparison.OrdinalIgnoreCase)
                   || !content.Contains("is not available", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasCookieValue(string cookieHeader, string cookieName)
    {
        var parts = cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }

            if (!kv[0].Trim().Equals(cookieName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return !string.IsNullOrWhiteSpace(kv[1]);
        }

        return false;
    }

    private static async Task<string?> TryReadUserAgentAsync(IPage page, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 3; i++)
        {
            try
            {
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                return await page.EvaluateAsync<string>("() => navigator.userAgent");
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("Execution context was destroyed", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(300, cancellationToken);
            }
        }

        return null;
    }
}
