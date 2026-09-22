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

        var userDataDir = PrepareUserDataDir(shard);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = true,
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

        // Headless cannot offer interactive login — the persistent profile's
        // cookies are either already valid or the user must sign in via the
        // browser window. A short wait only covers session writes racing in.
        var cookieHeader = await WaitForCookieHeaderAsync(
            browser, profile, page, TimeSpan.FromSeconds(10), cancellationToken);
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            return null;
        }

        var userAgent = await TryReadUserAgentAsync(page, cancellationToken);

        var bundle = new ShardAuthBundle(cookieHeader, userAgent);
        _onRefreshed(shard, bundle);
        return bundle;
    }

    public async Task<ShardAuthBundle?> OpenBrowserAsync(ShardType shard, CancellationToken cancellationToken)
    {
        var profile = _resolveProfile(shard);
        if (profile is null)
        {
            return null;
        }

        var userDataDir = PrepareUserDataDir(shard);

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

        var closeTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        browser.Close += (_, _) => closeTcs.TrySetResult();
        await using var registration = cancellationToken.Register(() =>
        {
            closeTcs.TrySetCanceled(cancellationToken);
        });

        // The browser is never closed by us: the user logs in, then closes the
        // window — cookies + page state snapshotted every poll decide whether
        // anything was captured. Cookie presence alone is not proof (Eden sets
        // session cookies for anonymous visitors and stale ones persist in the
        // user-data dir), so the live page must also stop showing the hub's
        // login indicators before the snapshot counts as authenticated.
        string? latestCookieHeader = null;
        string? latestUserAgent = null;
        var pageDenied = true;
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10);
        while (!closeTcs.Task.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var cookies = await browser.CookiesAsync([profile.HubUrl]);
                var header = BuildCookieHeader(cookies, profile);
                if (!string.IsNullOrWhiteSpace(header))
                {
                    latestCookieHeader = header;
                }

                latestUserAgent = await TryReadUserAgentAsync(page, cancellationToken) ?? latestUserAgent;
                pageDenied = await PageShowsDenyAsync(page, profile.HubDenyPhrases, cancellationToken);
            }
            catch (PlaywrightException)
            {
                break; // browser torn down mid-poll — last snapshot stands
            }

            await Task.Delay(750, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (pageDenied || string.IsNullOrWhiteSpace(latestCookieHeader) ||
            !await IsLoginValidatedAsync(profile, latestCookieHeader, latestUserAgent, CancellationToken.None))
        {
            return null;
        }

        var bundle = new ShardAuthBundle(latestCookieHeader, latestUserAgent);
        _onRefreshed(shard, bundle);
        return bundle;
    }

    private string PrepareUserDataDir(ShardType shard)
    {
        Directory.CreateDirectory(_profilesRoot);
        var userDataDir = Path.Combine(_profilesRoot, shard.ToString().ToLowerInvariant());
        Directory.CreateDirectory(userDataDir);
        return userDataDir;
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
        IPage page,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cookies = await browser.CookiesAsync([profile.HubUrl]);
            var cookieHeader = BuildCookieHeader(cookies, profile);
            var userAgent = await TryReadUserAgentAsync(page, cancellationToken);
            var pageDenied = await PageShowsDenyAsync(page, profile.HubDenyPhrases, cancellationToken);
            if (!pageDenied &&
                !string.IsNullOrWhiteSpace(cookieHeader) &&
                await IsLoginValidatedAsync(profile, cookieHeader, userAgent, cancellationToken))
            {
                return cookieHeader;
            }

            await Task.Delay(750, cancellationToken);
        }

        return string.Empty;
    }

    /// <summary>True when the live hub page still shows an anonymous-login
    /// affordance (profile HubDenyPhrases — Eden's "LOGIN" nav button). A
    /// failed evaluate (mid-navigation, closed tab) counts as denied.</summary>
    private static async Task<bool> PageShowsDenyAsync(
        IPage page, IReadOnlyList<string>? phrases, CancellationToken cancellationToken)
    {
        if (phrases is null || phrases.Count == 0)
        {
            return false;
        }

        try
        {
            var text = await page.EvaluateAsync<string>(
                "() => document.body ? document.body.innerText : ''");
            return ContainsAnyDenyPhrase(text, phrases);
        }
        catch (PlaywrightException)
        {
            return true;
        }
    }

    internal static bool ContainsAnyDenyPhrase(string? text, IReadOnlyList<string>? phrases)
    {
        return phrases is { Count: > 0 } &&
               phrases.Any(p => text?.Contains(p, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>Profile-driven validation: every cookie in RequiredCookieNames
    /// must carry a value, and when a ValidateUrl is configured the response
    /// must be a success page that does not contain ALL of the deny phrases.</summary>
    private static async Task<bool> IsLoginValidatedAsync(
        ShardAuthProfile profile,
        string cookieHeader,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        foreach (var required in profile.RequiredCookieNames ?? [])
        {
            if (!HasCookieValue(cookieHeader, required))
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(profile.ValidateUrl))
        {
            return true;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            using var req = new HttpRequestMessage(HttpMethod.Get, profile.ValidateUrl);
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

            var deny = profile.ValidateDenyPhrases;
            if (deny is null || deny.Count == 0)
            {
                return true;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return !deny.All(phrase => content.Contains(phrase, StringComparison.OrdinalIgnoreCase));
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
