# HeraldHelper C# Migration Draft

## Target Project Structure

- `src/HeraldHelper.Domain`
- `src/HeraldHelper.Application`
- `src/HeraldHelper.Infrastructure`
- `src/HeraldHelper.Desktop`
- `tests/HeraldHelper.Tests`

## Responsibility Split

- `Domain`: Pure models and enums.
  - `TargetProfile`, `AbilityHit`, `CcTimerEntry`, `ScreenRegion`
- `Application`: Use-case orchestration and interfaces.
  - `GameLoopOrchestrator`
  - `IChatCaptureService`, `IChatEventParser`, `IHeraldClientFactory`, `IOverlayRenderer`, `ICcImmunityTracker`
- `Infrastructure`: External details and adapters.
  - OCR/screen capture adapter
  - Herald API clients per shard
  - Overlay renderer
  - Composition root
- `Desktop`: WPF host and settings UI.
- `Tests`: Timer/parser behavior and orchestration tests.

## Mapping From `ocr.ahk`

- `readOutText()` -> `GameLoopOrchestrator.TickAsync()`
- `getHeraldInfo()` -> `IHeraldClient` implementations + `IHeraldClientFactory`
- `addToTimerInformation()` + `TimerDecrease` -> `CcImmunityTracker`
- `guiiInfoDraw()` + overlay draw methods -> `IOverlayRenderer`
- config/hotkeys (`readConfig`, `writeConfig`) -> Desktop settings + typed config model

## Suggested Next Implementation Order

1. Replace `StubChatCaptureService` with real capture + OCR.
2. Replace `SimpleChatEventParser` with full DAoC chat parser rules from `abilities.txt`.
3. Replace `StubHeraldClient` with per-shard HTTP clients and DTO mapping.
4. Replace `DebugOverlayRenderer` with transparent click-through overlay windows.
5. Add persistence for selected region, font, toggles, and shard settings.

## Current State

- Projects compile as a clean baseline architecture.
- Infrastructure is intentionally stubbed so you can migrate feature-by-feature without a big-bang rewrite.
