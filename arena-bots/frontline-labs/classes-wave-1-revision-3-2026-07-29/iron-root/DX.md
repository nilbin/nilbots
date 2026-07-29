# DX report — iron-root, revision 3

Written from this revision's own forensics, qualification report, and private
sparring runs against this lineage's own rebuilt predecessor. No other entrant's
source, directory, replays, standings, or aggregate balance report was opened,
and nothing was read from a shared scratch path. This revision's private scratch
was `sandbox/iron-root-v3-scratch-c47e19`, a uniquely named directory created
for it. Both frozen predecessor directories were left untouched, and this report
proves it below by reproducing their recorded hashes.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `iron-root` |
| Population / wave | Frontline Labs classes, wave 1 **revision 3** (`classes-wave-1-revision-3-2026-07-29`) |
| Authoring lineage | `iron-root-v1` |
| Class | `bulwark` (declared in `botarena.json`) |
| Doctrine | FORTRESS ROTATOR — revision 3 codename RATCHET CLOCK |
| Role | `verdict-doctrine` |
| Target tier | cumulative T4 (retain) |
| Budget | **one** strategic revision; mechanical/contract repairs free |
| Predecessors | `classes-wave-1-2026-07-29/iron-root` and `classes-wave-1-revision-2-2026-07-29/iron-root`, both left untouched |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `fcd6358d30064f38ea00a2ddd88c9dd0c7406a79ab8bd165c938fc44014c36b4` |
| Scaffold | `templates/botarena-generic-actor/`, `ArenaBasics.cs` synced verbatim |
| Source-tree hash | `f5e2aec627a0eb901034a2db725056a831153f5607b3491520d49b62fc533cc6` |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/Guest `0.10.4`, actor protocol 1.0, WASI p1 core module, platform-matched Docker builder (macOS arm64 host) |
| Build cache key | `d175b3d2060ba075bc8d42b53ee1a09996c09769074acd838a42575a722a3e7d` (final `--no-cache` compile) |
| **`out/bot.wasm` sha256** | **`00ede717dacf60eb8e778134cc12145a648c057b69fb570b99340c5bf22f7090`** |
| `evidence/t4/qualification.json` sha256 | `4f7f24facdeb3e90e3d6d32ad561a7b7fac984ee0ff8d17ff485a6d2cfc40577` |
| Cumulative T3 prerequisite report sha256 | `3ef8344705426fa624cf7bfe6258071f35be71fc7ec0b89b08a4d7afa30d9979` (hash-linked inside the T4 report) |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| T3 prerequisite contract fingerprint | `4e77075bd13bbe56485eb29b57c8b916fec9dcd8c9ef9fdaa40fc6fad6944e8e` |
| Qualification outcome | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, **exit 0**, **T4 awarded**, `balanceEvidenceEligible: true` |

All five T4 components passed (`suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`), with the cumulative T3
and T2 prerequisites rerun and hash-linked automatically. The suite runs the
duel-depth union profile — no pendulum, no coupling — and the artifact passes it
unchanged, which is the contract-driven claim this revision most needed to keep:
the four structural levels are read, not assumed.

## Per-file source hashes

Recipe (unchanged from revision 2, stated exactly so it stays reproducible):

```bash
ls *.cs botarena.json IronRoot.csproj | LC_ALL=C sort \
  | xargs shasum -a 256 | shasum -a 256
```

```text
a05ec4b4ef15753836ee107586fb6442378ed8078a6d4a3cafef9a9a5bd56368  ArenaBasics.cs
188aafbc2161fb971d773b7a3a446f4cc1eb285dcd1e642f64f03c878044f6eb  ArenaGeometry.cs
48aba5fd394b2e8b78dfbd5b0a546dc9e9a4d0c0471d7524fcd3e8a881c67089  ContractLens.cs
fc658780b30aa96d0aa0de2c9403c8640f48a5b75a3c08524c14c2f5daff3413  FortressPlan.cs
dfe64b33d0f4ee50d8d08ed8748114da966c488251623a06fc18a887414780d4  Gunnery.cs
b75c20a460115ae5f5e01964e1543058af608ac96baef4f8da9f11fcd00a0df7  IronRoot.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  IronRoot.csproj
4e69aef2560b076e1c114d65385b553b7052a28fcc51c7371887fe882d39032a  Kinematics.cs
a7db54dc981f17411bedb17984885168b03c1c688f2b1b3d08b89722277d8aba  RatchetClock.cs
f76050b17a6dfb7a8a42c86d25fef84423494035d210ac55a094678f0508383c  botarena.json
```

