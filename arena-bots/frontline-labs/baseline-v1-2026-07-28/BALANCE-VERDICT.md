# Frontline Labs baseline-v1 balance verdict

Date: 2026-07-28

Status: **invalid for numeric balance tuning; retained as diagnostic evidence**

## What the frozen cohort established

The primary run completed 36/36 mirrored WASM matches with canonical replay-v3
verification and no side advantage: team 0 and team 1 each won 12 matches.
Adapter led the four entrants with 12 points; no entrant exceeded half of all
available points.

Those standings are not a useful balance verdict. Twenty-four matches reached
500 ticks, 18 were classified as stalled, 18 as looped, and only 40.25% of
ticks were active. The outcome-blind review independently found seven of its
12 samples had stopped meaningful public activity by tick 56 or earlier, even
though most continued to tick 500. The product-owner review of the first
sample was simply that the game looked “somewhat stupid.”

Mechanic coverage also failed:

| Mechanic | Attempts | Successful actions | Completions |
| --- | ---: | ---: | ---: |
| Fabricate | 15,453 | 36 | 36 |
| Split | 156 | 0 | 0 |
| Anchor | 0 | 0 | 0 |

## Root cause

The generic action mask described Fabricate and Split too optimistically.
Fabricate was reported `Available` whenever a compatible Ready target existed,
even if the source Prime was outside its required home region. Split was
reported `Available` whenever its form route existed, even without enough
health or compatible Ready slots.

That made correct-looking bot code repeatedly select an action which joint
resolution could never accept:

- all 15,417 blocked Fabricate decisions came from Prime unit 0 away from the
  two valid home-pad source positions;
- all 156 blocked Split decisions were made with zero Ready slots. Of those,
  66 also had only 1 HP, 24 had 2 HP, and 66 had 3 HP.

The engine now includes source-local Fabricate eligibility plus Split
source/health/slot prerequisites in `Available`. Placement and simultaneous
claim conflicts deliberately remain authoritative resolution-time outcomes,
because pre-resolving those would both leak hidden occupancy and reject
actions that can succeed after a same-tick vacancy.

## Counterexample after the legality repair

The preserved Adapter artifact, mirrored against itself on seed 104729 with no
strategy change, produced:

- 2 successful Fabrications and 2 successful Splits;
- 88.8% active ticks;
- no stall, no loop, and a longest no-interaction run of 14 ticks;
- reciprocal multi-tick combat and 15 destructions;
- zero runtime faults.

A six-pair smoke matrix also made previously dormant Anchor behavior execute.
That exposed a separate SDK decoder defect: canonical one-tick
end-of-started-tick transitions emit `startedTick == dueTick`, while SDK 0.10.0
required `dueTick > startedTick` for event payloads. The engine/replay was
correct; every observing old WASM artifact faulted while decoding the event.
SDK/Guest 0.10.1 fixes the event invariant while keeping genuinely pending
transition state strictly future-due.

## Decision

Do not change capture thresholds, HP, damage, cooldowns, unlock timings,
windups, or map geometry from this cohort. A numeric change would be tuned
against false action masks and a decoder failure rather than gameplay.

The next admissible evidence is:

1. framework-owned mechanics probes built with SDK/Guest 0.10.1, proving
   Fabricate, Split, Anchor, and turret fire independently;
2. a new, explicitly post-reveal entrant cohort built against the corrected
   masks and SDK;
3. a short mirrored WASM tournament with blind replay review;
4. only then, at most one coherent numeric rules arm justified by the new
   dynamics.

The v1 entrants, artifacts, results, and review notes remain preserved. They
must never be relabelled as the repaired cohort.
