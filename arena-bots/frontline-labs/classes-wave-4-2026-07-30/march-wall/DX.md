# DX notes — march-wall, wave 4 (revision 4)

## Isolation statement

Everything in this revision was authored from the three permitted documents
(author packet, Labs rules card, class addendum — all three hash-verified against
the brief before reading), the `templates/botarena-generic-actor/` scaffold,
`src/BotArena.Sdk/` types, my own frozen directories, my own replays, and the
sandbox CLI. No other entrant's source, standings, replays, aggregate report or
scratch directory was opened. The three frozen predecessor directories
(`classes-wave-1-2026-07-29`, `-revision-2-`, `-revision-3-`) were read and left
byte-untouched; their artifact hashes were re-verified after the pass and are
unchanged. All working files live in
`sandbox/march-wall-w4-scratch-b73f1e28/`, a uniquely named private directory
created for this pass. Every match ever run in this pass had march-wall source on
both sides: the rebuilt revision 3, this revision, or one of its own ablation
variants. Nothing was committed to git (the wave-4 directory is untracked and
`git status` shows no staged or committed change). Nothing to disclose under the
packet's exposure clause.

## Identity

| | |
| --- | --- |
| Entrant | `march-wall` |
| Population / wave | Frontline Labs classes, wave 4 (`classes-wave-4-2026-07-30`) |
| Authoring lineage | `march-wall-v1`, revision 4 |
| Doctrine | A LANE WE ARE LOSING IS A LANE WE CAN TURN AROUND (advancing wall, fourth lineage) |
| Class | `bulwark` (declared in `botarena.json`, unchanged since v1) |
| Role | `verdict-doctrine` |
| Target | cumulative T4 (retain) |
| Budget | one strategic revision; mechanical/contract repairs free |
| Predecessors | wave-1, revision-2 and revision-3 `march-wall`, all untouched |
| Primary cell | `--pendulum keel --skills kit --bend universal --movement facing-locked` → `frontline-labs-1-bulwark-vs-bulwark-rig-facing-locked` |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `b91047df0c0c3e643fd627f45e9f82a0b60b593f986011107125f6ca28c99518` |
| Template helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` (byte-identical copy) |
| CLI | `sandbox/cli-publish`, nilbots 0.9.15 (SDK 0.10.6, game rules 0.5, runtime protocol 0.1) |

## Freeze identity

| | |
| --- | --- |
| Submitted sources | `AnchorPlanner.cs`, `ArenaBasics.cs`, `ContractView.cs`, `FireControl.cs`, `Geometry.cs`, `Lane.cs`, `MarchWall.cs`, `Navigation.cs`, `Pendulum.cs`, `Stance.cs`, `Threat.cs` (5 426 lines) |
| Project metadata | `botarena.json`, `MarchWall.csproj` |
| **`out/bot.wasm` sha256** | **`48e69714fced78b5a1c5a9396b663e72431b642d3928af9b4ff7a0789696eda5`** |
| Canonical WASM | `out/bot.wasm`, 3 435 939 bytes, built by `nilbots build <project> --no-cache`; a second `--no-cache` build reproduced the hash byte-for-byte |
| Deterministic source-tree hash | `1e8b70a6c070be9f5fd839bd6dfd3c7088e291da05db2bb79b78dc3f6a66154e` (sha256 over the sorted sha256 list of `*.cs` + `botarena.json` + `*.csproj`, same recipe as v1, revision 2 and revision 3) |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/guest 0.10.6, WASI p1 core module, platform-matched Docker builder on macOS arm64 |
| Qualification report | `evidence/t4/qualification.json`, sha256 `48ad91dea1e66993fa2f3c5a9637675d88a2b969a1414784b6f1b8f4c48176f7` |
| Verified probe replays | 36 replays under `evidence/t4/`, both team sides, three cumulative tiers |
| Sparring baseline | revision-3 source rebuilt unchanged with SDK 0.10.6 → `8423efb7167edbe707c9c0677dbadf4f40b09fb6a6ca0574313cb29913395584` |

### Per-file source hashes

| file | sha256 |
| --- | --- |
| `AnchorPlanner.cs` | `b4d63ec8968f1011d6c1f3d0e30c7019cffb4f49db264d925316f0fba73fd9d4` |
| `ArenaBasics.cs` | `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` |
| `ContractView.cs` | `cebdf8d15c1ad153cd7f128636b2e79cada301a14047c5889ded559f68d1461d` |
| `FireControl.cs` | `5497b7c28069d26806cc5e6258e5da52d8f12ac017702a79c9314bf01fe7d87a` |
| `Geometry.cs` | `7ae38f0c28cad98882c18fc0e0c107580b5438e70682d715f05210f60a91a827` |
| `Lane.cs` | `03d5f2c92ddc398e8c547d7e3e991a2cc4cd36d0f196d277bcd375dda543f8cd` |
| `MarchWall.cs` | `ca5a56871d433b8a2e965e42f589ae3a77755cdcdc82649d40d1d2d09a5f4492` |
| `Navigation.cs` | `d0689dce04e71a6d1c1cc4ed990abf1c3f2b1c94899bbed19ba893b93a4e3d1e` |
| `Pendulum.cs` | `be9502f662baee0334e730d503e322ec301609ff1df2d61efedb33297d770868` |
| `Stance.cs` | `5c2a7965cef02a51c89ad9b5b80ee6edddc4f59ea82796c436edfcdea1f31d2f` |
| `Threat.cs` | `d6b7bcd90d193016b353b669983aaa19048719147ac6428f46f00f47b0158695` |
| `botarena.json` | `43d359abe4262852ffdfb64249b255e3ece348bb59cbe297adb04e05bf552ecc` |
| `MarchWall.csproj` | `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573` |

## Qualification outcome

`experiment frontline-labs qualify --suite frontline-qualification-5`, profile
`frontline-duel-depth-union-t4-v1`, WASM runtime, artifact `48e69714fced…`.

**Exit 0 — T4 awarded.** Prerequisite T3 PASS (which re-ran and hash-linked T2).
All five T4 probes PASS: `suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`. `balanceEvidenceEligible`
is **`true`** in the report body, and the report's `artifactHash` equals the
frozen artifact hash above. Suite wall time 6.4 s. The suite carries no
pendulum, no skills and no bend envelope, so it is also the check that this
revision's kit code is genuinely inert on a contract that declares no stances —
it passed first time, with no probe repair needed.

## Doctrine, in one paragraph

The wall is still the set of straight lanes our guns close, and revision 3's
price list still decides what a tick of presence is worth. Revision 4 adds the
third answer to a lane we are losing: raise the declared shield, so the contact
that would wound us dies on the arc and is relaunched from our tile along the
exactly reversed heading under our own ownership. It goes up where the route's own
forbidden tile tags allow it (never on an objective tile on this map), against a
body whose *entire* declared arrival envelope is inside the arc we already face —
because a bend goes around an arc — and preferentially while our three-tick
cadence is mid-cycle, so the two windups come out of ticks the gun was not using.
It comes down the tick it stops paying: flanked past the arc, needed as objective
weight, or quiet with the cadence back. Our own fire never enters an enemy arc:
candidate shots are filtered by ARRIVAL heading inside the tracer, so the cheapest
*accepted* shot wins and a bend is chosen over a straight bolt exactly when the
flank requires it, rather than feeding three bolts into a shield to force a break.

## Measured per-arm records

Sparring baseline: revision-3 source rebuilt unchanged with the current SDK.
Eight seeds (104729, 130363, 155921, 202961, 224737, 262147, 293459, 350377),
both team sides, `--classes bulwark-vs-bulwark --movement facing-locked`, WASM
runtime: **16 matches per arm, 64 per artifact**. Territorial progress is summed
over the sixteen matches from revision 4's side.

| arm | flags | ruleset token | record (W-L-D) | territorial | early endings |
| --- | --- | --- | --- | ---: | --- |
| kit off, striker-only bend | `--pendulum keel` | `keel` | 8-6-2 | +26 | 0 |
| kit ON, striker-only bend | `+ --skills kit` | `helm` | **11-2-3** | **+198** | 0 |
| kit off, universal bend | `+ --bend universal` | `veer` | 8-6-2 | +69 | 2 |
| kit ON, universal bend | `+ --skills kit --bend universal` | `rig` | **15-1-0** | +80 | 0 |

**The mirror floor that makes those numbers readable is exactly zero, and it is
zero by construction, not by luck.** The rebuilt revision 3 against itself over
the same 64 cells scores 5-5-6 and **+0 territorial in every one of the four
arms**: with the same artifact on both sides, a cell's two sides are the same
match with the teams relabelled, so the sum cannot be anything else. The
empirical run confirms it and also confirms something more useful — for the
baseline, `keel` and `helm` are *identical* match-for-match, as are `veer` and
`rig`. The predecessor never touches a stance, so the kit-off/kit-on contrast in
the table above is entirely this revision's.

Per-match territorial, this revision, `rig`: `7 7 3 3 7 8 7 7 3 3 3 −15 7 13 3
14`. Same for `helm`: `0 26 8 4 −3 13 26 15 26 0 26 26 −8 0 13 26`.

## Skill-usage counts

Sixteen matches per arm, this revision's side only.

| arm | volleys cast | shells raised | deflections made | shells broken by budget | shields dropped by our own decision | bends fired (mine / theirs) | turrets stood back up | slots fielded |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `keel` | 0 | 0 | 0 | 0 | 0 | 0 / 0 | 5 | 3 |
| `helm` | 0 | 27 | 19 | 0 | 27 | 0 / 0 | 18 | 3 |
| `veer` | 0 | 0 | 0 | 0 | 0 | 212 / 198 | 10 | 3 |
| `rig` | 0 | 91 | 93 | 0 | 91 | 322 / 390 | 52 | 3 |

Four of those columns deserve a sentence each.

- **Volleys: zero, and structurally so.** The fan is the striker's. The stance
  machinery is written once and recognizes a fan by `volley.projectileCount > 1`
  on the target form's attack profile, so an artifact handed that kit would cast;
  `Stance.VolleyRoute` returns null on every arm a bulwark plays, and the entry
  predicate is therefore the one piece of this revision no measurement covers.
- **Slots: three, read rather than assumed.** `SlotCount` comes from the
  topology's `unitSlots`, and the fortification ration is now capped by the
  opposing roster's size so it cannot demand more scorers than the other side can
  field. A bulwark mirror is 3-vs-3, so the cap never binds here; it exists for the
  asymmetric-slot arm.
- **Shells broken: zero, in 118 raises.** The doctrine always left on its own
  decision before a third deflection, because "the cadence is back and the arc is
  idle" fires long before the budget does. The forced return is therefore a rule
  this bot never reached — worth stating plainly, because it means the break
  mechanic priced nothing for this doctrine.
- **93 deflections against 91 raises in `rig`** means several entries turned more
  than one bolt, and it is the one number that shows the kit doing work rather
  than being handled.

## Attribution: what the kit is worth, and what the repairs are worth

Same 16 cells per arm, same opponent. "Kit doctrine off" is this exact source
with the shield triggers and the arc-aware fire filter switched off and nothing
else changed, so the difference is the strategic revision and the rest is the
three mechanical repairs.

| arm | full revision 4 | kit doctrine off (repairs only) | the kit's contribution |
| --- | --- | --- | --- |
| `keel` | 8-6-2, +26 | 8-6-2, +26 | **exactly nothing — identical in all 16 cells** |
| `helm` | 11-2-3, +198 | 8-6-2, +24 | **+3 wins, 4 fewer losses, +174 territorial** |
| `veer` | 8-6-2, +69 | 8-6-2, +69 | **exactly nothing — identical in all 16 cells** |
| `rig` | 15-1-0, +80 | 8-6-2, +54 | **+7 wins, 5 fewer losses, +26 territorial** |

The two identity rows are the requirement "one artifact plays all four cells"
discharged as a measurement rather than an intention: on the arms that declare no
stance, the whole strategic revision provably cannot disturb a match it does not
apply to. The repairs alone still beat the predecessor on every arm, which is the
honest way to read the totals: a large part of this revision's margin is
revision 3's own bugs.

## The candidate that scored better and was not shipped

Two versions of one predicate were measured over the full 64 cells. Both are
correct readings; they differ in the quantifier.

| | `aimed` = a straight bearing into the arc | `aimed` = every declared arrival heading inside the arc |
| --- | --- | --- |
| `keel` | 8-6-2, +26 | 8-6-2, +26 |
| `helm` | 11-2-3, +198 | 11-2-3, +198 |
| `veer` | 8-6-2, +69 | 8-6-2, +69 |
| `rig` | **14-2-0, +335** (26 shells, **1** deflection) | 15-1-0, **+80** (91 shells, 93 deflections) |
| `rig` mechanism | 51 advances against 28, 11 base breaches | 65 advances against 65, 0 breaches, 5 996 objective body-ticks against 4 239 |

The shipped artifact is the right-hand column, and the left-hand column is the
uncomfortable evidence. On the three arms where a bulwark's gun cannot bend, the
two are identical match-for-match — a straight-only envelope has exactly one
arrival heading, so the quantifier is vacuous. Under the universal bend they
diverge sharply: the loose predicate wins two fewer matches but scores four times
the territorial margin, because it breaches the base in **11 of its 16 cells**
(+30 each) where the strict one never breaches at all and grinds out +3 to +14 at
the tick cap. I shipped the strict one on
record (15-1-0 versus 14-2-0), on mechanism (93 deflections versus 1 — the kit is
what this wave measures, and a rule that never fires measures nothing), and
because it is the reading the contract supports: whether an arc can be beaten is a
question about the shooter's declared envelope, not about one bearing. A reader
who weights margin over record should prefer the other artifact, and the
underlying source differs by one predicate.

The mechanism row says what the trade actually is, and it is not subtle: the loose
predicate **pushes** (51 advances against 28, and it breaches), while the strict
one **denies** (advances dead level at 65 apiece, but 1 757 more objective
body-ticks and 42 fewer deaths, which is how it converts a frozen front into a
one-sided tick-cap ranking). Those are two doctrines wearing the same source file,
separated by a quantifier. I did not expect a predicate that only changes WHEN a
shield goes up to change what the bot is FOR, and that is the most interesting
thing this pass measured.

## Measuring a rule the sparring baseline cannot exercise

The arc-aware fire filter is unmeasurable against revision 3, which never raises a
shield: there is no arc to poke. So it was measured against **this bot with only
that filter switched off**, four seeds, both sides, eight cells per arm.

| arm | arc-aware filter on, versus filter off |
| --- | --- |
| `keel`, `veer` | +0, 3-3-2 — no guards declared, so both artifacts are the same bot |
| `helm` | **0-0-8, +0** — eight draws, 96 shells raised on each side and **zero deflections in either direction** |
| `rig` | 4-3-1, **+53** |

The `helm` row is the finding worth carrying out of this pass, and it is a
balance observation rather than a bot one. When two shields meet on a
straight-only envelope the match becomes a total mutual null: both doctrines raise
into each other's aim, neither can fire while raised, and in eight matches not one
bolt ever touched an arc. The same mirror without the kit produces 5-5-6 with
decisive matches. The kit did not make that mirror sharper; it froze it.

In `rig` the filter is worth about +53 territorial and a 4-3-1 record over eight
cells — suggestive, not settled, and the honest description of an effect measured
on n=8.

## One repair that changed nothing at all, measured anyway

The shield's drop rule originally read "the cadence is back and no bolt is in the
air", which drops a shield raised in anticipation of a shot the moment before that
shot exists — strictly worse than never raising it. The fix ("and nothing is aimed
at us either") is obviously right and, measured over the full 64 cells, is
**exactly inert**: identical territorial, record, end tick and every one of eleven
counters in all 64 cells. It is shipped because it is correct, and recorded here
because "obviously right" and "changes an outcome" are independent properties, and
this lineage has now measured both directions.

While confirming that, one methodological trap: **a replay hash is not a
behavioural fingerprint.** `header.provenance` embeds each participant's artifact
hash, so two behaviourally identical artifacts differ in every replay hash. My
first identity check reported 0 of 32 cells identical when the correct answer was
32 of 32; the usable comparison is the per-match outcome and counter vector.

## Top three frictions

**1. The stance's tile tags silently delete the kit's headline property, and no
prose says so.** The addendum sells the shell on "objective weight stays **1**, so
it still holds ground" — the sentence that distinguishes it from the turret
bargain. On `frontline-labs-01-classes` that property is unreachable: the shell
route's `placement.forbiddenTileTags` is `["transition-placement-forbidden"]`, and
that tag covers **all 22 objective tiles across all five positions**, both home
pads, and the entire central corridor including both one-tile chokes at `(8,7)`,
`(9,7)`, `(13,7)`, `(14,7)`. A shield can never stand on the ground it would hold,
nor plug the corridor everything must walk through. Finding this took a tile-tag
dump plus a legality read; the `transform` legality entry on an objective tile
reports `available: false` while still listing both stance forms in its
`allowedFormIds` constraint, which reads as "you may become a shell here" to
anyone who checks the constraint before the flag. One sentence in the addendum —
"stances obey the same forbidden-tile tags as Anchor, so on the shipped map they
are off-objective only" — would have redirected an entire day of doctrine work.

**2. A parameterless route is invisible to the obvious way of submitting
transitions.** `mobilize` declares `parameterKinds: []`. A bot that starts
same-life transitions by searching available transition actions for a form-target
constraint naming its destination — which is how the whole catalog's other
transition works, and what this lineage did from v1 — matches nothing, and so can
never leave a turret or a stance. There is no error: the action is available, the
route is declared, the decision is simply never built. My revision-3 turrets could
not stand up in *any* class arm, through three frozen waves, and the measured cost
was large (52 mobilizes in `rig` alone). The route names its action and the action
declares its parameters, so the fix is small; the trap is that a payload-shaped
search fails silently rather than illegally. The class card's "reversible Anchor,
prime included" is the promise; `parameterKinds: []` is the fine print.

**3. Windup 1 does not mean "in time".** A stance completes at the end of the tick
it is requested, after mode update — so it protects the tick *after* this one. A
bulwark bolt launches one tile and then crosses two tiles per advance, so at the
range this class actually duels at (two tiles) an incoming bolt is visible for
exactly one tick before impact and every shield raised at it is a tick late. My
first two shield implementations raised on bolts in flight and fired **zero** times
in 500 ticks. The only usable trigger is pre-shot geometry — a body whose declared
envelope covers your tile — which is a different question requiring a different
reader. The addendum describes the windup as the cheap half of the exchange
(true), and the SDK's transition-completion policy ID states the timing exactly
(`end-of-started-tick-plus-duration-minus-one-after-mode-update`); what neither
does is connect the two into "a one-tick windup cannot answer a bolt already in
the air".

## Other documentation gaps

- **The quadrant predicate is a policy ID and nothing else.**
  `facing-quadrant-contacts-deflected` is the whole specification of which
  contacts a shell turns. It does not say whether "facing quadrant" is 90 degrees
  or 45, nor whether the test applies to the bolt's travel heading or the bearing
  it came from. I implemented "the reverse of the travel heading is within one
  45-degree sector of the facing" and then verified it against the published
  events across 96 replays: **121 of 121 observed deflections satisfy it**,
  spanning eight distinct (facing, travel) pairs including diagonal arrivals at
  exactly ±1 sector (a `north`-facing shell turning a `south-east` bolt). The
  negative half is confirmed by exactly one event in the whole pass — a
  `south`-facing shell taking damage from a `west`-travelling bolt, which the
  predicate correctly says is a flank contact. Guessing a geometric rule and
  validating it from event logs worked, but the arc is the one piece of the kit a
  bot cannot read.
- **`ratchetHoldTicks` is now easy and `controlResumesAtTick` is now the odd one
  out.** `holdOwnerTeamId`/`holdEndsAtTick` are exactly the two facts revision 3
  asked for, delivered with the grammar it asked for. Having them makes the older
  clock's role clearer and my old blind-spot complaint obsolete: I deleted 40
  lines of derivation and the doctrine got strictly better informed. Thank you.
- **`ObjectivePresence` is a lower bound and a bulwark's bound is very loose.**
  Carried from revision 3 and still true: vision range 4, omnidirectional, against
  a six-tile objective. Two of this revision's shield rules key on that read, and
  they are keying on "what my team can see", which for this chassis is often
  nothing.
- **The five-slot arm is unreachable from a bulwark manifest, so its readers are
  untestable.** `SlotCount` and the ration cap are written and verified only by the
  3-vs-3 case they cannot affect. A `--classes bulwark-vs-fabricator --skills
  five-slots` cell was outside this brief's four combinations, so the code is
  contract-driven on inspection alone.

## Confusing terminology

- **"Hold" now means three things**: the ratchet's protection window (published),
  a doctrine's sense of holding ground, and `HoldTheWall`/`HoldTheShield` as
  method names. Revision 3 flagged two of them; the kit added the third.
- **"Objective weight"** remains the most doctrine-relevant number with the least
  descriptive name — a franchise, not a multiplier — and the shell is where that
  bites hardest, since its weight is advertised and unusable.
- **"Stance" versus "form" versus "route"**: a stance is a form you reach through a
  route whose return leg carries a budget. Three words for one mechanism, none of
  them the contract's own — the contract just has `sameLifeTransitions` with an
  `automaticReturn` property, which is clearer than the prose.
- The `frontline-qualification-N` / `TN` off-by-one is still jarring, four waves in.

## Timings (Apple Silicon, warm)

- managed edit/compile loop: ~0.6 s.
- `build --no-cache` through the Docker builder: 7.6–10.5 s across nine builds
  (one predecessor rebuild, three candidates, two ablations, three iterations).
- one 500-tick WASM match through the CLI: ~3.9 s.
- one 64-match sweep: ~4 min. Six sweeps totalling **320 WASM matches** were run,
  two at a time (18 cores; two concurrent runners saturate them).
- `qualify --suite frontline-qualification-5`: **6.4 s wall** for 17 probe
  variants from both sides across three cumulative tiers.
- parsing 64 replays for counters: ~35 s (26 MB each).

## Repairs and reconciliation against the current template

`ArenaBasics.cs` is byte-identical to the current template. The diff against the
copy revision 3 carried added exactly what this pass needed: `LiveHold` (the
published hold, which retired my `Pendulum` derivation) and
`Threat(projectile, tile)`/`Incoming` (per-projectile cadence and damage, which is
where I learned revision 3's threat horizon was wrong for any bolt slower than one
tick per advance). My own `Threat.cs` keeps a wall- and corner-aware walk the
helper does not do, so the helper's arithmetic was adopted rather than its call.

Three genuine mechanical repairs, all described above: the parameterless route,
per-projectile damage and cadence, and the hold read. Plus two smaller contract
reads with no measurable effect in these cells and a clear purpose elsewhere:
observed `spawnReservation` claims are routed around (no fabrication exists in a
bulwark arm, so it never fires here), and `classId` is read from the topology for
identity while the scaffold's form-ID-prefix recovery is deliberately not called.

## The frozen artifact is not a frozen bot, again, and worse

Revision 3 recorded that its predecessor's frozen `.wasm` faulted at tick 0 on the
`ratchet` arm while still working on the control arm. The frozen revision-3
artifact (`3a0d079b1090…`, SDK 0.10.4) now faults at tick 0 on **every** arm
tested — `rig`, `keel`, and the bare class pair — with
`WASM generic actor exited before its life ended (peak completed tick fuel
0.0M/200.0M)`. The observation schema itself moved this wave (`classId`,
`spawnReservation`, `ticksPerAdvance`/`damagePerHit`), so there is no longer any
cell in which an old artifact is playable. The freeze that survives a contract
addition is the source plus its deterministic tree hash; the artifact hash pins
provenance, not portability. A cohort that archives artifacts and expects to
replay them against a later arm is archiving a fuel-exhaustion draw — and this
time it is archiving one against itself as well.

## Strategy passes

One, as budgeted: spend the kit. Three shield triggers (brace, cycle, screen), one
cycling rule, and one arc-aware fire filter, plus the predicate variant measured
and reported above. Everything else in the diff is the three mechanical repairs,
the two inert contract reads, and the template re-sync.
