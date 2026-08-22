# Broadloom probe settlement

Date: 2026-08-22
Branch: `agent/broadloom-probe` (worktree `sandbox/wt-broadloom`)
Scope: settles the playtest hypotheses left open by
`ARC-RELAY-BROADLOOM-37X27-CANDIDATE.md` with deterministic matches on the
real engine. Probe-only: nothing here promotes the map, the width cap, or
any doctrine.

## What ran

- **Platform**: the disclosed bounded cap change, implemented on this branch
  only — `ArenaMap.MaxWidth/MaxHeight 37` inside a hard `MaxCells = 1024`
  budget, enforced in both validators and pinned by updated map tests
  (32 map + 198 Arc Relay engine tests green).
- **Map**: `broadloom-probe` loop profile = the complete Tapline-14 ruleset
  on the byte-exact Broadloom terrain, registered for `--loop-profile`.
  (Tapline-15's two deltas — allied-projectile blocking and 50% XP death
  retention — are uncommitted work in another live workstream and are
  excluded; both are fight-micro/power-curve rules, neutral to the income
  questions settled here.)
- **Doctrines**: three archetypes, one shared eight-class composition.
  Authored twice: first as raw Orders v2 sheets, then — per the owner ruling
  that ground is authored with **posts and patrol only** — as
  `arc-relay-tactics-v2` documents compiled through the real materializer
  (`experiment arc-relay-playbook --tactics`), so the battery also exercises
  the player surface end to end.
  - *ball*: all eight massed on the centre plaza.
  - *trade*: two custody posts on the own Well, a raid element on the enemy
    Well, four guns posted on the west-mouth line.
  - *turtle*: eight in the home quarter (well garrison + keep posts).
- **Battery**: 96 deterministic matches (6 seeds × both orientations × 8
  pairings), stats extracted from replay events (banks by own/centre/enemy
  class, pulses, carrier deaths), motion-sanity sweeps on the decisive
  pairing. Harness: `docs/map-candidates/broadloom-37x27/probe/probe.py`.

## Results

| Pairing (12 matches each) | Record | Decisive economics |
| --- | --- | --- |
| ball vs trade (orders) | ball **11–1** | trade raids: 4 banks vs **50 carrier deaths** |
| ball vs trade-v2 (orders) | ball 9–3 | raids 4 banks vs 29 deaths |
| **ball vs trade (tactics)** | **trade 8–4** | raids **11 banks vs 2 deaths**; ball's own-Well banks collapse 26 → 10 |
| ball vs turtle (both stacks) | ball 12–0, 12–0 | turtle drips 6 own/match; ball farms 5 centre/match uncontested |
| trade vs turtle | trade 12–0 (orders), 7–2–3 (tactics) | posted turtles resist splits better than masses, still lose |

Motion audit of the decisive tactics pairing: the trade side is CLEAN in
all 12 games; the ball side shows local move orbits (blocked carriers and
screens in its own traffic, mostly at the reactor mouth) in **6 of 12**.
Restricted to the six movement-clean games the ball leads 4–2.

## Settled verdicts

1. **An eight-body deathball leaves profitable counterplay — and the map's
   own named doctrine is what beats it.** Authored through the player
   surface, custody + posted mid-line + patrol raid defeats the mass 8–4,
   with blowouts (18–2, 15–2) whenever the ball's congestion bites. The
   caveat is disclosed rather than hidden: the ball's stalls flow through
   the known carrier-traffic executor seam, so the margin is an upper
   bound; on clean-movement games the matchup is near parity with a ball
   edge. Either way the strategic claim holds — the split is competitive,
   the mass is not dominant, and massing eight bodies has a measurable
   congestion cost on this geometry.
2. **Raids are viable as motion, dead as siege.** The same raid target
   produced a 7% return rate for a parked converge (the respawn-adjacent
   quarter kills the return leg, and every dropped core subsidizes the
   defender) and an **85% bank rate (11:2)** for a closed patrol loop over
   the roof that snatches with `coreRule: hunt` and keeps moving — while
   simultaneously cutting the defender's own drip from 26 banks to 10.
   The roof flank is the raid artery in both directions, as designed.
3. **Heal-home turtling is not viable.** 0–24 against the ball across both
   authoring stacks. The income floor is real (near-lossless 6 own
   banks/match, games stretched to t440+) but conceding centre loses ~2:1
   on charge every time. The home-quarter heals do not create a fortress.
4. **The 8-step own-Well run is safe by default, deniable by commitment.**
   Uncontested it is near-lossless; a committed camp intercepted the ball's
   own carriers 20 times in one battery, and the tactics custody/patrol
   machine cut the mass's own income by 60%. That is the intended dial,
   now measured rather than assumed.
5. **The 3-step workshop bias cannot confer ownership.** 15 vs 18 steps is
   a ≤4-tick first-arrival edge at standard cadence (18.75t vs 22.5t cold;
   12.5t for both sides from centre). Auto-ownership would require a
   training channel shorter than ~8 ticks — an order of magnitude under
   every existing channel scale (heal 3t/hp, Hardlight 30t). Recorded as a
   design constraint on future workshop rules: any channel ≥ ~10 ticks
   keeps both workshops contestable. This is the strongest statement that
   can exist while workshop rules themselves are unimplemented.
6. **Authorability holds on 37×27.** Every doctrine is expressed in
   `arc-relay-tactics-v2` with posts and patrol only, compiles through the
   real materializer, and executes on the real mind. The mind's geometry
   gate rejected four of my own authoring mistakes (wall-facing posts, a
   post on a pillar) before any match ran — the gate works at the new width.

## Fixes the probe forced (all on this branch)

- `ArenaMap` bounded widening + cell budget, with contract tests.
- `BroadloomCross` geometry, spawn anchors, `broadloom-probe` profile.
- Smart Rail now names the nearest struck enemy as its lock target: the
  committed heading-only cast faulted every match that fired a materialized
  rail under this runtime contract (the same seam another workstream is
  relaxing from the contract side on main; the two fixes are compatible).
- Probe harness + tactics documents under
  `docs/map-candidates/broadloom-37x27/probe/`.

## Evidence boundary

Two doctrines per archetype, one mind, six seeds per orientation. These
matches settle existence and mechanism — counterplay exists, raids-in-motion
work, turtling fails, the biases are bounded — not population balance or
metagame optimality. The ball-congestion margin is entangled with the
carrier-traffic executor seam and shrinks if that seam is fixed; the
directional verdicts do not depend on it.
