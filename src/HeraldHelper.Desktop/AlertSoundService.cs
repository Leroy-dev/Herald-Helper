using System.Media;
using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Models;

namespace HeraldHelper.Desktop;

/// <summary>System-sound alerts gated by the overlay sound settings —
/// Settings reads happen per alert so toggles apply instantly.</summary>
public sealed class AlertSoundService : IAlertSound
{
    private readonly Func<OverlaySettings> _getOverlay;

    public AlertSoundService(Func<OverlaySettings> getOverlay)
    {
        _getOverlay = getOverlay;
    }

    public void Play(AlertKind kind)
    {
        var overlay = _getOverlay();
        if (!overlay.SoundsEnabled)
        {
            return;
        }

        var (enabled, sound) = kind switch
        {
            AlertKind.SelfCc => (overlay.SoundSelfCc, SystemSounds.Hand),
            AlertKind.IncomingAttack => (overlay.SoundPeel, SystemSounds.Exclamation),
            AlertKind.CastInterrupted => (overlay.SoundInterrupt, SystemSounds.Asterisk),
            _ => (false, SystemSounds.Beep)
        };

        if (enabled)
        {
            sound.Play();
        }
    }
}
