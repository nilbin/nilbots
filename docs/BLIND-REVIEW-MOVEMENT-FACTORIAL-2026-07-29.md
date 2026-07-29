# Outcome-blind viewing pass — movement factorial (2026-07-29)

The product owner rated 12 samples from
`frontline-classes-wave-2-movement-factorial-v1` (4 per movement arm,
outcome- and arm-blind, deterministically shuffled; gallery built by
`scripts/build-review-gallery.py` from the three seeded
`replay-review-sample.py` manifests in the run directory). Scores are
1–5 on "fun to watch" and "easy to follow". Sample-01 additionally
carries the five-dimension ratings from the panel's first revision.

## Un-blinded key and scores

| # | Arm | Cell | Match (seed) | Fun | Clarity | Notes |
| --- | --- | --- | --- | ---: | ---: | --- |
| 01 | move-sets-facing | bulwark-vs-fabricator, thin-fronts | iron-root vs ledger-fly (210011) | 3 | 4 | (also legibility 4, tension 4, counteraction 3, repetition 3, ending 4) |
| 02 | preserve-facing | fabricator-vs-striker, current | spark-line vs vector-edge (180001) | 2 | 3 | live reaction: "they're still side stepping!!" |
| 03 | facing-locked | bulwark-vs-bulwark, current | march-wall vs iron-root (240007) | 2 | 4 | |
| 04 | preserve-facing | bulwark-vs-fabricator, thin-fronts | march-wall vs ledger-fly (180001) | 3 | 4 | |
| 05 | facing-locked | bulwark-vs-striker, thin-fronts | iron-root vs vector-edge (240007) | 3 | 4 | |
| 06 | move-sets-facing | bulwark-vs-bulwark, outer-shoulder | iron-root vs march-wall (180001) | 3 | 4 | |
| 07 | facing-locked | fabricator-vs-striker, outer-shoulder | spark-line vs still-water (180001) | 4 | 4 | |
| 08 | move-sets-facing | bulwark-vs-striker, current | march-wall vs vector-edge (240007) | 3 | 4 | |
| 09 | move-sets-facing | bulwark-vs-fabricator, current | march-wall vs ledger-fly (240007) | 2 | 4 | |
| 10 | facing-locked | fabricator-vs-striker, current | spark-line vs vector-edge (240007) | 2 | 4 | |
| 11 | preserve-facing | fabricator-vs-fabricator, outer-shoulder | ledger-fly vs spark-line (210011) | 2 | 4 | "Duels are very repetitive and too even and strafing just doesn't feel right" |
| 12 | preserve-facing | fabricator-vs-striker, current | ledger-fly vs still-water (180001) | 4 | 4 | "Curve shots makes it more fun to watch and dynamic" |

## Findings

- **The movement arms do not separate on watchability.** Mean fun is
  2.75 on every arm (preserve 2/3/2/4, move-sets-facing 3/3/3/2,
  facing-locked 2/3/4/2); clarity is 3.75–4.0 everywhere. In
  particular, facing-locked's 37.2% rotation share carried **no
  watchability penalty** in this sample — one of the two best-rated
  games (sample 07, fun 4) was a facing-locked cell.
- **The qualitative signal is against strafing.** Both explicit
  negative reactions target preserve-facing cells: the live
  "still side stepping!!" (sample 02) and the sample-11 note
  ("strafing just doesn't feel right"). The owner separately confirmed
  the direction in conversation.
- **Bends are the watchability driver this sample can detect.** The
  only two fun-4 games are the only two featuring still-water — the
  high-bend striker (40–56% bend share; sample 12's replay contains 10
  programmed bends/offsets in 13 shots). The four games featuring
  vector-edge, the low-bend striker (25–26%), average fun 2.5. The
  owner's blind eye found the bend-heavy striker twice without labels.
  Owner verdict recorded: "curved shots actually do deliver value —
  definitely makes the game more dynamic; not sure if only a single
  class having them is right to begin with."
- **Dullness quantified: mean fun 2.75, no 5s.** Clarity never fell
  below 3, so the renderer/team-distinction work is no longer the
  bottleneck — the game itself is.
- Owner design notes attached to this pass: energy is not a candidate
  (tried and closed in DECISIONS #47/#48 — "taxes aggression as much
  as camping"); mechanism exploration should consider widening the
  bend envelope beyond the striker.

Raw export JSON is preserved in the run directory as
`blind-review-notes-2026-07-29.json`; the sample manifests and gallery
are reproducible from the run directory and
`scripts/build-review-gallery.py`.
