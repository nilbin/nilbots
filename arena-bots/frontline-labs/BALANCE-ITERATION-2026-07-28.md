# Frontline Labs population and pacing iteration

Status: architecture accepted; no gameplay candidate promoted into immutable
`frontline-labs-1`.

## Decision

Keep baseline v2 as the frozen calibration population. The engine, generic
contracts, WASM runtime, lifecycle mechanics, replay v3, and evidence tooling
are reliable enough for continued tuning: all 84 matches across v2 and the
five 12-game arms verified with zero faults or disqualifications.

Do not change the map yet. Do not lower the hosted capture threshold yet.
Pressure's bootstrap, Bastion's exact fire control, Fabricator's forward post,
and capture threshold 12 are all preserved as separate candidates, but none
clears its pre-registered watchability gate.

## Paired results

All rows use seed `104729`, both participant assignments, the same four
baseline-v2 WASM artifacts except for the named single-bot arm, and the same
map/format. V2's second seed repeated every ordered behavioral trajectory, so
the 12-game slice is the non-duplicated control.

| Arm | Breach | MaxTicks | Draws | Median ticks | Active | Stalled | Looped | ≥75 idle | Entropy |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| v2 baseline | 7 | 5 | 2 | 430.5 | 80.6% | 6 | 6 | 4 | 0.565 |
| v3 Pressure bootstrap | 5 | 7 | 3 | 500.0 | 81.3% | 6 | 7 | 5 | 0.527 |
| v4 Bastion fire control | 7 | 5 | 0 | 429.0 | 79.4% | 6 | 6 | 4 | 0.550 |
| v5 Fabricator forward post | 7 | 5 | 0 | 409.5 | 80.0% | 6 | 6 | 4 | 0.585 |
| v6 capture threshold 12 | 8 | 4 | 1 | 414.0 | 80.1% | 6 | 6 | 4 | 0.564 |
| v7 late gain `300:2` | 8 | 4 | 1 | 416.5 | 80.5% | 6 | 6 | 4 | 0.589 |

### v3 — Pressure bootstrap: rejected

Pressure completed a Split in all six opponent/assignment cells, proving the
mechanic and SDK path work. It earned only one draw, remained swept by Adapter
and Fabricator, increased MaxTicks from zero to two in its six-game subset, and
increased looped games from two to three. A full-health Prime becomes two
1-HP replicas by contract; numerical presence alone did not convert into
durable pressure.

Candidate artifact:
`764e7dba5041c80c5a8d398e12ba778e3aa96a6eca0dd4768ca484912ba9390c`.

### v4 — Bastion fire control: correctness retained, balance arm rejected

All 54 turret shots were replay-audited as exact cardinal/diagonal,
contract-range, wall-clear, strict-corner-clear rays at submission. The six
non-Bastion replay hashes remained byte-identical to v2. Nevertheless, all six
Bastion games still stalled and looped, Bastion fell from four to three
points, and both Fabricator pairings remained 500-tick non-breaches.

Candidate artifact:
`6fce011e28c4f9a61f6bb29dbcfa1614e23c3d85a8212e66dac9e46c4999aec8`.

### v5 — Fabricator forward post: strategically useful, pacing arm rejected

One surplus body staged on the forward perimeter after an uncontested
half-threshold claim. Fabricator improved combined terminal progress against
Adapter from `-56` to `-47`, changed the two 0-0 Bastion draws into `+6` and
`+3` timeout wins, and shortened one Pressure breach from 448 to 406 ticks.
However, all four Adapter/Bastion pairings still reached MaxTicks and the
stalled/looped tail did not change. Retain this as the strongest population
candidate, but do not call it a watchability fix.

Candidate artifact:
`b8e91829a30d62e5498457e349e3c476e93cf4dc95c4444af4b75cb3a4296084`.

### v6 — capture threshold 12: rejected

The candidate used ruleset
`frontline-labs-1-experiment-capture-12`, rules fingerprint
`5b5262f9f094ac03157acea4d28de83b7fb8ad020237c6c6ffc22f7eebb92b24`,
and match fingerprint
`f6d8d890be1112f4e82e1cf201a0c70a81998508e07614537d81f6c5bd3dd872`.
It reduced draws from two to one and MaxTicks from five to four, but breaches
rose only from seven to eight; stalled games, looped games, and long-idle
games did not move. It mostly relabeled the same repetitive trajectories.

### v7 — late capture gain `300:2`: architecture accepted, tune rejected

