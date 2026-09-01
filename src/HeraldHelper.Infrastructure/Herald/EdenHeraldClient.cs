using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Models;
using HeraldHelper.Infrastructure.Auth;
using HeraldHelper.Infrastructure.Configuration;

namespace HeraldHelper.Infrastructure.Herald;

public sealed class EdenHeraldClient : IHeraldClient, IHeraldProfileUpdateSource
{
    private static readonly long[] EdenRealmPointThresholds =
    [
        0, 25, 125, 350, 750, 1375, 2275, 3500, 5100, 7125, 9625, 12650, 16250, 20475, 25375, 31000,
        37400, 44625, 52725, 61750, 71750, 82775, 94875, 108100, 122500, 138125, 155025, 173250, 192850,
        213875, 236375, 260400, 286000, 313225, 342125, 372750, 405150, 439375, 475475, 513500, 553500,
        595525, 639625, 685850, 734250, 784875, 837775, 893000, 950600, 1010625, 1073125, 1138150,
        1205750, 1275975, 1348875, 1424500, 1502900, 1584125, 1668225, 1755250, 1845250, 1938275, 2034375,
        2133600, 2236000, 2341625, 2450525, 2562750, 2678350, 2797375, 2919875, 3045900, 3175500, 3308725,
        3445625, 3586250, 3730650, 3878875, 4030975, 4187000, 4347000, 4511025, 4679125, 4851350, 5027750,
        5208375, 5393275, 5582500, 5776100, 5974125, 6176625, 6383650, 6595250, 6811475, 7032375, 7258000,
        7488400, 7723625, 7963725, 8208750, 9111713, 10114001, 11226541, 12461460, 13832221, 15353765,
        17042680, 18917374, 20998286, 23308097, 25871988, 28717906, 31876876, 35383333, 39275499, 43595804,
        48391343, 53714390, 59622973, 66181501, 73461466, 81542227, 90511872, 100468178, 111519678,
        123786843, 137403395, 152517769, 169294723, 187917143
    ];

    private static readonly long[] EdenExperienceThresholds =
    [
        0, 50, 250, 862, 2452, 6566, 17264, 42969, 104624, 252597, 578184, 1084636, 1872442, 3097862,
        5004071, 7969199, 12581627, 19756017, 30915510, 47879943, 73669337, 108596860, 155900432, 219965478,
        306731040, 424247833, 583405025, 798957443, 1090888054, 1473042173, 1983893518, 2659707500, 3553701950,
        4736314134, 6300721436, 8370183229, 10954252985, 14180710266, 18209249899, 23239265725, 29519719603,
        37361918369, 47153651218, 59379562666, 74644778569, 93704855776, 117504553416, 147220744543,
        184324241109, 230651492549, 4999999999950
    ];

    private static readonly IReadOnlyDictionary<int, string> EdenClassList = new Dictionary<int, string>
    {
        [59] = "Warlock",
        [58] = "Vampiir",
        [63] = "Occultist",
        [2] = "Armsman",
        [11] = "Mercenary",
        [1] = "Paladin",
        [19] = "Reaver",
        [13] = "Cabalist",
        [12] = "Necromancer",
        [8] = "Sorcerer",
        [5] = "Theurgist",
        [7] = "Wizard",
        [4] = "Minstrel",
        [6] = "Cleric",
        [10] = "Friar",
        [9] = "Infiltrator",
        [3] = "Scout",
        [33] = "Heretic",
        [43] = "Blademaster",
        [45] = "Champion",
        [44] = "Hero",
        [56] = "Valewalker",
        [55] = "Animist",
        [40] = "Eldritch",
        [41] = "Enchanter",
        [42] = "Mentalist",
        [48] = "Bard",
        [47] = "Druid",
        [46] = "Warden",
        [49] = "Nightshade",
        [39] = "Bainshee",
        [50] = "Ranger",
        [31] = "Berserker",
        [32] = "Savage",
        [24] = "Skald",
        [21] = "Thane",
        [22] = "Warrior",
        [30] = "Bonedancer",
        [29] = "Runemaster",
        [27] = "Spiritmaster",
        [26] = "Healer",
        [28] = "Shaman",
        [25] = "Hunter",
        [23] = "Shadowblade",
        [34] = "Valkyrie"
    };

