# Rules 0.3 candidate slate — design round

Written BEFORE implementation (2026-07-23). GAME-DESIGN.md holds the evidence
base and verdicts; this file holds the deliberation: what goes in the slate,
exactly how each piece works, what could go wrong, and how we'll judge it.

## Evidence recap (what we're fixing)

Three tournaments, one meta: **defense is structurally dominant.** Every
champion is a fortress; the three draw modes are never-find-each-other,
find-but-won't-commit (fortress math), and mutual kills. Root causes in the
BASICS, ranked by indictment strength:

1. **Unlimited shot range** — infinite information-free lane denial is the
   backbone of every champion (Rampart: 27+ blind shots/game down one row).
2. **The turn tax** — approaching a facing defender donates a free hit during
   rotation; all gen-2/3 agents independently derived "never enter a lane vs
   a loaded gun".
3. **One early hit converts to turtle-and-win** at the MaxTicks health
   tiebreak.

Energy taught us the shape of a failed fix: a symmetric tax on ACTION punishes
attackers as much as campers. 0.3 instead removes defender privileges and adds
a reason to leave home.

## The slate

### A. Shot range cap — `ShotRange = 8` (0 = unlimited, all older versions)

Ray stops at min(first wall/bot, 8 tiles). Rays are axis-aligned so "tiles" is
unambiguous. 8 > vision 6 keeps the "shots outrange sight" information game
(muzzle-flash localization stays a skill) while ending cross-map suppression:
a fortress controls its neighborhood, not a map-length corridor. Sieges can
stage at distance 9+ and CHOOSE their entry tick. No SDK/protocol change; the
replay's Shot toX/toY simply stops sooner; viewer unchanged.

### B. Strafe — `AllowStrafe = true`; new actions StrafeLeft/StrafeRight

Move one tile perpendicular to facing (left/right of the facing vector)
WITHOUT rotating. Movement resolution identical to MoveForward (same-target
both fail, swaps fail, blocked → Wait/Blocked). This kills the turn tax at its
root: an attacker can cross lanes and close distance with the gun already
trained (e.g. face West, strafe north row by row — arriving pre-aimed), so the
defender's free-hit-during-rotation disappears.

**Known risk, deliberately accepted for measurement:** strafe also makes
DODGING cheaper (step off-lane and back without losing aim), which could
deepen standoffs instead of breaking them. This is exactly the kind of
second-order effect theory can't settle — the harness and, if needed, a gen-4
agent decide. If strafe measures draw-positive it ships disabled (energy
precedent).

Compat: guest reply "A 5/6"; hosts always run current code and gate by rules —
under strafe-less rules a strafe validates to Wait with ActionResult.Blocked
(documented; never a fault — a 0.3-built bot must degrade gracefully on 0.2
servers). Old artifacts never emit 5/6. Wire format unchanged → protocol stays
0.1; SDK 0.4.0 adds Actions.StrafeLeft/StrafeRight.

### C. Zone control (king of the hill) — `ZoneControl = true`

- **Zone tiles come from the map** (`"zone": [[x,y],…]` in map JSON, optional;
  fallback = floor tiles of the center 3×3). Checked reality: arena-01's
  computed center is walled — both shipped maps get declared zones and a map
  version bump (v2). Validation: zone tiles must be floor, connected to
  spawns.
- **Scoring:** each tick, every ACTIVE bot standing on a zone tile at end of
  tick accrues +1 zone-tick (both can accrue simultaneously — contested is
  contested).
- **Domination:** `ZoneDominationTicks = 150` — at completion check, an active
  bot at ≥150 zone-ticks wins immediately (`MatchEndReason.Domination`).
  If both cross simultaneously: higher total wins; equal totals → play on
  (they resolve at MaxTicks).
- **Tiebreak chain at MaxTicks becomes zone-first:** zone-ticks → health →
  damage → draw. Zone-first is the anti-entrenchment teeth: a bot that lands
  one hit and turtles in a corner LOSES to the bot on the hill. Health-first
  would preserve exactly the pathology we're removing.
- **Observability (bots must be able to play the objective):** observation
  gains trailing sections — map dimensions (`M w h`, agents currently can't
  know them!), zone tiles (`Z n x:y…`), and both scores (`ZT mine theirs` —
  the score is public, like any sport). All trailing-optional: protocol-0.1
  parsers ignore them (Slot/Energy precedent). SDK: MapWidth/MapHeight,
  ZoneTiles, MyZoneTicks/EnemyZoneTicks (null when disabled).
