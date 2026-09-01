using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop.Services;

internal sealed class RestOnlineTargetProfileClient : IOnlineTargetProfileClient
{
    private readonly HttpClient _httpClient;
    private readonly IWritableSettings<HeraldHelperSettings> _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public RestOnlineTargetProfileClient(HttpClient httpClient, IWritableSettings<HeraldHelperSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<TargetProfile?> DownloadAsync(ShardType shard, string name, CancellationToken cancellationToken = default)
    {
        var baseUrl = GetBaseUrl();
        if (baseUrl is null)
        {
            return null;
        }

        try
        {
            var shardName = shard.ToString().ToLowerInvariant();
            var url = $"{baseUrl}/api/targets/{Uri.EscapeDataString(shardName)}/{Uri.EscapeDataString(name)}";
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var profile = await response.Content.ReadFromJsonAsync<TargetProfile>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (profile is not null && !string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                profile = profile with { Name = name };
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UploadAsync(ShardType shard, TargetProfile profile, CancellationToken cancellationToken = default)
    {
        var baseUrl = GetBaseUrl();
        if (baseUrl is null)
        {
            return false;
        }

        try
        {
            var url = $"{baseUrl}/api/targets";
            var response = await _httpClient.PostAsJsonAsync(url, profile, JsonOptions, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = GetBaseUrl();
        if (baseUrl is null)
        {
            return false;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var response = await _httpClient.GetAsync($"{baseUrl}/health", cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string? GetBaseUrl()
    {
        var url = _settings.Value.Online.ServerUrl?.Trim();
        return string.IsNullOrWhiteSpace(url) ? null : url.TrimEnd('/');
    }
}
