# Client DLLs: eden.dll / Blackthorn

What each server DLL provides and where HeraldHelper consumes it.

## Spell catalogs (the main integration surface)

- `EdenCastSpellCatalog` / `BlackthornCastSpellCatalog` — server-side
  spell lists used for recast times, auto-suggest, and ability profiles.
- **Instant-cast entries must be kept**: both catalogs previously dropped
  entries with `castTime <= 0` — instant abilities can still carry recast
  delay (e.g. styles/spells with `recast`/`cooldown` fields). Rule:
  retain an entry when it has *any* useful catalog data even if cast time
  is zero or absent.
- Blackthorn entries may lack cast-time entirely — accept entries that
  are otherwise useful.
- Catalog updates crawl in-process: `HttpCatalogCrawler` →
  `EdenCharplanCrawl` / `BlackthornCharplanCrawl` (C# ports of the old
  `scripts/crawl_*.js`), staged writes `<catalog>.staging-<pid>` then
  atomic swap into `data/`.

## Headers

- `edenHeaders.json` / `edenHeaders.js` were scrubbed from git history
  and are git-ignored — do not recommit.
- Client packets/headers differ per server build; the spell catalog is
  the stable interface, not wire formats.

## Compatibility profiles (DaocUI side)

- `eden-nf` / `blackthorn-of` compatibility profiles drive the build
  pipeline's `CompatibilityReferenceValidator` and runtime catalogs —
  server-specific UI deltas are captured in the profiles, not hardcoded.
