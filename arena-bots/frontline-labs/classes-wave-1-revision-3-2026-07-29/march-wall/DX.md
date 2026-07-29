# DX notes — march-wall, revision 3

## Isolation statement

Everything in this revision was authored from the four permitted documents, the
scaffold, the SDK types, my own two frozen directories, and my own replays. No
other entrant's source, standings, replays, aggregate report or scratch
directory was opened; the wave-1 and revision-2 directories were read and left
untouched. All working files live in `sandbox/march-wall-r3-scratch-d4a917/`, a
uniquely named private directory created for this pass. Sparring was against my
own rebuilt revision 2 only — no other artifact was ever loaded into a match.
Nothing to disclose under the packet's exposure clause.

## Identity

| | |
| --- | --- |
| Entrant | `march-wall` |
| Population / wave | Frontline Labs classes, wave 1, revision 3 (`classes-wave-1-revision-3-2026-07-29`) |
| Authoring lineage | `march-wall-v1`, revision 3 |
| Doctrine | THE LANE IS THE WALL, PRICED (advancing wall, third lineage) |
| Class | `bulwark` (declared in `botarena.json`, unchanged) |
| Role | `verdict-doctrine` |
| Target | cumulative T4 (retain) |
| Budget | one strategic revision; mechanical repairs and contract adaptation free |
| Predecessors | `classes-wave-1-2026-07-29/march-wall` and `classes-wave-1-revision-2-2026-07-29/march-wall`, both untouched |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `fcd6358d30064f38ea00a2ddd88c9dd0c7406a79ab8bd165c938fc44014c36b4` |
| Toolchain doc | `docs/WASM-DEVELOPMENT.md`, sha256 `7f0bcafff85fb1fbcf9a9633237509888bfd4884f180af36afa77fce1173c5df` |
| Template helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `a05ec4b4ef15753836ee107586fb6442378ed8078a6d4a3cafef9a9a5bd56368` (byte-identical copy, re-synced this pass) |
| CLI | `sandbox/cli-publish`, nilbots 0.9.10 (SDK 0.10.4, game rules 0.5) |

## Freeze identity

| | |
| --- | --- |
| Submitted sources | `AnchorPlanner.cs`, `ArenaBasics.cs`, `ContractView.cs`, `FireControl.cs`, `Geometry.cs`, `Lane.cs`, `MarchWall.cs`, `Navigation.cs`, `Pendulum.cs`, `Threat.cs` |
| Project metadata | `botarena.json`, `MarchWall.csproj` |
| **`out/bot.wasm` sha256** | **`3a0d079b10908a639354f3674cf449b104b212f4d8b95a205d97af185d1021f9`** |
| Canonical WASM | `out/bot.wasm`, 3 349 038 bytes, built by `nilbots build <project> --no-cache` |
| Deterministic source-tree hash | `eccaef7746e6ed12d84c2f28d0518442a8d45dc8192b9bd14bde585002a172d1` (sha256 over the sorted sha256 list of `*.cs` + `botarena.json` + `*.csproj`, same recipe as v1 and revision 2) |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/guest 0.10.4, WASI p1 core module, platform-matched Docker builder on macOS arm64 |
| Build cache key | `01ea9791016db2d1d7f219cf0835da76…`; the builder's reported artifact hash equals the sha256 above |
| Qualification report | `evidence/t4/qualification.json`, sha256 `0bbf5587b449880be86ea132ea9d349fe17e9ec84c6b1d1ef9ee91e575fda082` |
| Verified probe replays | 36 replays under `evidence/t4/` across 17 probe variants (5 T4, 6 T3, 6 T2), both team sides |

### Per-file source hashes

