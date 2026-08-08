# DX notes — spark-line revision 3 (fabricator, `spark-line-v1`)

Population: Frontline Labs classes wave 1, revision 3. Role: verdict-doctrine,
target cumulative T4. Budget: **one** strategic revision; mechanical and
contract repairs free. Frozen revisions 1 and 2 live untouched at
`arena-bots/frontline-labs/classes-wave-1-2026-07-29/spark-line/` and
`.../classes-wave-1-revision-2-2026-07-29/spark-line/`; their DX notes are
preserved there and are not restated except where a friction changed status.

**Isolation statement.** This pass read only the author packet,
`FRONTLINE-LABS-RULES.md`, `EXPERIMENTAL-FRONTLINE-CLASSES.md`,
`templates/botarena-generic-actor/`, `src/BotArena.Sdk/` (types and XML docs),
this entrant's own frozen directories, and replays this entrant produced in
this session. No other entrant's source, no standings, no aggregate balance
report, no engine implementation, no non-assigned replay. Scratch was a
uniquely named private directory (`sandbox/spark-line-rev3-scratch-4b7e2a/`),
never a shared or guessable one. Nothing was committed to git. No accidental
exposure to disclose.

## Frozen identities

| Item | Value |
| --- | --- |
| Entrant | `spark-line` (entry type `SparkLine`), revision 3 |
| Class | `fabricator` (declared in `botarena.json`, unchanged) |
| Canonical artifact | `out/bot.wasm` |
| **bot.wasm SHA-256** | `5b38ee1cfd0f88f16ab58d3ab7620522652c0b9aa6f852f013a53908ff5b8a50` |
| Qualification report | `evidence/t4/qualification.json` |
| qualification.json SHA-256 | `8781d0eb2ea5c6a9024a557fe58736ab03b7ba6973f371b56b34aca0312be4f3` |
| Hash-linked T3 report SHA-256 | `85a7e84a56d688fd6fc557521dfebbe2e53fd349c04a44ae2c87d9dc27fbe091` |
| Hash-linked T2 report SHA-256 | `50c9a29c0b5af56e8cd766de08b681a838e1a15da24c69c37f8e4f98d8f4125e` |
| Source-tree hash (sorted per-file SHA-256, then SHA-256 of that list; `.cs` + `botarena.json` + `README.md`) | `269704e26701061204a74f6b120ff80a0678ce9bd9ddf69e428ad6b6d35911c9` |
| Builder | CLI 0.9.10, SDK 0.10.4, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, platform-matched Docker builder |
| Build cache key | `4fed5bdd3dae3031386dbed458d648dc9f393f0cffa2ed3efb14935a0c448e09` (`build --no-cache`, cache miss, compiled) |
| Submitted source | `SparkLine.cs` (2228 lines), `ContractLens.cs` (517), `Tactics.cs` (336), `ArenaBasics.cs` (1096, template sync) |
| Predecessor artifact (rebuilt r2, this SDK) | `6d53d2c81440ec18b17166947444649c2ede157f2dddb0566c8c0b26f4b1c5f6` |
| Predecessor artifact (r2 as frozen) | `8bb386542d4ef3b203e2885fb643e6cf29cb4f3b4241ea98987050b1e8985290` — **faults on sticky arms; not used for any measurement here** |

Per-file SHA-256:

```text
a05ec4b4ef15753836ee107586fb6442378ed8078a6d4a3cafef9a9a5bd56368  ArenaBasics.cs
e1f4b2b59a56a5b3672bd1d13b6333b2ea56869823768eff9ca68ed418e9a270  ContractLens.cs
d46b791d9007113f0cc36171aef0d8964d79f106ec0a73ba85b64c8da9d78b4f  README.md
d634ae523bfafa5ec31c7fb7e7c6aae84d3d1fe23d3224fbeab69182370b4baf  SparkLine.cs
9a5d157bca2d4737748c0628534bb19a248cf009e01677ba23fa361243f9f19f  Tactics.cs
340a566bf2a177dd4b1b81f2e74dae80aabb106b1d8e70acf98ef19c362c9c95  botarena.json
```

