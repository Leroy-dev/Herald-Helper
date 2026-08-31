# HeraldHelperAHK Project Plan

Status: 2026-08-31. This is the practical technical handoff and prioritized roadmap for the current .NET application. It intentionally contains no cookie, token, session, or user-agent values.

## 1. Project purpose and users

HeraldHelper is a Windows companion overlay for Dark Age of Camelot freeshard players. It reads visible game UI regions, recognizes chat and character statistics, identifies the latest player target, queries the selected shard's herald, tracks crowd-control immunity, and displays target and cast information without modifying the game client.

The primary users are players on Eden and Blackthorn who need quick target information and reliable cast/CC timing during live play. Phoenix, Titan, and Celestius lookup clients exist, but their catalog and class-profile support is not at Eden/Blackthorn parity.

The C# application is the implementation to extend and validate. New work should be driven by current user-visible behavior, captured OCR sessions, generated shard data, and automated tests.

## 2. Current state and implementation progress

### Implemented

- .NET 10 WPF desktop application split into Domain, Application, Infrastructure, Desktop, and xUnit test projects.
- Configurable OCR/game loop running at approximately 350 ms with overlap protection.
- Region-based chat OCR plus direct DAoC bitmap-font glyph reading for supported attribute and resistance windows.
- English chat parsing for player targets, friendly/enemy membership, cast start/completion/interruption, repeated casts, and ability hits.
- Eden and Blackthorn herald lookups with cancellation, retry behavior, background refresh, and a shard-separated SQLite target cache.
- CC immunity calculations with class-sensitive Determination and light-Determination rules.
- Eden/Blackthorn class-aware cast catalogs, rank selection by level, official icon lookup, and local metadata/icon overrides.
- Per-server, per-class, and per-character ability profiles with aliases and enabled/disabled overrides.
- Cast metrics using OCR-read dexterity, acuity, casting speed, spell damage, fixed-cast metadata, and a two-second minimum for adjustable casts.
- Target, CC timer, and cast-bar overlays; non-player targets preserve the last displayed player.
- Searchable, virtualized Eden/Blackthorn spell browser with hover details, entry editor, and icon chooser.
- Catalog updater with staging, validation, backup, publish, and rollback behavior.
- SQLite configuration and profiles, one-time legacy config import, DPAPI protection for local secrets, and secret-free JSON export.
- OCR replay capture under `%LocalAppData%\HeraldHelper\ocr-replay`.

### Partial or intentionally limited

- Eden and Blackthorn are the primary complete profiles. Other shard clients can resolve players but have no equivalent generated cast catalog.
- Damage output is an estimate, not a server-authoritative combat simulator.
- OCR replay data is captured, but there is no complete headless replay-to-regression-test runner.
- The resistance toggle is persisted and read by the renderer, but there is no dedicated resistance overlay surface yet.
- A newly seen uncached player gets a pending name-only profile, but the current renderer suppresses it until herald details arrive.
- Mauler is intentionally excluded from Eden generation because Eden does not provide that class.

## 3. Architecture deep-dive

### Solution boundaries

- `src/HeraldHelper.Domain` contains shard/effect enums and state models such as `TargetProfile`, `CastSpellInfo`, `CastBarState`, and CC entries. Keep it independent of WPF, HTTP, SQLite, OCR libraries, and the filesystem.
- `src/HeraldHelper.Application` contains contracts, orchestration, state transitions, CC tracking, and cast calculations. It should express behavior through interfaces and remain testable without desktop infrastructure.
- `src/HeraldHelper.Infrastructure` implements screen capture, OCR, bitmap glyph reading, parsing, herald HTTP clients, auth automation, generated catalog loading, diagnostics, and replay persistence.
- `src/HeraldHelper.Desktop` owns WPF windows, composition, configuration editing, SQLite persistence, browser/editor UI, and integration with the UI dispatcher.
- `tests/HeraldHelper.Tests` contains cross-layer xUnit tests. Some tests reference Desktop intentionally to validate persistence and generated-data integration.

### Composition and runtime flow

