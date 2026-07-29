# DX notes — spark-line revision 2 (fabricator, `spark-line-v1`)

Population: Frontline Labs classes wave 1, revision 2. Role: verdict-doctrine,
target cumulative T4. Budget: **one** improvement pass; mechanical contract
repairs free. Frozen revision 1 lives untouched at
`arena-bots/frontline-labs/classes-wave-1-2026-07-29/spark-line/`; its DX notes
are preserved there and are not restated here except where a friction changed
status.

**Isolation statement.** This pass read only the author packet,
`FRONTLINE-LABS-RULES.md`, `EXPERIMENTAL-FRONTLINE-CLASSES.md`,
`WASM-DEVELOPMENT.md`, `templates/botarena-generic-actor/`,
`src/BotArena.Sdk/`, this entrant's own directories, and this entrant's own
factorial replays. No other entrant's source, no engine source, no balance
report, no standings table. Scratch was a uniquely named private directory
(`sandbox/spark-line-rev2-scratch-9f3c/`), never a shared or guessable one.
No accidental exposure to disclose.

## Frozen identities

| Item | Value |
| --- | --- |
| Entrant | `spark-line` (entry type `SparkLine`), revision 2 |
| Class | `fabricator` (declared in `botarena.json`, unchanged) |
| Canonical artifact | `out/bot.wasm` |
| **bot.wasm SHA-256** | `8bb386542d4ef3b203e2885fb643e6cf29cb4f3b4241ea98987050b1e8985290` |
| Qualification report | `evidence/t4/qualification.json` |
| qualification.json SHA-256 | `aab9bf95d1b912a6e13f8a34b5d6be602c650e0a05c4e35788365b6728a176d0` |
| Hash-linked T3 report SHA-256 | `5dbecc77362c6b9e482f3b76168af59507663b90c235f7bd121f6b4500d1ce36` |
| Hash-linked T2 report SHA-256 | `f10cca886373d81588f0851092ccf1392998ed9764b6da431f19c2f61bd9b210` |
| Source-tree hash (sorted per-file SHA-256, then SHA-256 of that list; `.cs` + `botarena.json` + `README.md`) | `8cf656efe47fbbed47bcd79fb1c1b50ddebd098fc42a8aed9feac35075d5638f` |
| Builder | CLI 0.9.7, SDK 0.10.4, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, platform-matched Docker builder |
| Build cache key | `688f4491869c76fda1ef6925a822316f97eee85c1c556a6860d769d6d0110a64` (`build --no-cache`, cache miss, compiled) |
| Submitted source | `SparkLine.cs` (1984 lines), `ContractLens.cs` (464), `Tactics.cs` (336), `ArenaBasics.cs` (766, template sync) |
| Predecessor artifact | `6ede923500f7bce21dee6dff5ae61865ed08d30df6f88e95428269b435a9af2c` (revision 1, T2) |

Per-file SHA-256:

```text
9cae31d594188f46403887578c2347307cfecd018b2a9d84e06fc82c09afb194  ArenaBasics.cs
e7fa924eab7f21d8cd541ef8891fa0993c794d665aaa2d6a98d165a13db48380  ContractLens.cs
ad6ea36b04b45a555494e63ccaeac6b978a895db250f34b9c8cb7117d3a4f5c0  README.md
9fee23e1042ff0f601c7a7edcbc021e41bb4f9e94996e811f6d42a736a707c56  SparkLine.cs
9a5d157bca2d4737748c0628534bb19a248cf009e01677ba23fa361243f9f19f  Tactics.cs
340a566bf2a177dd4b1b81f2e74dae80aabb106b1d8e70acf98ef19c362c9c95  botarena.json
```

## Qualification outcome

`experiment frontline-labs qualify --bot <project> --suite
frontline-qualification-5` (profile `frontline-duel-depth-union-t4-v1`) exits
**0**. **Tier awarded: T4.** `balanceEvidenceEligible: true`.

| Component | Result |
| --- | --- |
| T2 (`frontline-qualification-3`) | PASS |
| T3 (`frontline-qualification-4`) — wall-terminated-bend, strict-corner, cadence-parity, cooldown-window, local-form-safety | **PASS (all five, all four cadence cases, `failedCriteria: []`)** |
| T4 (`frontline-qualification-5`) — suppression-choke, entry-initiative, prediction-chamber, front-rotation, map-holdout | PASS |