| file | sha256 |
| --- | --- |
| `AnchorPlanner.cs` | `b4d63ec8968f1011d6c1f3d0e30c7019cffb4f49db264d925316f0fba73fd9d4` |
| `ArenaBasics.cs` | `a05ec4b4ef15753836ee107586fb6442378ed8078a6d4a3cafef9a9a5bd56368` |
| `ContractView.cs` | `5ef7a49c8438a5ef28502b9dea5f20b442dae41299145957bded5fb3a11eb2ec` |
| `FireControl.cs` | `4737a23de256a558472c2c8674eae35f8d832a9d8b9cc22a10ff36eba98561b6` |
| `Geometry.cs` | `7ae38f0c28cad98882c18fc0e0c107580b5438e70682d715f05210f60a91a827` |
| `Lane.cs` | `13de7b157a1c853c177f3fafd15437171d9c42eb8ecb401f5aa2302f7255d655` |
| `MarchWall.cs` | `418fa9b715dc042f7aa4b38f0486b8e955a36e0037e982f04a1cb16f877dccec` |
| `Navigation.cs` | `d0689dce04e71a6d1c1cc4ed990abf1c3f2b1c94899bbed19ba893b93a4e3d1e` |
| `Pendulum.cs` | `c0bbb25f5a085798d497355832c65c84e366c35917780161bbd12add5c587cd8` |
| `Threat.cs` | `5095984ee77dcfd9acf4b2e6443522ed49c81d18eabd1336a7bbf1ff9a8bd416` |
| `botarena.json` | `43d359abe4262852ffdfb64249b255e3ece348bb59cbe297adb04e05bf552ecc` |
| `MarchWall.csproj` | `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573` |

## Qualification outcome

`experiment frontline-labs qualify --suite frontline-qualification-5`, profile
`frontline-duel-depth-union-t4-v1`, WASM runtime, artifact `3a0d079b1090…`.

**Exit 0 — T4 retained.** Prerequisite T3 PASS (which itself re-ran and
hash-linked T2). All five T4 probes PASS: `suppression-choke`,
`entry-initiative`, `prediction-chamber`, `front-rotation`, `map-holdout`.
`balanceEvidenceEligible` is **`true`** in the report body.

A correction to my own record while I am here: revision 2's DX says this flag
"is `false` in the report body, as in v1". Both frozen reports were re-read this
pass and both say **`true`** — `cb7b182e707c` (v1) and `c0e24671a241`
(revision 2), same suite ID, same field. That was an author error in the
revision-2 notes, not a field that moved, and it is exactly the kind of error a
freeze is supposed to make catchable. Left standing in the revision-2 document,
which is frozen; corrected here.

Only the frozen artifact was qualified this pass; every intermediate build was
judged by sparring instead. That is the opposite of revision 2, where two
intermediate artifacts dropped to T1 and T2 before the third passed — this
revision touches ration and timing rules rather than the movement and firing
geometry the probes examine, so the probes had nothing to say about it.

## The mechanical repair the brief predicted, measured

The frozen revision-2 artifact `c0e24671a241…` behaves like this:

| arm | frozen r2 artifact |
| --- | --- |
| control | fine — draw at max-ticks, tick 499 |
| `--pendulum ratchet` | **`draw — fault-eligibility at tick 0`**, both participants, `WASM generic actor exited before its life ended (peak completed tick fuel 0.0M/200.0M)` |

So the fault is exact and arm-specific: the guest attests a contract schema that
no longer decodes once `capture.ratchetHoldTicks` is present, and it dies before
its first decision. (The wave-1 artifact is worse — it faults at tick 0 on the
*control* arm too, having been built against an older toolchain still.)
Rebuilding revision 2 from unchanged source with the current SDK produced
`79d9f673fe6c…`, and that rebuild is the only opponent used below.

Worth stating plainly because it is a real trap for a frozen-artifact
population: **a frozen `.wasm` is not a frozen bot.** The freeze that survives a
contract addition is the source plus the deterministic source-tree hash; the
artifact hash pins provenance, not portability. A cohort that archives artifacts
and expects to replay them against a later arm is archiving a fuel-exhaustion
draw.

## The strategic revision (one, as budgeted)

