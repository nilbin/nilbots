# The west residual hunt (2026-08-05 … 2026-08-06)

Owner directive: "Yeah hunt it." — the stock-vs-stock mirror on the
veterancy warren kept favouring the west side (85% at the start of the
hunt), and the goal was to localize the asymmetry, attribute it with
controlled experiments, fix what is a game defect, and say plainly what
is not.

Every claim below is a measured result on the 16-seed verdict battery
(seeds 2001–2004, 3001–3012), stock mind vs stock mind, same parity
control sheet both sides, in-process screening runtime.

## TL;DR

The "west residual" was four stacked asymmetries. Three were game or
reference-player defects and are fixed; the fourth is a property of the
strategy sheet, demonstrated by a controlled probe, and neutralized at
the rules level anyway:

1. **Engine deployment bug (fixed, `arc-relay-ambush-07`)** — team 1
   spawns were assigned by X-flip on maps that are 180°-rotation
   symmetric, so no eastern class stood in terrain equivalent to its
   western counterpart's.
2. **Reference-mind frame dependence (fixed, stock-mind-v4)** — every
   tie-break comparing raw direction enums, raw (Y,X) coordinates, or
   well-id strings picked the non-rotation-paired option for east, and
   plan theaters resolved to physical wells, stationing east's rotated
   units across the map from their assignments.
3. **Alternation parity was seed-invariant (fixed earlier as
   `arc-relay-ambush-06`)** — real, but measurably not the driver: with
   the flag active, tick-stream event order changes while outcomes are
   identical, because the deterministic opening kill was caused by (1)
   and (2), not by contested-resolution order.
4. **Flank-schedule × wing-composition tempo (attributed; neutralized
   by `arc-relay-ambush-08`)** — the north well births 25 ticks before
   the south one. On a chiral map under rotational mirroring, the same
   canonical sheet meets the first-born well with its *north* wing as
   west but its *south* wing as east. The parity sheet's wings carry
   different classes (kestrel/lantern vs relay/palisade), so the side
   whose fast wing met the early well converted tempo, every seed.

## The measurements that carried the hunt

| Round | Configuration | Verdict (W–E) | First divergence from symmetry |
| --- | --- | --- | --- |
| 0 | -05/-06, X-flip anchors, pre-hunt mind | 11–5 (69%) | tick 0 (no rotation pairing exists at spawn) |
| 1 | -07 anchors, pre-canonical mind | 14–2 (88%) | tick 0 (first moves X-flip vs rotation) |
| 2 | -07 anchors, rotation-canonical mind | 12–4 (75%) | ticks 48–58 in **all 16 seeds** = the north well's first birth (t50 ± 6 jitter) |
| 3 | same, wing-symmetric probe sheet | **9–7 (56%)** | same window — but the matchup at the early well is now class-identical |
| 4 | -08 (seed-phased well lead), standard sheet | *see below* | — |

Round 2 is the load-bearing measurement: with deployment and mind frame
fixed, every seed's mirror game is **perfectly rotation-symmetric in
unit position, facing, and health through the entire opening** —
including the centre-well contest — and first diverges exactly when the
schedule itself stops being rotation-symmetric (only the centre well is
self-paired; north leads south by 25 ticks). The first north-core
pickup went to the same side in 13 of 16 games, and that side won 11 of
16.

Round 3 is the attribution: change *nothing* except making the sheet's
north and south trios class-identical (legal under the two-copy cap)
and the verdict is statistically a fair coin (9–7, p ≈ 0.8 binomial).
The game core — map, engine, deployment, reference mind — is fair; the
tempo edge belongs to the sheet meeting a fixed schedule orientation.

## What was actually wrong, in one paragraph each

**Deployment (engine).** `ArcRelayH0Definition` derived team 1 anchors
as `(maxX − x, y)` — correct on the historical left-right-symmetric
maps, structurally unfair on the chiral warrens, where the fair pairing
is the full rotation `(maxX − x, maxY − y)`. The western anchor column
is Y-symmetric, so the eastern anchor *tile set* is identical either
way — only which unit index stands where changes, which is why -07 is a
map-only mint (rules byte-identical to -06) and why the stock mind's
spawn-anchor-derived plan relabelling became the identity on its own.
Tick-0 rosters on -06 admit **no** bijection pairing eastern units to
rotated western units (wrong classes at every rotated position); on -07
the pairing is exact.

