# DX report — iron-root, revision 2

Written from this revision's own forensics, qualification report, and private
sparring runs. No other entrant's source, directory, replays, standings, or
aggregate balance report was opened, and nothing was read from a shared scratch
path. This revision's private scratch was `sandbox/iron-root-v2-scratch-9f3a1c`,
a uniquely named directory created for it.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `iron-root` |
| Population / wave | Frontline Labs classes, wave 1 **revision 2** (`classes-wave-1-revision-2-2026-07-29`) |
| Authoring lineage | `iron-root-v1` |
| Class | `bulwark` (declared in `botarena.json`) |
| Doctrine | FORTRESS ROTATOR — revision 2 codename TENURED ROOT |
| Role | `verdict-doctrine` |
| Target tier | cumulative T4 (retain) |
| Budget | **one** strategic improvement revision; mechanical repairs free |
| Predecessor | `classes-wave-1-2026-07-29/iron-root`, left untouched |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `3fb217bbf9ad1e181c103ebf19cd4b56ed1e8d38c54343fdc5cc7e6531b1aedf` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `4795ee5ac1f2afffd27d532eeee1a70242ca139a3dd64557015975d14ba427c3` |
| Source-tree hash | `9bf1b4caebefdfb77b3d608ecd8ce01aa5f54e20ad77cfa73db20033abbd114b` |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/Guest `0.10.4`, actor protocol 1.0, WASI p1 core module, platform-matched Docker builder (macOS arm64 host) |
| Build cache key | `04f2be93ba11fdaadf0488fb43f1b1c159af9116ef112c45f9e816300dac604d` (final `--no-cache` compile) |
| **`out/bot.wasm` sha256** | **`793c4f2e3406c5ea29efdc5b8f4f1ff6830449be4042c7bc52baa589bca4841c`** |
| `evidence/t4/qualification.json` sha256 | `bb0479c8acb027235787cafe1ebc9001943845e60f257f5faa93f65fd2eb8a8e` |
| Cumulative T3 prerequisite report sha256 | `145629b7c1f53353136f662c41d65aa8953525d17be2330e09f5a2d5467f1c5a` (hash-linked inside the T4 report) |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| Qualification outcome | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, **exit 0**, **T4 retained**, `balanceEvidenceEligible: true` |

All five T4 components passed (`suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`), with the cumulative T3
and T2 prerequisites rerun and hash-linked automatically.

## Per-file source hashes

The source-tree hash above is the lineage's recipe, stated exactly because
revision 1 wrote it in prose and I had to rediscover it by trial:

```bash
ls *.cs botarena.json IronRoot.csproj | LC_ALL=C sort \
  | xargs shasum -a 256 | shasum -a 256
```

That is sha256 over the ordinal-sorted `"<sha256>  <filename>\n"` lines — the
names are inside the digest, not only the hashes. Re-running it against the
frozen revision 1 tree reproduces its recorded
`0b1cf8673df95cf328a39f90487f383ab6bf653ba5db8ed750e79dde6271e728`, which is
also this revision's evidence that revision 1 was left untouched (its
`out/bot.wasm` still hashes to `ed5c7bcc…`, as recorded in its own DX).

```text
9cae31d594188f46403887578c2347307cfecd018b2a9d84e06fc82c09afb194  ArenaBasics.cs
188aafbc2161fb971d773b7a3a446f4cc1eb285dcd1e642f64f03c878044f6eb  ArenaGeometry.cs
21c16ae6f9588a0233a0ae29ee15b692712f2e3cb43f5c5309894bc84df772cf  ContractLens.cs
fc658780b30aa96d0aa0de2c9403c8640f48a5b75a3c08524c14c2f5daff3413  FortressPlan.cs
dfe64b33d0f4ee50d8d08ed8748114da966c488251623a06fc18a887414780d4  Gunnery.cs
2853952da809a594078673f4446c0fc0f33617c239a5923fc304e4b63bd29040  IronRoot.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  IronRoot.csproj
4e69aef2560b076e1c114d65385b553b7052a28fcc51c7371887fe882d39032a  Kinematics.cs
f76050b17a6dfb7a8a42c86d25fef84423494035d210ac55a094678f0508383c  botarena.json
```

