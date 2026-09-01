namespace HeraldHelper.Domain.Models;

public sealed record SyncResult(bool Success, string? Error = null, TargetProfile? Profile = null)
{
    public static SyncResult Ok(TargetProfile? profile = null) => new(true, null, profile);

    public static SyncResult Offline => new(true, "Online sync is disabled.", null);

    public static SyncResult Failed(string error) => new(false, error, null);
}
