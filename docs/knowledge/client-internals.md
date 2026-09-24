# DAoC Client Internals (Eden / OpenDAoC)

Memory-layout findings used by HeraldHelper's live-reading paths. All
discovered without per-build offsets (structures self-locate).

## Chat log FILE* buffer

- The CRT `chat.log` FILE* holds the chat buffer; the file pointer RVA is
  auto-derived per binary by `ClientChatLogFilePtrLocator` — scan the
  binary, no hardcoded offsets.
- `DaocMemoryChatSource` reads the buffer directly from process memory
  (requires elevation). Config keys: `chatMemReadEnabled`,
  `chatMemProcess`, `chatMemRva`.

## Adapter registry (std::map on the heap)

- `DaocMemoryStatsSource` + `DaocAdapterMapReader` heap-scan for a known
  adapter name string → climb the `std::map` head sentinel → enumerate
  ~1000 adapter records. No offsets needed.
- Record kinds self-classify:
  - numeric = `double` @ +0x10 (bound sentinel `double.MinValue` @ +0x18)
  - text = rendered `std::string` @ +0xC (resists arrive as `"+14%"`)
- Values surface via `IAdapterValueSource.LatestAdapterValues` on the
  capture chain (also feeds `summary_target` fallback for CC parsing).

## Chat text arena (scrollback)

- `DaocScrollbackChatSource` diffs client heap regions whose strings score
  chat-shaped (hint words + complete-sentence endings). New strings at the
  arena's growing edge = fresh lines.
- Dedupe keyed by allocation address (new alloc = new line; reused
  address + changed text = new line); cross-window copies collapse via a
  ~1.5 s containment window.
- Lines must start uppercase/`[`/`(`/`*`/`<`/digit — lowercase start is a
  wrapped fragment. Wrapped/merged lines under spam → opt-in only
  (`scrollbackChatEnabled` on top of `chatMemReadEnabled`, never auto-on).
- Segments carry small leaked metadata prefixes (`X9>`, `0B`, `r=`
  shapes) — stripped in `TrimToLineStart`. Prefer arenas holding
  unwrapped full lines.
- Caveats: needs ≥1 chat line before binding (lazy); occasional junk
  lines (parser ignores); no client timestamps — HeraldHelper assigns
  arrival time.

## Global object registry

- `{objPtr, seqId}` ~60K contiguous ptr-sorted entries — holds chat line
  objects but is **not tail-able** (inserts mid-table). Per-window
  `{linePtr, metric}` display arrays and glyph-index tables live in the
  same arena.

## Bitmap font / customN regions

- `DaocUiBitmapFontProfileResolver` resolves `customN` watch regions in
  any package layout (flat `customN_window.xml`, root `<Include>` stubs,
  recursive feature dirs) and finds font catalogs in every package dir
  (`runtime/catalogs/fonts.xml`, root + feature `fonts.xml`) with
  ui/-relative or package-relative `<File>` paths.
- `DaocBitmapFontGlyphReader` does real TGA atlas glyph matching before
  generic-OCR fallback.