`src/HeraldHelper.Infrastructure/Composition/AppComposition.cs:16` is the dependency-composition entry point. It selects the OCR implementation, active spell catalog, parser, herald factory, trackers, replay sink, and renderers. `src/HeraldHelper.Desktop/MainWindow.xaml.cs:60` initializes local storage and browser profiles; the dispatcher loop calls the orchestrator around line 636.

```text
saved screen regions
  -> capture each visible region
  -> bitmap glyph reader or OCR fallback
  -> combine labeled OCR segments
  -> parse character stats and chat events
  -> frame-aware event deduplication
  -> target cache and asynchronous herald lookup
  -> CC and cast calculations
  -> one immutable OverlaySnapshot
  -> WPF target/timer/cast windows
  -> optional changed-frame replay record
```

### Main services

- `src/HeraldHelper.Application/Services/GameLoopOrchestrator.cs:9`: owns live target, cast, stats, event-tracker, lookup, and retry state. `TickAsync` starts at line 103. Read this and `GameLoopOrchestratorTests` first for runtime behavior.
- `src/HeraldHelper.Infrastructure/Capture/ScreenCaptureOcrService.cs:12`: captures configured desktop rectangles and records capture diagnostics/snapshots.
- `src/HeraldHelper.Infrastructure/Capture/DaocBitmapFontGlyphReader.cs:16`: reads known DAoC bitmap text directly from active XML/TGA UI assets. `DaocUiBitmapFontProfileResolver` maps a custom window to its font atlas and row geometry.
- `src/HeraldHelper.Infrastructure/Ocr/AdaptiveOcrEngine.cs`: compares configured OCR engines and periodically reevaluates weak output instead of permanently locking onto an early winner.
- `src/HeraldHelper.Infrastructure/Parsing/AbilitiesChatEventParser.cs:9`: identifies target, cast, interrupt, completion, and ability-hit phrases. Fuzzy matching is deliberately bounded to active profile candidates.
- `src/HeraldHelper.Infrastructure/Herald/HeraldClientFactory.cs:8`: selects the shard-specific HTTP client. Endpoint/page changes should be isolated inside the relevant client.
- `src/HeraldHelper.Infrastructure/Auth/PlaywrightShardAuthRefreshService.cs:7`: owns interactive persistent-browser authentication and session collection.
- `src/HeraldHelper.Application/Services/CcImmunityTracker.cs:6`: calculates active mezz/stun/root timers and expiration.
- `src/HeraldHelper.Application/Services/CastMetricsCalculator.cs:5`: applies cast-speed and optional damage calculations. Fixed casts bypass dexterity adjustment; adjustable casts preserve naturally shorter base times and otherwise stop at two seconds.
- `src/HeraldHelper.Desktop/DesktopOverlayRenderer.cs:10`: converts an `OverlaySnapshot` into click-through WPF target, timer, and cast-bar windows.
- `src/HeraldHelper.Desktop/AppDataStore.cs:13`: owns SQLite migrations, settings, profiles, overrides, backups, stats, and target caching. It is currently the main persistence boundary.
- `src/HeraldHelper.Desktop/DataBrowserWindow.cs:20`: shared Eden/Blackthorn data browser. It opens `CatalogEntryEditWindow` and `IconBrowserWindow` for local overrides.
- `src/HeraldHelper.Desktop/CatalogUpdateService.cs:8`: coordinates crawler execution and safe catalog replacement.

### Runtime state and concurrency

- `MainWindow.TickOnceAsync` uses `_tickInProgress` so only one tick runs at once. Slow ticks are skipped rather than queued.
- `TickAsync` currently captures selected regions sequentially, combines their labeled text, parses once, updates state, and renders once.
- Character stats are saved only after two stable observations, reducing persistence of transient OCR corruption.
- `VisibleEventTracker` gives target, cast, and ability events frame-aware identity. Casts reset after one missing frame; target and ability events use the default two missing frames.
- `_targetLock` protects current target, displayed profile, generation, lookup task, retry time, and lookup cancellation source. It does not intentionally cover network waiting.
- A target change increments `_targetGeneration` and cancels the prior lookup. Completion is applied only when generation, shard, and normalized target name still match.
- Target HTTP lookup starts without being awaited on the render path. The result is consumed by `ApplyCompletedTargetLookup` on the same or a later tick.
- Failed target lookup is retryable after five seconds. An incomplete successful profile is refreshable after 30 seconds.
- Herald profile update events must pass the same current-target checks before they update `_lastTarget`.
- `DesktopOverlayRenderer.RenderAsync` marshals window updates onto `Application.Current.Dispatcher`; application services must not manipulate WPF controls directly.
- `FileOcrReplaySink` performs synchronous writes for changed OCR snapshots and retains a maximum of 250 records. Include this work when measuring tick latency.