`ArenaGeometry.cs`, `FortressPlan.cs`, `Gunnery.cs`, `Kinematics.cs`,
`IronRoot.csproj` and `botarena.json` are byte-identical to revision 2.
`ArenaBasics.cs` is the current scaffold, synced verbatim.

### Predecessor integrity

Both frozen trees still reproduce their recorded identities exactly, which is
this revision's evidence that neither was touched:

```text
0b1cf8673df95cf328a39f90487f383ab6bf653ba5db8ed750e79dde6271e728  wave-1 source tree
ed5c7bccaa98947b9e413d506eeb527c6ffe9e17af2de20cfb3ea10611d18928  wave-1 out/bot.wasm
9bf1b4caebefdfb77b3d608ecd8ce01aa5f54e20ad77cfa73db20033abbd114b  revision-2 source tree
793c4f2e3406c5ea29efdc5b8f4f1ff6830449be4042c7bc52baa589bca4841c  revision-2 out/bot.wasm
```

The revision-2 sparring partner used throughout this report is that exact source
rebuilt in private scratch — all seven `.cs` files and `botarena.json` verified
byte-identical, only the editing project's SDK path adjusted for its scratch
depth (which the controlled build ignores). Rebuilt artifact:
`777df1bbeffe80af74eb1ae2cedc4163b9bf87090074414d100a042061cd3c11`.

## Timings (Apple Silicon, warm Docker builder)

| Step | Time |
| --- | --- |
| `dotnet build` of the editing project | ~0.5 s |
| `botarena build --no-cache` (cold, Docker) | **8.5 s** |
| `qualify --suite frontline-qualification-5` (WASM, both assignments) | **6.8 s wall / 10.0 s CPU** |
| in-process 500-tick class-arm match (both projects rebuilt in-process) | ~5 s |
| WASM 500-tick class-arm match | ~4 s |
| full 40-cell sweep (4 levels × 5 seeds × 2 sides), in-process | ~3.5 min |
| full 40-cell sweep, WASM | ~3 min |
| one 10-cell single-arm ablation | ~50 s |

The inner loop is still excellent, and for a revision whose whole content is
"read four new contract fields correctly", the thing that mattered most was that
a **10-cell single-rule ablation costs under a minute**. Nine of them — five
from the candidate, four from the corrected base — found one rule costing 52
points of territory and another costing 21 on the level where its own reasoning
did not apply. Neither was visible in the aggregate, which sat at roughly zero
throughout; I would have shipped both.

## Forensics: what revision 2's own ratchet replays said

Outputs in `evidence/forensics/`. Everything below is computed from this
lineage's own replays, using only fields a bot can observe.

Revision 2 holds T4 and its doctrine is sound on a mean-reverting frontline. On
the sticky levels it is playing a different game than it thinks:

| Per 500-tick ratchet cell, revision 2 as team 0 | |
| --- | --- |
| ticks inside somebody's live hold | **160–190** |
| my sole objective presence inside **their** hold (progress is spent) | **57–61** |
| my sole objective presence inside **my own** hold (progress counts double) | **0–7** |
| my completed captures that reset without moving the front | **2** |
| the opponent's, likewise | **1–2** |

Two full capture windows a match — thirty-odd ticks of exactly the thing this
doctrine exists to buy — resolving to a claim reset and an unmoved objective.
And the mirror image: the window where our presence is protected *and*
productive is the window we are almost never standing in.

That is not a tuning error. It is a doctrine authored for a world where the
front always comes back, applied to one where it does not.

## The one strategic revision: RATCHET CLOCK

One idea: **price every commitment against the two clocks the structural levels
publish — whose progress is real right now, and what a death actually costs —
instead of against relief alone.**

Seven coupled rules, all contract-derived, all inert where the contract declares
nothing:

1. **The hold clock is recoverable without memory.**
   `ControlResumesAtTick - RedeployPauseTicks` is the tick of the last advance,
   published every tick, readable on a life's very first one. That property is
   the whole reason this works: under `forward-rally` every death makes a fresh
   life with empty private memory, and a doctrine that could only learn the hold
   by watching would be blind exactly where the arm is most active. Ownership
   comes from a watched index change, or from watching a capture collapse from
   one-below-threshold to zero without the front moving (declared gain and
   erosion are one per tick, so that cannot be decay), and failing both from the
   signed displacement of the front — reported as a guess and gated accordingly.