`Tactics.cs` and `botarena.json` are byte-identical to revision 2.

## Qualification outcome

`experiment frontline-labs qualify --bot out/bot.wasm --suite
frontline-qualification-5` (profile `frontline-duel-depth-union-t4-v1`) exits
**0**. **Tier awarded: T4.** `balanceEvidenceEligible: true`, `passed: true`.

| Component | Result |
| --- | --- |
| T2 (`frontline-qualification-3`), hash-linked | PASS |
| T3 (`frontline-qualification-4`), hash-linked | PASS |
| T4 — suppression-choke, entry-initiative, prediction-chamber, front-rotation, map-holdout | PASS (all five) |

The suite runs the duel-depth union profile, which declares no hold and no
weight-scaled control, so every behaviour added this revision switches itself
off inside qualification. That is the intended shape of a contract-driven
change and it is why the tier was never at risk.

## The strategic revision (one pass, as budgeted)

Revision 2's thesis was **presence only pays while it is sole presence**.
Revision 3's is one level up: **a body is only worth more than the ground it is
standing on when the contract says headcount is worth something.**

Two capture fields decide that, and the measured finding of this revision is
that they only pay *together*:

- `capture.ratchetHoldTicks` — a completed advance is protected, so ground
  taken ahead of the front is ground that will not have to be given back.
- `capture.controlPolicy` declaring `net-positive-objective-weight-difference`
  — one enemy body subtracts one instead of nulling everything, so the screen
  that used to be the entire claim becomes something the occupier can absorb.

Sticky ground says a forward investment is *safe*. Contest weight says it is
*affordable*. Neither alone is sufficient, and the artifact behaves exactly
that way: on the unmodified control contract, on the numbers-only contract
(`--capture-threshold 9 --prime-respawn-ticks 9`), and on `--pendulum ratchet`
it is decision-for-decision identical to revision 2. Only on
`--pendulum ratchet-contest` does anything change.

What changes there is three things, all derived rather than tuned:

1. **Take the next ground before you own this one.** Bodies not holding the
   active objective route to the next objective in the chain
   (`activeIndex + this team's declared index delta`), timed so the walk and
   the advance land together: leave when the remaining walk is no shorter than
   the remaining claim. Refused outright when no hold is declared.
2. **Price the fabricator's death by the walk home.** Under
   `lifecycle.automaticReturnPlacement = own-side-chain-adjacent-objective-…`
   the prime returns near the fight and away from the region its fabrication is
   bound to. `ContractLens.WalkToSource` derives that walk from map geometry
   and it scales how reluctant the occupier election is to put the fabricating
   body on the ground. It is zero — and the term vanishes — where arrivals land
   at the spawn anchor, **and also where fabrication is not bound to a region
   at all**, which turns out to include this bot's own class arm. See the
   composition finding below.
3. **A locked advance makes a trip home free of positional risk**, so the
   fabrication-source return may be started for a slot that is not Ready yet.

Whose mark a live hold protects is inferred, and only positively: the active
index moves solely on a real advance, so the sign of the change names the team
that made it, and the advance is *dated* from `ControlResumesAtTick − 1 −
RedeployPauseTicks` so the attribution survives the one-tick observation lag
and can be compared against later ticks. A life created inside a hold window
inherits nothing and cannot derive the owner; it therefore never acts on the
hold at all. See friction #1.

## Measured effect versus rebuilt revision 2

Method: candidate versus **rebuilt** revision-2 source (same SDK, same
toolchain, `--no-cache`), `--movement facing-locked`, six seeds
(104729, 130363, 155921, 181081, 206699, 232391), both sides, controlled WASM
runtime. 48 matches per shape.

