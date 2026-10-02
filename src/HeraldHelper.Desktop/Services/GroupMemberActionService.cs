using HeraldHelper.Application.Contracts;
using HeraldHelper.Desktop.Models;
using HeraldHelper.Infrastructure.Capture;

namespace HeraldHelper.Desktop.Services;

/// <summary>
/// Click-to-heal: a click on group row N sends the configured "select member N"
/// key followed by the configured action key (heal/qbind bind) to the game via
/// SendInput. The overlay never steals focus (WS_EX_NOACTIVATE) so the game
/// stays foreground and DirectInput receives the keys.
/// </summary>
public sealed class GroupMemberActionService
{
    private readonly Func<OverlaySettings> _getOverlay;
    private readonly IGameInputSender _sender;
    private readonly IResponseDiagnostics? _diagnostics;

    public GroupMemberActionService(
        Func<OverlaySettings> getOverlay,
        IGameInputSender sender,
        IResponseDiagnostics? diagnostics = null)
    {
        _getOverlay = getOverlay;
        _sender = sender;
        _diagnostics = diagnostics;
    }

    public void OnMemberClicked(int memberIndex)
    {
        var overlay = _getOverlay();
        var selectKeys = DaocChatLogPump.ParseVirtualKeyList(overlay.GroupHealSelectKeys);
        if (memberIndex < 0 || memberIndex >= selectKeys.Length)
        {
            _diagnostics?.Log($"[GroupAction] member {memberIndex} clicked but only {selectKeys.Length} select key(s) configured");
            return;
        }

        var keys = new List<int> { selectKeys[memberIndex] };
        if (!string.IsNullOrWhiteSpace(overlay.GroupHealActionKey))
        {
            keys.Add(DaocChatLogPump.ParseVirtualKey(overlay.GroupHealActionKey));
        }

        if (!_sender.SendSequence(keys))
        {
            _diagnostics?.Log($"[GroupAction] member {memberIndex}: keys not delivered (game not foreground?)");
        }
    }
}