- Replay: header gains optional `zoneTiles` (viewer draws the zone),
  BotMatchResult gains optional `zoneTicks`; both omitted for older rules —
  existing replay hashes unaffected (nullable-field precedent).
- **Evaluation caveat, honestly:** zone-ignorant champions can't test zone
  play (energy lesson). BUT unlike energy, the mechanic is harmless to
  ignorant bots (pure extra win path; their games play out identically until
  the tiebreak). Harness measures range/strafe properly; zone's real test is
  a gen-4 tournament under 0.3. Ship-shape: zone can ship on "does no harm +
  design conviction" with gen-4 as the confirmation, or wait for gen-4 —
  decide on the data.

### D. Spawn lane safety — `SpawnLaneSafety = true`

Gen-3 filed wart: seed spawns can start a bot inside the opponent's firing
lane (tick-0 hit before its first decision). Under 0.3, SpawnVariation rejects
candidate pairs sharing a clear row/column within ShotRange. Gated by flag —
0.2 spawn streams must stay bit-identical (goldens).

### E. Deliberate non-changes

- **HP stays 3** — one-hit entrenchment is better attacked by zone-first
  tiebreaks; 5 HP would lengthen games and confound the A/B. Separate arm
  someday if needed.
- **MaxTicks stays 500** — domination ends held games early anyway; changing
  it now adds a confound. Revisit after 0.3 data.
- **Cooldown/vision/damage unchanged.** One meta-shift at a time is already a
  lot.
- **Energy stays off** (closed as-tuned, DECISIONS #48).

### F. Content: one new map

`crossfire-01` (16×12): broken sightlines (no full-length lanes — the maps
amplify the range pathology), open declared zone, seed-spawn friendly. Joins
the ranked pool and the harness arms. Map content is rules-independent.

## Versioning matrix

| Axis | Change |
| --- | --- |
| GameRules | V0_3 = V0_2 + ShotRange 8, AllowStrafe, ZoneControl(+150), SpawnLaneSafety; `Current` flips ONLY on a ship verdict |
| CLI --rules / BOTARENA_RULES | `0.3` full slate + isolation arms `range`, `strafe`, `hill` (each = 0.2 + one feature, exp version strings) |
| SDK | 0.4.0 (strafe actions, map dims, zone fields) |
| GuestAdapter | 0.4.0 (cache invalidation on rebuild) |
| Runtime protocol | stays 0.1 (trailing-optional observation sections; action range growth is rules-gated host-side) |
| Replay format | stays 1 (optional zoneTiles/zoneTicks, omitted when off) |
| Maps | basic-01/arena-01 → v2 (declared zones); crossfire-01 v1 |

## Evaluation plan

1. Unit tests: ray cap; strafe targets/conflicts/gating-to-Blocked; zone
   accrual/domination/simultaneous-cross/tiebreak order; spawn lane safety;
   map zone parsing/validation/fallback; and REGRESSION: 0.1/0.2 goldens,
   spawn streams, and result semantics bit-unchanged.
2. `balance-eval` arms: `0.2` (control), `range`, `strafe`, `hill`, `0.3` —
   population: both champions + Metronome + Oracle/Switchblade artifacts
   (5 bots, 10 pairings, 60 games/arm, 300 games). Fixed seeds.
3. Ship rule (methodology): an arm ships if draws drop and games shorten
   without archetype collapse. Expected: range and hill help, strafe is the
   open question. `Current` flips to whatever subset survives; survivors get
   docs (site rules card, template README, skill brief), DECISIONS entry with
   the table, and a gen-4 tournament as confirmation.

## Failure modes being watched

- Strafe-powered oscillation dodging → draws UP (measured; drop the flag).
- Range 8 turns fortresses into brawlers so hard that pure aggression
  dominates (watch elim share by archetype; range 10 as fallback).
- Zone camping = fortress-on-the-hill meta (acceptable — it's a CONTESTED
  fortress by construction; watch domination vs tiebreak ratios).
- Two zone-aware bots parked adjacent to the zone refusing to enter (the
  chicken variant) — zone-first tiebreak means SOMEONE must step in; watch
  for late-entry stalling in gen-4 replays.
