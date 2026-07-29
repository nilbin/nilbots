# DX notes — spark-line (fabricator, `spark-line-v1`)

Written before seeing any opponent source, standings, or aggregate balance
results. Population: Frontline Labs classes wave 1. Role: verdict-doctrine,
target cumulative T4. Budget: one authoring pass; mechanical contract repairs
free; no open-ended strategic iteration.

## Frozen identities

| Item | Value |
| --- | --- |
| Entrant | `spark-line` (entry type `SparkLine`) |
| Class | `fabricator` (declared in `botarena.json`) |
| Canonical artifact | `out/bot.wasm` |
| **bot.wasm SHA-256** | `6ede923500f7bce21dee6dff5ae61865ed08d30df6f88e95428269b435a9af2c` |
| Qualification report | `evidence/t4/qualification.json` |
| qualification.json SHA-256 | `367cb8895406ce1844615b259a588d5031e83bbc81508cc269b494932ea232ac` |
| Source-tree hash (sorted per-file SHA-256, then SHA-256 of that list; `.cs` + `botarena.json` + `README.md`) | `7de7e6a74117de96244e82b82a3d5125b8053fda622c7139d9bc02f05aa89569` |
| Builder | CLI 0.9.7, SDK 0.10.4, NativeAOT-LLVM 10.0.0-rc.1.26306.1, build pipeline 4, wasi-wasm p1 core module, platform-matched Docker builder |
| Submitted source | `SparkLine.cs` (1551 lines), `ContractLens.cs` (357), `Tactics.cs` (265) |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` as read on 2026-07-29 |

## Qualification outcome

`experiment frontline-labs qualify --suite frontline-qualification-5`
(profile `frontline-duel-depth-union-t4-v1`) exits **3** — clean capability
failure. **Tier awarded: T2.**

| Component | Result |
| --- | --- |
| T2 (`frontline-qualification-3`) — contract matrix, automatic life cycle, objective path, direct fire, straight evade, manual fabrication | **PASS (all six)** |
| T3 (`frontline-qualification-4`) — wall-terminated bend, strict corner, cooldown window, local form safety | PASS |
| T3 — `cadence-parity` variant `range-3-harmless` | **FAIL** (both team assignments) |
| T4 — suppression-choke, entry-initiative, prediction-chamber, front-rotation, map-holdout | **PASS (all five)** |

So the artifact clears every T4 component and is held at T2 by exactly one
prerequisite variant. That is worth recording precisely because it is an
unusual shape: the tier ladder is cumulative, so a single T3 sub-variant masks
a complete T4 pass. If the population wants the T4 evidence, it is in
`evidence/t4/qualification.json` regardless of the awarded tier.

### The variant that held it

`cadence-parity/range-3-harmless`: bot and controller both carry
`maxTravelTiles: 3`; the bot starts at `(11,7)` and the controller at `(15,7)`,
four tiles away, and fires one straight shot on tick 0. The passing sibling
variant (`range-4-threatening`) gives the bot range 4, so it can fire on tick 0
before any threat exists and still dodge on tick 1 — it scores `damageTaken: 0`
and `damageDealtDuringCooldown: 1`.

At range 3 those two look mutually exclusive from inside the bot. Tracing my
own evidence replays: the only tile within range 3 that has a legal trajectory
to `(15,7)` is `(12,7)`, and `(12,7)` is exactly the terminal tile of the
controller's bolt. `(12,8)` and `(12,6)` are walled off to the east
(`(13,8)`/`(13,6)` are walls), so no curved one-bend program reaches from them.
The controller's cooldown window is ticks 1–2, and a shot fired from `(12,7)`
on tick 1 lands on tick 2 — but standing on `(12,7)` on tick 1 is the hit.
I could produce `damageTaken: 0, dealt: 0` (evade first) or
`damageTaken: 1, dealt: 1` (trade first) but not both, and both fail. I
stopped there rather than spend improvement budget guessing at the analyzer's
exact predicate; the doctrine kept the version that takes no damage, because
"a body that survives keeps contesting the objective" is the doctrine.

If the analyzer intends a third line here, the probe would be much more
teachable if the report named the failing predicate rather than only emitting
the metric vector. See friction #1.

## Frictions, in the order they cost me time

### 1. Probe reports emit metrics but never the failing predicate

Every failed case gives a flat vector (`damageTaken`,
`damageDealtDuringCooldown`, `successfulRealThreatMoveCount`, …) and
`passed: false`. Nothing says *which* comparison failed or what the threshold
was. Diagnosing each failure meant loading my own evidence replay, replaying
the tick-by-tick geometry by hand, diffing the metric vector against a passing
sibling variant, and inferring the predicate. That worked for
`straight-evade`, `entry-initiative` and `map-holdout`; it did not work for
`cadence-parity/range-3-harmless`, where I still cannot construct a line that
satisfies my inferred predicate. A single `failedCriteria: ["damageTaken <= 0",
…]` array per case would have turned three archaeology sessions into three
edits, and would remove the incentive to fit behaviour to a guessed metric —
which is exactly what a balance population does not want.

Related: the probe maps are per-probe (`mapFingerprint` differs per case) and
the attack profile is per-probe too (`maxTravelTiles` is 3, 4 or 8 depending on
the variant). I initially debugged `cadence-parity` against the hosted map and
the hosted range and reached a confidently wrong conclusion. The probe name
encodes it (`range-3-…`) but the report does not surface the resolved rules
values next to the metrics.

### 2. The generated starter cannot shoot on my own class contract

`ArenaBasics.TryDirectShot` handles two shapes: an action with a
`ProjectileHeadingConstraint`, or an action with an optional/valid
`ShotProgram` payload. The fabricator class exposes `shoot-straight` with
**zero** declared parameter kinds and `shotProgram.enabled: false`,
`payloadOptional: false`. The helper walks both branches, finds neither, and
returns `null` — the scaffolded bot silently never fires a shot in its own
class arm. Nothing errors; you only notice by counting `attack` events in a
replay. Since the starter is explicitly advertised as "a competent apprentice"
and the class addendum says "if your forms allow `shoot-straight`, the action
takes no payload", the shipped helper contradicts the shipped addendum. Either
the helper should handle the no-parameter attack, or the template README should
say the helper is hosted-v1-shaped.

### 3. Contract facts that are legal to read but expensive to *notice*

Three rules cost me a debugging cycle each, all of them documented, none of
them visible in the legality mask:

- **Reserved spawn tiles.** `automaticReturnPlacement:
  assigned-spawn-permanently-reserved-for-slot-against-other-actors-and-lifecycle-claims`
  means a child fabricated onto `(1,7)` can *never* step east onto the prime
  spawn `(2,7)`. `move` remained `available: true` with `east` in its
  `DirectionConstraint` every single tick, and the move was refused by joint
  resolution every single tick — 183 wasted ticks per child in one base-contract
  run, 516 blocked moves in a 500-tick match. The mask cannot promise joint
  resolution, which is fair, but "permanently reserved for the whole match
  against this body" is a *static* fact and reads more naturally as a tile tag
  than as a policy-ID string buried in `lifecycle.automaticReturnPlacement`.
- **Placement offset ordering.** `positionSelection:
  first-eligible-declared-offset` + `candidateReference: queue-time-source-pose`
  with a declared list that starts at `(forward: -1, right: 0)` means the child
  lands *behind* you. That is a genuinely interesting decision surface once you
  see it — it is the whole reason facing matters to a fabricator — but the two
  policy IDs that create it are adjacent to two dozen other policy-ID strings
  and read like boilerplate.
- **Movement does not rotate.** `move` takes an absolute `Direction` and the
  `Movement` event documents "actor facing retained during movement". So facing
  is an independent axis and a facing-locked gun costs a separate `rotate`
  tick. Correct and interesting; not obvious from the action catalog, where
  `move` and `rotate` look like a matched pair.

### 4. Hardcoding temptations I had to actively resist

Real ones, in order of pull:

- **Unlock ticks.** The rule card says 120/260; my class says 60/180; the probe
  contracts say both. Reading `LifecycleAssignment.UnlockTick` and
  `ObservedUnitSlot.State` is the only thing that survives, and the assignment
  brief warned about it — but the temptation is strong because the numbers are
  right there in prose.
- **`maxTravelTiles`.** Two documented values (7 for my class, 8 for hosted v1)
  and a third undocumented one (3 and 4 in the cadence probes). Anything keyed
  to 8 would have silently mis-aimed in three probe variants.
- **Which tiles are "my" pad.** The `spawn-protected` tag covers *both* pads and
  the region IDs are `team-0-home-pad` / `team-1-home-pad` — name-keyed. I
  attribute each protected tile to whichever side's declared initial-deployment
  spawns are nearer, which is derived rather than named, but it took a
  deliberate detour to avoid `regionId.StartsWith("team-0")`.
- **`objectiveWeight`.** Nothing in the mask tells a body it cannot capture; you
  have to look the current form up in the catalog. Easy to assume every mobile
  body counts.

### 5. Terminology that reads as one thing and means another

- **`Available`.** It reads as "this will work". It means "individually legal
  before the joint step". Every blocked-move bug I had was me trusting it. The
  XML doc says so plainly; the field name does not.
- **"threat"** in the probe metrics splits into `apparentThreatTurnCount` and
  `realThreatTurnCount` with no definition anywhere I am permitted to read. I
  inferred "apparent = aimed at you, real = has range left to reach you", which
  is a lovely thing to test — but I inferred it from a metric name.
- **Prime "respawn" vs child "rebuild"** are both `AvailabilityPending` in the
  slot union but one is `AutomaticReturnPending` and only the other needs an
  action. `AvailabilityReason.DestructionRecovery` vs `InitialUnlock` is the
  useful distinction and it lives one level down.
- **`RelativePositionOffset(Forward, Right)`** is relative to *facing*, not to
  the map or to the team's home direction — while the child's own facing comes
  from `outputFacing: participant-output-region-assignment-facing`, i.e. the
  team's authored direction. Two different frames, one record.

### 6. Timing

- Contract archaeology (reading permitted docs + SDK, dumping three resolved
  contracts out of my own replays to see the real shapes): ~45 min. Dumping the
  contract from a replay header was by far the highest-value move of the whole
  session and is not suggested anywhere in the docs — `--print-candidate-contract`
  only emits identity/fingerprints, not the catalogs.
- First working policy: ~50 min.
- In-process self-play iteration on the class mirror, plus base /
  `--auto-companions` / `--one-bend-shots` / `--duel-map thin-fronts` sanity
  runs: fast, a few seconds per 500-tick match. This loop is genuinely good.
- WASM build: 7.4 s cold with `--no-cache` through the platform-matched Docker
  builder. Excellent.
- Qualification suite 5 (which reruns hash-linked T3 and T2 underneath): ~10 s
  wall, 84 s CPU. Also excellent, and the cheapness is why I could afford four
  qualification cycles.

### 7. Mechanical repairs made (all contract-handling, no strategy budget spent)

| # | Symptom | Cause | Artifact after fix |
| --- | --- | --- | --- |
| 1 | Placement-facing logic never fired | `context.Tick - int.MinValue` overflowed the cooldown guard | — (pre-build) |
| 2 | 516 blocked moves per base-contract match | children pathing onto the permanently reserved prime spawn | `978dd2…` |
| 3 | Two-tick block/retry oscillation | only the immediately previous blocked destination was remembered | `978dd2…` |
| 4 | T2 `straight-evade` failed, `damageTaken: 1` | 2-advance threat horizon saw the bolt one tick too late, and movement scoring merely de-prioritised a tile the bolt entered sooner | `96d9ae…` → `f7c94a…` |
| 5 | T4 `entry-initiative` / `map-holdout` failed, `firstLifeObjectiveTick: null` | evasion and long-range fire both outranked walking onto the objective | `f7c94a…` |
| 6 | Base contract stopped fabricating entirely | pad-return sat below fire in the priority chain, so a corridor firefight starved the rebuild clock | `6ede92…` (frozen) |

### 8. Strategy passes

One, as budgeted. The doctrine (queue-first, forward placement, occupier
presence, suppression-not-concession, no anchor, no split) was fixed before the
first build and never revised; everything after it was contract-handling
repair driven by faults, blocked-action counts, or named probe failures.

## Behaviour of the frozen artifact

WASM class mirror (`--classes fabricator-vs-fabricator`, seed 104729,
pre-freeze build): 27 fabrications, 40 destructions, 0 runtime faults, 58
blocked moves in 1776 decisions. Every companion is queued on the exact tick
its slot becomes Ready — ticks 60 and 180 on the class contract, and one
rebuild cycle later after each loss. On hosted-v1 shape the prime walks home to
the pad and queues there instead; on the automatic-companion arm `fabricate` is
absent from the catalog and the same policy simply never asks for it.

Known rough edge, recorded rather than fixed: perfect mirrors deadlock on
same-destination moves at the central choke (both sides pick the same tile).
The blocked-destination memory and the "don't contest a tile adjacent to an
enemy body" guard cut this from ~33% to ~7% of decisions on the hosted map, but
it is a symmetry artifact and it does not appear against the qualification
controllers.