**Price the pendulum.** Revision 2 assumed a tick of presence is worth the same
as any other tick of presence, which is true only while the frontline reverts.
Three declared policies each falsify it, and the revision is the single act of
reading them instead of assuming them:

1. `capture.ratchetHoldTicks` — a hold makes taken ground keepable and makes a
   capture completed inside another team's hold *spent*, so a claim is worth
   what a clock says. `Pendulum.cs` infers whose hold is live (an active-position
   index change, signed against our declared advance delta) and, on a life's
   first tick where there is no history, dates it from `controlResumesAtTick`
   minus one minus the declared redeploy pause. Inside our own hold the idle
   patience that forces a standoff open drops from three ticks to one and the
   approach's soft caution penalty comes off entirely; inside theirs, a claim
   that cannot complete before the hold lapses is not built and the standoff is
   not forced, because a mutual null *is* the denial.
2. `capture.controlPolicy` — when surplus weight scales gain, a weighted body on
   the objective is a vote, so a body does not walk off to extend the wall while
   the count on the tile is level or against us. When control is binary the same
   walk is free, because the second body was never adding to the claim.
3. `lifecycle.automaticReturnPlacement` — when arrivals rally onto our own-side
   objective, presence is already redundant on its declared clock, so the
   fortification ration relaxes from "match the roster the other side has shown"
   to one, and the surplus body becomes the turret.

Everything else in the diff is the plumbing those three reads need
(`ContractView` gains the capture policy, the hold, the pause, the gain and the
per-slot return delay; `Pendulum.cs` is new at 134 lines) plus the template
re-sync.

## Measured effect versus the rebuilt revision 2

Eight seeds (104729, 130363, 155921, 202961, 224737, 262147, 293459, 350377),
both team sides, `--classes bulwark-vs-bulwark --movement facing-locked`, WASM
runtime, 64 matches. Territorial progress is summed over all sixteen matches per
arm from revision 3's side.

| arm | record (W-L-D) | territorial | vs the mirror floor |
| --- | --- | ---: | --- |
| control (unmodified) | 0-0-16 | +0 | **bit-identical**, 16/16 |
| numbers-only (`--capture-threshold 9 --prime-respawn-ticks 9`) | 3-3-10 | +0 | **bit-identical**, 16/16 |
| `--pendulum ratchet` | **16-0-0** | **+140** | 0/16 identical |
| `--pendulum ratchet-contest` | **15-1-0** | **+154** | 1/16 identical |

Per-match territorial, ratchet: side a `10 10 15 10 10 15 15 15`, side b
`5 5 5 5 5 5 5 5`. Contest: side a `8 15 30 15 −1 6 10 15` (the 30 is a base
breach at tick 463, the only early ending in the sweep), side b `7 ×8`.

**The mirror floor is the control that makes those numbers readable.** The brief
is right that self-play cannot A/B an arm, and the reason is worth recording as
a number: running the rebuilt revision 2 against *itself* over the same 64 cells
sums to exactly **+0 in every arm** — 0-0-16 on control, 3-3-10 on numbers,
0-0-16 on ratchet, and 8-8-0 on contest, where the arm has a real ±15 side bias
that mirrors away perfectly. So every point above is revision 3 and none of it is
the map.

The two identity rows are the part I care most about. On the arms that declare
none of the three fields, revision 3 and revision 2 produce **the same replay
hash, the same completion tick and the same score in all sixteen matches each**.
That is the "one artifact plays all four" requirement discharged as a
measurement rather than an intention: the revision provably cannot disturb an
arm it does not price.

Mechanism, summed over the sixteen matches per arm — advances completed, and the
share of body-ticks spent as a weight-zero turret:

| arm | r3 advances | r2 advances | r3 turret share | r2 turret share |
| --- | ---: | ---: | ---: | ---: |
| control | 0 | 0 | 11.4 % | 11.4 % |
| numbers | 47 | 47 | 20.3 % | 20.3 % |
| ratchet | **44** | 32 | 39.6 % | 36.0 % |
| contest | **58** | 44 | 32.8 % | 26.0 % |