### Core invariants

- `_currentTargetName` is the newest OCR target; `_lastTarget` is the profile kept on screen. They are intentionally separate.
- A dummy, monster, or other confirmed non-member target must not clear the last valid player profile.
- A cached player is shown immediately and refreshed in the background.
- A stale herald response must never overwrite a newer target, even if requests finish out of order.
- A continuously visible chat line produces at most one action. A genuine repeated occurrence must produce a new action.
- Spell resolution and fuzzy matching stay scoped by shard, active class, and level where available.
- Completion/interruption clears the current cast; an old visible start line must not restart it.
- OCR or network failure should degrade the current snapshot, not terminate the dispatcher loop.

## 4. Data and assets

`data/eden-charplan/` and `data/blackthorn-charplan/` are generated snapshots, not primary hand-edited databases.

### Eden

Run `node scripts/crawl_eden_charplan.js`. The crawler reads Eden charplan resources, downloads class/spell/ability JSON, icons, and sprite sheets into staging, validates minimum counts and required structures, then publishes atomically with backup/rollback. The current manifest reports charplan version `1.0.62`, generated on 2026-08-29, with 45 published classes and Mauler ignored.

### Blackthorn

Run `node scripts/crawl_blackthorn_charplan.js`. It discovers classes from the deployed Next.js application, reads each class page's data, downloads spell/style sheets, validates the result, and publishes atomically. The current manifest was generated on 2026-08-29 with 39 classes. Blackthorn currently does not serve `spells/spl_1600.bmp`; consumers must tolerate that missing upstream asset.

### Ownership rules

- Auto-generated: `data/*-charplan/raw/`, `generated/`, manifests, and downloaded icon/sprite assets.
- Hand-maintained: crawler scripts, catalog loaders, parsers, validation rules, fixed-cast interpretation, schema code, and tests.
- User-maintained: SQLite entry overrides and per-server/class/character ability profiles.
- Never normally repair generated JSON by hand. Fix the crawler/loader, regenerate into staging, inspect the manifest and diff, then run catalog tests.
- `scripts/inspect_eden_charplan.js` is a diagnostic Playwright inspector, not the normal update path.

### Catalog contract

- A spell identity must include shard and class; name alone is not unique.
- Level-aware resolution should select the appropriate rank without crossing class boundaries.
- Icon metadata and cast metadata must come from the same resolved catalog entry.
- Local overrides may change display metadata but must preserve base fixed-cast and damage metadata unless the override explicitly supports replacing it.
- A crawler update is acceptable only when manifests, class counts, local asset references, and catalog-loading tests pass.

## 5. Auth and credentials

Eden auth is handled by `src/HeraldHelper.Infrastructure/Auth/PlaywrightShardAuthRefreshService.cs:7` and `src/HeraldHelper.Desktop/ShardAuthProfileResolver.cs:6`. The service opens a visible persistent Chromium profile under `%LocalAppData%\HeraldHelper\browser-profiles\<shard>`, lets the user complete login, collects shard-domain cookies, and validates the resulting session against the herald endpoint. Automatic Eden refresh defaults to a 25-minute interval; other shards are disabled unless configured.

Cookie headers and user-agent settings are stored in `%LocalAppData%\HeraldHelper\heraldhelper.db`. Sensitive values are encrypted with Windows DPAPI for the current Windows account. JSON backups exclude authentication settings, and imports preserve the destination installation's existing authentication values.

Auth rules:

- Never add raw cookie headers, tokens, browser-storage dumps, or user-agent values to source, fixtures, screenshots, plans, or issue text.
- Do not replace the persistent browser profile while the application or Playwright browser is running.
- A copied database is not a portable credentials backup because DPAPI is tied to the Windows account.
- A 401 should trigger the configured refresh path; repeated 401s require an interactive browser refresh rather than blind request retries.
- Diagnostics may report cookie names/counts and lengths, but not cookie values.

