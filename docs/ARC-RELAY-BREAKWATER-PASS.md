# Arc Relay strategy ladder — finals reports

## Grammar-2 landscape (2026-08-05, analysis lane, `arc-relay-forward-combat-02`)

First screens under the new signature physics
(`evidence/grammar-2-landscape.json`, seed 424242, in-process):

- **Sentinel-zone v1 keeps the crown among strategies** — beats
  Breakwater 3-1/3-0 and Home Siege 3-1/3-1 — but its parity read cracks
  to a split (W 3-2 west, L 2-3 east): dodgeable bolts are a real nerf.
- **Breakwater v2 collapses**: loses both ways to Home Siege and to the
  parity control. Its linger-hunt tuning and hook usage were fitted to
  grammar-1 physics.
- **Home Siege v3 splits parity** (W 3-1 west, L east).

No sheet meets its old bars under the new physics — the expected result
of an honest grammar change. Every -01 record stands as history; the
-02 ladder starts from zero and needs grammar-2-native tuning as its
own owner-gated goal.



## Sentinel-zone v1: Stage 3 PASS (2026-08-05, freeze `52e8a4a1`)

The first Stage 3 candidate under the ratified cohort gate (DECISIONS
#208) passed every registered bar across 75 WASM games with zero faults:

| Cell | Result |
|---|---|
| Breakwater v2 (reigning), west / east | W 3-1@370 / W 3-1@380 |
| Home Siege v3, west / east | **W 3-0@314** / W 3-1@455 |
| parity, west / east | W 3-1@376 / W 3-1@351 |
| mortar-line BLIND holdout, first contact | **W 3-0@365 / W 3-0@378** |
| cohort gate (bar: 75%, no 0-2 sweeps) | **60/64 (93.75%), zero sweeps** |
| false positives | 0 fortify ticks, all three games won |

The holdout was authored adversarially — double mortar artillery, the
designed counter to sentinel clusters — and was shut out both ways on
first contact. The four cohort split-losses (control-grid E,
counter-courier E, sensor-grid E, sustain-attrition W; three of four on
the east side) are the named signal for future work; no entrant beats it
both ways. Sentinel-zone v1 is the Stage 3 deliverable. Evidence:
`evidence/sentinel-zone-v1-finals-results.json`; curated gallery
(including the losses, unedited) at the standing tunnel URL.

# Breakwater — Stage 2 finals report

## v2 finals: PASS (2026-08-05, freeze `c7d47647`)

Breakwater v2 — release-only memory freshness (18 ticks) on the
siege-release predicate, no side overrides — met every registered bar:
**10/10 evidence cells by elimination, zero faults** (WASM, tick-identical
to the in-process screens).

| Cell | Result |
|---|---|
| frozen siege west / east | W 3-2@479 / W 3-1@453 |
| parity west / east | W 3-1@378 / W 3-1@415 |
| south-mirror west / east | W 3-1@478 / W 3-2@526 |
| double-relay west / east (the v1-failed opponent) | W 3-2@532 / **W 3-2@488** |
| double-kestrel BLIND holdout, first contact | **W 3-1@576 / W 3-2@478** |

False positives: zero fortify latches vs three non-siege opponents. The
loss mechanism v1 shipped with — remembered approach mass never decaying,
so courier through-traffic pinned the team home for 84% of a game — is
fixed at the release side only; decaying detection or hunt memory instead
was measured to cause premature release against a lurking besieger.
Evidence: `evidence/breakwater-v2-finals-results.json`; gallery (all 13
games, curated) at the URL below. v2 supersedes the accepted v1 as the
Stage 2 deliverable pending owner confirmation.

## v1 resolution — owner accepted 7/8 (option 1, 2026-08-05)

The bar was re-ruled post-outcome to "all registered pairings plus at least
one of two holdouts" (DECISIONS #206). Breakwater ships as the Stage 2
deliverable; the double-relay weakness is the next strategy target; the
consumed holdout becomes an open development opponent.

## Original decision framing (pre-ruling)

The pre-outcome freeze (`evidence/breakwater-finals-freeze.md`, commit
`a2d4202f`) registered eight WASM cells. **Seven passed at or above their
bars; one holdout failed**: `home-siege-v3-four-down-double-relay` beat
Breakwater's east orientation 2-3 at tick 486 (elimination). Under the
registered bar the finals verdict is **FAIL**, reported as such. The
holdout is now consumed — any further tuning that sees it makes it a dev
opponent. Owner options:

1. **Accept 7/8** — re-rule the bar to "both registered pairings + one
   of two holdouts", ship Breakwater as the Stage 2 deliverable, log the
   double-relay weakness as the next strategy target.
2. **Iterate openly** — reclassify four-down-double-relay as a dev
   opponent, re-tune the east-side answer to a two-courier rotation, and
   mint a fresh unseen holdout for a new freeze.
3. **Reject** — keep Home Siege v3 as sole champion; Breakwater returns
   to development.

Review gallery (outcome-visible, all 11 finals games):
**https://trucks-therapist-bloomberg-elephant.trycloudflare.com**
— card 1 is the decision game.

## Registered-cell results (WASM, seed 424242, zero faults)

| Cell | Bar | Result | |
|---|---|---|---|
| vs frozen siege, west | W 3-2+ elim | **W 3-2 @ 566** | PASS |
| vs frozen siege, east | W 3-1+ elim | **W 3-1 @ 470** | PASS |
| vs parity, west | W 3-1+ elim | **W 3-1 @ 378** | PASS |
| vs parity, east | W 3-1+ elim | **W 3-1 @ 415** | PASS |
| south-mirror (holdout), west | W elim | **W 3-1 @ 478** | PASS |
| south-mirror (holdout), east | W elim | **W 3-2 @ 531** | PASS |
| four-down-double-relay (holdout), west | W elim | **W 3-2 @ 532** | PASS |
| four-down-double-relay (holdout), east | W elim | **L 2-3 @ 486** | **FAIL** |

WASM outcomes are tick-identical to the in-process screens (determinism
invariant held end to end). Games are seed-invariant for these
deterministic minds (validated across 424242 / 777001 / 777002).

## Recognition causality

- **False positives: none.** Three non-siege opponents (`balanced`,
  `courier-sprint`, `three-well-race`), WASM: **zero fortify ticks** in
  all three games — recognition never fires on ordinary play.
- **Camping/degeneracy: clean.** All eleven finals games kept both teams
  eligible under the v4 felt-degeneracy bars (no formation freeze, no
  sustained passivity, no pickup-drop cycling).
- Honest ladder signal: Breakwater lost two of the three false-positive
  games. The depth-map cohort strategies are stronger than the parity
  control; the registered baseline gate is the parity control, but the
  cohort is the sterner future yardstick.

## What Breakwater is now (nestk-r212hl, commit `17ba91ac`)

Composition [palisade ×2, patchbay, lantern, **nest**, kestrel, relay,
towline] — nest (newly wired executor kit) free-roams as a runner whose
sentinels anchor the mid-field. Detection dm5/ds2, release rm2/rs12,
choke-hold support-first, run-fight control-first, forward pass declined
(measured harmful). New `counter-courier-linger` task hunts visible
couriers during balanced while remembered approach mass ≥ 3, closing the
post-release windows. Open question on record: west-vs-siege margin is
3-2 across ~15 searched configs; the west/east orientations want
different detection sensitivity, which a per-side parameter override (a
format extension) would address.

## Provenance

- Freeze: `a2d4202f` (bar + sha256 identities, before any finals game)
- Candidate: `breakwater-v1` sha256 `4496dd39…`, executor artifact
  `f97792b9…` (executor v2), generated by tracked scripts
- Evidence: `out/breakwater-finals/` (11 games: replay + run records)
- Dev history: `evidence/siege-recognizer-dev-ledger.json` (search space
  and rejections), friction ledger #11–13
