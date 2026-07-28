# Outcome-blind watchability review

## Recommendation

Host these five first, in this order:

1. `sample-02`
2. `sample-06`
3. `sample-10`
4. `sample-01`
5. `sample-04`

This order leads with the cleanest overall pacing, follows with the strongest
objective reversals, then alternates a long comeback-shaped match, a compact
match, and a late multi-unit escalation. Do not place `sample-07` or
`sample-09` in the initial rotation: their spectator-visible trajectories are
exact duplicates of `sample-02` and `sample-01`, respectively.

## Blind-review lock and method

- The 10-replay package was selected by
  `scripts/replay-review-sample.py` from replay headers only, using seed
  `20260728`, map balancing, unseen-pair-first selection, and neutral
  `Entrant A` / `Entrant B` aliases.
- The population was 24 replays; the locked sample contains 10.
- Neither `results.json` nor `dynamics.json` was opened. No replay `result`
  field was consulted, and this review makes no claim about standings.
- Review evidence was limited to the neutral package's authoritative replay
  chronology: ticks, public events, projectile traversals, public mode state,
  and public post-state.
- Normal playback is five ticks per second at 1x. Durations and quiet stretches
  below use that presentation cadence.

This is a structural presentation review, not a substitute for a human
real-device pass. It can identify pacing, repetition, and cue opportunities
from the authoritative chronology, but it does not certify rendering,
mix balance, legibility, or subjective visual/audio quality.

## Initial hosting order

| Order | Replay | 1x length | Why it belongs |
| ---: | --- | ---: | --- |
| 1 | `sample-02` | 89.6 s | The smoothest complete arc in the sample: action starts at tick 6, the longest combat-silent gap is only 25 ticks (5.0 s), and eight objective pushes keep the second half changing direction. The closing ticks 412-431 combine 12 attacks, 7 damage events, 2 destructions, and a push at tick 418. |
| 2 | `sample-06` | 100.0 s | The strongest territorial back-and-forth: 12 pushes across the full lane, with a six-actor escalation and repeated late reversals. Ticks 393-412 contain 16 attacks, 8 damage events, 4 destructions, and a push at tick 400. It ranks second because ticks 43-130 are combat-silent for 17.6 s. |
| 3 | `sample-10` | 100.0 s | The most objective changes in the sample (13), plus a strong mid-match action run. Ticks 264-283 contain 20 attacks, 11 damage events, and 3 destructions; pushes then arrive at ticks 289, 309, 329, and 351. The cost is a 24.8 s combat-silent opening plateau. |
| 4 | `sample-01` | 62.0 s | The compact option. Combat lands early (first attack tick 6, first destruction tick 15), and four pushes fit into just over a minute. Its repeated mutual-destruction loop and a 7.0 s quiet gap at ticks 221-255 keep it below the three more varied matches. |
| 5 | `sample-04` | 82.6 s | A late escalation reaches six active actors around ticks 309-310, followed by a push at 315 and a dense ticks 315-334 window with 21 attacks, 8 damage events, and 2 destructions. A form transition adds presentation variety. The 24.8 s early plateau and 114 blocked-movement events make it a fifth choice rather than a lead replay. |

## Per-replay notes