Revision 3 moves the front about a third more often on both ratcheted arms while
carrying *more* guns, which is the ration and the hold clock doing exactly what
they were written to do. Control is a total mutual null — zero advances by
either side in sixteen matches — and that is a property of two near-identical
doctrines meeting, not of the arm.

## Six readings measured one at a time; five did not survive

Each row is a real build sparred over the same four arms, four seeds, both
sides (32 matches), against the rebuilt revision 2. Territorial progress is
summed from my side; every reading was added or removed one at a time from a
fixed base. I am recording all six because every one of them reads as correct,
and three of them read as *more* correct than the ones that shipped.

| # | reading | ratchet with | ratchet without | contest with | contest without | verdict |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| 1 | Price a death as declared return delay + the walk back from the declared arrival tiles, both sides, and let the ratio decide the exchange | 1-4-3 **−34** | 4-4-0 **+3** | 7-0-1 +73 | 8-0-0 +70 | **loss** (and −17 on `numbers`, which it binds alone) |
| 2 | Do not anchor inside our own hold; relax the ration by one inside theirs | 4-4-0 **+3** | 8-0-0 **+51** | 8-0-0 +70 | 8-0-0 +83 | **large loss** |
| 3 | Never step off the objective while a hold is live or arrivals rally forward | 8-0-0 **+51** | 8-0-0 **+65** | 8-0-0 +83 | 8-0-0 +96 | **loss** |
| 4 | Fortification ration relaxes to one under binary control + forward rally | 8-0-0 **+51** | 4-4-0 **+15** | 8-0-0 +83 | 8-0-0 +83 | **kept** |
| 5 | Anchor sites also score coverage of the enemy's expected arrival region | 8-0-0 +51 | 8-0-0 +51 | 8-0-0 +83 | 8-0-0 +83 | **exactly inert; removed** |
| 6 | A turret stands back up to return a vote to a weight-scaled election | — | — | — | — | **never fired in 64 matches; removed** |

Rows 5 and 6 are identical in every cell they touch, which is the point of
listing them: removing row 5 changed no completion tick and no score in 32
matches, and removing row 6 changed nothing across the full 64-match final
sweep. Row 4's "without" column is the only place a shipped rule is worth
naming a number for on its own: 36 territorial on the ratchet arm.

Reading 1 is the instructive failure. The `numbers` arm isolates it perfectly:
it is the only one of the six that binds when no hold, no rally and no scaling
are declared, and on that arm alone it cost **−17 territorial across sixteen
matches** while everything else was inert. The bug is not arithmetic, it is
whose question it answers. A child's rebuild clock is 30 against the Prime's 18,
so pricing an exchange by "what does *my* death cost" tells every child that
every fight is bad, and a body that never fights is not durable, it is furniture.
The forward rally I wanted to reward is symmetric — it shortens the walk for both
sides equally — so it belongs in what a body is *scarce for* (reading 4, which
shipped and is worth 36 points on the ratchet arm) and not in who wins a duel.

Reading 3 is the one I most expected to work, and it is the plausible idea this
class invites: five health, ground that cannot be taken back, arrivals landing
beside the fight — stand and take the hit. It loses 14 points on ratchet and 13
on contest. Revision 2's DX recorded the same instinct failing in its own
"absorbing the bolt while holding a lane" form. Twice measured, twice wrong: the
bulwark's durability buys it the *right to be in a lane*, never the right to
stand in one it is not shooting down.

Reading 2 is the subtler failure and I still do not fully understand it. Inside
our own hold the front cannot come back, so trading a scorer for a gun looks
strictly bad, and refusing to anchor there looks strictly right. Measured, it is
worth −48 on the ratchet arm. My best account is that a hold is 40 ticks and a
capture is 15, so the window closes long before an anchor site chosen inside it
becomes relevant, while the refusal reliably wastes the one uncontested stretch
of the match in which a body can walk to a choke unmolested. If that is the
mechanism, the rule was not wrong about value, it was wrong about *when* the
value is collectable — which is a mistake a hold clock invites specifically.