Raw records are almost useless here because the map's side bias dominates
them, so the headline statistic is the **paired edge**: a candidate that is
behaviourally identical to its opponent produces exact mirror cells whose
signed territorial progress sums to zero on every seed. The sum is therefore
the candidate's advantage with the side bias differenced out, and a row of
exact zeroes is a positive statement — "no decision changed" — rather than a
missing measurement.

**Two contracts were measured, and they answer differently. Both are reported
because the difference is the most important thing this revision learned.**

### On the fabricator class arm (`--classes fabricator-vs-fabricator`)

| Arm | W-L-D | Paired edge / seed | Per-seed edges | r3 advanced / spent | r2 advanced / spent |
| --- | --- | --- | --- | --- | --- |
| control (unmodified) | 0-0-12 | **0.0** | 0,0,0,0,0,0 | 0 / 0 | 0 / 0 |
| numbers-only (`--capture-threshold 9 --prime-respawn-ticks 9`) | 0-0-12 | **0.0** | 0,0,0,0,0,0 | 0 / 0 | 0 / 0 |
| `--pendulum ratchet` | 6-6-0 | **0.0** | 0,0,0,0,0,0 | 54 / 72 | 54 / 72 |
| `--pendulum ratchet-contest` | 6-6-0 | **0.0** | 0,0,0,0,0,0 | 12 / 12 | 12 / 12 |

**Decision-for-decision identical to revision 2 on all four arms.** Not a
regression and not an improvement: on this arm the revision is inert, for two
structural reasons that are worth more than the numbers.

*First, the class deletes the forward-rally penalty by construction.* The
class arm resolves `fabrication-source`/`fabrication-output` to the region
`fabrication-source-anywhere`, where the base contract resolves both to
`team-0-home-pad`. That is the class table's "the child materializes beside the
prime in the field, never on a protected pad" expressed in the contract — and
it means the fabricator has no workbench to be carried away from. The walk home
is zero everywhere, so the term that prices a forward-rally death vanishes, and
`TryReachFabricationSource` never fires because the body is always already at
its source. Forward rally is a real tax on pad-bound fabrication and **free for
this class**. That composition is invisible in either document on its own.

*Second, the mirror never fields the surplus the change is about.* Across the
representative `ratchet-contest` cell the team held two weighted bodies for 27
ticks of 174, one body for 75, and **zero bodies for 72** — the 2-HP prime
dies, and the fabrication cadence cannot outrun it. A doctrine about where
surplus bodies should go has, on this cell, almost no surplus to direct.
Revision 2's greedy queueing is already maximal here (every legal queue was
taken); what limits the class in its own mirror is survival, not tempo.

### On the base contract (no `--classes`)

| Arm | W-L-D | Paired edge / seed | Per-seed edges | r3 advanced / spent | r2 advanced / spent |
| --- | --- | --- | --- | --- | --- |
| control (unmodified) | 6-6-0 | **0.0** | 0,0,0,0,0,0 | 62 / 1 | 62 / 1 |
| numbers-only | 5-5-2 | **0.0** | 0,0,0,0,0,0 | 0 / 0 | 0 / 0 |
| `--pendulum ratchet` | 5-5-2 | **0.0** | 0,0,0,0,0,0 | 56 / 37 | 56 / 37 |
| `--pendulum ratchet-contest` | **8-4-0** | **+21.8** | 57,16,13,23,8,14 | **70 / 41** | 55 / 61 |

Here the preconditions do occur — three-slot topology, a pad-bound fabrication
region, and enough surviving bodies for a screen — and the change pays: all six
paired edges positive (sign test p ≈ 0.016), 70 captures converted into
advances against revision 2's 55, and 41 wasted against 61. This is also the
contract family the qualification profile uses, so it is not a hypothetical
shape.

"advanced / spent" counts completed captures that moved the front versus
completed captures consumed inside a live hold, summed over both sides of all
twelve matches.