The candidate used ruleset
`frontline-labs-1-experiment-gain-t300-2`, rules fingerprint
`8d6642f0d4626189083ee8657e0b63004d3d346f06c5752cd0ddf0f1fc0a8042`,
and match fingerprint
`acafc50d1bbc81dbcb74b413b2fbf1970a317bc3c5a19a6e5eb394660db1a4bb`.
All four baseline-v2 policy sources were byte-identical and mechanically
rebuilt against SDK/Guest 0.10.3.

After normalizing only contract/artifact identity and the ruleset-derived
per-life seed, every ordered pairing was spectator-trajectory-identical
through tick 299 or its earlier terminal tick. The schedule therefore passed
its causal implementation check. It modestly improved breaches, MaxTicks,
draws, duration, and action entropy, but stalled, looped, and long-idle counts
did not move. It changes how quickly an already-controlled objective resolves;
it does not help a doctrine disengage, relocate, or break a positioning loop.

## Population verdict

- Adapter remains the control and best complete starter example.
- Fabricator v5 is the best candidate revision for a later native population,
  but needs a pacing mechanic before promotion can be judged fairly.
- Bastion's problem is not a turret-vs-turret deadlock: no arm produced a
  60-tick both-turret deadlock. It is a doctrine-level failure to turn area
  denial into the next objective.
- Pressure shows that Split's real cost is meaningful. Its next authoring pass
  should decide when the two fragile bodies create a positional payoff, rather
  than Split whenever legality appears.

## Recommended next arm

Stop changing universal capture numbers. The remaining repetitive tail is
concentrated in Bastion pairings and survives exact fire control and phased
gain. Test a reversible, contract-defined `Mobilize` transition from turret
back to child-mobile, then give only Bastion one declared policy pass to use
it when the active objective has moved beyond its fire support or the late
phase begins.

This is a native-rules product comparison, not a same-artifact numeric arm:
historical bots remain compatibility sentinels, while the candidate Bastion
must understand the new action. Keep health continuity, windup, placement,
cooldowns, map, capture schedule, and every non-Bastion policy fixed. The
hypothesis is specific: fortification should be a strategic commitment, not a
permanent self-removal, and late remobilization should break the six
Bastion-centred positioning loops without erasing the turret's early/mid value.

### v8 pre-registration — one-way remobilization

This is a substantial action-contract experiment, not a numeric tune and not
a ship verdict. The candidate ruleset is
`frontline-labs-1-experiment-mobilize`; hosted `frontline-labs-1`, its map,
static capture tuning, lifecycle, fabrication, Split, attacks, vision, and
every non-Bastion policy stay fixed.

The candidate adds one declared `mobilize` action and a
`turret -> child-mobile` same-life transition. It preserves actor/runtime
identity, private memory, position, facing, cooldown, and energy; health is
preserved but capped to the child maximum. Anchor retains its existing `+2`
health and placement restriction. Mobilize consumes a one-tick windup and is
irreversible back toward turret for that life, preventing Anchor/Mobilize
healing loops while still letting a committed turret rejoin the mobile game.

Isolation and evidence:

1. Baseline-v2 artifacts under the candidate are compatibility sentinels.
   Because none can submit `mobilize`, their normalized behavioral
   trajectories must remain identical to hosted v1 after contract/artifact
   identity and ruleset-derived actor seeds are removed.
2. Only Bastion receives one declared, candidate-aware policy pass. It may
   mobilize its designated turret after the active objective moves outside
   useful fire support. Adapter, Fabricator, and Pressure sources remain
   byte-identical and are mechanically rebuilt only if the SDK contract
   version changes.
3. The final screen uses seed `104729`, both participant assignments, all six
   unordered doctrine pairs (12 WASM matches), the same map, and all verified
   replay-v3 evidence.

Hard gates are zero runtime faults/disqualifications, deterministic replay
verification, at least one legal Mobilize completion, no pre-Mobilize
trajectory drift in the candidate Bastion games, and exact non-Bastion match
behavior. The watchability hypothesis passes this exploratory arm only if the
six Bastion-centred games reduce both stalled and looped classifications from
six to at most three without increasing the full cohort's MaxTicks count above
five or reducing its breach count below seven. Duration and outcome balance
remain diagnostics; a numeric pass still requires human replay review before
any rules promotion.

## Replay review

The outcome-blind v2 review found exact spectator duplicates and long shared
openings despite healthy combat totals. The five recommended hosted replays,
in order, are `sample-02`, `sample-06`, `sample-10`, `sample-01`, and
`sample-04`. A human mobile/desktop pass is still required for actual 3D
legibility and audio.