**Mind frame (reference player).** With fair deployment, tick-0 states
were perfectly rotation-symmetric and the minds *still* diverged on the
first move: LINQ tie-breaks like `.ThenBy((int)heading)` and
`.ThenBy(p.Y).ThenBy(p.X)` are not rotation-invariant, and a fixed
enum order can never be — but rotation *preserves* every scalar the
mind actually cares about (distances, health, unit identity, and
clockwise orientation), so the fix is to compare canonical keys: west
compares raw values (byte-identical behaviour by construction), east
compares the rotated value. The same treatment covered the patrol-ring
walk order, the reactor landmark and heal-tile picks, well-id string
tie-breaks (north↔south swap under rotation), and — the biggest one —
plan theaters, which now resolve through `MirrorTheater` so east's
"north" doctrine physically operates in the south, where its units
actually spawn.

**Schedule orientation (rules).** The staggered flank cadence is a
design feature (one lane heats up first), but its *orientation* was a
constant, which on a chiral map is a per-side constant advantage for
any wing-asymmetric strategy — i.e., for normal strategies.
`arc-relay-ambush-08` keeps the stagger and makes the orientation a
seed coin: the driver swaps the birth schedules of each pair of wells
whose positions are mutual 180° rotations when
`SeedDerivation.DeriveWellLeadSwap(seed)` says so (salt
`arc-well-lead`, same construction as the resolution-phase coin, both
golden-pinned). Identities, positions, custody, regions, and the
observation surface are untouched; minds see the swapped timings as
ordinary schedule data.

## Verdict on arc-relay-ambush-08

**9 west – 7 east with the standard wing-asymmetric parity sheet** —
the same fair-coin split the wing-symmetric probe produced, now without
touching the sheet. Round-0 to round-4 the verdict moved 11–5 → 14–2 →
12–4 → 9–7, and the last step is causally clean:

- Every seed whose lead coin is 0 (ten of sixteen) replays its -07 game
  **byte-identically** modulo the contract fingerprint — the flag is
  surgically inert when the coin keeps the north lead.
- Every coin-1 seed plays a genuinely different game with the south
  well leading (verified in the replay well events, e.g. seed 2002
  births south t85 before north t127), and the three most lopsided -07
  west wins among them flipped east (2001, 3002, 3005).

Game health on the -08 battery matches the veterancy arm: per game on
average 73 destructions, 32 level-ups, 13 zone-heals, 15 banked cores,
and the usual reactor-destruction endings between t342 and t481 — the
fairness work changed who converts tempo, not how the game feels.

## Rulesets minted during the hunt

| Ruleset | Change | Nature |
| --- | --- | --- |
| `arc-relay-ambush-06` | `seedPhasedResolutionOrder: true` | rules-only mint on warren-04 |
| `arc-relay-ambush-07` | rotational spawn assignment, map `arc-relay-ambush-warren-05` | map-only mint (rules ≡ -06) |
| `arc-relay-ambush-08` | `seedPhasedWellLead: true` | rules-only mint on warren-05 |

All earlier rulesets re-derive byte-identically (pinned by the ambush
warren test suite, now 13 tests). Well access is measured equal by BFS
(11 tiles from either spawn cluster to every well ring on warren-05).
The stock mind change alters no game rule; its western play is
byte-identical by construction, and the canonical accessors are inert
on non-mirrored assignments.

## What this does NOT claim

- Stock mirror games after round 2 are symmetric *until the first
  asymmetric-by-design event* (staggered flank birth, or an
  alternation-resolved contest). They are not, and should not be,
  identical games — jitter, the alternation coin, and the lead coin are
  the designed variance.
- The wing-symmetric probe sheet is an instrument, not a
  recommendation: sheets should keep differentiating their wings; -08
  is what makes that fair.
- WASM finals have not been run on -07/-08; per house rules, in-process
  results are screening evidence and any adoption gate needs the
  authoritative runtime pass.