## Timings (Apple Silicon, warm Docker builder)

| Step | Time |
| --- | --- |
| `dotnet build` of the editing project | ~0.5 s |
| `botarena build` (warm cache) | **0.05 s** |
| `botarena build --no-cache` (cold, Docker) | **7.8 s** |
| in-process 3-seed batch (1500 ticks, both bots rebuilt) | 6.1 s |
| WASM 500-tick class-arm match | 3.7 s |
| `qualify --suite frontline-qualification-5` (WASM, both assignments) | **10.9 s wall / 88 s CPU** |
| full 27-cell sparring sweep (3 classes × 3 maps × 3 seeds, in-process) | ~110 s |

The inner loop stayed excellent. The single most valuable thing about it for a
*revision* pass — as opposed to a first pass — is that a whole 27-cell A/B
sweep of the previous freeze against the new candidate costs under four
minutes, which is what made the strategy claims below measurable rather than
argued.

## Loss forensics: what the wave-1 replays actually said

Revision 1 held T4 and went 15–39–0. The brief's working hypothesis was that
forward prime anchors were being punished and that relief-and-lane pricing was
optimistic. Reading 54 of my own replays, the first half is wrong and the
second half is right for a different reason. Outputs are in
`evidence/forensics/`.

1. **Turrets were not punished; they were wasted.** Per completed root: median
   tenure 12 ticks (vs bulwark), 25 (vs fabricator), 15 (vs striker); mean
   damage absorbed 0.67 / 0.00 / 1.40; mean kills scored 0.29 / 0.43 / 0.40.
   Nothing was shooting at them. They just did not matter.
2. **Four out of five roots ended in an immediate return** (57/72, 21/21,
   36/45). The reflex was `mobileAllies == 0 && (enemyPressing || endgame)`,
   which a single tick of relief gap satisfies. Because the reverse route is
   `irreversibleForLife`, each of those permanently deleted the life's anchor
   option — the doctrine's entire identity — in exchange for nothing.
3. **The relief test was satisfied permanently.** It asked "does a companion
   exist or is one due within windup + a settling period", which is true from
   the first unlock tick onward. The roots therefore clustered at ticks
   108–118 and then wherever, with the relief usually nowhere near the surface.
4. **Fire control was clean.** Audited against the authoritative per-tick
   legality mask, of 21 822 waits there were **zero** where an available attack
   action could have hit a visible enemy that tick, and **zero** where a
   rotation would have lined one up. 5 598 were an enemy reachable only on a
   diagonal, which a straight-only chassis genuinely cannot shoot — that is a
   positioning fact, not a bug, and it is now a small tie-break term.
5. **The real deficit was presence, and it was structural.** Uncontested
   objective ticks, mine vs theirs: 116 / 144 vs striker, 53 / 73 vs
   fabricator. 51% of all decisions were a wait, and roughly 280 ticks a match
   were bodies standing on distant overwatch posts that merely *saw* the
   surface. Against fabricators I averaged 9.7 kills to 3.7 deaths **and still
   lost every non-thin-fronts cell** — killing a class that rebuilds in 15
   ticks buys nothing; standing on the tile does.

## The one strategic revision: TENURED ROOT

One idea, applied at both ends of the fortress lifecycle: *a body's commitment
is priced in the uncontested presence-ticks it buys, and a root that cannot
serve a tenure is a screen*.

- root only with **relief already in place** (an allied mobile body on the
  surface or one step off), never on "a companion exists" or "one is due";
- **the return is a rotation, not a reflex** — spent on coverage that has been
  zero for the full declared redeploy pause, or on a last call when only a body
  on the surface can still change the result;
- the **cheapest body roots**: shortest declared windup, then the slot the
  contract does *not* renew automatically. Revision 1 preferred the prime,
  which is both the longer windup (3 vs 1) and the renewable body;
