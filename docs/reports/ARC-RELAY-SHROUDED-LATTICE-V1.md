# Shrouded Lattice — Broadloom ladder rung 5

`shrouded-lattice-v1` is the first sheet admitted above Loomsplit,
Loomshears, Crosscut and Sixfold under the promoted `broadloom-7` rules. It
wins each fixed six-game mirrored series, uses eight different classes, and
keeps meaningful counterplay: Sixfold takes two cells, while Loomsplit and
Crosscut each take one.

This is a ladder result, not a population-balance conclusion.

## Doctrine

The sheet stages two three-body arms before closing on centre:

- **North hook:** Kestrel, Relay and Towline supply speed, custody tempo and
  formation displacement.
- **South null:** Repulsor, Patchbay and Hush supply adjacent area pressure,
  sustain and full signature suppression.
- **Home trap:** Nest and Minesmith hold the own Well with a sentry and two
  hidden Trip Nodes.

All six centre bodies use aggressive posts and heal-tile recovery after the
formation releases. Custody is an escort plan with Relay as the first
preferred carrier. The Hush has two deliberate centre marks; this is the
specific counter-pick that replaces Sixfold's second Mason without creating a
second executor or signature scheduler.

The roster has no duplicate class:

`Kestrel · Relay · Towline · Repulsor · Patchbay · Hush · Nest · Minesmith`.

## Current-rules ladder audit

The rules and signature promotion changed one historical edge, so the old
chronological ladder is no longer a strict chain. On the standard cells:

| Matchup | Current result |
|---|---:|
| Loomshears over Loomsplit | 6–0 |
| Crosscut over Loomsplit | 6–0 |
| Loomshears over Crosscut | 4–2 |
| Sixfold over Loomsplit | 4–2 |
| Sixfold over Loomshears | 5–1 |
| Sixfold over Crosscut | 4–2 |

Sixfold therefore remained the actual admission gate. Shrouded Lattice was
tested against every prior sheet rather than relying on the historical order.

## Admission record

Profile: `broadloom-7`. Review cells, always in this order:
`w-9001 w-9002 e-9005 e-9009 w-9012 e-9004`.

| Opponent | Series | Per-cell Lattice outcomes |
|---|---:|---|
| Loomsplit | **5–1** | L411, W269, W350, W267, W267, W376 |
| Loomshears | **6–0** | W276, W345, W343, W345, W288, W351 |
| Crosscut | **5–1** | W277, L369, W351, W296, W357, W262 |
| Sixfold | **4–2** | W336, W333, W343, L366, L352, W358 |

All 24 raw replays passed the movement-sanity gate and the felt-degeneracy
eligibility gate. Seventeen conformance reports had no deviations; the other
seven report only the known Hold-checker severity seam for the home defenders
(at most one-tile combat excursion, including reports whose measured maximum
was zero). Two zero-deviation reports additionally carry that checker as a
note. No authored job, transition, delivery or signature promise deviated.

## What the iteration proved

The useful improvements were strategic, not stat changes:

1. A body nominally assigned to centre is not enough. Friendly bodies block
   allied lines, and an early Longshot was placed behind a Mortar/wall lane.
   Correcting the firing geometry flipped a focused Loomshears matchup from
   0–2 to 2–0.
2. Custody matters. The first ranged lattice repeatedly picked up centre Cores
   but lost solo carriers. Escort custody changed one focused Sixfold loss
   into a win and made the delivery intent visible in the replay.
3. Counter-picks matter. A slower Longshot/Mortar/Palisade lattice lost both
   focused Sixfold cells. Retaining Sixfold's mobile control spine while
   exchanging Mason for Hush and the home Mason for Minesmith won both and
   then took the honest full series 4–2.
4. Roster diversity did not require throwing the match. The admitted sheet
   uses eight classes, and Hush/Minesmith have an observable reason to exist
   rather than being cosmetic substitutions.

## Friction and missing depth found

### Authoring/tooling

- `coreRule` is tactic-group-wide. A Workshop trainee authored with `hunt`
  abandoned training at tick 46 to chase a visible centre Core. Changing it
  to `grab` fixed the common case, but a phase-local override remains the
  cleaner vocabulary.
- The CLI Tactics materializer accepted an exact post on Broadloom wall
  `(14,11)` and only the match runtime rejected it. The hosted save path has a
  real-map gate; the direct authoring command should use the same validation.
- The editor/map should preview allied firing-line obstruction. That mistake
  was tactically decisive and is hard to see in JSON.
- The conformance reader still overstates some valid one-tile Hold responses
  as deviations. Its readiness/leash rule should match the executor exactly.

### Executor correctness found and fixed

An empty-handed escort could choose the own reactor from the standoff ring
around an adjacent carrier. In `w-9012` that escort occupied the bank while two
loaded allies waited beside it for more than 40 ticks. The shared follow
planner now reserves the own reactor. Replaying the exact seed removed all
orbits/held yields and correctly changed the result from an artifact win at
tick 478 to a Sixfold win at tick 352.

The ladder work also exposed replay-checker gaps for autonomous Sentinel and
wall-breaching Hook declarations, plus a no-op Arc Toss landing. Those were
fixed and covered before the admitted series.

### Gameplay/depth warning

The strongest general-purpose shape is still **six bodies converting centre
plus two bodies harvesting/defending the own Well**. Multiple more spatially
diverse attempts failed:

- three outer runners ceded too much centre tempo;
- a four-channel veteran Kestrel/Repulsor investment matured too late against
  immediate six-body centre pressure;
- a solo veteran raid repeatedly died to an ordinary two-body Well ward;
- a six-body ranged lattice still lost to the closer-control package until it
  adopted much of Sixfold's spine.

That does not make the current game shallow—the winning edge involved
formation, custody, recovery and signature counter-picks—but it is the clearest
remaining depth risk. Future map/economy work should make leaving centre a
credible route to a general-purpose win, not merely a hard exploit when an
opponent forgets its Well. Workshop tempo/reward also deserves review if a
protected veteran strategy is meant to compete before the first two centre
Core conversions.

## Reproducible artifacts

- Tactics source:
  `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/shrouded-lattice-v1.json`
- Tactics SHA-256:
  `0a8998ca1991f7fb13da8a1842d7a635d6b1c33544bc6ad8f2970c5c4ddbf684`
- Materialized Orders SHA-256:
  `4691886c04a9901f30eb76001fbbe8eeb3f2ae2fe018a0801e29821cc904e819`
- Map-plan SHA-256:
  `d5515f674cc8b892976d2cd9f21c319f1079451401704ce40a6720c41b10e4d4`
- Linked package/content hash:
  `a771ea678c1627edbcdafac1af017b7c2a989aed043db78496b916272453c10a`

The four six-cell galleries were built from the exact final materialized
artifact and are collected in the public
[Shrouded Lattice replay gallery](https://mixed-unfortunately-illustration-drivers.trycloudflare.com).
The gallery index, all 24 replay cards and an actual replay playback were
browser-verified at desktop and phone widths without console errors.

The sheet is also available in the owner's local account database as Tactical
Sheet and paired Arc Relay Entrant `9b25c9ee-f346-4e3a-9300-0d83b27df70a`.
