namespace HeraldHelper.Domain.Enums;

public enum ControlEffectType
{
    Mezz,
    Stun,
    Root,
    /// Vision-range debuff — no hard-CC immunity window, timer tracks the
    /// debuff duration itself.
    Nearsight,
    /// Snare that does NOT create a root/snare immunity window — damage
    /// spells with a snare component (DamageSpeedDecrease). Pure snares
    /// share root immunity and should be configured as Root instead.
    Snare
}
