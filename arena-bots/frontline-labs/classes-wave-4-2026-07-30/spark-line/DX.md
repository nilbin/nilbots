# DX notes — spark-line revision 4 (fabricator, `spark-line-v1`)

Population: Frontline Labs classes wave 4, revision 4. Role: verdict-doctrine,
target cumulative T4. Budget: **one** strategic revision; mechanical and
contract repairs free. Frozen revisions 1–3 live untouched at
`arena-bots/frontline-labs/classes-wave-1-2026-07-29/spark-line/`,
`.../classes-wave-1-revision-2-2026-07-29/spark-line/` and
`.../classes-wave-1-revision-3-2026-07-29/spark-line/`; their DX notes are
preserved there and are not restated except where a friction changed status.

**Isolation statement.** This pass read only the wave-4 brief, the author packet
(`FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aa…`),
`FRONTLINE-LABS-RULES.md` (`06ff461e…`), `EXPERIMENTAL-FRONTLINE-CLASSES.md`
(`b91047df…`), `templates/botarena-generic-actor/`, `src/BotArena.Sdk/` (types
and XML docs), this entrant's own frozen directories, and replays this entrant
produced in this session. No other entrant's source, no standings, no aggregate
balance report, no engine or App implementation, no non-assigned replay. Every
sparring opponent was this entrant's own predecessor rebuilt from source, or a
variant of this entrant's own source. Scratch was a uniquely named private
directory (`sandbox/spark-line-w4-scratch-9c3f71d/`), never a shared or
guessable one. Nothing was committed to git. No accidental exposure to
disclose.

## Freeze identity

| Item | Value |
| --- | --- |
| Entrant | `spark-line` (entry type `SparkLine`), revision 4 |
| Class | `fabricator` (declared in `botarena.json`, unchanged since revision 1) |
| Canonical artifact | `out/bot.wasm` |
| **bot.wasm SHA-256** | `b5c328875993fd69f2b8d5ba7ca54eb91da1feb26b3a69d6cbb9ea76d3861f4a` |
| Build | `build --no-cache`, cache miss, compiled; key `e8393456b94872eba2dea73a77591b527ea9ec1d8f9a6ca424c0150515caab02`; re-run reproduced the same artifact hash |
| Builder | CLI 0.9.15, SDK 0.10.6, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, platform-matched Docker builder |
| Qualification | `experiment frontline-labs qualify --suite frontline-qualification-5` → **exit 0**, tier **T4**, `passed: true`, `balanceEvidenceEligible: true` |
| Report | `evidence/t4/qualification.json`, sha256 `b6f792fd4d6a44576820bd6bf67e91e834441e87c7b87edf708812d371ffad6d` |
| Hash-linked T3 report | `ee1048c01fd36bccd805726492bf60b1fe74f05a93d9b953d5f83d39a29ad1df` |
| Hash-linked T2 report | `1c376b7604dd19c47ec5ca2f71f3d8bdf6bdc06ed3bdd126a325bf7977952ad1` |
| Source-tree hash | `f0a3b12cb770d3e403fd6413ba83f5c02a017d5592f2e2c5ef91f0086860949e` |
| Submitted source | `SparkLine.cs` (3117 lines), `ContractLens.cs` (627), `Tactics.cs` (394), `ArenaBasics.cs` (1205, template sync) |
| Sparring baseline | revision-3 source **rebuilt** on this SDK, `--no-cache` → `03f4cbd4477e3f4e0620223f8c86951bc493667dfa67f5013aab502f1f1c105e` |
| Revision 3 **as frozen** | `5b38ee1cfd0f88f16ab58d3ab7620522652c0b9aa6f852f013a53908ff5b8a50` — **faults at tick 0** on this contract generation (`WASM generic actor exited before its life ended`, peak fuel 0.0M/200.0M). Confirmed once, then never used for any measurement. |

Per-file SHA-256:

```text
567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627  ArenaBasics.cs
0ecb427c420ec0cbe82a2431742cd4cbfc159e3ee0b4a8c96730a5b9a94bb94e  ContractLens.cs
7158d88b735409fc9fe432ec3d428fe2f2be7e0a47cd0465a39cc647b61b5fdb  README.md
8468be05a5995556ce0164b110faa01734746b8a3728da95c44006dabffd22d9  SparkLine.cs
049b42b422c704c48a825205edcf7cb1c2acf9974a13bee33a6b30e7c74bd4b9  Tactics.cs
340a566bf2a177dd4b1b81f2e74dae80aabb106b1d8e70acf98ef19c362c9c95  botarena.json
```

`botarena.json` is byte-identical to revisions 2 and 3. `ArenaBasics.cs` is the
current template verbatim (1205 lines, up from 1096 — the new `LiveHold` and
`Threat` readers).

## Doctrine, in one paragraph

Under keel every counterweight points the same way: the only currency that
cannot be taken from you is surviving objective weight, so the question a
surplus body asks is whether it belongs *on* the ground or on a different
bearing to it — and the contract answers that with one number, the count of
distinct trajectories one gun can put on the board from a fixed pose. A
single-trajectory gun covers one lane, so two bodies on one bearing are one gun
and the team's reach is the number of distinct bearings it occupies: hold with
one body and separate the rest by more than the widest simultaneous heading fan
the ruleset declares, because that fan (and a guard's frozen quadrant, and a
bolt stopping on the first enemy body) is exactly what answers two bodies with
one decision. A gun that bends covers several lanes from one facing, so coverage
stops binding and the capture clock starts: concentrate to precisely the weight
the arithmetic pays for — the enemy's observed weight plus one, or bare parity
inside an enemy hold where my own completion would be spent and only his gain
needs freezing — and drop the separation penalty, which now costs the very
concentration that weight-scaled capture multiplies. **Concentrate what the gun
can defend, spread what it cannot**, with the branch chosen by the shot
envelope rather than by an arm name; on top of that, ask the observation for the
hold's owner and expiry instead of inferring them from an advance this life may
never have witnessed, price every bolt by its own published cadence and damage,
treat every declared spawn reservation as a tile that will refuse the step, and
recognise a volley by its fan, a shell by its guard and a stance by its
automatic-return budget rather than by anyone's class name.

## Measured records — candidate vs rebuilt revision 3

Method: both artifacts built from source with `--no-cache` on the same SDK,
`--classes fabricator-vs-fabricator --movement facing-locked`, the four phase-2
arm combos, **six seeds** (104729, 130363, 155921, 181081, 206699, 232391),
**both sides**, controlled WASM runtime. 12 matches per cell, 48 total.