| Replay | Chronology and watchability | Initial decision |
| --- | --- | --- |
| `sample-01` | 310 ticks; 67 attacks, 51 damage events, 17 destructions, 4 pushes. Eight ticks contain simultaneous opposing destructions. The action is readable and compact, but the opening repeats the same mutual-destruction beat at ticks 15, 49, 83, and 117. | Host fourth. |
| `sample-02` | 448 ticks; 96 attacks, 68 damage events, 22 destructions, 8 pushes. No combat-silent gap exceeds 5.0 s. After the shared opening, pushes at 167, 207, 254, 281, 304, 333, 371, and 418 create a sustained exchange. | Host first. |
| `sample-03` | 350 ticks; 77 attacks, 22 damage events, 7 destructions, 4 pushes. Ticks 12-135 have no attack, damage, or destruction, and ticks 17-119 have no movement. Ticks 200-249 then contain 25 attacks but no damage, producing activity without consequence. | Hold back. |
| `sample-04` | 413 ticks; 123 attacks, 41 damage events, 11 destructions, 6 pushes. The early plateau is long, but the match later reaches six active actors, includes a form transition, and has strong action density after tick 200. Frequent blocked movement may read as congestion. | Host fifth. |
| `sample-05` | 500 ticks; 157 attacks, 113 damage events, 37 destructions, 6 pushes. It is combat-heavy, but the first objective claim does not begin until tick 291 (58.2 s). Fourteen ticks contain paired destructions, making the first half feel like a respawn-and-rematch loop. | Hold back; consider a late highlight clip instead. |
| `sample-06` | 500 ticks; 135 attacks, 69 damage events, 22 destructions, 12 pushes. It establishes objective motion early at tick 41 and later moves repeatedly across both sides of center. The ticks 43-130 plateau is the main pacing defect. | Host second. |
| `sample-07` | Spectator-visible chronology is identical to `sample-02` for all 448 ticks. | Do not host beside `sample-02`. |
| `sample-08` | 500 ticks; 155 attacks, 114 damage events, 38 destructions, 6 pushes. The late swarm is a standout: ticks 332-351 contain 16 attacks, 10 damage events, 5 destructions, 3 spawns, and 2 lifecycle completions. However, the first claim is delayed to tick 298 (59.6 s), and the first 278 ticks are identical to `sample-05`. | Hold back as a full replay; strong highlight candidate. |
| `sample-09` | Spectator-visible chronology is identical to `sample-01` for all 310 ticks. | Do not host beside `sample-01`. |
| `sample-10` | 500 ticks; 128 attacks, 73 damage events, 24 destructions, 13 pushes. After its long static opening, it has the most frequent territorial reversals and adds fabrication, transformation, and replication beats. Its first 165 ticks are identical to `sample-03`. | Host third. |

## Boring, stall, and repetition findings

The main cohort-level issue is not a lack of combat events; it is repeated
opening choreography.

- `sample-01`, `sample-02`, `sample-05`, `sample-07`, `sample-08`, and
  `sample-09` share the same first 136 public ticks (27.2 s). Their opening
  repeatedly ends both initial actors at the same two positions at ticks 15,
  49, 83, and 117.
- `sample-05` and `sample-08` remain identical through tick 277, a 55.6 s
  shared opening. They should not both appear early in a hosted playlist.
- `sample-03` and `sample-10` share their first 165 public ticks (33.0 s).
- `sample-01` / `sample-09` and `sample-02` / `sample-07` are exact
  full-trajectory duplicate pairs.
- `sample-03`, `sample-04`, and `sample-10` each contain a 124-tick
  (24.8 s) stretch with no attack, damage, or destruction. Their longest
  no-movement stretch is 103 ticks (20.6 s).
- `sample-06` has a shorter but still noticeable 88-tick (17.6 s)
  combat-silent stretch.
- `sample-05` and `sample-08` defer meaningful objective pressure for almost a
  minute. Their high destruction totals do not fully compensate for the
  repeated reset loop at normal speed.

## Compelling moments

- `sample-02`, ticks 412-431: a late push lands amid 12 attacks, 7 damage
  events, and 2 destructions.
- `sample-06`, ticks 393-412: one of the best combined combat/objective
  sequences, with a push and 4 destructions in 20 ticks; further pushes at
  448, 468, and 499 sustain the closing motion.
- `sample-10`, ticks 264-283: the densest useful mid-match exchange among the
  recommended five, followed by four pushes between ticks 289 and 351.
- `sample-04`, ticks 309-334: the active cast expands to six, the objective
  moves, and the attack rate peaks without being another exact replay copy.
- `sample-08`, ticks 332-351: the strongest standalone highlight window in
  the held-back group, with lifecycle completions feeding a five-destruction
  swarm fight.

Before public release, a human should watch these five in this order at 1x on
the target desktop and mobile devices, with sound enabled, and confirm that
the identified chronology actually reads clearly in the 3D presentation.
