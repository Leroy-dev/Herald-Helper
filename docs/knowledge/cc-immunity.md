# CC / Immunity Findings (Eden + OpenDAoC)

What the client actually prints, how timers are derived, and the edge
cases that broke attribution. `AbilitiesChatEventParser` +
`CcImmunityTracker` implement these rules; tests in
`CcImmunityTrackerTests`/`OverlayTimerLineTests` pin them.

## Immunity formulas (server-source-verified)

OpenDAoC `GameServer/ECS-Effects/CrowdControlECSEffect.cs` — every hard-CC
effect computes one `ImmunityDuration` at creation:

```
adaptive (property OFF by default): min(60000, appliedDuration * adaptive_length /*=6*/)
flat    (deployed default):         immunity_timer_flat_length /*=60*/ * 1000
```

The deployed `runtime/data/opendaoc.sqlite3.db` `serverproperty` table
confirms `immunity_timer_use_adaptive=False`, `flat=60`, `adaptive=6` —
immunity starts when the CC ends, so **all** hard CC gets flat 60 s
post-effect on this server (Slam 9 s → 69 s total, not 63 s).

`EffectHelper.GetImmunityEffectFromSpell` — the authoritative
spell→immunity map:

| SpellType | Immunity |
|-----------|----------|
| Mesmerize | MezImmunity |
| **Stun + StyleStun** | **StunImmunity (one shared bucket)** |
| SpeedDecrease + UnbreakableSpeedDecrease | SnareImmunity |
| Nearsight | NearsightImmunity |
| DamageSpeedDecrease | (absent → **no immunity**) |

Other findings: summoned-pet stuns set `TriggersImmunity=false`;
`UnresistableStunSpellHandler` skips immunity; NPCs get halving
diminishing returns (`NpcImmunityEffect`), not the player flat timer.

Tracker model now: Eden keeps the user-measured `6 × applied stun`
melee immunity; all other shards use the server-verified flat 60 s
(`applied + 60`). Casted CC is `effective + 60` on both — identical
under flat and under adaptive for any duration ≥10 s. Nearsight now
tracks `resist-scaled duration + 60 s immunity` (it has a real immunity
effect); DamageSpeedDecrease stays debuff-duration-only. `shard` flows
TickAsync → RegisterSuccessfulHit → PreviewImmunitySeconds.

- `skill_code` in hand-edited ability rows ('s'/'m' = spell vs melee
  line) is a trigger label only — never drive immunity math or rendering
  from it.
- Confusion and amnesia are intentionally ignored.

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