Zero runtime faults in 68,824 WASM decisions; 2.0 % blocked outcomes, which is
the ordinary joint-resolution rate for a doctrine that walks bodies into the
same contested tiles.

The net position I am freezing: a strictly gated change that is provably a
no-op wherever its two contract preconditions are absent, measurably strong
where they are both present, and — on this entrant's own class mirror —
structurally unable to express itself. I would rather freeze that and say so
than tune until the mirror moves.

## What did not work, and why it is worth recording

Four ideas were implemented, measured, and removed. Each was a plausible
reading of the arm, and the negative results are the more useful half of this
revision.

**1. Banking the claim under an enemy hold (removed).** The obvious play: a
capture completing inside an enemy hold is deleted, so park the claim one point
under the threshold by stepping off the objective, and convert the tick the
hold lapses. It measured negative on the controlled runtime (`ratchet` −13.0
paired edge per seed) and it *raised* the number of spent captures rather than
lowering it. Two reasons, and the second is the interesting one:

- pinning the claim at the ceiling means every tick where stepping off is
  unsafe converts immediately into a spent capture, whereas an unmanaged claim
  spends most of its time far from the ceiling;
- more fundamentally, **a spent capture does not cost an advance**. The claim
  restarts and the next one lands. The real comparison is "hold the ground
  badly for the rest of the hold" against "hold it properly and rebuild a claim
  from nothing", and rebuilding takes `threshold / gain` ticks — usually less
  than what is left of a 40-tick hold. Gating the stall on
  `holdRemaining < rebuildTicks` (which is the analytically correct condition)
  did not rescue it either.

The design conclusion is worth stating for the balance lab: **the sticky arm's
headline mechanic is not a claim-management problem.** A hold changes where
bodies should stand, not when a claim should complete, and a bot cannot recover
the wasted ticks by manipulating its own progress.

**2. Stacking the objective under contest-majority (removed).** `one body no
longer nulls two` reads like an instruction to delete the screen split and put
every weighted body on the ground. Measured, it is a loss. The screen is what
prevents the enemy reaching the objective at all, and the extra gain from a
second body does not cover an enemy who now arrives freely.

**3. Reinforcing a *contested* objective under contest-majority (removed).**
The narrower version of the same idea — send the surplus only when the enemy
already has weight on the ground, where the net-weight arithmetic is exactly
what the arm advertises. This was the worst result of the revision: 0-6-0 with
a −30 paired edge, breached from both sides.

**4. Pricing the death by the walk home on *every* arm (kept, but gated).**
Scaling the fabricator's reluctance by `WalkToSource` is a correct reading of
forward rally, but ungated it costs `ratchet` −12.8 per seed while gaining
`ratchet-contest` +21.8. The gate is not arm-sniffing: a companion is worth a
walk only where a companion adds capture pressure, which is the same
`SurplusWeightScalesGain` fact. The same gate applies to the
fabrication-return window.

A methodological note on all four: the first three were selected on
**in-process** measurements that later proved unrepresentative. See friction
#2 — this cost most of the session.

## Frictions, in the order they cost me time

### 1. The hold's owner is the one fact the arm turns on, and it is not published

`capture.ratchetHoldTicks` says how long a hold lasts.
`ControlResumesAtTick` dates its start exactly and — usefully — persists long
after the redeploy pause lapses and is rewritten only by a real advance, never
by a capture that was spent. So any life can compute the hold *window* from a
single observation, including a life created inside it. Excellent design.

What no life can compute is **whose mark the hold protects**, and that is the
only thing that decides whether a capture about to complete is an advance or a
deletion. The active index, the redeploy clock, the claim, the decay clock and
the scoreboard are all identical under either answer. A life that watched the
index move knows; a life created inside the window — which for a fabricator is
most lives, since every companion is a fresh instance with empty private memory
— cannot know and has no team-shared memory to ask.

