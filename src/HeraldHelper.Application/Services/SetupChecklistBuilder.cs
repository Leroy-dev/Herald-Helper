namespace HeraldHelper.Application.Services;

/// <summary>One setup-checklist row — what's checked, whether it passes,
/// and a one-line hint on how to fix it.</summary>
public sealed record SetupCheckItem(string Name, bool Ok, string Hint);

/// <summary>Inputs the checklist needs — assembled by the desktop layer from
/// settings, runtime flags and the filesystem so the builder stays pure.</summary>
public sealed record SetupChecklistInput(
    bool ConservativeMode,
    bool ChatMemReadEnabled,
    bool StatsMemReadEnabled,
    bool IsElevated,
    bool HasChatRegion,
    bool HasWatchRegions,
    bool ChatLogAvailable,
    bool RelayAvailable,
    bool CustomUiFolderResolved,
    bool CatalogsPresent,
    bool CharacterSelected,
    bool OcrEngineAvailable,
    int OverlayElementsEnabled);

/// <summary>"Why doesn't X work" answered before the user asks — the
/// configuration surface keeps growing and the failure modes all live in
/// different corners (elevation, conservative mode, missing region, empty
/// UI folder, missing catalogs).</summary>
public static class SetupChecklistBuilder
{
    public static IReadOnlyList<SetupCheckItem> Build(SetupChecklistInput input)
    {
        var items = new List<SetupCheckItem>();

        items.Add(new SetupCheckItem(
            "Character selected",
            input.CharacterSelected,
            input.CharacterSelected
                ? "Active character drives class-aware features."
                : "Pick a character on the Config view — abilities and stats need it."));

        if (input.ConservativeMode)
        {
            items.Add(new SetupCheckItem(
                "Process memory (conservative mode)",
                true,
                "Off by choice — OCR / chat.log / relay still feed the helper."));
        }
        else
        {
            items.Add(new SetupCheckItem(
                "Elevation",
                input.IsElevated,
                input.IsElevated
                    ? "Running elevated — process memory reads allowed."
                    : "Run as administrator to enable memory chat/stats/group frames."));
            items.Add(new SetupCheckItem(
                "Memory chat source",
                input.ChatMemReadEnabled,
                input.ChatMemReadEnabled
                    ? "Reading chat from the client's chat.log memory buffer (scrollback reader is opt-in)."
                    : "Enable 'chat memory read' in Config or rely on OCR/chat.log."));
            items.Add(new SetupCheckItem(
                "Live stats / adapters",
                input.StatsMemReadEnabled,
                input.StatsMemReadEnabled
                    ? "Adapter map live — target names, group frames, world timers."
                    : "Enable 'stats memory read' for group frames, target names, world timers."));
        }

        var chatSources = (input.HasChatRegion ? 1 : 0) +
                          (input.ChatMemReadEnabled && !input.ConservativeMode ? 1 : 0) +
                          (input.ChatLogAvailable ? 1 : 0) +
                          (input.RelayAvailable ? 1 : 0);
        items.Add(new SetupCheckItem(
            "Chat source",
            chatSources > 0,
            chatSources switch
            {
                0 => "No chat source — drag an OCR region over the chat window or enable memory/chat.log/relay.",
                1 => "One chat source active.",
                _ => $"{chatSources} chat sources active."
            }));

        items.Add(new SetupCheckItem(
            "OCR engine",
            input.OcrEngineAvailable,
            input.OcrEngineAvailable
                ? "Windows OCR ready."
                : "Windows OCR unavailable — customN regions and generic chat OCR will not work."));

        items.Add(new SetupCheckItem(
            "Watch regions",
            input.HasWatchRegions,
            input.HasWatchRegions
                ? "OCR watch regions configured."
                : "No watch regions — optional, but customN regions need them for stats/buffs."));

        items.Add(new SetupCheckItem(
            "Custom UI package",
            input.CustomUiFolderResolved,
            input.CustomUiFolderResolved
                ? "UI package found — bitmap-font OCR can read your chat font."
                : "Set the custom UI folder in Config or install the launcher (auto-detect)."));

        items.Add(new SetupCheckItem(
            "Ability catalogs",
            input.CatalogsPresent,
            input.CatalogsPresent
                ? "Ability/spell catalogs on disk."
                : "Run 'Update Catalogs' in Abilities — needed for cast times and ability metadata."));

        items.Add(new SetupCheckItem(
            "Overlay elements",
            input.OverlayElementsEnabled > 0,
            input.OverlayElementsEnabled > 0
                ? $"{input.OverlayElementsEnabled} overlay element(s) enabled."
                : "Everything's hidden — enable elements on the Overlay view."));

        return items;
    }
}