- **stations are the scoring surface first**, preferring tiles an allied
  fortress actually covers, overflowing to posts one step off — because
  suppression only becomes territory when somebody stands under it;
- **durability is spent on ground**: a holder eats the bolt and keeps the tile
  unless the hit would leave it too thin, or the mode's own pause says presence
  pays nothing this tick.

Measured against a fixed private sparring set (my own template-starter partners
in the three chassis, 3 classes × 3 maps × 3 seeds, in-process):

| | v1 frozen | revision 2 |
| --- | --- | --- |
| record | 9–18–0 | **14–9–4** |
| mean territorial score | −3.1 | **+6.0** |
| vs striker | 3–6 (−5.0) | **4–2–3 (+5.9)** |
| vs fabricator | 3–6 (−2.2) | **4–5 (+3.9)** |
| vs bulwark | 3–6 (−2.0) | **6–2–1 (+8.3)** |
| median rooted tenure | 4 ticks | **60 ticks** |
| sole presence, mine / theirs | 60 / 53 | **60 / 42** |
| outer-shoulder-bypass map | 0–9 (−17.9) | **2–4–3 (−3.6)** |

Head to head against the frozen v1 artifact, both sides, all three movement
arms: **9–0**.

One tried-and-rejected variant is recorded in the source comment where it was
reverted: interleaving surface and ring stations (one holder, one body a step
off, one holder) cost nine wins, 14–9–4 → 9–18–0. Actors block actors, so a
full surface is ground the opponent cannot walk onto at all; spread exposure is
worth less than denied entry. That measurement is the reason the code looks
"greedy" about the surface.

## Movement-arm adaptation

`GenericActorRulesContract.MovementProfile.FacingCoupling` is read per form and
turned into three quantities, all in `Kinematics.cs`:

- **route cost** — under `facing-locked` a route that turns a corner needs a
  rotation per corner, so the body deliberately turns onto its route;
- **evade cost** — 1 tick normally, 2 when the escape direction is not the
  current facing, which moves the dodge trigger and makes a corridor whose only
  exit is *behind* a facing-locked body count as a trap;
- **punish time** — a muzzle must reach a firing lane *and* be pointed down it.
  Omnidirectional guns owe nothing; `face-movement-direction` costs the
  approacher one rotation on arrival (it lands facing its own travel
  direction); `facing-locked` costs it two (one to travel, one to aim). This is
  the "enemies dodging your covering fire pay with their aim" effect, expressed
  as the exact term it belongs in: **the windup is measurably cheaper on the
  coupled arms**, so the doctrine's forward roots survive there.

Under `face-movement-direction` the policy additionally stops spending ticks on
alignment rotations while it still has walking to do — travel *is* aim, and the
next step would overwrite the rotation anyway.

**The repair this exposed is severe and it also affects the shipped starter.**
`ArenaBasics.TryAdvanceToActiveObjective` passes the movement legality mask as
`allowedFirstSteps` into the route search. On the `facing-locked` arm that mask
contains exactly one direction, so every route that is not already straight
ahead is pruned, no step is found, and the bot waits. Revision 1 inherited the
same idiom and consequently, on `facing-locked`, waited **78% of its ticks**,
never anchored once, and finished a 500-tick mirror 0–0 with zero sole presence
on either side. Routes are now searched over every cardinal and the body emits
a rotation when the wanted first step is not the one it faces. Post-repair, the
same mirror is a 30–0 win with 105 sole-presence ticks to 51.

## Template sync and reconciliation

`ArenaBasics.cs` was synced verbatim from `templates/botarena-generic-actor/`.
Reconciliation notes:

- `ArenaBasics.OrderedDirections` is now the **only** source of direction
  tie-breaks in this bot: route search, evasion candidate order, and trap
  counting. `ArenaGeometry.FirstStep` gained an explicit `searchOrder`
  parameter for exactly this and made the legality-mask filter optional.
  Because the helper draws from `context.Random`, it is called **once per
  tick** and cached; calling it per decision would make two decisions in the
  same tick disagree about which lateral is preferred.
