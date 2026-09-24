# Herald Helper

A C# .NET 10 WPF in-game overlay and helper for Dark Age of Camelot (DAoC), targeting the Eden and Blackthorn freeshards.

## What it does

Herald Helper watches the DAoC client and renders click-through overlay windows with:

- Target name and profile while herald details load.
- Pending cast bars with fixed, repeated, and interrupt-aware timing.
- Ability / crowd-control timers driven by landed ability and spell hits.
- Target resists (thrust/slash/crush verdicts from armor class).
- Self-CC banner and a "who is peeling me" attacker list.
- **Your active buffs with real names and icons** — read from the client's
  own effects list, the same data the top-of-screen buff bar draws.
- A pet window with vitals and active-effect icons.
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