## 6. Persistence and configuration

### Local paths

| Purpose | Location |
| --- | --- |
| SQLite database | `%LocalAppData%\HeraldHelper\heraldhelper.db` |
| Persistent auth browsers | `%LocalAppData%\HeraldHelper\browser-profiles\<shard>` |
| OCR replay records | `%LocalAppData%\HeraldHelper\ocr-replay\<timestamp-guid>` |
| Startup failure log | `%LocalAppData%\HeraldHelper\startup-error.log` |
| Generated shard catalogs | Repository `data/eden-charplan` and `data/blackthorn-charplan` |

### SQLite schema

The current database migration level is 8. `AppDataStore.Initialize` creates `schema_migrations` and applies each missing migration transactionally.

| Table | Responsibility |
| --- | --- |
| `schema_migrations` | Applied database versions and timestamps. |
| `settings` | General key/value configuration; sensitive values are DPAPI-encrypted before storage. |
| `abilities` | Original editable ability rows retained for compatibility/import. |
| `app_meta` | One-time import and application metadata. |
| `eden_entry_overrides` | Local browser/cast metadata and icon overrides; the historical table name is shared by current browser logic. |
| `ability_profile_entries` | Materialized server/class ability rows introduced in migration 5. |
| `ability_profile_overrides` | Current server/class/character deltas, aliases, enabled state, and custom entries. |
| `character_stats` | Per-shard/per-character attributes and item casting/damage bonuses. |
| `target_profile_cache` | Per-shard normalized player profiles used for immediate repeat display. |

The portable JSON backup format currently reports schema version 7, which is separate from SQLite migration version 8. It exports non-sensitive settings, abilities, entry overrides, ability-profile overrides, and character stats. It intentionally does not export auth values or the disposable target cache. Keep those two version concepts explicit in future migrations.

### Important setting groups

| Keys | Meaning |
| --- | --- |
| `server`, `ocrEngine`, `resis` | Active shard, OCR strategy, and CC resist percentage. |
| `MX`, `MY`, `w`, `h` | Compatibility chat rectangle when no character window list supplies chat. |
| `daoc.character.<shard>` | Selected DAoC character for a shard. |
| `daoc.ocr.windows.<shard>.<character>` | JSON list of selected OCR windows and screen rectangles. |
| `daoc.ocr.stats.<shard>.<character>` | Dedicated character-stats OCR region. |
| `ability.profile.class.<shard>[.<character>]` | Selected class defaults and character-specific profile selection. |
| `overlayX/Y`, `overlayX/YTimer`, `overlayX/YCast` | Overlay positions in desktop pixels. |
| `fontSize`, `timerSize`, color keys, `overlayShow*` | Overlay presentation and visibility. |
| `dynamicCastSpeedEnabled`, `estimatedSpellDamageEnabled` | Optional stat-driven cast calculations. |
| `ocrReplayEnabled` | Changed-frame screenshot/text/parse recording. |
| `auth.autoRefresh*`, `auth.<shard>.*` | Auth schedule, profile, and encrypted session settings. |

Configuration changes are loaded into an immutable `AppRuntimeSettings` snapshot. Changes that affect composition, OCR regions, class catalogs, or parser candidates require runtime rebuild; visual overlay changes can be applied directly by the renderer.

## 7. Build, test, and run

Prerequisites: Windows 10/11, .NET 10 SDK, PowerShell, and Node.js 18+ only for catalog generation. Tesseract is optional. Windows OCR requires the corresponding Windows OCR language capability; chat phrase parsing currently expects English game messages.

From `D:\Projects\Daoc\HeraldHelperAHK`:

```powershell
dotnet --version
dotnet restore HeraldHelper.slnx
dotnet build HeraldHelper.slnx
dotnet test HeraldHelper.slnx
dotnet run --project src/HeraldHelper.Desktop/HeraldHelper.Desktop.csproj
```

Install Chromium after the first Desktop build:

```powershell
& .\src\HeraldHelper.Desktop\bin\Debug\net10.0-windows10.0.19041.0\playwright.ps1 install chromium
```