2. **Root inside their live hold.** Our captures are spent while it runs, so
   weight on the surface buys only denial, and this class's way to deny without
   weight is a gun with three times the cadence, twice the reach and no facing.
   Tightly gated: watched evidence only, a remainder that outlasts the windup
   *and* a whole capture window, and a mobile body left to take the ground back.
   Measured the permissive way first — any trusted phase, any remainder — it
   doubled the roots, halved sole presence and cost 37 points in its worst cell.
3. **Whether to keep weight inside *our own* hold is the control policy's
   question, not the hold's.** This is the revision's one genuine discovery and
   it was a measured correction of my own reasoning. "Our hold doubles presence,
   so never convert a body to zero weight" is right under a net-weight control
   policy and wrong under a binary one, where the second and third bodies add no
   capture rate at all and the fortress is free suppression over ground that
   cannot be lost. Ablated as a hold rule it was worth **−21 points** across the
   plain-ratchet cells — removing it recovered them — while the same build's
   contest cells were the ones gaining. I did not ablate it on the contest level
   separately, so "it carries the contest gain" is an inference from that split,
   not a measurement.
4. **A completion that will be spent moves nothing.** Revision 2 refused to
   commit to a windup whenever any capture was within a windup of finishing.
   Inside a hold protecting the other side, that completion resets and the front
   stays — the refusal was for an event that will not happen.
5. **When our own advance emptied the lanes, unroot at once.** Revision 2 waited
   out the full redeploy pause to confirm the front had genuinely moved. Under a
   live own hold we already know it moved and that it cannot come back.
6. **A death is priced by where it puts the body.** Not by the placement
   policy's name: the walk from the contract's declared arrival tiles to the
   scoring surface is compared against the walk from the authored spawn, and
   only a materially shorter one counts. Where it holds, the body is renewable
   and the ground is not — a holder eats the bolt until the hit is actually
   lethal, the relief radius widens to the windup's own travel budget, and a
   return due inside the windup counts as relief, as a *reinforcement* only. The
   last mobile body on the team never roots against a promise; letting it do so
   put the doctrine's only scoring body into a turret at tick 118 of a 500-tick
   match. (I also wrote the tick-denominated version of this — return delay plus
   the walk — and deleted it in the freeze pass, because no rule ended up
   calling it. Shipping an unused reader is how the next revision inherits a
   number nobody has ever measured.)
7. **Under contest arithmetic, know when you are the margin.** A root is only
   taken from a position already net-positive without the rooting body's weight,
   and the body whose departure would take the net difference from positive to
   zero does not step off a lane for anything short of a lethal hit.

### Tried and reverted, with its measurement

Ranking stations and fortress sites by **where reinforcements actually arrive**
(`ExpectedArrivalTiles`) instead of by the home anchor. The reasoning was that
under `forward-rally` the authored spawn is a corner of the map nothing will
walk out of again, so "nearer home" ranks posts by an irrelevance. Measured on
the ratchet cells with the hold veto out of the way, it cost **52 points of
territory across ten cells** (−4 → −56). The home anchor is not really "home":
it is the rearmost point of our own approach, and ranking by it puts bodies on
the side of the objective the opponent has to walk past. Reverted, with the
measurement recorded in the source comment where it was reverted.

It was also invisible until the veto was removed — with the veto on, every
single-rule ablation read −77. That is the most useful methodological lesson
here: **ablate from the corrected base, not from the candidate.**

## Measured effect vs the rebuilt predecessor

Bulwark mirror (both projects declare the class), `--movement facing-locked`,
five seeds, both sides, all four levels — 40 cells. Because both artifacts play
the same class on a mirror-symmetric map, swapping sides makes the predecessor's
own baseline **exactly zero-sum**, so any non-zero margin is signal rather than
side bias. Score is signed territorial progress; margin is candidate minus
predecessor, averaged per cell.

**WASM runtime (the frozen-cohort standard):**

| Level | W–L–D | margin | same score | same decisions |
| --- | --- | --- | --- | --- |
| unmodified control | 4–4–2 | **0.0** | 10 / 10 | **10 / 10** |
| `--capture-threshold 9 --prime-respawn-ticks 9` | 4–4–2 | **0.0** | 10 / 10 | **10 / 10** |
| `--pendulum ratchet` | 5–5–0 | **−0.4** | 7 / 10 | 4 / 10 |
| `--pendulum ratchet-contest` | 6–4–0 | **+14.6** | 0 / 10 | 0 / 10 |
| all | **19–17–4** | **+3.5** | 27 / 40 | 24 / 40 |

The in-process sweep reproduces those numbers cell for cell.