Reading 6 is a different category of failure and worth separating: it is not
that it lost, it is that it could not be measured. A fortified body's own sensors
almost never reach the active objective, so the presence read that would trigger
it returns empty essentially always. Removing it changed no result in any of the
64 cells, which is the confirmation.

## Where the hold inference is genuinely blind

The brief says the hold's start is not an observation field, and that is the
sharpest contract edge in this round. Two derivations cover most of it, but not
all, and the gap is structural rather than fixable:

- A body alive across an advance sees the index change and knows the tick and
  the owner exactly.
- A body whose life begins during the redeploy pause can date the advance from
  `controlResumesAtTick`, but nothing names the team that made it.
- **A body whose life begins after the pause lapses cannot tell that a hold is
  live at all.** Under `forward-rally` that is the common case, because the
  contract deliberately produces a stream of fresh lives arriving beside a fight
  they know nothing about, and private memory is life-scoped by design.

So the doctrine reads an unnamed hold as the opponent's — the conservative
branch — and simply does not know about a hold that started more than five ticks
before a life existed. I would not ask for the hold to become an observation
field; inferring it is a genuinely good design. But `controlResumesAtTick` is
doing double duty as the only timestamp in the mode state, and one adjacent
integer — ticks elapsed since the last position change, which the engine already
knows and which reveals nothing private — would close the gap without giving
anything away.

## Documentation gaps

- **The class card's stat table still omits the turret's gun** (carried from
  revision 2, unchanged). Bulwark is listed at "projectile range 6"; the
  resolved contract says `turret-bolt` travels **8** on **cooldown 1** with
  eight headings and seven health. It remains the single most important number
  in my class and the only place to find it is the contract.
- **`ratchetHoldTicks` is documented as a duration and used as a deadline.** The
  addendum's table says "a completed advance holds for 40 ticks", and the SDK
  remark on the field is genuinely excellent — it names the spent-capture trap
  outright and tells you to track when the hold started. What neither says is
  that the hold is only actionable *relative to how long a capture takes*: 40
  ticks against a 15-progress threshold at gain 1 means the interesting question
  is never "is a hold live" but "does it outlast the capture I could start now".
  That comparison is the entire content of the mechanic for a bot author and it
  has to be derived.
- **Nothing warns that `ratchet` implies a fresh-life amnesia.** `forward-rally`
  is documented as a placement change, which it is. Its second-order effect is
  that it manufactures the exact bodies that cannot see the hold — new lives, at
  the front, mid-hold — so the two halves of `ratchet` interact against the
  author in a way neither half suggests on its own.
