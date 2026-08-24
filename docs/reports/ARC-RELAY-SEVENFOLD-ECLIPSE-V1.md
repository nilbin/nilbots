# Sevenfold Eclipse — Broadloom ladder rung 7

`sevenfold-eclipse-v1` is admitted above all six prior Broadloom-7 ladder
sheets. It wins every fixed six-game mirrored series, uses eight different
classes, and exposes a more important result than the rung itself: moving one
home defender into centre turns a competitive 4–2 top sheet into a 36–0
sweep of the prior ladder.

These are fixed-set records, not a population-balance conclusion.

## Doctrine

Sevenfold retains Eclipse's roster, custody and signature geometry but changes
the allocation:

- Kestrel, Relay and Mortar form the north battery.
- Repulsor, Patchbay, Hush and Minesmith form a four-body south mine arm.
- One Nest alone guards and harvests the own Well.

The centre seven close aggressively with heal-tile recovery. Minesmith moves
its two hidden nodes from staging ground into the centre fight; Mortar keeps
the two plotted counter-formation impacts; Relay remains the preferred Core
carrier. Nest's sentry and ordinary Hold behavior are enough to harvest the
uncontested home supply against every existing ladder opponent.

Roster: `Kestrel · Relay · Mortar · Repulsor · Patchbay · Hush · Nest · Minesmith`.

## Admission record

Profile: `broadloom-7`. Cells, always in this order:
`w-9001 w-9002 e-9005 e-9009 w-9012 e-9004`.

| Opponent | Series | Per-cell Sevenfold outcomes |
|---|---:|---|
| Loomsplit | **6–0** | W253, W360, W263, W271, W273, W267 |
| Loomshears | **6–0** | W265, W275, W295, W261, W292, W273 |
| Crosscut | **6–0** | W263, W360, W257, W265, W262, W531 |
| Sixfold | **6–0** | W245, W283, W265, W373, W355, W257 |
| Shrouded Lattice | **6–0** | W246, W355, W262, W340, W353, W280 |
| Eclipse Lattice | **6–0** | W372, W265, W353, W272, W253, W270 |

All 36 replays passed movement-sanity and felt-degeneracy eligibility. Thirty-
five conformance reports contain no deviation. One contains only the known
Hold-checker seam for the lone Nest: a one-tile combat excursion. Five reports
carry notes from that same checker. No post, phase, custody, delivery or
core-rule promise deviated.

## What the iteration proved

Several plausible “deeper” counters did not beat Eclipse:

- **Slipweave** moved off Eclipse's plotted impact crosses but ceded too much
  centre tempo and lost both focused cells.
- **Parallax** kept Eclipse's roster/artillery while using the offset formation
  and still lost both cells at tick 343.
- **Twin-Star**, **Binary Eclipse**, **Umbra** and **Painted Eclipse** tested a
  duplicate Mortar, Palisade screening and Sunder focus. Each only produced a
  1–1 seat split.
- **Razor Eclipse** made the centre builds damage-first. It also split 1–1;
  veterancy paths were secondary to allocation.
- **Afterlight Raid** tried a real phase pivot: after the first bank, send
  three bodies to the enemy Well while three held centre. It lost both cells,
  but the execution was not a clean balance test because current Core intent
  repeatedly pulled the raiders back toward centre.

The first clean edge was simply seven centre bodies and one home body. That
candidate then swept all 36 admission games.

## Friction and missing vocabulary

### Enemy-Well raid intent is not expressible cleanly

`coreRule` is tactic-group-wide and only offers `ignore`, `grab` and `hunt`.
Afterlight needed:

> Ignore centre Cores while travelling; once near the enemy Well, claim only
> that Well's Core and escort it home.

With `hunt`, the raid group repeatedly diverted or respawned into centre-Core
work. With `ignore`, it could never claim the raid Core. The vocabulary needs
an ordinary phase-local Core policy with optional `well` and `coreBankValue`
filters, compiled into existing Instructions. This should not introduce a new
scheduler or custody system.

The conformance projection also presents pickup-phase custody restoration as
brief phase `state-change` windows, which makes a multi-phase raid harder to
read even though custody binding itself is intentional.

### Geometry validation failed again

The direct materializer accepted a post at Broadloom wall `(14,11)`; runtime
then faulted both focused matches at tick zero. This is the second ladder rung
to hit the same problem. The CLI path must call the hosted save gate's real-map
validation before emitting a playable package.

## Gameplay/depth verdict

Sevenfold is a valid sheet and a valid ladder admission, but its sweep is a
balance warning rather than evidence that the strategy space is healthy.

- Friendly-shot blocking, mines, artillery and signature counter-picks did not
  make seven-body centre density self-punishing.
- A lone Nest safely converts the own-Well supply because the current ladder
  applies almost no enemy-Well pressure.
- Workshops and outer routes are still too slow or too weak to punish the body
  deficit at centre before several high-value centre conversions.
- A seven-class roster floor or one-duplicate cap controls composition, not
  spatial concentration; it cannot solve this macro problem.

The next ladder counter should deliberately pressure Sevenfold's lone Nest and
steal/deny its 4-value enemy-Well supply. Before that can be authored honestly,
phase-local Well-filtered Core intent is the highest-leverage vocabulary fix.
If a clean raid still cannot compete after that, map distance, Workshop reward
and/or centre-Core tempo need direct tuning. A hard cap on bodies in centre
would hide the issue rather than create counterplay.

## Reproducible artifacts

- Tactics source:
  `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/sevenfold-eclipse-v1.json`
- Tactics SHA-256:
  `77ab0e8891ec758afa30407356a1bb7c013156213cdad1dcec315bc95372dd33`
- Materialized Orders SHA-256:
  `c51a2d6607ce3a02ef6953b961b5ef69c4fe52643625cc907b8d365ba260a903`
- Map-plan SHA-256:
  `6ed79bce975f8e64f8d7668f3a4244f25810816d39589b1e294c4a084b3646a3`
- Linked package/content hash:
  `4fcb43d281ee9ba155554b77f1d2959366c89c9fa01c1372e6569230330f70b0`

The six matchup galleries are collected in the public
[Sevenfold Eclipse replay gallery](https://hewlett-john-inline-poster.trycloudflare.com).

The sheet is available in the owner's local account database as Tactical
Sheet and paired Arc Relay Entrant `7063d99b-24a5-46d9-b2a4-b4b8357f20c0`.