    private readonly HttpClient _httpClient;
    private readonly Func<HeraldRuntimeConfig> _getConfig;
    private readonly Func<CancellationToken, Task<ShardAuthBundle?>>? _refreshAuth;
    private readonly IResponseDiagnostics? _diagnostics;
    private readonly ConcurrentDictionary<string, int> _soloKillsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _soloKillsRefreshInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TargetProfile> _latestProfiles = new(StringComparer.OrdinalIgnoreCase);

    public event Action<TargetProfile>? TargetProfileUpdated;

    public EdenHeraldClient(
        HttpClient httpClient,
        Func<HeraldRuntimeConfig> getConfig,
        Func<CancellationToken, Task<ShardAuthBundle?>>? refreshAuth = null,
        IResponseDiagnostics? diagnostics = null)
    {
        _httpClient = httpClient;
        _getConfig = getConfig;
        _refreshAuth = refreshAuth;
        _diagnostics = diagnostics;
    }

    public async Task<TargetProfile?> GetTargetProfileAsync(string targetName, CancellationToken cancellationToken)
    {
        var profileUrl = $"https://eden-daoc.net/hrald/proxy.php?player/{Uri.EscapeDataString(targetName)}";
        var referer = $"https://eden-daoc.net/herald?n=player&k={Uri.EscapeDataString(targetName)}";
        var (status, payload) = await GetJsonPayloadWithRetryAsync(profileUrl, referer, cancellationToken);
        _diagnostics?.Log($"[Eden] player/{targetName} => {(int)status} | full body:\n{payload}");
        if (status != HttpStatusCode.OK)
        {
            return null;
        }

        if (!TryExtractJsonPayload(payload, out var jsonPayload))
        {
            return null;
        }

        var profile = await ParseProfileAsync(targetName, jsonPayload, cancellationToken);
        if (profile is not null)
        {
            _diagnostics?.Log($"[Eden] parsed profile {profile.Name} | class={profile.Class ?? "-"} level={profile.Level?.ToString() ?? "-"} rr={profile.RealmRank ?? "-"} guild={profile.Guild ?? "-"} solo={profile.SoloKills?.ToString() ?? "-"}");
        }
        return profile;
    }

    private async Task<TargetProfile?> ParseProfileAsync(string targetName, string json, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(json);
        var root = UnwrapPlayerRoot(doc.RootElement);

        var guild = ResolveGuild(root);
        var @class = ResolveClass(root);
        var xp = ResolveLong(root, "experience");
        var rp = ResolveLong(root, "realm_points");
        var level = ResolveLevel(root, xp);
        var rr = ResolveRealmRank(root, rp);
        _soloKillsCache.TryGetValue(targetName, out var solo);
        TriggerSoloKillsRefresh(targetName);
        var profile = new TargetProfile(targetName, guild, @class, level, rr, _soloKillsCache.ContainsKey(targetName) ? solo : null);
        _latestProfiles[targetName] = profile;
        return profile;
    }

    private async Task<int?> TryGetSoloKillsAsync(string targetName, CancellationToken cancellationToken)
    {
        var soloUrl = $"https://eden-daoc.net/hrald/proxy.php?rank/pvp/{Uri.EscapeDataString(targetName)}";
        var referer = $"https://eden-daoc.net/herald?n=player&t=pvp&k={Uri.EscapeDataString(targetName)}";
        var (status, json) = await GetJsonPayloadWithRetryAsync(soloUrl, referer, cancellationToken);
        _diagnostics?.Log($"[Eden] rank/pvp/{targetName} => {(int)status} | full body:\n{json}");
        if (status != HttpStatusCode.OK)
        {
            return null;
        }

        if (!TryExtractJsonPayload(json, out var jsonPayload))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(jsonPayload);
        var root = UnwrapPlayerRoot(doc.RootElement);
        if (!TryGetPropertyIgnoreCase(root, "solo_kills", out var soloEl))
        {
            return null;
        }

        return soloEl.TryGetInt32(out var solo) ? solo : null;
    }

