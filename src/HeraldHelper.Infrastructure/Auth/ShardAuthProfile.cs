using HeraldHelper.Domain.Enums;

namespace HeraldHelper.Infrastructure.Auth;

public sealed record ShardAuthProfile(
    ShardType Shard,
    string HubUrl,
    string Domain,
    IReadOnlyList<string> PreferredCookieNames,
    IReadOnlyList<string>? RequiredCookieNames = null,
    string? ValidateUrl = null,
    IReadOnlyList<string>? ValidateDenyPhrases = null,
    /// <summary>Phrases in the live hub page's visible text that mean the
    /// session is anonymous (e.g. Eden's "LOGIN" nav button). Interactive
    /// capture only trusts cookies once the page no longer shows them.</summary>
    IReadOnlyList<string>? HubDenyPhrases = null,
    /// <summary>UA captured at login. Eden binds sessions to the user agent —
    /// a headless refresh must present the SAME UA or the server invalidates
    /// the session (manifests as "the browser logged me out").</summary>
    string? SavedUserAgent = null);
