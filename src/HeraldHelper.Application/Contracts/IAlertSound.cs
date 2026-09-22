namespace HeraldHelper.Application.Contracts;

public enum AlertKind
{
    /// <summary>You got crowd-controlled.</summary>
    SelfCc,

    /// <summary>A new enemy entered the peel list.</summary>
    IncomingAttack,

    /// <summary>Your cast got interrupted.</summary>
    CastInterrupted
}

/// <summary>Optional audible alerts — desktop plays system sounds; tests stay
/// silent. Fired from the parse/track path, not the render path, so alerts
/// happen exactly once per chat event.</summary>
public interface IAlertSound
{
    void Play(AlertKind kind);
}