    private void TriggerSoloKillsRefresh(string targetName)
    {
        if (!_soloKillsRefreshInFlight.TryAdd(targetName, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var solo = await TryGetSoloKillsAsync(targetName, CancellationToken.None);
                if (solo is not null)
                {
                    var hadBefore = _soloKillsCache.TryGetValue(targetName, out var previous);
                    _soloKillsCache[targetName] = solo.Value;
                    _diagnostics?.Log($"[Eden] cached solo_kills for {targetName}: {solo.Value}");
                    if (!hadBefore || previous != solo.Value)
                    {
                        var updated = BuildUpdatedProfile(targetName, solo.Value);
                        if (updated is not null)
                        {
                            TargetProfileUpdated?.Invoke(updated);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _diagnostics?.Log($"[Eden] solo_kills refresh failed for {targetName}: {ex.Message}");
            }
            finally
            {
                _soloKillsRefreshInFlight.TryRemove(targetName, out _);
            }
        });
    }

    private TargetProfile? BuildUpdatedProfile(string targetName, int soloKills)
    {
        if (_latestProfiles.TryGetValue(targetName, out var profile))
        {
            var updated = profile with { SoloKills = soloKills };
            _latestProfiles[targetName] = updated;
            return updated;
        }

        return new TargetProfile(targetName, null, null, null, null, soloKills);
    }

    private void AddAuthHeaders(HttpRequestHeaders headers)
    {
        var config = _getConfig();
        if (!string.IsNullOrWhiteSpace(config.EdenCookieHeader))
        {
            headers.TryAddWithoutValidation("cookie", config.EdenCookieHeader);
        }

        if (!string.IsNullOrWhiteSpace(config.EdenUserAgent))
        {
            headers.TryAddWithoutValidation("user-agent", config.EdenUserAgent);
        }
    }

    private static bool LooksLikeJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var trimmed = payload.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static bool TryExtractJsonPayload(string payload, out string jsonPayload)
    {
        jsonPayload = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var trimmed = payload.Trim();
        if (LooksLikeJson(trimmed))
        {
            jsonPayload = trimmed;
            return true;
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace)
        {
            return false;
        }

        var candidate = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
        if (!LooksLikeJson(candidate))
        {
            return false;
        }

        jsonPayload = candidate;
        return true;
    }

    private async Task<(HttpStatusCode StatusCode, string Payload)> GetJsonPayloadWithRetryAsync(string url, string referer, CancellationToken cancellationToken)
    {
        string lastPayload = string.Empty;
        HttpStatusCode lastStatus = HttpStatusCode.ServiceUnavailable;
        var refreshedAuth = false;
        var variants = new[] { true, false };
        for (var attempt = 0; attempt < variants.Length; attempt++)
        {
            var useHeraldHeader = variants[attempt];
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (useHeraldHeader)
            {
                req.Headers.TryAddWithoutValidation("X-Herald-Api", "minified");
            }
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/javascript, */*; q=0.01");
            req.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            req.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            req.Headers.TryAddWithoutValidation("Referer", referer);
            AddAuthHeaders(req.Headers);
            _diagnostics?.Log($"[Eden] request attempt {attempt + 1} {(useHeraldHeader ? "with" : "without")} X-Herald-Api | cookie={DescribeCookieHeader(req.Headers)}");

            using var response = await _httpClient.SendAsync(req, cancellationToken);
            lastStatus = response.StatusCode;
            lastPayload = await response.Content.ReadAsStringAsync(cancellationToken);
            _diagnostics?.Log($"[Eden] response attempt {attempt + 1}: {(int)response.StatusCode} {response.ReasonPhrase} | content-type={response.Content.Headers.ContentType} | content-length={response.Content.Headers.ContentLength?.ToString() ?? "-"} | body-len={lastPayload.Length}");

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                _diagnostics?.Log($"[Eden] {url} attempt {attempt + 1}: 401 unauthorized.");
                if (_refreshAuth is not null && !refreshedAuth)
                {
                    _diagnostics?.Log("[Eden] refreshing auth after 401.");
                    await _refreshAuth(cancellationToken);
                    refreshedAuth = true;
                }

                continue;
            }

            if (response.IsSuccessStatusCode && TryExtractJsonPayload(lastPayload, out var normalizedJson))
            {
                return (response.StatusCode, normalizedJson);
            }

            _diagnostics?.Log($"[Eden] {url} attempt {attempt + 1}: non-json or non-success ({(int)response.StatusCode}), retrying.");
        }

        return (lastStatus, lastPayload);
    }

    private static string? ResolveClass(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "class", out var classEl)
            && !TryGetPropertyIgnoreCase(root, "class_id", out classEl))
        {
            return null;
        }

        if (classEl.ValueKind == JsonValueKind.Number && classEl.TryGetInt32(out var id))
        {
            return EdenClassList.TryGetValue(id, out var className) ? className : id.ToString();
        }

        var raw = classEl.ToString();
        if (int.TryParse(raw, out id))
        {
            return EdenClassList.TryGetValue(id, out var className) ? className : raw;
        }

        return raw;
    }

    private static string? ResolveGuild(JsonElement root)
    {
        if (TryGetPropertyIgnoreCase(root, "guild_name", out var guildName))
        {
            return guildName.GetString();
        }

        if (TryGetPropertyIgnoreCase(root, "guild", out var guild))
        {
            if (guild.ValueKind == JsonValueKind.String)
            {
                return guild.GetString();
            }

            if (guild.ValueKind == JsonValueKind.Object && guild.TryGetProperty("name", out var guildNested))
            {
                return guildNested.GetString();
            }
        }

        return null;
    }

    private static int? ResolveLevel(JsonElement root, long? experience)
    {
        if (TryGetPropertyIgnoreCase(root, "level", out var levelEl) && levelEl.TryGetInt32(out var explicitLevel))
        {
            return explicitLevel;
        }

        if (experience is null)
        {
            return null;
        }

        var level = 0;
        for (var i = 0; i < EdenExperienceThresholds.Length; i++)
        {
            if (experience.Value < EdenExperienceThresholds[i])
            {
                break;
            }

            // AHK arrays are 1-based, preserve equivalent behavior.
            level = i + 1;
        }

        return level == 0 ? null : level;
    }

    private static string? ResolveRealmRank(JsonElement root, long? realmPoints)
    {
        if (TryGetPropertyIgnoreCase(root, "realmRank", out var rrEl)
            || TryGetPropertyIgnoreCase(root, "realm_rank", out rrEl))
        {
            var rrRaw = rrEl.ToString();
            if (!string.IsNullOrWhiteSpace(rrRaw))
            {
                return rrRaw;
            }
        }

        if (realmPoints is null)
        {
            return null;
        }

        var rankIndex = 0;
        for (var i = 0; i < EdenRealmPointThresholds.Length; i++)
        {
            if (realmPoints.Value < EdenRealmPointThresholds[i])
            {
                break;
            }

            // AHK arrays are 1-based, preserve equivalent behavior.
            rankIndex = i + 1;
        }

        if (rankIndex == 0)
        {
            return null;
        }

        var rrMajor = (rankIndex / 10) + 1;
        var rrMinor = rankIndex % 10;
        return $"RR{rrMajor}L{rrMinor}";
    }

    private static long? ResolveLong(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(root, propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        var raw = value.ToString();
        return long.TryParse(raw, out number) ? number : null;
    }

    private static JsonElement UnwrapPlayerRoot(JsonElement root)
    {
        var current = root;

        if (current.ValueKind == JsonValueKind.Array && current.GetArrayLength() > 0)
        {
            foreach (var first in current.EnumerateArray())
            {
                if (first.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    current = first;
                }

                break;
            }
        }

        if (current.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(current, "data", out var data)
            && data.ValueKind == JsonValueKind.Object)
        {
            current = data;
        }

        if (current.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(current, "player", out var player)
            && player.ValueKind == JsonValueKind.Object)
        {
            current = player;
        }

        if (current.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(current, "character", out var character)
            && character.ValueKind == JsonValueKind.Object)
        {
            current = character;
        }

        return current;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return TryGetPropertyIgnoreCase(root, propertyName, out var value) ? value.ToString() : null;
    }

    private static string DescribeCookieHeader(HttpRequestHeaders headers)
    {
        if (!headers.TryGetValues("cookie", out var values))
        {
            return "<none>";
        }

        var raw = string.Join("; ", values);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "<empty>";
        }

        var names = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split('=', 2)[0].Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return $"{string.Join(",", names)} (len={raw.Length})";
    }

}
