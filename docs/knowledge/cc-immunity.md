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

## Handler-level duration rules (spells/CCSpellHandler.cs)

- `StyleStun.CalculateSpellResistChance` → 0 and `CalculateEffectDuration`
  ignores `eProperty.StunDurationReduction` — **melee stuns land at full
  listed duration, no det, no resists** on OpenDAoC. Only NPC-immunity
  halving shortens them.
- `StunSpellHandler`/`MesmerizeSpellHandler`/`SpeedDecreaseSpellHandler`
  multiply `StunDurationReduction`/`MesmerizeDurationReduction`/
  `SpeedDecreaseDurationReduction` — **det shortens casted stun, mez and
  snare alike** ("mez ignores det" was wrong). Det = RA property,
  15 %/level (NF max 5 = 75 %, OF max 3 = 45 %); Stoicism adds −25 %.
- Casted duration also scales by to-hit chance (`87.5 + (spellLevel −
  targetLevel)/2 + toHitBonus`): <55 % shrinks duration, >100 % grows it
  (cap 4×). Resist itself is binary (`100 − hitChance`).
- `StyleStun.OnEffectExpires` returns `Spell.Duration × 5` citing
  camelotherald/more/1749 — the live-era rule: **style-stun immunity =
  5× the stun** (Slam 9 s → 45 s). In ECS mode the flat-60 property
  overrides it; the ×5 documents the classic rule Eden follows.
- Pet stuns: `Spell.ResurrectHealth` scales immunity (pet stun immunity
  is ~5× too).

## Tracker model now

- **Eden**: melee = `applied + applied × 5` (live rule; det shortens
  the applied stun), casted = `effective + 60`, nearsight =
  `resist-scaled + 60`, DamageSpeedDecrease = duration-only.
- **Other shards (OpenDAoC-verified)**: melee = `full duration + 60`
  (styles ignore det/resists → Slam 9 s = 69 s), casted =
  `dur × det + 60` (det applies to stun/mez/snare), nearsight =
  `resist-scaled + 60`, DamageSpeedDecrease = duration-only.
- `shard` flows TickAsync → RegisterSuccessfulHit → PreviewImmunitySeconds.

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

## Server-emitted reject/immune strings (parser coverage verified)

`AbstractCCSpellHandler`/`StunSpellHandler`/`MesmerizeSpellHandler`/
`SpeedDecreaseSpellHandler`/`HereticSpeedDecrease` emit exactly:

| Message | Parser handling |
|---------|-----------------|
| `Your target is immune to this effect!` | ImmuneMarkers ✓ |
| `{name} resists the effect! (X%)` (caster-side) | ResistTargetRegex ✓ |
| `You resist the effect!` (target-side) | self-side only — no timer |
| `Your target is enraged and resists the spell!` | ImmuneMarkers ✓ |
| `Your target is moving too fast for this spell…` | ImmuneMarkers (charge/sprint) ✓ |
| `{name} is moving to fast for this spell…` | FailedApplicationNamed — **server typo "to fast" kept** (regex allows to[o]?) ✓ |
| `Ceremonial Bracer intercept your mez!` / `Your item effect intercepts …` | ImmuneMarkers ✓ |
| `Your spell has no effect on the {name}.` | SpellNoEffectRegex ✓ |

## OpenDAoC message strings

- Self-CC markers exist for stun, mezz, root, snare, nearsight — plus
  pet resists, no-effect, and cast-reject phrases (commit `d00ab48`).
- Songs apply on *begin-playing*, not per-pulse; failed applications are
  suppressed (commit `8cd9874`).
- **Message coverage is complete** against the deployed `Spell` table:
  Message1 (self-apply) and Message3 (self-expire) strings for all
  CC-producing types are in `SelfCcMarkers`/`SelfCcExpireMarkers`. The
  only intentionally-skipped strings are `CombatSpeedDebuff`/`StyleCombatSpeedDebuff`
  ("attacks are being slowed by an invisible force" / "attacks return to
  normal") — `EffectHelper` maps them to `MeleeHasteDebuff`, an attack-
  speed debuff with NO immunity, not crowd control.
- **Message2/4 broadcasts** parse as `BroadcastCcEvent` — third-person
  lines for NEARBY players: "{name} is stunned/entranced/mesmerized/
  surrounded by constricting bonds/cannot seem to move/'s feet are
  frozen", "rocks rise and trip {name}", "{name} stumbles, unable to see",
  "{name} begins moving more slowly", plus expire forms. The orchestrator
  joins them onto group members (adapter `group_nameN`) — the group
  frame shows a `·S/·M/·R/·Sn/·NS` badge for ≤12 s (broadcast lines carry
  no duration). Enemy names also parse but only badge if they match a
  group member — no member list, no display.
- **Group icons carry CC state too** — `group_%dicon%d` adapters expose each
  member's buff strip (~30 slots). `CcIconIndex` maps icon id → effect from
  `server-spells.csv` (both `Icon` and `ClientEffect` columns; icons shared
  with non-CC spells are dropped — e.g. pet summons reusing a snare glyph).
  Badges show for the effect's whole visible duration at any range, no chat
  needed; broadcast lines still win ties as the fresher signal.
- **Broadcast apply on the current target synthesizes a stun timer** —
  "another player stunned your target" now starts a real immunity entry
  without your cast line ever being seen. Stun only: its duration band is
  tight (3–11 s in the deployed table → 11 s guess → ~71 s immunity
  window, errs on the READY-late/safe side), while mez/root/snare span
  5–80 s and a wrong guess is worse than no timer. Skipped when a fresh
  stun timer already exists (dedupes your own hit's echo).

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