Useful focused tests:

```powershell
dotnet test tests/HeraldHelper.Tests/HeraldHelper.Tests.csproj --filter FullyQualifiedName~GameLoopOrchestratorTests
dotnet test tests/HeraldHelper.Tests/HeraldHelper.Tests.csproj --filter FullyQualifiedName~AbilitiesChatEventParserTests
dotnet test tests/HeraldHelper.Tests/HeraldHelper.Tests.csproj --filter FullyQualifiedName~DaocBitmapFontInfrastructureTests
dotnet test tests/HeraldHelper.Tests/HeraldHelper.Tests.csproj --filter FullyQualifiedName~CatalogAndMigrationIntegrityTests
```

Catalog regeneration:

```powershell
node scripts/crawl_eden_charplan.js
node scripts/crawl_blackthorn_charplan.js
```

The recent known-good baseline is 97 passing tests. Close a running Desktop instance if it locks build outputs.

## 8. Operational runbook

### First local start

1. Verify .NET 10 with `dotnet --version`.
2. Restore, build, and run the full test suite.
3. Install Playwright Chromium once.
4. Start the Desktop project and choose Eden or Blackthorn.
5. Discover/select the active DAoC character and class.
6. Select the visible chat region plus optional attribute/resistance windows.
7. Save configuration and confirm diagnostics report the intended OCR engine/profile.
8. For Eden, run the interactive auth refresh and confirm a player lookup succeeds.

### Normal field verification

1. Start with replay disabled and verify idle tick time in diagnostics.
2. Target a known friendly player, a known enemy player, a dummy/monster, and the first player again.
3. Confirm the non-player target does not blank or replace the last player.
4. Cast one adjustable spell, one fixed-time song/spell, the same spell twice, and an interrupted spell.
5. Confirm the spell browser and cast bar show the same shard/class icon and base metadata.
6. Enable dynamic cast speed and compare the displayed value with the stored character stats.
7. Enable damage estimates only after confirming acuity and item bonuses were parsed correctly.

### Capturing an OCR/parser regression

1. Enable `ocrReplayEnabled` before reproducing the issue.
2. Reproduce the smallest sequence that fails and note the wall-clock time, shard, character, class, selected windows, resolution, and UI profile.
3. Locate the matching replay folder and retain its `record.json` plus all `capture-*.png` files.
4. Compare raw image, recognized text, and serialized `parseResult` to identify capture, recognition, or parser failure.
5. Add a minimized test fixture before changing fuzzy matching or event deduplication.
6. Disable replay after collection if disk activity is not needed.

### Investigating slow target display

1. Test one cached and one uncached player separately.
2. Record timestamps for chat capture start/end, parse completion, cache lookup, HTTP start/end, and render.
3. Confirm name-only pending state is produced on the first parsed member target.
4. Verify no stats/resistance capture is delaying chat processing unexpectedly.
5. Rapidly switch target A to B and complete A's response last; B must remain current.
6. Repeat with auth already valid so login refresh is not confused with normal herald latency.

### Catalog update and rollback

1. Run the relevant crawler, never edit the live generated directory during a running application.
2. Inspect manifest version, class count, ignored classes, missing assets, and validation output.
3. Review added/removed spells and icons, especially duplicate names across classes.
4. Run parser/catalog, browser-catalog, and migration-integrity tests.
5. Launch the spell browser and manually sample multiple classes before accepting the snapshot.
6. If validation or loading fails, retain the previous generated directory and crawler output for diagnosis.

### Auth recovery

1. Confirm the selected shard and system clock are correct.
2. Use interactive auth refresh in the Desktop app.
3. Complete login in the persistent browser and wait for validation.
4. Retry one known player lookup.
5. If it still fails, inspect status/content type and cookie-name diagnostics without exposing values.
6. Do not delete the browser profile or database until a backup and reproducible failure record exist.

### Backup and recovery

1. Export JSON from the running application before schema, profile, or catalog experiments.
2. Remember that auth and target cache are intentionally absent from the export.
3. Test imports against a temporary database when changing backup schemas.
4. Preserve local auth during replace-mode import.
5. Run migration and export/import tests before shipping an `AppDataStore` change.

## 9. Test map