The last column is the claim I care most about and it is stronger than equal
scores: on the twenty cells of the two levels that declare none of the new
fields, **every accepted action, every argument, every life and every body
position is identical to the predecessor's**, tick for tick. That is not a
tuning result, it is proof that the seven rules are gated on contract fields
rather than on an assumed arm — the artifact that plays the structural levels
*is* revision 2 wherever the structure is absent.

Read honestly, the rest is: a clear gain on the level whose arithmetic the
revision actually reasons about, and a wash on the level where the hold binds
but a second body is worth nothing.

The plain-ratchet wash deserves its own sentence, because I would rather report
it than dress it. On that level the revision's only live rules are the free
window, the spent-completion test and the immediate unroot; the two rules that
carry the contest gain are gated off by the control policy, correctly. Three of
ten cells move at all, and they net −2.

## Documentation gaps

- **The hold's owner is not derivable, and the addendum documents the harder
  half of the clock.** The scaffold's `CaptureRules.HoldTicks` comment says "the
  advance itself is observable as a change in the active position index, so
  track when the hold started" — true, and it requires a life that was alive
  when it happened. Under `forward-rally`, deaths are frequent and every one
  makes a life with empty private memory, so the documented method fails for
  most of the bodies on the board. The memoryless method
  (`ControlResumesAtTick - RedeployPauseTicks`) is exact, is published every
  tick, and is mentioned nowhere. One sentence would have replaced a day of
  inference design. And **ownership has no derivation at all**: my last resort
  is a documented guess from the signed displacement of the front, which is
  wrong after an opponent's first regression from a two-position lead. A
  nullable `holdOwnerTeamId` / `holdRemainingTicks` pair on the Frontline
  observation would delete `RatchetClock.cs` entirely.
- **Nothing connects `contest-majority` to zero-weight forms**, and for a
  bulwark that is the whole class. The addendum's table says surplus weight
  "scales capture pressure, so one body no longer nulls two". What it does not
  say is the consequence: under binary control the second and third bodies add
  no capture rate, so converting one into a turret is nearly free, and under
  net-weight control the same conversion is a direct subtraction from the
  quantity that decides the match. The turret bargain section states the
  zero-weight fact plainly and in isolation; the pendulum section changes what
  that fact is worth and does not say so. I found it by ablation, at a cost of
  21 points of territory in the wrong direction.
- **`--pendulum` composes with declared classes only through project specs.**
  Passing a prebuilt `out/bot.wasm` silently resolves the *base* contract —
  `frontline-labs-1-experiment-ratchet-facing-locked` instead of
  `frontline-labs-1-bulwark-vs-bulwark-ratchet-facing-locked`. Same command
  shape, different game, no warning. My first probe measured the wrong thing and
  I only caught it by reading the ruleset ID in the header. The class addendum
  says declared classes "resolve from the manifests"; it does not say that a
  WASM path has no manifest.