I measured both guesses. Guessing "enemy" makes fresh bodies hold their own
team's winning push back for up to a full hold. Guessing "mine" spends
captures. Neither is cheap, so the shipped artifact acts only on positive
knowledge and simply declines the decision otherwise — which means the
mechanic is, for a whole class of bodies, unreachable.

One field would close this completely: `holdingTeamId` (or
`holdProtectsTeamId`) on the Frontline mode observation, null when no hold is
live. It publishes nothing that is not already inferable by a life that
happened to be watching, so it leaks no information — it only removes the
arbitrary advantage held by bodies that survived the advance. Without it I
suspect the phase-1 factorial will measure "how often did each doctrine's
bodies happen to witness the advance" as much as it measures doctrine.

A partial workaround exists and I tried it: a claim that reaches the threshold
and resets *without* moving the front or rewriting the redeploy clock is a
spent capture, and only the non-holder can spend one, so observing one names
the owner. It measured worse than declining (`ratchet` −0.5 against +6.5),
because by the time a life has witnessed one the damage is done. Recorded in
case another author finds a better use for the signature.

### 2. In-process and WASM are different games, and only one of them is scored

`FRONTLINE-LABS-RULES.md` says to use in-process "only for fast mechanical
diagnosis" and to confirm in WASM. I read that as a warning about fuel and
memory limits. It is much stronger than that.

On the control contract with `--movement facing-locked`, **in-process
fabricator mirrors deadlock at exactly zero progress for 500 ticks — zero
captures by either side — while the identical artifacts in WASM produce 62
advances per side across the same twelve matches.** In-process also produced
byte-identical outcomes across all three seeds in nearly every cell, so the
whole first round of A/B looked far more decisive than it was; WASM results
vary per seed as expected.

I selected three of the four removed ideas on in-process evidence and had to
discard the conclusions when the WASM matrix disagreed on both sign and
magnitude. That is roughly half the session.

