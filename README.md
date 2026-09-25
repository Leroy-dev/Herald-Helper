# Herald Helper

A C# .NET 10 WPF in-game overlay and helper for Dark Age of Camelot (DAoC), targeting the Eden, Blackthorn and OpenDAoC-compatible freeshards.

## What it does

Herald Helper watches the DAoC client and renders click-through overlay windows with:

- Target name, profile, and live HP% while herald details load.
- Pending cast bars with fixed, repeated, and interrupt-aware timing.
- Ability / crowd-control timers driven by landed ability and spell hits —
  immunity math is server-verified (flat 60s post-effect on OpenDAoC, live-era
  5x melee rule on Eden).
- A per-category `READY:` line (Stun / Mezz / Root / Nearsight) on the
  current target, plus an optional sound when a category opens up.
- A cooldowns window for spell recasts and realm abilities (real cooldown
  values from the server spell table and realm-ability handlers).
- Target resists — real `stats_*` adapter values when memory reads are on,
  class-table verdicts otherwise.
- Self-CC banner that clears at the real effect-expire message.
- A "who is peeling me" attacker list.
- **Group frames with vitals, buff icons, and CC badges** — broadcast lines
  ("Bobby is stunned!") mark the matching group member for ~12s.
- **Your active buffs with real names and icons** — read from the client's
  own effects list, the same data the top-of-screen buff bar draws.
- A pet window with vitals and real effect names resolved from the client's
  icon→spell map.
- Configurable OCR replay capture for deterministic debugging.

## Data sources

The app layers several capture paths and prefers the most reliable one available:

| Source | Needs | Notes |
| --- | --- | --- |
| `/chatlog` file tail | `/chatlog` enabled in game | Preferred chat source; plain file reads, no memory access. |
| Adapter map read | stats memory read + elevated | Client UI adapters: stats, resists, group, pet vitals, **self-effect names/icons**. |
| Chat `FILE*` buffer | chat memory read + elevated | Reads the CRT buffer of the client's own `chat.log` handle. |
| Scrollback arena | chat memory read + elevated | Experimental; can drop/mangle lines under chat spam. Off by default. |
| OCR watch regions | nothing | Fallback and always-on path; no memory access at all. |

Memory-dependent features are **opt-in** and read-only. With all memory reads
off, the app runs in conservative mode on OCR + file tailing alone.

**Note:** reading another process's memory requires running Herald Helper
elevated (the game runs elevated). All reads are passive `ReadProcessMemory`
calls — nothing is written to or injected into the client. Check your shard's
rules; you can leave every memory feature disabled and lose only the
memory-backed panels.

## Project structure

- `src/HeraldHelper.Domain` — core models and enums.
- `src/HeraldHelper.Application` — application logic and contracts.
- `src/HeraldHelper.Infrastructure` — OCR, parsing, herald HTTP clients, casting catalogs, process-memory readers.
- `src/HeraldHelper.Desktop` — WPF desktop UI, catalog update service, and SQLite persistence.
- `tests/HeraldHelper.Tests` — xUnit tests.
- `data/eden-charplan` and `data/blackthorn-charplan` — generated DAoC catalog data.
- `data/client-tables` — tables exported from the client's `gamedata.mpk` and
  the deployed server DB (`spells.csv`, `icons.csv`, `server-spells.csv`,
  `ra-cooldowns.csv`) — used for icon→name resolution and as the cast-spell /
  cooldown catalog on non-Eden/Blackthorn shards.
- `docs/knowledge/` — reverse-engineering notes on the client, the server
  CC/immunity model, OCR, and capture internals.
- `scripts/` — catalog crawlers, portable release packaging.

## Build and test

Requires the .NET 10 SDK on Windows.

```powershell
dotnet build HeraldHelper.slnx -v minimal
dotnet test HeraldHelper.slnx --no-build
```

## Portable release

```powershell
.\scripts\release.ps1
```

Produces a self-contained win-x64 folder plus a packaged archive under
`artifacts/release/` — no .NET install needed on the target machine. Chromium
is bundled for catalog crawling; the bundled Tesseract OCR is used if present.

## Configuration

First-run settings are stored in a local SQLite database and are not committed.
Do not commit `cfg.ini` or any `edenHeaders.*` files; they contain session cookies and machine-specific state.

## Status

Herald Helper is an active work in progress. The roadmap is tracked in [PROJECT_PLAN.md](PROJECT_PLAN.md).