| Area | Primary tests | Current guarantees |
| --- | --- | --- |
| Chat parsing and cast catalogs | `AbilitiesChatEventParserTests.cs` | Phrases, merged OCR blobs, newest-target selection, enemy players, ordinals, aliases, bounded fuzzy matching, class/level resolution, icons, and fixed casts. |
| Loop state and target concurrency | `GameLoopOrchestratorTests.cs` | Non-members, last-player preservation, retries, async/stale lookup handling, cache-first display, target ordering, cast lifecycle, repeated casts, stable stats, and cancellation. |
| CC tracking | `CcImmunityTrackerTests.cs` | Registration and expiration of timers. Expand when class formulas change. |
| Character stats, metrics, replay, icons | `CharacterStatsAndReplayTests.cs` | Compact stat parsing, OCR substitutions, acuity mapping, two-second minimum, fixed casts, changed-frame replay, icon cropping, and runtime regions. |
| Bitmap glyph infrastructure | `DaocBitmapFontInfrastructureTests.cs` | TGA origin/RLE/bounds, marker atlas parsing, glyph values, row spacing, capture width, and batch diagnostics. |
| DAoC window geometry | `DaocWindowParsingTests.cs` | XML dimensions, INI fallback, marked custom windows, and chat rectangle behavior. |
| OCR engine selection | `AdaptiveOcrEngineTests.cs` | Best initial output and weak-output reevaluation. |
| Ability profiles | `AbilityProfileCatalogTests.cs`, `AppDataStoreAbilityProfileTests.cs` | Shard/class scoping, character deltas, secret-free export, stats persistence, and legacy backup import. |
| Catalog overrides | `OverrideAwareCastSpellCatalogTests.cs` | Live name/time/icon overrides and suppression of instant/no-cast metadata. |
| Generated data and migrations | `CatalogAndMigrationIntegrityTests.cs` | Shipped classes/assets, browser loading, migration 6-to-current, and shard-separated target cache. |
| Blackthorn HTTP parsing | `BlackthornHeraldClientTests.cs` | Current player-page parsing. |

Important gaps in automated coverage:

- No WPF UI automation for opening/closing the spell browser, editor, icon browser, or overlay windows.
- No full replay runner that feeds recorded image/text sequences through capture/parser/orchestrator expectations.
- No dedicated Eden HTTP contract test independent of live auth.
- No tests for target-cache expiry/eviction because no retention policy exists yet.
- Limited failure-injection coverage for catalog publish/rollback and corrupt generated data.
- No performance budget test for per-region capture or end-to-end target display.

## 10. Current feature gaps and technical debt

- First-time uncached player display can exceed one second. `SetPendingPlayerTargetLocked` creates the name-only profile, but `DesktopOverlayRenderer.IsRealPlayerTarget` suppresses it.
- All OCR windows are captured sequentially. Stats/resistance capture can delay chat parsing even though target text is more latency-sensitive.
- The dispatcher loop skips overlapping ticks. Slow OCR is safe for state but can miss short-lived repeated lines.
- Replay writes occur inline and can add disk latency when enabled.
- OCR depends on visible pixels, desktop coordinates, DPI, resolution, active UI XML, font atlas, and language.
- Cast deduplication needs more long-session replay coverage for identical casts, scrolling/reappearing text, songs, and queue-noise phrases.
- Fixed-cast behavior is inferred from catalog attributes/categories. It should become explicit versioned server/class metadata.
- Fuzzy matching remains a correctness risk because both supported catalogs contain duplicate names. Never broaden it globally.
- Damage estimates omit specialization, target resistance, variance, critical hits, buffs/debuffs, and shard-specific formulas.
- `overlayShowResist` is loaded, but no resistance text/window is rendered.
- Target cache has no TTL, size bound, clear UI, or in-memory front layer. SQLite reads occur while target state is locked.
- `MainWindow.xaml.cs`, `AppDataStore.cs`, and `GameLoopOrchestrator.cs` are large and mix responsibilities. Refactoring must follow characterization tests, not precede them.
- Browser/editor classes use Eden-specific names while serving both supported shards.
- Catalog crawlers depend on changing public frontend schemas; Blackthorn parsing is especially coupled to its deployed Next.js structure.
- SQLite migration version and backup payload version are different but not named as separate concepts in code.
- Full herald payload diagnostics are useful but can be noisy and should remain opt-in/redacted.

