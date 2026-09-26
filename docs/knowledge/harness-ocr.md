# HeraldHelper Reading Paths

How the harness gets game state, and where each path can fail.

## Chat sources (in priority/merge order)

| Source | Notes |
|--------|-------|
| `DaocMemoryChatSource` | Reads CRT `chat.log` FILE* buffer from process memory. Requires elevation. Most faithful. |
| `DaocScrollbackChatSource` | Heap-arena diff, no chatlog needed. Opt-in (`scrollbackChatEnabled`), wrapped fragments possible, no client timestamps. |
| chat.log file | Fallback in the chain. |

- The loop no longer refuses to tick without a dragged chat region when
  a non-OCR chat source is enabled.

## OCR paths

- `customN` regions: `DaocUiBitmapFontProfileResolver` + real TGA atlas
  glyph matching (`DaocBitmapFontGlyphReader`) before generic OCR.
- `customUiFolder` setting accepts game root / `ui/` dir / package dir /
  `uimain.xml`; empty = Eden/Blackthorn launcher auto-detect.
- Region resolution tolerates any package layout — flat files, root
  Include stubs, recursive feature dirs — and every fonts.xml location.

## Adapter values

- `IAdapterValueSource.LatestAdapterValues` — memory (`std::map` heap
  records) feeding the capture chain; supplies `summary_target` and the
  numeric/text classification described in client-internals.md.
- `ClientStateExtractor` projects the raw dict into `ClientStateSnapshot`;
  surfaces include `summary_player_hits/power/end` (own vitals %),
  `summary_target_hits` (target HP %), `stats_<resist>` (own resists as
  "+15%"/"-20%"), `group_*` members, `self_effectN`, `mini_pet_*`,
  `summary_icon_desc{row}{col}` buffs, `combat_mode`, `compass_heading`,
  `stats_realm_points`, `bounty_points`, `mino_relic_time_percent`,
  `release_timer_time`, `timer_time`, `siege_*`.
- Enemy resists are NOT exposed by the client — the resists overlay keeps
  class-armor verdicts for targets and shows the *own* resists/vitals
  read live instead.
- The timers window appends a `READY:` line per immunity category
  (Stun/Mezz/Root/Nearsight — root+snare share one bucket) for the
  current target, using the same name normalization as the tracker.
  A short optional ping (`alertCcReady`) plays when a category opens.
- Group frames carry CC badges from broadcast `Message2` lines
  ("{name} is stunned!") joined onto `group_nameN` — needs the stats
  adapter for member names.
- A broadcast stun apply naming the *current target* synthesizes a
  tracker entry (~11s + 60s flat ≈ 71s window) even when no cast line
  was seen — stun only; other CC durations are too variable to guess.

## Failure behavior

- ~20 consecutive identical tick failures auto-stop the loop
  (`RuntimeLoop.Stopped` → toolbar button re-sync).
- `RuntimeController.Rebuild` preserves running state; the Run/Stop
  button re-syncs from `IsRunning` on every view switch (fixes the
  "have to click stop then start" desync when switching Live/Config).
- `GameLoopOrchestrator` emits `[Timing]` diagnostics per
  capture/parse/replay/cache/lookup/render/tick — first tool when
  something looks stale.
- Overlay windows clamp position into the virtual screen so saved
  positions can't render off-screen on smaller resolutions.