- `ArenaBasics.Wait`'s tiering (available wait → unavailable wait → available
  parameterless → first declared) replaced this bot's own fallback, which could
  throw when no parameterless action existed. It now cannot throw at all.
- `ArenaBasics.Capabilities` / `ClassOf` were read and deliberately **not**
  branched on by name. The addendum's advice to prefer stats and routes over
  class names is right, and this doctrine already derives "static form",
  "anchor route", "reversible route", "fabrication source region", and now
  "facing coupling" from the contract, which is strictly more general. `ClassOf`
  is genuinely useful for a *narrating* bot; a policy that needs it is a policy
  that is about to hard-code.
- `ArenaBasics.TryDirectShot`'s straight-only handling (an attack action
  declaring no parameters fires along facing) matches this bot's `Gunnery`
  path; no change needed. `Occupied` now filters allied projectiles through the
  declared `AlliedProjectileContact` policy, which this bot was already doing
  by hand.

## Documentation gaps

- **The shipped starter freezes on the `facing-locked` arm** (above). Either
  `TryAdvanceToActiveObjective` should search unrestricted and emit the turn,
  or the template README should say in one line that a facing-coupled contract
  makes the movement mask a *filter on the last step*, not a search space. As
  it stands the arm's own headline behaviour is a trap laid by the helper the
  template tells you to start from.
- **Where `facingCoupling` lives is not stated in player docs.** The rules card
  says "Experiment arms may couple facing to movement; read the contract, not
  this sentence" — good — but not *where*. It is on the **movement profile**,
  referenced by each form, and the field is omitted from canonical bytes when
  it is the inert default. That is three hops and one absence to discover; one
  sentence in the rules card would remove all three.
- **`ObservedProjectile` still cannot answer "should I eat this?"** It reports
  `TilesPerAdvance` and `TicksUntilAdvance` but neither `TicksPerAdvance` nor
  damage. Time-to-impact needs the first (I take the minimum declared cadence)
  and the eat-or-move decision needs the second (I take the heaviest declared
  hit). Both stand-ins are deliberately pessimistic, which is exactly wrong for
  a chassis whose doctrine is to trade health for ground.
- **`OrderedDirections` consumes the deterministic random stream.** Its XML
  comment explains *why* the order is randomized but not that calling it twice
  in one tick yields two different orders. Anyone using it inside a scoring
  loop will get incoherent tie-breaks and no error.
- **The class addendum still does not connect `objectiveWeight: 0` to "a
  bulwark that anchors before relief exists has deleted its scoring body".**
  Carried over from revision 1; it is the whole class and it is still left to
  be rediscovered, and my own wave-1 record is the receipt.
- **The addendum predates the movement arms** and does not mention them,
  although `--movement` composes with `--classes`. A bulwark reads "the prime's
  three-tick windup is a visible, punishable commitment" and has no way to know
  that the punishment is roughly a third cheaper to inflict on one arm than
  another.

### One gap from revision 1 that is now fixed

`--ignore-declared-classes` did not exist when revision 1 was written, and its
absence was that report's single biggest complaint: a class-declaring project
could not be run locally against the base contract it is actually qualified on
without maintaining a second class-free copy of itself. The flag now does
exactly that. Worth saying plainly because it closed the worst workflow hole in
the experiment.

## Hardcoding temptations

All resisted; the new ones this revision created:

- **Windup 3 for the prime, 1 for the child.** The whole "cheapest body roots"
  rule is one `Windup.DurationTicks` comparison away from being two magic
  numbers, and the two-number version would have been indistinguishable on the
  class arm while inverting itself on the base contract, where the prime has no
  anchor route at all.
- **"Capture window = 20."** Threshold 15 plus redeploy pause 5 is right there.
  It is `CaptureThreshold + RedeployPauseTicks` in the lens, which is what
  keeps the tenure arithmetic meaningful when `--capture-threshold` moves it.