## 11. Immediate priorities and acceptance criteria

### P0.1: target latency and pending display

Work:

- Add structured per-stage timing for every capture region, parse, replay write, cache read, HTTP lookup, and render.
- Allow a confirmed member's name-only pending profile to render as `Name` plus a loading indicator.
- Keep non-player behavior and generation-based stale-response protection unchanged.

Done when:

- An uncached confirmed player name appears in the first render after its target event is parsed, without waiting for HTTP.
- A cached profile appears in that same tick with full cached details.
- Targeting a dummy/monster leaves the last player visible.
- Switching A to B cannot be reverted by A's late response.
- Diagnostics expose stage durations without secrets, and focused orchestrator tests cover all four cases.

### P0.2: repeated and fixed casts

Work:

- Convert real long-session replays into tests for repeated identical casts, reappearing lines, completion, interruption, and song starts.
- Make fixed/minimum cast behavior explicit in catalog/profile metadata where possible.

Done when:

- One continuously visible start line starts exactly one bar.
- A genuine second occurrence of the same spell starts a second bar.
- Completion/interruption prevents an old visible line from restarting the bar.
- Minstrel speed metadata produces a fixed three-second bar on the intended shards/classes.
- Adjustable casts never calculate below two seconds unless the catalog base time itself is shorter.

### P0.3: replay regression runner

Work:

- Define a stable replay fixture format derived from `record.json`.
- Add a headless runner that compares expected OCR/parser events across ordered frames.
- Keep large private gameplay screenshots outside normal source unless sanitized/minimized.

Done when:

- A developer can run one command against a replay fixture and receive a deterministic event diff.
- Parser fixes include a fixture that failed before the fix.
- Replays can validate repeated events across frames, not only a single text snapshot.

### P1: data and persistence reliability

- Add an in-memory target-cache front layer, explicit retention policy, diagnostics, and asynchronous SQLite writes.
- Add catalog update previews for class/spell/icon additions, removals, conflicts, and schema changes.
- Separate `DatabaseMigrationVersion` from `BackupFormatVersion` and add real old-database fixtures.
- Add publish/rollback failure-injection tests and catalog schema validation before replacement.
- Implement a real resistance overlay or remove/disable its misleading toggle until supported.

### P2: maintainability

- Extract runtime/settings/character/profile controllers from `MainWindow`.
- Split `AppDataStore` into settings, auth, backup, ability profile, catalog override, stats, and target-cache repositories behind interfaces.
- Split loop capture/parse processing from target-resolution state only after stage timings and characterization tests exist.
- Rename shared browser/editor classes away from Eden-specific names.
- Add CI for clean build, all tests, generated manifest validation, and secret-pattern scanning.

## 12. Open questions for the project owner

1. For a newly selected uncached player, should `Name / loading` immediately replace the previous profile, or should the previous full profile remain until lookup completes?
2. Is the three-second fixed rule limited to Minstrel speed songs, or should every instrument pulse/song use fixed catalog timing on Eden and Blackthorn?
3. Should damage remain a clearly labeled estimate, or is server-accurate simulation a real goal? If accurate, which formulas and combat variables are authoritative?
4. Are Phoenix, Titan, and Celestius active support targets, or should development focus exclusively on Eden and Blackthorn?
5. Should cached player profiles expire by age, be size-bounded, have a manual clear action, or remain indefinitely?
6. Must arbitrary modular DAoC UI fonts/windows work, or only known chat, attribute, and resistance layouts?
7. Is English the only supported game-client language?
8. Should a dedicated resistance overlay be implemented now, or should the current toggle be removed until there is a defined design?
9. Confirm that Mauler remains excluded only from Eden and is accepted on shards that actually provide it.
10. Should authentication remain machine/account-local through DPAPI with no portable secret export?
11. What end-to-end target-display latency is acceptable on the actual gaming machine for cached and uncached targets?
12. Confirm that `D:\Projects\Daoc\HeraldHelperAHK` is the canonical development directory; it currently does not expose Git worktree metadata, so the expected source-control workflow is unclear.
