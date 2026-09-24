# CC / Immunity Findings (Eden + OpenDAoC)

What the client actually prints, how timers are derived, and the edge
cases that broke attribution. `AbilitiesChatEventParser` +
`CcImmunityTracker` implement these rules; tests in
`CcImmunityTrackerTests`/`OverlayTimerLineTests` pin them.

## Immunity formulas (user-confirmed)

| CC source | Immunity |
|-----------|----------|
| Casted (spell) CC | `60 s + actual CC duration` |
| Style/melee CC (e.g. Slam) | `6 × actual stun duration` |

- Root and snare **share one immunity category** (same `ControlEffectType`
  bucket).
- Damage spells that *include* a snare component do **not** apply an
  immunity timer — flagged non-immunizing on the hit/profile.
- Nearsight is a debuff, not CC — its tracked duration is **not**
  multiplied through the CC immunity math.
- Confusion and amnesia are intentionally ignored.
- Slam cap/rounding vs the 60 s casted floor: open question — currently
  follows `6 × duration` with no cap clamp.
- `skill_code` in hand-edited ability rows ('s'/'m' = spell vs melee
  line) is a trigger label only — never drive immunity math or rendering
  from it.

## Target attribution

- The parser resolves each ability-name mention against a `you target X`
  mention ≤180 chars earlier in the same text, else falls back to
  `fallbackTargetName` (orchestrator passes `_currentTargetName`, then
  the live `summary_target` adapter via `IAdapterValueSource` — needed
  for incremental sources and mid-fight loop starts).
- Melee/style stuns were previously lost because the hit carried no
  casted-vs-style context → attributed to the stale target or dropped.
  `AbilityHit` now carries the distinction.
- Effect-expire messages are **unreliable for attribution**: they only
  render when near the target. Don't build timers off them.

## OpenDAoC message strings

- Self-CC markers exist for stun, mezz, root, snare, nearsight — plus
  pet resists, no-effect, and cast-reject phrases (commit `d00ab48`).
- Songs apply on *begin-playing*, not per-pulse; failed applications are
  suppressed (commit `8cd9874`).

## Cooldowns vs CC timers

- Recast data lives in the Eden/Blackthorn spell catalogs; instant-cast
  entries with recast delay are real cooldown sources (don't discard
  `castTime <= 0` entries).
- Cooldowns render in a **separate overlay window** — independent
  setting (`overlayShowCooldowns`), position, size, and font. They are
  not mixed into the CC-timer list.
- Icons are preserved on both timers and cooldowns.

## Catalog auto-suggest

- Ability profile editing auto-suggests from the catalogs; Nearsight
  aliases normalize (`AbilityProfileController`).