Two asks, either of which would have saved it: make the runtime difference
loud (`nilbots experiment frontline-labs --runtime in-process` could print one
line — "diagnostic runtime: dynamics may differ from the scored WASM runtime;
do not select behaviour on these results"), or document what actually differs.
A 24-cell WASM matrix costs 72 seconds, so there is no real reason for an
author to A/B in-process at all — but the current wording reads like a
performance tip rather than a validity warning.

### 3. `--swap` swaps the artifacts, not the participant IDs

`--swap` is documented as "reverses participant and team assignment". What it
does is exchange which artifact is participant 0 — participant 0 remains team
0, and the *bot* moves. Any accounting keyed on participant ID therefore scores
every swapped cell for the opponent.

This inverted an entire matrix before I noticed, and it inverted it *silently
and plausibly*: the sign flip turned a 3-3 into a 0-6 and I spent time
debugging a doctrine defect that did not exist. The fix is to key on
`header.provenance.participants[].artifactHash`, which is the only unambiguous
identifier in the replay. Worth one sentence in the CLI help ("`--swap`
exchanges the artifacts; participant and team IDs are unchanged — identify your
bot by artifact hash"), and worth knowing that a self-mirror makes the mistake
invisible.

### 4. A `.wasm` spec silently drops the declared class

`EXPERIMENTAL-FRONTLINE-CLASSES.md` says two class-declaring projects need no
`--classes` flag, because "the arm resolves from the manifests". That is true
of *project* specs. A built artifact has no manifest, so
`--bot out/bot.wasm --opponent other.wasm` resolves to the **base** contract —
`frontline-labs-1-experiment-ratchet-facing-locked` rather than
`…-fabricator-vs-fabricator-ratchet-facing-locked` — and runs a different game
with different topology, different form stats and, as it turns out, a different
fabrication binding.

Nothing warns about it. The run prints its ruleset ID, but "experiment" versus
"fabricator-vs-fabricator" is a quiet difference in a line that is already
long, and the rule card tells authors to do their final parity check on
`out/bot.wasm` — which is exactly the spec that loses the class. I measured a
complete 48-match matrix on the wrong contract and only caught it while
verifying the frozen contract fields for these notes.

Two fixes, either sufficient: carry the declared class in the artifact (it is
already a build input), or warn when a `.wasm` spec is used without
`--classes` in a command that supports class arms. The current
`Classes resolved from bot manifests: fabricator-vs-fabricator.` line is
printed on the project path and simply absent on the artifact path; making the
absence explicit — "no class declared by either spec; running the base
contract" — would close it.

### 5. The new pendulum readers are exactly right, and the doc comments carry the doctrine

Unqualified credit where it is due. `Capture()`, `ObjectivePresence()`,
`ArrivalsRallyForward()` and `ExpectedArrivalTiles()` gave me every fact this
revision needed without a single rediscovery, and their XML docs state the
*consequences* rather than the field values — "pricing every capture as an
advance is how a bot overpays for a push", "an absent hold means captures never
lock", "read enemy weight as a lower bound", "only `context.Self.Position` on a
life's first tick is the fact". Revision 2 had to derive the equivalent of
`ObjectivePresence` from the mode observation and wrote a DX friction asking
for exactly this. Resolved.

The one thing I still had to derive by experiment: that a capture is spent iff
it completes at `advanceTick + HoldTicks` or earlier — the boundary is
inclusive. `CaptureRules.HoldTicks` documents the semantics but not the
comparison, and the difference is one tick of stall margin. Twenty-one
completions across one replay settled it.

### 6. Still true from revision 2, unchanged

`ObservedSound.Bearing` remains a documented sensor with no published mapping
from sector index to direction (`hearingBearingModel` is a policy-ID string,
and `ProjectileHeading` uses a differently-named model), so hearing is still
unusable for a bot that will not ship a guess. `--print-candidate-contract`
still emits identity and fingerprints only, so reading the resolved catalog
still means dumping `header.contract` out of a replay — which was again the
first thing I did this pass, and again the highest-value single move.
`Available` still reads as "this will work" and means "individually legal
before the joint step".

## Timing

- Reading the three permitted docs, the new scaffold readers, and dumping the
  resolved contract for all four arms: ~25 min.
- Establishing the arm's mechanics empirically from one mirror replay (hold
  length, inclusive boundary, `ControlResumesAtTick` persistence, the spent
  capture signature): ~15 min, and it settled every question the prose left
  open.
- Implementation across five measured shapes: ~90 min.
- Measurement: in-process matrix 24 cells ≈ 90 s; WASM matrix 48 cells ≈ 145 s.
  Cheap enough that the correct workflow is to skip in-process entirely.
- WASM build: 11–19 s cold with `--no-cache`.
- Qualification suite 5 including hash-linked T3 and T2 reruns: ~35 s wall.

## Behaviour of the frozen artifact

Identical to revision 2 on the unmodified control contract, the numbers-only
contract and `--pendulum ratchet`; on `--pendulum ratchet-contest` it converts
27 % more captures into advances and wastes a third fewer. Zero runtime faults
in 68,824 controlled-runtime decisions.

Known rough edges, recorded rather than fixed:

- Every hold-dependent behaviour is unavailable to a life created inside a hold
  window (friction #1). For this class that is a large fraction of bodies, and
  it is a floor on how much of the sticky arm this doctrine can reach at all.
- On this entrant's own class mirror the revision is inert. The measured
  reasons are structural rather than tunable, and they are set out above; a
  fourth revision that wanted to move that cell should target the fabricator's
  survival, not its tempo.
- The forward investment is timed against the *shortest* walk to the next
  objective and does not model the enemy contesting the body en route. On a map
  with a longer approach than `frontline-labs-01` it would leave too late.
- Perfect self-mirrors on a symmetric map still tend to the tick cap. Against a
  differing opponent this does not arise.
