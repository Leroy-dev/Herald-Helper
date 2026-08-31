# Herald Helper

A C# .NET 10 WPF in-game overlay and helper for Dark Age of Camelot (DAoC).

## What it does

Herald Helper watches OCR regions of the DAoC client, parses chat and UI events, looks up player profiles from the Eden and Blackthorn heralds, and renders a click-through overlay with:

- Target name and profile while details load.
- Pending cast bars with fixed, repeated, and interrupt-aware timing.
- Ability/ crowd-control timers.
- Configurable OCR replay capture for deterministic debugging.

## Project structure

- `src/HeraldHelper.Domain` — core models and enums.
- `src/HeraldHelper.Application` — application logic and contracts.
- `src/HeraldHelper.Infrastructure` — OCR, parsing, herald HTTP clients, casting catalogs, and overlay rendering.
- `src/HeraldHelper.Desktop` — WPF desktop UI, catalog update service, and SQLite persistence.
- `tests/HeraldHelper.Tests` — xUnit tests.
- `data/eden-charplan` and `data/blackthorn-charplan` — generated DAoC catalog data.
- `scripts/` — Node.js catalog crawlers.

## Build and test

Requires the .NET 10 SDK on Windows.

```powershell
dotnet build HeraldHelper.slnx -v minimal
dotnet test HeraldHelper.slnx --no-build
```

## Configuration

First-run settings are stored in a local SQLite database and are not committed.
Do not commit `cfg.ini` or any `edenHeaders.*` files; they contain session cookies and machine-specific state.

## Status

Herald Helper is an active work in progress. The roadmap is tracked in [PROJECT_PLAN.md](PROJECT_PLAN.md).
