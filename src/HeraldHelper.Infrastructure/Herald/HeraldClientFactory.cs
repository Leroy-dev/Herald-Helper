using HeraldHelper.Application.Contracts;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Infrastructure.Auth;
using HeraldHelper.Infrastructure.Configuration;

namespace HeraldHelper.Infrastructure.Herald;

public sealed class HeraldClientFactory : IHeraldClientFactory
{
    private readonly IHeraldClient _phoenix;
    private readonly IHeraldClient _titan;
    private readonly IHeraldClient _celestius;
    private readonly IHeraldClient _eden;
    private readonly IHeraldClient _blackthorn;
    private readonly IHeraldClient _default;

    public HeraldClientFactory(
        HttpClient httpClient,
        Func<IReadOnlyDictionary<string, string>> getSettings,
        IShardAuthRefreshService? authRefreshService = null,
        IResponseDiagnostics? diagnostics = null)
    {
        ShardAuthBundle PhoenixAuth() => ShardAuthSettingsResolver.Resolve(getSettings(), ShardType.Phoenix);
        ShardAuthBundle TitanAuth() => ShardAuthSettingsResolver.Resolve(getSettings(), ShardType.Titan);
        ShardAuthBundle CelestiusAuth() => ShardAuthSettingsResolver.Resolve(getSettings(), ShardType.Celestius);
        ShardAuthBundle BlackthornAuth() => ShardAuthSettingsResolver.Resolve(getSettings(), ShardType.Blackthorn);

        _default = new NullHeraldClient();
        _phoenix = new PhoenixHeraldClient(httpClient, PhoenixAuth);
        _titan = new TitanHeraldClient(httpClient, TitanAuth);
        _celestius = new CelestiusHeraldClient(httpClient, CelestiusAuth);
        _eden = new EdenHeraldClient(
            httpClient,
            () => HeraldRuntimeConfig.FromMap(getSettings()),
            authRefreshService is null ? null : (ct => authRefreshService.RefreshAsync(ShardType.Eden, ct)),
            diagnostics);
        _blackthorn = new BlackthornHeraldClient(httpClient, BlackthornAuth);
    }

    public IHeraldClient Resolve(ShardType shardType)
    {
        return shardType switch
        {
            ShardType.Default => _default,
            ShardType.Phoenix => _phoenix,
            ShardType.Titan => _titan,
            ShardType.Celestius => _celestius,
            ShardType.Eden => _eden,
            ShardType.Blackthorn => _blackthorn,
            _ => throw new ArgumentOutOfRangeException(nameof(shardType), shardType, "Unsupported shard")
        };
    }
}