Revision 1 was held at T2 by exactly one T3 sub-variant while every T4
component already passed; revision 2 clears the ladder without losing a single
previously passing case.

## Did the new diagnostics shorten debugging? Yes — from three sessions to one read.

This is the headline DX finding of the revision, and it is the exact friction
revision 1 filed as #1.

Revision 1 spent three archaeology sessions on `cadence-parity/range-3-harmless`
and still froze with a wrong conclusion recorded in its own DX notes ("I could
produce `damageTaken: 0, dealt: 0` or `1/1` but not both, and both fail"). That
conclusion was wrong, and it was wrong in a specific, instructive way: the metric
vector was consistent with several predicates, and I picked one, and the one I
picked made the probe look unsatisfiable.

Revision 2 spent **one JSON read**. The three new fields do three different jobs
and all three were load-bearing:

- `failedCriteria: ["never-entered-the-shot-declared-remaining-range",
  "made-no-evasive-move-while-the-threat-was-apparent-only"]` named the two
  predicates. The second one immediately reframed the case: the failure was not
  "I dodged badly", it was "I dodged at all".
- `expectation` supplied the *reason* — "its declared remaining travel expires
  before your tile" — which is the mechanical distinction the probe exists to
  teach and the one thing a metric vector cannot carry.
- `resolvedScenario` printed `maxTravelTiles: 3` next to the start positions.
  Revision 1's DX notes record debugging this variant against the hosted range
  and reaching "a confidently wrong conclusion"; that specific failure mode is
  now structurally impossible.

The actual defect, once named, took one guard to fix and was not where revision
1 looked at all. The projectile arithmetic in `Tactics.Threatens` was already
correct: it walks `RemainingTiles` and correctly reports that a bolt at `(14,7)`
with 2 tiles left cannot reach `(11,7)`. Revision 1 never dodged that shot. It
failed because **on tick 0, before any shot existed, it walked east from `(11,7)`
to `(12,7)`** — the one tile inside the controller's 3-tile reach — in order to
open a firing line of its own, and then had to dodge back out on tick 1. Both
failed criteria are downstream of one voluntary step.

Concrete measurement of the improvement: revision 1 logged ~3 sessions of
replay archaeology across several probes; revision 2's entire probe-diagnosis
cost was one `python3 -c` over `qualification.json`. **Friction #1 from
revision 1 is resolved.** Please keep these fields.

One residual request, small: `failedCriteria` names the predicate but not the
observed-versus-required values. `"never-entered-the-shot-declared-remaining-range
(entered (12,7) on tick 0; remaining travel covered (13,7),(12,7))"` would close
the last inference gap. It would not have changed this revision's outcome.

## The strategic revision (one pass, as budgeted)

Revision 1's doctrine counted presence. It never asked whether presence was
*paying*. The revision names that distinction — **presence only pays while it
is sole presence** — and derives three regimes from it. The cadence-parity fix,
the movement-arm adaptation, and the loss forensics are all the same idea, which
is why this is one pass and not three.

### Regime 1 — holding alone: stand where the walls do the work

`ContractLens` now precomputes a per-tile **exposure** map once per life: the
number of `(origin, heading)` firing lines the map geometry permits onto each
tile, bounded by the widest `maxTravelTiles` any attack profile in the resolved
catalog declares and terminated by the same wall and strict-corner rules a real
bolt obeys. `TryImproveStance` walks the occupier to the least-exposed tile of
the region it already holds, never leaving the region, so presence is never
interrupted.

This exists because of the forensics below: it is the only tool that works
against a gun that outranges the sensor. On the standard centre region it is the
difference between row 7 (the map's one fully open corridor, reachable from
almost anywhere) and row 8, which walls close off at `x=8,9` and `x=13,14` —
`(11,8)` in particular can only be shot from inside the objective itself. That
whole analysis is derived, not encoded: the bot computes it from `tileRows` and
the declared range, and it recomputes for the thin-fronts and outer-shoulder
maps and for every probe map without knowing their names.

### Regime 2 — holding alone and safe: spend nothing (the cadence-parity fix)

Two contract-driven predicates:

- `Tactics.WithinDeclaredRemaining` walks a bolt's own `RemainingTiles` budget,
  so a shot two advances out that expires a tile short is correctly *not* a
  reason to do anything.
- `Exposed(tile)` adds visible enemy bodies, using each enemy's own declared
  `maxTravelTiles` and corner rule along a clear eight-way ray, deliberately
  ignoring the enemy's current facing — a rotation is one action, so a body's
  reach is its range, not its aim.

The rule: while I hold the objective and nothing can reach my current tile, a
step to a tile something *can* reach is refused. It applies to
`TryUnstickTheGun` and `TryImproveStance`, which are the two things that
voluntarily spend a tile while winning. This is a real doctrine claim, not a
probe patch: with symmetric ranges, walking into your own firing range walks
into theirs, and the capture clock was already running my way.

### Regime 3 — contested: tiles are cheap, lines are everything

When the ground is shared, nobody scores, so every tile-safety veto that is
correct while winning is exactly what produces a 400-tick draw. Under contest
`TryUnstickTheGun` keeps only the lethality veto, drops the
"don't step next to an enemy body" guard, and runs during weapon cooldown too.

Contest is detected two ways, and the second sees through fog: a visible enemy
with objective weight standing in the region, **or** the mode reporting that
nobody is accumulating progress while I have stood on the active region with
positive weight for two ticks and control has resumed. Sole presence would have
made the claim mine, so an absent claim is somebody else's body. That inference
is free, exact, and available through a facing quadrant that cannot see the
contester — which turned out to matter more than anything else in the revision.

### Regime 3b — and when the contest cannot be seen, look for it

The single highest-value finding of the forensics. Every downstream tactic in
revision 1 required `context.Enemies` to be non-empty, so a contest with no
visible enemy fell straight through to `wait`. `TrySweepForContester` rotates
toward the nearest active-objective tile that `context.VisibleTiles` reports as
unseen. One tick, no movement risk, and it converts an unbreakable draw into a
duel. Measured on the thin-fronts class mirror: shots per match went 78 → 232.

## Loss forensics (own factorial replays only)

Revision 1 went **12-27-15** across 54 own-entrant replays. Reading them by
candidate cell rather than in aggregate made the shape obvious:

| Cell | Revision 1 record |
| --- | --- |
| vs bulwark, current / outer-shoulder | 12-0-0 |
| vs bulwark, thin-fronts | 0-3-3 |
| fabricator mirror, current | 0-3-3 |
| fabricator mirror, outer-shoulder | 0-0-6 (all zero-progress) |
| fabricator mirror, thin-fronts | 0-6-0 |
| **vs striker, all three maps** | **0-12-0** |

Three findings, and the revision addresses all three:

**1. The striker column is a sensor problem, not a duel problem.** Striker
reaches 8 tiles; my facing quadrant sees 6. In `007--spark-line-vs-still-water`
(breach at tick 193) my action mix was **90 waits out of ~171 turns**, and my
side was empty of living bodies at ticks 25, 50, 125 and 175. A 2-HP prime
standing still in the map's one open corridor is free damage for a gun it cannot
see. No amount of aiming fixes that; standing somewhere else does, which is
regime 1.

**2. The all-draw bypass mirror was two blind bots, not a standoff.** In the
outer-shoulder mirror, from tick ~100 to 499 my three bodies sat frozen and the
action mix was **1080 waits, 34 shots, 2 fabrications**. On thin-fronts the
active region is the three-tile column `(11,6) (11,7) (11,8)`; my body held
`(11,8)` facing east and the enemy held `(11,6)` facing west, two tiles apart,
each outside the other's quadrant and outside proximity-1. Neither could see the
other, so neither could shoot, aim, or reposition — and a cardinal straight gun
cannot hit a diagonally adjacent body at all, which is what revision 1's
"never step next to an enemy body" guard was blocking the fix for. Regimes 3
and 3b.

**3. Seed-identical results were the side bias, visible.** All three seeds
produced byte-identical outcomes in essentially every cell, and the fabricator
mirror on `current` split by side (draw as team 0, loss as team 1) with nothing
else different. That is the systematic absolute-direction preference the current
template documents as a 40-of-40 side sweep. Adopting `OrderedDirections` for
every tie-break replaces it with per-life seed noise that mirrored accounting
can wash out; the self-mirror is no longer a fixed point.

## Movement-arm adaptation

`facingCoupling` is read from the movement profile the current form references
(`ContractLens.CouplingFor`), never assumed, and an absent field correctly means
`PreserveFacing`.

- **`FaceMovementDirection`** — a step is a turn. Every destination is now scored
  with the facing the step will actually leave behind (`FacingAfterStep`) instead
  of the four facings revision 1 optimistically searched; searching all four
  flattered every move, because a line that needs a rotation first is not a line
  this tick. This was a latent correctness bug in the `PreserveFacing` baseline
  too, so the coupling work paid for itself immediately. For forward
  fabrication, the placement facing is the direction of the last successful move,
  so `StepAlong` tie-breaks the final approach step by where the child would
  land — the approach buys the pose the rotate used to cost.
- **`FacingLocked`** — the movement mask offers only the current facing, so a
  direction it does not offer is reached by turning first. `MoveOrTurn` makes
  that explicit and priced (`"turning to walk: …"`), and evasion only pays for a
  turn when the bolt is still more than one advance out. Without this the bot
  would silently refuse to path whenever its facing disagreed with the route.
  Measured on the facing-locked mirror: 76 turn-to-walk rotations, 0 runtime
  faults, 11 blocked outcomes in 635 decisions.

Both arms verified by in-process mirror (`--movement move-sets-facing`,
`--movement facing-locked`): zero runtime faults, zero blocked-action storms,
and both arms produce a moving front rather than a freeze.

## Template sync and reconciliation

`ArenaBasics.cs` is synced verbatim from `templates/botarena-generic-actor/`
and is a submitted source file. The policy calls
`ArenaBasics.OrderedDirections` once per tick and uses the result for **every**
direction tie-break (evasion, objective entry, stance, alignment, pathing) and
for the residual ordering term in `SafetyScore`, replacing revision 1's
absolute `(int)direction`.

Reconciliation notes on the other three template changes:

- `TryDirectShot` now fires parameterless straight attacks. That closes
  revision 1's friction #2 for the scaffolded bot. SparkLine already handled the
  zero-parameter `shoot-straight` shape in `EvaluatePrograms` (it omits the
  payload when the action declares no `ShotProgram` parameter kind), so this is
  a template fix I no longer depend on — but it is the right fix and the class
  addendum and the helper now agree.
- `ClassOf` / `Capabilities` are read and deliberately unused. SparkLine already
  branches on the underlying facts (`ObjectiveWeight`, `AttackProfile`,
  `FabricationTransitions`, `LifecycleAssignment.UnlockTick`) rather than on a
  class name, which the class addendum explicitly recommends; routing through a
  digest that returns a string prefix would be a step *toward* name-keying.
- `ArenaBasics.TryDodge` compares against `RemainingTiles` correctly. Worth
  recording that the helper was already right about the exact thing
  `cadence-parity` tests, and revision 1 was too — the bug was never in the
  projectile arithmetic.

## Mechanical repairs made this pass (no strategy budget)

| # | Symptom | Cause | Fix |
| --- | --- | --- | --- |
| 1 | Every trajectory score for a candidate destination was optimistic | `KeepsFiringSolution` and `TryUnstickTheGun` searched all four facings at a tile the body would arrive at with exactly one | score with `FacingAfterStep`; omnidirectional profiles keep the all-headings check via `AnyHeadingHits` |
| 2 | Facing-locked profiles could not path at all | every movement loop filtered on `allowed.AllowedValues`, which offers only the facing in that arm | `CanReach` + `MoveOrTurn` |
| 3 | `ImpactIn(self)` recomputed once per candidate direction in `StepAlong` | loop-invariant call | hoisted to `stayImpact` |
| 4 | Absolute direction order as the universal tie-break | measured team-side bias | `ArenaBasics.OrderedDirections` |

## Frictions, in the order they cost me time

### 1. (RESOLVED) Probe reports emit metrics but never the failing predicate

Filed by revision 1; fixed by the suite. See the section above — this was the
single largest DX improvement between revisions and it turned a wrong frozen
conclusion into a one-read fix. The residual ask is observed-versus-required
values inside `failedCriteria`, which is a refinement, not a gap.

### 2. Contest is observable, but only by inference

`ModeObservationState.Frontline` gives `ClaimingTeamId`, `CaptureProgress`,
`DecayTicksElapsed` and `ControlResumesAtTick`, and from those plus my own
position I can *derive* "someone else is standing here". That derivation is the
most valuable single fact in the whole revision — it is what lets a
facing-quadrant bot react to a contester it cannot see — and nothing in the rule
card or the SDK docs points at it. The rule card says contested control decays
progress; it does not say the observation therefore reports the contest. A
sentence in `FRONTLINE-LABS-RULES.md` under "Objective and ending" ("an absent
claim while your own weighted body stands on the active position is another
team's body, whether or not you can see it") would hand every author the same
insight for free, and I suspect the zero-progress mirror is a common wave-1
shape precisely because it is currently discovered rather than told.

### 3. `HeardSounds` is the obvious answer to being sniped, and I could not use it

`hearingRadius: 8` exactly covers the striker's `maxTravelTiles: 8`, so hearing
is clearly designed to be the counter to being outranged by a gun the quadrant
cannot see. But `ObservedSound.Bearing` is documented as "coarse sector index
under the observer's vision profile", and the profile publishes
`hearingBearingModel: "eight-octants-strict-two-to-one-cardinal-v1"` and
`hearingBearingSectors: 8` — a policy ID string with no published mapping from
sector index to direction. `ProjectileHeading` uses a *different* named model
(`eight-way-clockwise-modulo-v1`), so assuming the two indices agree is a guess,
and a guess about which way to look is a guess I would rather not ship into a
balance population. I built the exposure map instead, which needs no mapping and
works when nothing is audible either. Publishing the sector-zero direction and
the winding order — one sentence, or an SDK helper `ProjectileHeading
FromBearingSector(int, string model)` — would unlock an entire documented sensor
that is currently decorative.

### 4. Still true from revision 1, unchanged

`Available` reads as "this will work" and means "individually legal before the
joint step"; reserved spawn tiles are a static fact expressed as a policy-ID
string rather than a tile tag; `RelativePositionOffset(Forward, Right)` is
facing-relative while the child's own facing is team-relative; unlock ticks,
`maxTravelTiles` and pad ownership are all documented in prose in values that
differ per arm and are therefore all hardcoding traps. Full detail in revision
1's DX notes; none of it regressed and none of it changed.

### 5. Dumping the resolved contract still requires a replay

`--print-candidate-contract` emits identity and fingerprints only. Every actual
catalog — forms, attack profiles, vision profiles, movement profiles, actions —
has to be read out of `replay.json`'s `header.contract.rules`. That was the
highest-value single move of revision 1 and it was again the first thing I did
this pass (it is how I confirmed `movementProfiles` publishes no
`facingCoupling` on the class arm, i.e. `PreserveFacing`). It is not mentioned
anywhere an author is permitted to read. A `--print-candidate-contract --full`
would save every author the same detour.

## Timing

- Reading the new probe diagnostics and locating the actual defect: **~5 min**
  (revision 1: ~3 sessions).
- Own-replay loss forensics across 54 factorial replays, aggregated by candidate
  cell then drilled into three representative matches: ~35 min. Aggregating by
  cell first was what made the striker column and the frozen mirror visible;
  the flat 12-27-15 record says nothing.
- Implementation: ~50 min.
- In-process mirror iteration (class arm, three duel maps, two movement arms):
  ~2.7 s per 500-tick match. Still the best part of this toolchain.
- WASM build: 7.9 s cold with `--no-cache`.
- Qualification suite 5 including hash-linked T3 and T2 reruns: ~19 s wall,
  85 s CPU. Cheap enough to run after every behavioural change, which I did.

## Behaviour of the frozen artifact

WASM class mirror (`fabricator-vs-fabricator`, seed 104729): 0 runtime faults,
157 blocked outcomes in 1961 decisions, decisive at the tick cap
(TerritorialProgress 16 / −16) where revision 1's outer-shoulder mirror was a
0/0 freeze.

In-process mirror action mix versus revision 1 on the same seed, same map:
waits 1080 → 431, shots 34 → 94, fabrications 2 → 13, plus 66 alignment steps
and 11 cover steps that revision 1 had no concept of.

Known rough edges, recorded rather than fixed:

- Perfect self-mirrors on a symmetric map still tend to the tick cap. With
  identical policies and now-identical exposure maps, both sides pick
  symmetric answers; `OrderedDirections` breaks the tie into seed noise rather
  than into a decision. Against a differing opponent this does not arise, and
  the dynamics underneath are no longer passive.
- `TryImproveStance` is deliberately timid: it never runs with a bolt in flight
  (a step then reads as a dodge, and dodging a shot that cannot reach you is the
  exact mistake this revision fixed) and, with an enemy watching, only when the
  current tile is already inside that enemy's reach. There are probably safe
  re-stances it declines. That is the right side to err on.