- **`ObjectivePresence` returns a lower bound and says so; nothing says how
  low.** The doc comment is careful ("treat this as a lower bound on the
  opposition"), and for a bulwark with vision range 4 on a six-tile objective the
  bound is very loose indeed — a fortified body's read is empty almost always,
  which is what killed reading 6 above. A sentence pointing at the form's own
  vision range next to that warning would have saved me a build.
- **Probe pass predicates are still unpublished** (carried from v1 and revision
  2). `failedCriteria` names remain excellent; the numeric thresholds still have
  to be inferred.
- **`balanceEvidenceEligible` is easy to misread and I have the proof.** My own
  revision-2 notes recorded it as `false` when the frozen report says `true`
  (see the qualification section). The field sits in the report body while the
  tier award is what the console prints, so an author reading the console and
  writing up the JSON from memory gets it backwards. Printing the flag next to
  `Tier awarded:` would close that gap; so would the packet naming it as a thing
  to record, since the freeze list asks for "qualification JSON, its SHA-256"
  and not for any field inside it.

## Confusing terminology

- **"Objective weight"** remains the most doctrine-relevant number in the
  contract with the least descriptive name. A third pass has not softened this:
  it is not a multiplier, it is a franchise.
- **"Control policy" versus "decay clock" versus "redeploy policy"** are three
  fields whose IDs are long declarative sentences and whose *interaction* is
  what a doctrine needs. `net-positive-objective-weight-difference-scales-gain-`
  `non-positive-applies-configured-decay-opposition-erodes-to-neutral` is
  admirably exact and still took a replay to believe.
- **"Hold" is used for two unrelated things** — the ratchet's protection window
  and every doctrine's sense of "holding ground". This document had to say
  "protected ground" throughout to stay unambiguous.
- **`--pendulum ratchet` is one token for two mechanics** that a bot must read
  from two unrelated contract sections (`gameMode.capture` and `lifecycle`).
  That is correct design and mildly hostile naming.
- The `frontline-qualification-N` / `TN` off-by-one is still jarring every time.

## Timings (Apple Silicon, warm)

- managed edit/compile loop: ~0.6 s.
- `build --no-cache` through the Docker builder: 7.6–9.3 s, across fifteen
  builds (one predecessor rebuild, nine ablations, five candidates).
- one 500-tick WASM match through the CLI: ~3.7 s.
- one 32-match ablation sweep: ~2 min; the full 64-match final sweep: ~4 min.
- one `qualify --suite frontline-qualification-5` (17 probe variants from both
  sides across three cumulative tiers): **~5.3 s wall, ~5.4 s CPU at ~110 %** —
  against ~12 s wall and ~92 s CPU at ~790 % for the same suite ID in revision
  2. The suite got dramatically cheaper *and* stopped running wide between
  passes. Very welcome and not explicable from the player side; recorded as an
  observation rather than a complaint, since the outcome is unchanged.
- parsing all 64 final replays: 16 s (26 MB of JSON each, 1.7 GB total).

## Repairs and reconciliation against the current template

`ArenaBasics.cs` was re-synced byte-identical to
`templates/botarena-generic-actor/ArenaBasics.cs` and the diff against the copy
revision 2 carried is where several of this pass's facts came from:

1. **`TryAdvanceToActiveObjective` gained the rotation fallback** the template
   previously lacked — it now searches all cardinals and emits the unlocking
   rotation when the mask refuses the step. Revision 2 had already implemented
   exactly this in `Navigation.Toward`, independently, after `facing-locked`
   made it mandatory; the two agree. Verified equivalent rather than switched,
   because my router also carries the two-pass transient/permanent blocker split
   the helper does not.
2. **`ObjectiveTiles(index)` and `OwnSideObjectiveTiles` are new and public.**
   `ContractView` already resolved the ordered objective chain, so these were a
   confirmation rather than a change; the doctrine keeps its own reader because
   it also needs `AnchorForbiddenTiles` and the tag sets from the same
   dictionary.
3. **`Capture()`, `ObjectivePresence()`, `ArrivalsRallyForward()` and
   `ExpectedArrivalTiles()` are the round's real gift** and I used the first
   three directly. They are the difference between reading three policy ID
   strings correctly and reading them the way the engine means them —
   `controlPolicy.Contains("net-positive-objective-weight-difference")` is not
   something I would have got right from the addendum's prose alone.
   `ExpectedArrivalTiles` is the one I could not make earn its keep (readings 1
   and 5 above); it is a good reader and I did not find the question it answers.
4. **`FindFirstStep`'s new comment about transient occupants** — that blocking
   the router on bodies at every depth surrenders routes exactly when bodies are
   densest, "which is the state a contract that rallies arrivals onto one
   objective region produces on purpose" — describes a bug revision 2 does not
   have (its router already drops transient blockers on the second pass) but
   names the mechanism better than my own comment did.

Genuine mechanical repairs found this pass: none. The probes passed first time
and no contract handling needed fixing, which is the first pass in this lineage
where that is true.

## Strategy passes

One, as budgeted — the three-policy price list — plus the six readings measured
and rejected above. Everything else is template reconciliation and the forced
rebuild.
