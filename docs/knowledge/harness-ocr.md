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
