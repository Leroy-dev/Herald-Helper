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
- `ServerCastSpellCatalog` serves the shards without a charplan catalog
  (Default/Phoenix/Titan/Celestius + offline): `data/client-tables/server-spells.csv`
  is the deployed `Spell` table export (4,152 rows — cast time, recast
  ms, damage, icons; `RecastDelay` is in **milliseconds** and short
  spell recasts only — RA cooldowns are hardcoded in handlers, not in
  this table). `ra-cooldowns.csv` fills that gap: 42 `GetReUseDelay`
  values mined from `realmabilities_atlasOF` handlers joined to
  `Ability.Name` (Purge/Vanish 1800s, Battle Yell/Grapple 900s).
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
