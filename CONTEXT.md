# HeraldHelper Context

The in-game helper domain: watching DAoC chat and screen regions, resolving targets against herald data, and projecting an overlay. One context covering the runtime loop, persistence, and catalog upkeep.

## Language

**Runtime loop**:
The repeating cycle that captures screen regions, parses them, tracks events, and renders the overlay; runs on a timer or as a single manual tick.
_Avoid_: game loop, main loop

**Frame**:
One pass through the runtime loop: capture, stats observation, parse, cast/target/ability event tracking, render.
_Avoid_: tick result, iteration

**Overlay snapshot**:
The immutable picture produced by one frame: current target, active CC timers, active cast, raw OCR text. Everything the overlay draws comes from it.
_Avoid_: frame state, render model

**Watch region**:
A named screen rectangle the loop OCRs each frame (chat, character stats, custom windows).
_Avoid_: capture area, hot zone

**Target profile**:
The resolved herald record for the player under the crosshair: class, guild, level, realm rank, solo kills.
_Avoid_: player info, character record

**CC timer**:
An active crowd-control countdown derived from an observed ability hit, adjusted for target class and resists.
_Avoid_: debuff, effect

**Cast state**:
The in-progress enemy spellcast tracked between cast-start and completed/interrupted events, enriched with catalog cast-time and damage data.
_Avoid_: channel, spell bar

**Character stats snapshot**:
The player's own stats (dexterity, casting speed, spell damage) read from the stats watch region and stabilized across consecutive frames before being trusted.
_Avoid_: attributes, sheet

**Shard**:
A ruleset/server family (Eden, Blackthorn, default) that picks which herald client, catalogs, and settings keys apply.
_Avoid_: realm, server

**Character settings key**:
The canonical settings identifier for per-character, per-shard values (ability profile class, OCR windows, OCR stats, character level); built by normalizing names to lowercase segments.
_Avoid_: config name, preference id

**Catalog**:
A downloaded dataset (Eden or Blackthorn) of classes, skills, realm abilities, and icons used for ability editing and cast metadata.
_Avoid_: database, dataset

**Catalog update transaction**:
The backup, crawl, validate, rollback, cleanup sequence that replaces catalog snapshots only when every catalog validates; preview runs the same transaction then restores.
_Avoid_: download, sync

**Replay fixture**:
A recorded sequence of frames (OCR text + parse results + expected overlay) replayed through the loop to diff behavior without the game.
_Avoid_: recording, test script