- **Coupling by arm name.** The ruleset ID literally contains `facing-locked`
  and `sets-facing`. Reading the enum off the movement profile is four lines
  and is the only reason a fourth arm would work.
- **Evade cost 2.** Tempting to write "dodging costs two ticks now". It is a
  function of coupling *and* whether the escape direction is the current
  facing, and the difference matters every time the escape is straight ahead.
- **Objective tile counts.** The centre position is 6 tiles on the current map
  and 4 elsewhere; the station list is built from the region, not the number.

## Confusing terminology

Carried forward and still true: "Anchor"/"Mobilize" are prose words with no
contract representation; `irreversibleForLife` reads backwards on the reverse
route; "Available" versus "will succeed"; `ObservedSound.Distance` is a band
index and `Bearing` a sector index, both plain `int`.

New this revision:

- **"facing-locked" reads like "you cannot turn".** It means the opposite of
  what a first reading suggests: rotation is completely unrestricted, and it is
  *movement* that is restricted to the facing. The enum's own doc comment says
  so precisely; the arm's name does not.
- **`PreserveFacing = 0` is both "the default" and "the value a missing field
  means".** Correct and documented, but it means a contract that publishes no
  `facingCoupling` and a contract that publishes `preserve-facing` are
  indistinguishable to a bot — which is fine, and worth one sentence so nobody
  writes a "did the contract declare coupling?" branch.
- **"Tenure" is my word, not the contract's.** Flagging it because the debug
  strings use it: nothing in the schema knows what a tenure is.

## Repairs and strategy passes

One strategic revision; the rest are mechanical repairs, each driven by a
measurement rather than a hunch.

1. **Strategy — TENURED ROOT** (the one revision; five coupled rule changes
   listed above, all justified by the forensics section).
2. **Repair — facing-locked navigation.** Route search no longer prunes to the
   movement mask; the body turns onto its route. 78% waits → 35%; 0–0 mirror →
   30–0.
3. **Repair — coupling-aware windup pricing and evasion.** `TicksToFirstShot`
   and `EvadeCost` replace a bare distance test and a fixed 2-tick trigger.
4. **Repair — ordered direction tie-breaks everywhere**, cached once per tick.
5. **Repair — `SafeAction` can no longer throw**, matching the template's
   `Wait` tiering.
6. **Repair — the diagonal blind spot.** A straight-only chassis cannot shoot a
   diagonal target at all; cardinal alignment with visible enemies is now a
   small tie-break in tile scoring, worth far less than safety or ground.
7. **Repair — dead state removed.** The old distant-overwatch post list became
   unreachable when stations moved to the surface and was deleted rather than
   left to rot.

## What I could not evaluate

Revision 1's honest complaint was that a mirror only measures who reaches the
covering tile first. This revision fixed that by writing three sparring
partners of its own — the template starter, one per chassis, in private scratch
— so the class matchups became measurable without reading anyone else's work.
That is a real improvement in evidence quality and it has a real ceiling: the
template starter is *one* policy, and it is a policy that walks at the
objective and shoots. Everything above is conditional on that opponent model.
The sparring partners also inherit the facing-locked navigation freeze
described earlier, so the coupled-arm sparring cells flatter this bot; the
head-to-head against frozen v1 is the honest measurement there.

Two specific things I still cannot see:

- **Whether the tenure rule is too strict against an opponent that actively
  hunts turrets.** My partners never do, so my measured "0.7–1.4 damage
  absorbed per root" may be an artefact of them and not of the class. The
  windup pricing is built to answer that from observation, but it has not been
  tested against anything that tries.
- **The `current` map.** It remains this doctrine's worst cell (3–5–1, −8.3),
  and the trace of the losses is a late-game simultaneous wipe of three
  low-health bodies inside one beaten zone followed by an uncontested
  three-position collapse. The obvious counter — spreading the bodies — was
  tried and measured strictly worse. I do not know the right answer and would
  rather report that than spend a second strategic revision guessing at it.
