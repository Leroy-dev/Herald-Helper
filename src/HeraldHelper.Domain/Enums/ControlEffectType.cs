namespace HeraldHelper.Domain.Enums;

public enum ControlEffectType
{
    Mezz,
    Stun,
    Root,
    /// Vision-range debuff — no hard-CC immunity window, timer tracks the
    /// debuff duration itself.
    Nearsight
}