- **A slot's return delay is three hops from the player docs.** Pricing a death
  needs `lifecycleAssignments[slot].lifecycleProfileId` →
  `rules.lifecycle.profiles[id].delayTicks`, and the rules card states 18 and 30
  as prose values without naming the path. `ExpectedArrivalTiles` (excellent,
  and correctly warns it is the contract's *intent*) has no delay counterpart.
- **`ratchetHoldTicks` is documented as "zero means absent"** and the scaffold
  maps that to `null`, which is right — but the same contract publishes
  `redeployPolicy` as a long descriptive policy ID whose text
  (`…deny-enemy-regression-past-the-high-water-mark…`) is the only place the
  *semantics* of the hold appear. A bot that read only `ratchetHoldTicks` would
  know the duration and not the rule.

## Hardcoding temptations

All resisted; the new ones this revision created:

- **"The hold is 40 ticks."** It is in the contract, it is absent when inert,
  and the numbers-only level would have made a literal 40 into a phantom hold
  over a contract that declares none.
- **"Ratchet means forward-rally."** The two are separate fields and the CLI
  composes them separately (`sticky-frontline`, `forward-rally`,
  `contest-majority` are all individually selectable). Every rule here is gated
  on its own field, so the ablation levels behave correctly even though I never
  measured them.
- **"Forward-rally means arrivals are near the front."** It means arrivals are
  chain-derived. Whether that is *nearer* is geometry, so `ForwardReturn`
  compares the arrival walk against the spawn walk rather than trusting the
  policy's name — which is also what keeps the rule inert on the control level
  instead of merely usually-inert.
- **"A capture that vanished was spent."** Only if it vanished from within one
  tick of the threshold and the front did not move. Declared gain and erosion
  are both one per tick, so the test is arithmetic on contract values, not a
  magic constant.
- **"Prime windup 3, child windup 1."** Still resisted, still one
  `Windup.DurationTicks` comparison away from being two magic numbers, and the
  base contract still gives the prime no anchor route at all.

## Confusing terminology

Carried forward and still true: "Anchor"/"Mobilize" are prose words with no
contract representation; `irreversibleForLife` reads backwards on the reverse
route; "Available" versus "will succeed"; "facing-locked" restricts movement,
not rotation.

New this revision:

- **"Hold" is three things.** `ratchetHoldTicks` is a *duration*; the live
  interval is unnamed; and "hold the objective" in every other document means
  standing on it. My source says "phase" for the first two and "holding the
  scoring surface" for the third, and that split is not the contract's — nothing
  in the schema knows what a phase is.
- **"Spent" has no representation either.** A capture that completes inside a
  hold is described in prose as SPENT, and on the wire it is indistinguishable
  from a normal completion except that the position index did not change. The
  most consequential event in the arm has no event.
- **`controlResumesAtTick` sounds like a redeploy detail** and is in fact the
  only published trace of when the last advance happened. Its own doc comment
  ("earliest tick on which objective control may resume") describes what it
  gates, not what it encodes.
- **`ObjectivePresence` returns weight, and "one body" is not "weight one".**
  The helper is right and its doc comment is careful; the trap is that under
  binary control the numbers it returns are still weights, and comparing them
  arithmetically is only meaningful when `SurplusWeightScalesGain` says so.

## Repairs and strategy passes

One strategic revision; the rest are mechanical, each driven by a measurement.

1. **Strategy — RATCHET CLOCK** (the one revision; seven coupled rules above,
   one variant tried and reverted with its number).
2. **Repair — rebuild before sparring.** The frozen revision-2 artifact faults at
   tick 0 on every sticky level (the capture contract gained a field), and the
   match reports it as a `draw — fault-eligibility at tick 0` with no message
   naming the cause. Both the predecessor and this revision were rebuilt from
   source for every measurement.
3. **Repair — scaffold sync.** `ArenaBasics.cs` taken verbatim from the current
   template, which now carries the facing-locked route fix this lineage had to
   discover for itself in revision 2, plus `Capture`, `ObjectivePresence`,
   `ArrivalsRallyForward` and `ExpectedArrivalTiles`. All four are used;
   `ObjectivePresence` in particular replaced a hand-rolled weight count that
   would have been wrong the moment a form declared weight two.
4. **Repair — two trust gates, not one.** A phase from the displacement prior
   may drive a reversible preference; only a watched phase may spend a one-use
   irreversible route. Conflating them is how a guess becomes a permanent loss.
5. **Repair — dead code removed.** A rally-anchor accessor and a station rule
   that measured inert were both deleted rather than left to rot; the reverted
   variant survives as a comment with its number, not as an unused method.

## What I could not evaluate

- **Only one legal opponent, and it is a mirror.** The isolation rules permit
  sparring against my own predecessor and nothing else, and two bulwarks on this
  map produce a slow match: **one to three advances per 500 ticks**, so a hold
  is live for only 160–190 of them and the whole revision is being judged on
  about a third of each game. Everything above is conditional on that opponent
  model, and specifically on an opponent that also anchors, also stations on the
  surface, and never hunts turrets. A striker or fabricator opponent would
  change the free-window rule's value in both directions and I cannot see it.
- **The ablation levels.** `sticky-frontline`, `forward-rally`,
  `contest-majority` and `enemy-sole-decay` compose individually, and every rule
  here is gated on its own contract field, so I believe the behaviour is right
  on each. I measured the two registered structural levels, the numbers-only
  level and the control, as briefed — not the four ablations. In particular the
  `enemy-sole-decay` reader (`OnlyEnemySolePresenceDecays`) is parsed, exposed
  and **never branched on**, because I could not find a rule for it I was
  willing to ship untested.
- **Whether the free window is worth taking at all on the plain-ratchet level.**
  It is one of three live rules there and the level nets −2 over ten cells. The
  ablation budget went to the two rules that were costing 21 and 52 points; the
  free window was never isolated on its own, and a fourth ablation round would
  have told me. I would rather record the gap than imply it was measured.
- **Timing a capture to land the tick a hold expires.** The obvious refinement —
  hold progress one below the threshold and complete the instant the enemy's
  hold lifts — I designed and then rejected on arithmetic rather than
  measurement: declared decay is one per two ticks, so waiting costs roughly
  what rebuilding costs, and stepping off the surface to wait concedes it to an
  opponent whose own captures *do* count during their hold. I record it as
  reasoned-and-declined, not as tested.
