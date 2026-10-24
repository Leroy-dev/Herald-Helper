using HeraldHelper.Desktop.Models;
using HeraldHelper.Desktop.Services;
using HeraldHelper.Infrastructure.Capture;

namespace HeraldHelper.Tests;

public sealed class GroupMemberActionServiceTests
{
    private sealed class FakeSender : IGameInputSender
    {
        public readonly List<int> Sent = [];
        public bool Result = true;
        public bool SendSequence(IReadOnlyList<int> virtualKeys)
        {
            Sent.AddRange(virtualKeys);
            return Result;
        }
    }

    private static OverlaySettings Overlay(
        bool enabled = true,
        string selectKeys = "F1,F2,F3,F4,F5,F6,F7,F8",
        string actionKey = "F9") => new()
        {
            GroupClickHeal = enabled,
            GroupHealSelectKeys = selectKeys,
            GroupHealActionKey = actionKey
        };

    [Fact]
    public void Click_member0_sends_select_key_then_action_key()
    {
        var sender = new FakeSender();
        var service = new GroupMemberActionService(() => Overlay(), sender);

        service.OnMemberClicked(0);

        // F1 = 0x70 select, F9 = 0x78 action
        Assert.Equal(new[] { 0x70, 0x78 }, sender.Sent);
    }

    [Fact]
    public void Click_member5_sends_slot_five_key()
    {
        var sender = new FakeSender();
        var service = new GroupMemberActionService(() => Overlay(), sender);

        service.OnMemberClicked(5);

        Assert.Equal(new[] { 0x70 + 5, 0x78 }, sender.Sent);
    }

    [Fact]
    public void Empty_action_key_sends_select_only()
    {
        var sender = new FakeSender();
        var service = new GroupMemberActionService(() => Overlay(actionKey: ""), sender);

        service.OnMemberClicked(2);

        Assert.Equal(new[] { 0x72 }, sender.Sent);
    }

    [Fact]
    public void Member_beyond_configured_keys_sends_nothing()
    {
        var sender = new FakeSender();
        var service = new GroupMemberActionService(() => Overlay(selectKeys: "F1,F2"), sender);

        service.OnMemberClicked(5);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public void Unconfigured_select_keys_default_to_f12()
    {
        // ParseVirtualKeyList("") yields the pump default [F12] — clicking
        // member 0 with no configured keys still presses F12 rather than
        // throwing.
        var sender = new FakeSender();
        var service = new GroupMemberActionService(() => Overlay(selectKeys: ""), sender);

        service.OnMemberClicked(0);

        Assert.Equal(new[] { 0x7B, 0x78 }, sender.Sent);
    }
}