Side accounting is keyed on `header.provenance.participants[].artifactHash`, not
on participant ID (see friction #4 of revision 3 — still true). The headline
statistic is the **paired edge**: the sum over both sides of a seed of
(candidate signed territorial progress − baseline's). Mirror-symmetric map bias
differences out, so a row of exact zeroes means "no decision changed" rather
than "not measured".

| Arm combo | registered token | W-L-D | Paired edge / seed | Per-seed edges | seeds +/− |
| --- | --- | --- | --- | --- | --- |
| kit off, bend striker-only | `keel` | **9-3-0** | **+55.7** | 106, 106, 18, 18, 18, 68 | 6 / 0 |
| kit on, bend striker-only | `helm` | **12-0-0** | **+108.3** | 120, 120, 110, 90, 98, 112 | 6 / 0 |
| kit off, bend universal | `veer` | **11-1-0** | **+87.0** | 120, 64, −22, 120, 120, 120 | 5 / 1 |
| kit on, bend universal (primary) | `rig` | **10-2-0** | **+89.7** | 120, 18, 40, 120, 120, 120 | 6 / 0 |

Overall **42-6-0**. Sign test on the paired edge: 6/6 positive on `keel`,
`helm` and `rig` (one-sided p ≈ 0.016 each), 5/6 on `veer` (p ≈ 0.11). Three of
the four cells reach a positive edge on every seed, which is the strongest form
six seeds can produce; `veer` loses seed 155921 by 22, and that seed is also the
weakest one on `rig` (+40 rather than +120), so the shape of the loss is one
seed rather than one arm.

Capture conversion, summed over both sides of all 12 matches per cell
("advanced" = completions that moved the front, "spent" = completions consumed
inside a live hold):

| Arm | candidate advanced / spent | baseline advanced / spent |
| --- | --- | --- |
| `keel` | **47 / 70** | 33 / 32 |
| `helm` | **45 / 63** | 24 / 40 |
| `veer` | **38 / 27** | 19 / 23 |
| `rig` | **38 / 30** | 20 / 24 |

The candidate roughly doubles advances on every cell. It also spends more
captures in absolute terms on `keel`/`helm`, and that is not a defect: it
completes far more captures overall, so more of them land inside somebody's
40-tick hold. Revision 3 already established that a spent capture does not cost
an advance — the claim restarts and the next one lands — so the ratio worth
reading is advances, and it moved from 33→47, 24→45, 19→38, 20→38.

Skill and behaviour counts, both sides, all 12 matches per cell:

| Arm | volleys cast | shells raised / broken | slots fielded (mean per side per match) | bends fired |
| --- | --- | --- | --- | --- |
| `keel` | 0 | 0 / 0 | 3.00 of 3 | 0 |
| `helm` | 0 | 0 / 0 | 3.92 of 5 | 0 |
| `veer` | 0 | 0 / 0 | 2.75 of 3 | 206 |
| `rig` | 0 | 0 / 0 | 3.25 of 5 | 207 |

**Volleys and shells are zero by construction, not by omission.** On
`fabricator-vs-fabricator` the whole kit *is* five slots — the volley belongs to
the striker and the shell to the bulwark — and the resolved contract carries an
empty `sameLifeTransitions` array, so there is no stance to enter on any of the
four cells this brief measures. **Bends are zero on `keel`/`helm` for the same
kind of reason**: without `--bend universal` the fabricator's action is the
parameterless `shoot-straight` and `shotProgram.enabled` is false, which is also
precisely what selects the spread branch of the doctrine there.

Runtime health: **zero faults in 45,206 controlled-runtime decisions** across
the 48 matches (23,194 of them the candidate's). Blocked outcomes 792 total
(1.75 %; candidate 418 / 23,194 = 1.80 %), the ordinary joint-resolution rate
for a doctrine that walks bodies into the same contested tiles.

### Off-class robustness probe (not one of the four cells)

The stance code cannot execute on a fabricator, so it was exercised by running
**this same artifact on both sides of the other class pairs** under `rig` —
still only this entrant's own artifact, on a chassis it does not own. Three
seeds each, 15 matches, 12,150 decisions, **zero faults, zero rejected or
invalid actions**:

| pair | volleys entered / cast | shells raised | deflections |
| --- | --- | --- | --- |
| `striker-vs-striker` | 96 / 96 | — | — |
| `fabricator-vs-striker` | 3 / 3 | — | — |
| `bulwark-vs-bulwark` | — | 0 | 0 |
| `bulwark-vs-fabricator` | — | 0 | 0 |
| `bulwark-vs-striker` | — | 0 | 0 |

Every volley entry converted into a cast through the engine's own
`automatic-threshold-return`; none was wasted. The shell's zero is discussed in
friction #2 — it is a real interaction between the doctrine and a tile tag, and
it survived an attempt to force it.

## The one strategic revision, and the ablation that chose it

The revision was implemented as two rules and then measured **separately**,
because the first full candidate lost badly (`rig` −93.0 per seed, 0/6 seeds
positive) and the loss had to be attributed rather than tuned away. Each row is
the same 6-seed, both-sides, 48-match WASM matrix.

| configuration | `keel` | `helm` | `veer` | `rig` |
| --- | --- | --- | --- | --- |
| repairs only (hold read, per-bolt threat, reservations) | +1.3 | +28.7 | 0.0 | 0.0 |
| repairs + slot rebuild cost | +1.3 | +35.7 | 0.0 | 0.0 |
| repairs + **weight target**, ungated | **−62.7** | **−34.0** | **+87.0** | **+89.7** |
| repairs + **bearing spread**, ungated | **+55.7** | **+108.3** | **−42.0** | **−33.3** |
| both ungated, plus rebuild cost (first candidate) | −44.3 | −50.0 | +9.0 | −93.0 |
| **gated by shot envelope (shipped)** | **+55.7** | **+108.3** | **+87.0** | **+89.7** |

The two halves of the revision are almost exactly antisymmetric across the bend
factor, and applying both at once is worse than either. That is the finding, and
it is why the shipped doctrine is a single branch on a contract field rather
than two behaviours added together:

- **Weight target** (hold the objective with the enemy's weight plus one) pays
  +87/+90 where the gun bends and costs −63/−34 where it does not. A stacked
  body under a straight-only cardinal gun is a body whose one lane is the lane
  its neighbour already covers; it adds capture pressure it cannot defend.
- **Bearing spread** (separate weighted bodies by more than the widest declared
  fan, and prefer distinct objective tiles by election ordinal) pays +56/+108
  where the gun is straight-only and costs −42/−33 where it bends. Once a body
  covers five trajectories, separation stops buying reach and starts costing the
  concentration the capture arithmetic multiplies.

Revision 3 had already measured the ungated weight idea twice as a loss on
`ratchet-contest` (once broadly, once narrowed to a contested objective) and
recorded it as removed. That conclusion was right for the arm it was measured
on and wrong as a general rule; the missing variable was the gun, not the
capture policy. Recording it that way is the most useful thing in these notes.

The two arms whose behaviour is *unchanged* by the shipped gate are also worth
naming: `veer` and `rig` never engage the spread branch, and `keel` never
engages the weight branch, so on each cell exactly one half of the revision is
live — and the repairs are live on all four.

### What did not work, and is not shipped

**1. Pricing a slot by its rebuild clock (measured, removed).** The five-slot
arm gives the late slots a 30-tick rebuild clock against the early slots' 15, so
those bodies genuinely are dearer to lose, and ranking them behind the cheap
ones for the exposed ground reads as obviously correct. Measured, it is a net
loss: it took `helm` from +108.3 to +90.3 and `rig` from +89.7 to +87.0, and it
flipped one `helm` seed from +112 to 0. It is inert on the three-slot arms,
where every child shares one lifecycle profile. The reason is a timing
mismatch — under weight-scaled capture a thinner objective costs gain on the
very next tick, while a rebuild clock is a contingent future loss, and the body
already standing on the ground is worth more than a cheaper body two tiles away
regardless of what either costs to replace. The contract readers for it were
deleted rather than left dead; the negative result is recorded in
`OccupierRank`'s comment where the term would have gone.

**2. Bracing a shell against a body rather than a bolt (measured, removed).**
Raising the arc whenever a visible enemy has a clear in-range line into the
guarded quadrant produced **1086 entries and 1080 immediate exits across three
matches** of an off-class bulwark mirror — a body that spends its match in the
doorway. Adding contract-derived hysteresis (never leave inside the
exit-plus-entry windup, which is the punish window the document itself names)
halved it to 546/540 and no more. The asymmetry that actually fixes it is the
honest one: an enemy's cooldown is redacted, so a line is a possibility and a
bolt in flight is a commitment. Entry now requires a bolt; exit still settles
for the absence of one. The hysteresis was kept anyway, because it is cheap and
because the punish window is a real price whatever triggers the entry.

## Frictions, in the order they cost me time

### 1. Two correct ideas measured as one candidate is a wasted matrix

This is a methodology friction rather than a platform one, but it is the one
that cost the most, so it goes first. The first candidate bundled two
independently-motivated rules and lost on three of four cells; nothing in the
result said which rule was wrong, and the two turned out to have opposite signs
across the bend factor. Four single-factor matrices (≈70 s each including the
build) recovered the whole picture and produced a better artifact than either
rule alone.

What would have prevented it is a habit rather than a feature: **a phase-2 cell
factorial deserves a per-rule factorial inside it.** The tooling already makes
this nearly free — the `--pendulum`/`--skills`/`--bend` flags compose, a 48-match
WASM matrix costs 50 s wall on this machine, and `--no-cache` builds are 10 s. If
the balance lab wants authors to attribute their own regressions, saying so in
the packet next to "implementation, mechanical-repair, and improvement budgets"
would be enough. It is currently implied by `docs/EVALUATION-METHODOLOGY.md`,
which authors are not given.

### 2. The aegis shell cannot be raised where the ground is

Every objective tile on `frontline-labs-01-classes` carries the
`transition-placement-forbidden` tag — all 22 of them, verified from the map
tags — and both `shell-bulwark-*` routes declare that tag in
`placement.forbiddenTileTags`. So the stance that keeps objective weight 1
specifically so that it can hold ground **cannot be entered on any objective
tile**. Measured on a doctrine that lives on the objective, `transform` offering
a shell was available on 1200 of 4317 bulwark body-ticks (28 %); of those, 243
had any hostile bolt visible at all, and none was a committed bolt on a guarded
bearing into the body's own tile.

That is not a bug — the shell is plainly designed as an approach and corridor
tool — but the class table's framing ("objective weight stays 1, so it still
holds ground") reads like the shell is a way to hold the objective, and it is
not one. One sentence in the `shell` row would close it: "like Anchor, the
stance is illegal on every transition-forbidden tile, which includes every
objective tile." Without it, an author reading only
`EXPERIMENTAL-FRONTLINE-CLASSES.md` will write objective-holding shell doctrine
and get a legality mask that silently never offers it. The `automaticReturn`
prose is unusually good precisely because it states this kind of consequence; the
placement rule deserves the same treatment.

### 3. `holdOwnerTeamId` / `holdEndsAtTick` are exactly the right two fields

Unqualified credit. Revision 3's DX asked for one field — "`holdingTeamId` on
the Frontline mode observation, null when no hold is live" — and argued it leaks
nothing because it is already inferable by a life that happened to be watching.
Both halves shipped, they travel together or not at all, `holdEndsAtTick` reads
with the same grammar as `controlResumesAtTick`, the lapse publishes an ordinary
`mode-changed`, and `ArenaBasics.LiveHold` is the one-line version with a doc
comment that says what the derivation it replaces got wrong. Revision 3's
inference is now a fallback nobody reaches; I kept it only for a contract that
declares `ratchetHoldTicks` while publishing no live hold.

The measured value of the change on its own is in the ablation table above:
+28.7 per seed on `helm` and +1.3 on `keel`, with `veer` and `rig` exactly zero.
The asymmetry is itself informative — the five-slot arm is where a fabricator
makes the most fresh lives, and a fresh life is exactly the body that could not
derive the owner before.

Two smaller readers landed with it and both are right. `ticksPerAdvance` and
`damagePerHit` per projectile let a dodge be denominated in ticks rather than
advances; on every contract in this brief every profile advances once per tick,
so I verified the rewrite is decision-for-decision identical here (0 differing
decisions in 5,638 across four matches on `keel` and `rig`) and it stops being
identical the first time two cadences ship. `spawnReservation` on an observed
tile replaced a guess: an ally's return anchor, an outstanding fabrication
bundle and an enemy's queued output are three different facts with the same
consequence for a step, and one nullable field covers all three.

### 4. A `.wasm` spec still silently drops the declared class

Unchanged from revision 3's friction #4, and it is the one I still trip over.
`--bot out/bot.wasm --opponent other.wasm` with no `--classes` resolves to the
**base** contract, because an artifact carries no manifest, while the rule card
tells authors to do their final parity check on exactly that spec. The run
prints `Classes resolved from bot manifests: …` on the project path and prints
nothing at all on the artifact path, so the difference between
`frontline-labs-1-fabricator-vs-fabricator-rig-facing-locked` and
`frontline-labs-1-experiment-rig-facing-locked` is a quiet substring in an
already-long line. Making the absence explicit — "no class declared by either
spec; running the base contract" — would close it. I now pass `--classes`
explicitly in every scripted run, which is a workaround rather than a fix.

### 5. Small things that cost real minutes

- **The CLI binary is named `botarena`, the tool calls itself `nilbots`.** The
  brief, the rule card, the template README and the CLI's own `--help` all say
  `nilbots build …`; the published executable in `sandbox/cli-publish/` is
  `botarena`. Two minutes and a `no such file or directory`.
- **`--print-candidate-contract` still emits identity and fingerprints only**,
  so establishing what a cell actually resolves to still means running one match
  and dumping `header.contract` out of the replay. That was again the
  highest-value first move of the session — the five-slot lifecycle profiles, the
  24 fabrication placement offsets in declared order, the shell's forbidden tile
  tags and the volley's spread policy all came from there and none of them from
  prose. A `--print-candidate-contract --full` that emitted the resolved rules
  would remove a step every author repeats.
- **`ObservedSound.Bearing` remains unusable**: `hearingBearingModel` is a
  policy-ID string (`eight-octants-strict-two-to-one-cardinal-v1`) with no
  published mapping from sector index to direction, so hearing is still a sensor
  I will not ship a guess against. Third revision running.
- **`Available` still reads as "this will work"** and means "individually legal
  before the joint step". 1.8 % of this artifact's decisions are blocked, all of
  them joint-resolution, and the name is why a first-time author debugs a
  doctrine defect that does not exist.

## Timing

- Reading the three permitted docs, the SDK's new observation records, the
  scaffold diff, and dumping resolved contracts for all four cells plus three
  off-class pairs: ~35 min.
- Implementation: ~70 min.
- Ablation and measurement: six 48-match WASM matrices at ~50 s wall each
  (4-way parallel), plus a 10 s `--no-cache` build per variant. ~15 min of
  machine time total; the matrices are cheap enough that guessing is never
  justified.
- Qualification suite 5 including hash-linked T3 and T2 reruns: **7 s wall**
  (revision 3 measured ~35 s; the suite got much faster).
- Equivalence check of the rewritten threat model against the one it replaced
  (decision-by-decision replay diff): ~5 min, and it is the check I would keep
  if I could keep only one.

## Behaviour of the frozen artifact

Beats rebuilt revision 3 on all four phase-2 arm combos — `keel` +55.7,
`helm` +108.3, `veer` +87.0, `rig` +89.7 paired territorial progress per seed,
42-6-0 over 48 matches, with a positive edge on every seed of three cells and
five of six on the fourth. Roughly doubles completed advances on every cell.
Zero runtime faults in 45,206 controlled-runtime decisions, plus 12,150 more in
off-class robustness probes.

Known rough edges, recorded rather than fixed:

- **The doctrine has no answer for the fifth slot as such.** Five slots are
  spent by the weight target and the bearing spread, which count observed
  bodies rather than slots, and on the kit-on cells only 3.25–3.92 of 5 slots are
  ever fielded because slots 4 and 5 unlock at 300 and 420 and most matches end
  first. A revision that wanted the late slots to matter should target the
  fabricator's *survival* to tick 300, not its tempo — the same conclusion
  revision 3 reached about its own mirror, now with a number attached.
- **The volley is cast on a fixed threshold of two covered bodies**, chosen from
  the mechanic's arithmetic rather than measured, because this chassis cannot
  cast one and the only cells that can are not this brief's. 96 casts in an
  off-class striker mirror confirm the path works and say nothing about whether
  two is the right number.
- **The shell is effectively unreachable for this doctrine** (friction #2), and
  the shipped entry condition raises it zero times in 15 off-class matches. The
  forced variant that did raise it flapped; I would rather freeze zero and
  explain it.
- **Spread separation is derived from the widest fan the contract declares**,
  which is 1 on every cell without a volley — so on `keel` and `helm` the
  separation term reduces to "one tile apart, and prefer distinct objective
  tiles by election ordinal". That it still buys +56 and +108 suggests the
  binding constraint there is simply not standing in each other's only lane,
  and that a fan-aware separation has never actually been tested.
- **Perfect self-mirrors on a symmetric map still tend to the tick cap** in the
  off-class bulwark and striker pairs. Against a differing opponent this does
  not arise.
