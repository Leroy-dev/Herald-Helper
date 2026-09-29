using System.IO;
using System.Text;
using HeraldHelper.Application.Contracts;
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
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly string _profilesRoot;

    /// <summary>One persistent context per shard at a time — a headed login
    /// window and a headless refresh on the same userDataDir collide on
    /// Chromium's profile lock and wedge the headed session.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<ShardType, SemaphoreSlim>
        LaunchLocks = new();

    /// <summary>Stable real-Chrome UA so the login session and every refresh
    /// present the same fingerprint — Eden invalidates the session when the
    /// UA drifts ("HeadlessChrome" got the user logged out).</summary>
    private const string StableUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";

    public PlaywrightShardAuthRefreshService(
        Func<ShardType, ShardAuthProfile?> resolveProfile,
        Action<ShardType, ShardAuthBundle> onRefreshed,
        string profilesRoot,
        IResponseDiagnostics? diagnostics = null)
    {
        _resolveProfile = resolveProfile;
        _onRefreshed = onRefreshed;
        _profilesRoot = profilesRoot;
        _diagnostics = diagnostics;
    }

    public async Task<ShardAuthBundle?> RefreshAsync(ShardType shard, CancellationToken cancellationToken)
    {
        var profile = _resolveProfile(shard);
        if (profile is null)
        {
            return null;
        }

        var launchLock = LaunchLocks.GetOrAdd(shard, _ => new SemaphoreSlim(1, 1));
        if (!await launchLock.WaitAsync(0, cancellationToken))
        {
            _diagnostics?.Log($"[Auth] {shard}: refresh skipped — a login browser is open for this shard");
            return null;
        }

        try
        {
            return await RefreshLockedAsync(profile, cancellationToken);
        }
        finally
        {
            launchLock.Release();
        }
    }

    private async Task<ShardAuthBundle?> RefreshLockedAsync(ShardAuthProfile profile, CancellationToken cancellationToken)
    {
        var userDataDir = PrepareUserDataDir(profile.Shard);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = true,
                // Same UA the session was captured with — drift invalidates it.
                UserAgent = string.IsNullOrWhiteSpace(profile.SavedUserAgent)
                    ? StableUserAgent
                    : profile.SavedUserAgent,
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
            browser, profile, page, TimeSpan.FromSeconds(10), _diagnostics, cancellationToken);
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            _diagnostics?.Log($"[Auth] {profile.Shard}: no validated session in persistent profile — sign in via the Browser button");
            return null;
        }

        _diagnostics?.Log($"[Auth] {profile.Shard}: session cookies captured ({cookieHeader.Split(';').Length} cookies)");

        var userAgent = await TryReadUserAgentAsync(page, cancellationToken);

        var bundle = new ShardAuthBundle(cookieHeader, userAgent);
        _onRefreshed(profile.Shard, bundle);
        return bundle;
    }

    public async Task<ShardAuthBundle?> OpenBrowserAsync(ShardType shard, CancellationToken cancellationToken)
    {
        var profile = _resolveProfile(shard);
        if (profile is null)
        {
            return null;
        }

        var launchLock = LaunchLocks.GetOrAdd(shard, _ => new SemaphoreSlim(1, 1));
        await launchLock.WaitAsync(cancellationToken);
        try
        {
            return await OpenBrowserLockedAsync(profile, cancellationToken);
        }
        finally
        {
            launchLock.Release();
        }
    }

    private async Task<ShardAuthBundle?> OpenBrowserLockedAsync(ShardAuthProfile profile, CancellationToken cancellationToken)
    {
        var userDataDir = PrepareUserDataDir(profile.Shard);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = false,
                UserAgent = string.IsNullOrWhiteSpace(profile.SavedUserAgent)
                    ? StableUserAgent
                    : profile.SavedUserAgent,
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
        // Same rule as headless: page-deny only gates profiles without a
        // validateUrl — Eden's hub keeps a hidden login affordance in the DOM
        // for logged-in users, which would veto a live session otherwise.
        var denyBlocks = string.IsNullOrWhiteSpace(profile.ValidateUrl) && pageDenied;
        if (denyBlocks || string.IsNullOrWhiteSpace(latestCookieHeader) ||
            !await IsLoginValidatedAsync(profile, latestCookieHeader, latestUserAgent, CancellationToken.None))
        {
            _diagnostics?.Log(
                $"[Auth] {profile.Shard}: browser closed — denied={pageDenied}, cookies={(string.IsNullOrWhiteSpace(latestCookieHeader) ? "none" : "present")}, validation failed");
            return null;
        }

        _diagnostics?.Log($"[Auth] {profile.Shard}: login captured via browser window");

        var bundle = new ShardAuthBundle(latestCookieHeader, latestUserAgent);
        _onRefreshed(profile.Shard, bundle);
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
        IResponseDiagnostics? diagnostics,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cookies = await browser.CookiesAsync([profile.HubUrl]);
            var cookieHeader = BuildCookieHeader(cookies, profile);
            if (string.IsNullOrWhiteSpace(cookieHeader))
            {
                diagnostics?.Log($"[Auth] {profile.Shard}: poll — no session cookies yet");
                await Task.Delay(750, cancellationToken);
                continue;
            }

            var userAgent = await TryReadUserAgentAsync(page, cancellationToken);
            var hasValidateUrl = !string.IsNullOrWhiteSpace(profile.ValidateUrl);
            var pageDenied = await PageShowsDenyAsync(page, profile.HubDenyPhrases, cancellationToken);
            // Server-side validateUrl is the authority when configured —
            // Eden's hub keeps a hidden login affordance in the DOM even for
            // a live session, so page-deny alone can't veto a validated one.
            // Without a validateUrl, deny phrases are the only logged-out
            // signal and still gate.
            if (((!hasValidateUrl && !pageDenied) || hasValidateUrl) &&
                await IsLoginValidatedAsync(profile, cookieHeader, userAgent, cancellationToken))
            {
                return cookieHeader;
            }

            diagnostics?.Log(
                $"[Auth] {profile.Shard}: cookies={cookies.Count} denied={pageDenied} validate={(hasValidateUrl ? "failed" : "n/a")}");

            await Task.Delay(750, cancellationToken);
        }

        return string.Empty;
    }

    /// <summary>True when the live hub page still shows an anonymous-login
    /// affordance. A bare phrase hit on innerText was too easy — "login" shows
    /// up in footers and help text of logged-in pages — so the page must also
    /// expose a real login control (password field, login link/form). A
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
            var probe = await page.EvaluateAsync<string>(
                "() => (document.querySelector('input[type=\"password\"], " +
                "a[href*=\"login\" i], form[action*=\"login\" i]') ? '\u0001LOGINUI\u0001' : '')" +
                " + (document.body ? document.body.innerText : '')");
            return probe.Contains("\u0001LOGINUI\u0001") && ContainsAnyDenyPhrase(probe, phrases);
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
