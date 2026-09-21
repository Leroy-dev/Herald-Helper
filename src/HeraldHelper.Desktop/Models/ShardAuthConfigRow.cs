using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Desktop.Models;

/// <summary>One editable row of the per-shard auth table on the Config view.</summary>
public sealed class ShardAuthConfigRow
{
    public required ShardType Shard { get; init; }
    public bool Enabled { get; set; }
    public string HubUrl { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string CookieNames { get; set; } = string.Empty;
    public string RequiredCookies { get; set; } = string.Empty;
    public string ValidateUrl { get; set; } = string.Empty;
}
