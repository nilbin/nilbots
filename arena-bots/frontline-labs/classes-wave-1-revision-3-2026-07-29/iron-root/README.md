# Iron Root

**Class:** bulwark · **Lineage:** iron-root-v1 · **Revision:** 3 (RATCHET CLOCK)
**Doctrine:** FORTRESS ROTATOR · **Role:** verdict-doctrine
**Qualified:** T4 (`frontline-duel-depth-union-t4-v1`)

A bulwark does not win by out-shooting anyone. It wins by making one piece of
ground expensive to stand on for longer than the opponent can afford to keep
paying, and then moving that expense forward.

## The idea

One body **roots**. It walks to a tile beside the active objective whose eight
firing lanes actually cross the scoring surface, and it commits to the transform
windup — a slow, visible, punishable thing — only in a window where nothing on
the board can make it hurt. Once rooted it is a tough omnidirectional gun on a
fast cadence that cannot capture anything, which is the whole trade: it does not
score, it makes the opponent unable to.

Every other body stays **mobile**. They hold the scoring surface, because
territory is the only currency, and they contest rather than concede.

When the front rotates and the lanes stop crossing anything that matters, the
fortress spends its **one return**, walks to the new line, and roots again.

## What revision 3 changed, and why

Revision 2 priced a root in **tenure**: a root has to buy a full declared
capture window of covering, relieved presence. That is still true. It was
written for a frontline that always reverts to the mean.

The capture definition can now declare a **hold**, and the lifecycle definition
can place a returning body **beside the fight instead of at home**. Under a
hold, the losing side's captures are *spent*: the claim resets exactly as a
successful capture does and the objective does not move. So "stand on the
objective" is no longer one instruction. It is two opposite ones, alternating,
and which one is live is not in the observation schema.

Reading revision 2's own ratchet-arm replays:

| Measured over the ratchet cells, revision 2 | |
| --- | --- |
| ticks of a 500-tick match inside somebody's live hold | **160–190** |
| my sole objective presence inside **their** hold | **57–61 ticks** |
| my sole objective presence inside **my own** hold | **0–7 ticks** |
| my completed captures that were **spent** for nothing | **2 per match** |

Nearly all of the doctrine's presence was being spent in the one window where
presence buys nothing, and almost none in the window where it buys double.

So revision 3 keeps the shape and adds the clock:

- **Read the hold, and whose it is.** `ControlResumesAtTick` minus the declared
  redeploy pause is the tick of the last advance, available to a body on the
  first tick of its life with no memory at all. Who owns it comes from a watched
  index change, or from watching a capture collapse to zero without the front
  moving — which cannot be decay, because declared gain and erosion are one per
  tick — and failing both, from the signed displacement of the front, reported
  as the guess it is.
- **Root inside their hold.** Our captures are spent while it runs, so weight on
  the surface buys only denial — and this class's way to deny without weight is
  a gun with three times the cadence, twice the reach and no facing. The windup
  is the cost and the void window is what pays for it.
- **Whether to keep weight inside *our* hold is the control policy's question,
  not the hold's.** Under binary control one body nulls any number, so a second
  and third body add no capture rate and the fortress is free suppression over
  ground that cannot be lost. Under a net-weight policy every body is pressure
  and the hold briefly doubles what that pressure buys, so nothing converts to
  zero weight. Reading the hold without the control arithmetic cost 21 points of
  territory across the plain-ratchet cells.
- **A completion that will be spent moves nothing.** Revision 2 refused to
  commit to a windup whenever a capture was about to finish. Inside a hold that
  protects the other side, that capture resets and the front stays — so the
  refusal was for a thing that will not happen.
- **When our own advance is what emptied the lanes, unroot at once.** Revision 2
  waited out the redeploy pause to confirm the front had moved; under a hold we
  already know it did, and that it cannot come back.
- **Price a death by where it puts the body.** The walk from the contract's
  declared arrival tiles to the scoring surface, compared against the walk from
  the authored spawn — geometry, not the placement policy's name. Where the
  arrival is materially nearer, the body is renewable and the ground is not: a
  holder then eats the bolt until the hit is actually lethal, and a reliever
  inside the windup's own travel budget counts as relief — as a *reinforcement*
  only. The last mobile body on the team never roots against a promise.
- **Under contest arithmetic, know when you are the margin.** Where surplus
  weight scales capture pressure, the body whose departure takes the net
  difference from positive to zero is personally holding the claim up, and it
  does not step off a lane for anything short of a lethal hit. A root is
  likewise only taken from a position that is already net-positive without the
  rooting body's own weight.

One variant was tried and reverted with its measurement, in the source comment
where it was reverted: ranking posts by where reinforcements actually arrive
instead of by the home anchor. It cost **52 points of territory across ten
ratchet cells**. The home anchor is not really "home" — it is the rearmost point
of our own approach, and ranking by it puts bodies on the side of the objective
the opponent has to walk past.

## One artifact, four levels

Nothing in the source names an arm. `ratchetHoldTicks`, `controlPolicy`,
`decayClock` and `automaticReturnPlacement` are read from the resolved contract,
and canonical contracts omit inert fields — so on the unmodified control level
and on the numbers-only level every rule above is *provably* inert. Across those
twenty sparring cells, revision 3 reproduces revision 2 tick for tick: every
accepted action, every argument, every body position, identical.

| Level | What the doctrine becomes |
| --- | --- |
| unmodified control | revision 2, unchanged, cell for cell |
| `--capture-threshold` / `--prime-respawn-ticks` | revision 2 with a shorter tenure, derived from the threshold |
| `sticky-frontline` + `forward-rally` | the hold clock, the free window, forward-priced deaths |
| `+ contest-majority` | and weight arithmetic: no zero-weight conversions inside our own hold, no root without a net-positive relief |

## Movement and facing coupling

The contract may couple facing to movement, and this doctrine reads which arm it
is in from the movement profile rather than assuming one.

| Declared coupling | What the doctrine does |
| --- | --- |
| `preserve-facing` | a step is a free strafe; turning is a separate tick |
| `face-movement-direction` | travel *is* aim, so a body that still has walking to do never spends a tick rotating |
| `facing-locked` | a route that turns a corner costs a rotation per corner, so the body turns onto its route deliberately; leaving a tile sideways costs two ticks, so evasion is priced at two and the gun trades instead |

The same reading re-prices the windup from the other side: a muzzle only punishes
a root if it can reach a firing lane **and be pointed down it** before the
transition completes, and the punisher pays for its approach in exactly the
currency the windup is spent in.

## What it will not do

- **Root without relief** — outside a hold that has made presence worthless.
- **Root as the last mobile body**, on the strength of a reinforcement that is
  merely due.
- **Root into a line that is about to be taken from us**, where taking it would
  actually move the front.
- **Convert weight to suppression while our own hold is live and weight is
  pressure.**
- **Concede a lane.** An idle rooted gun keeps firing down the lane that crosses
  the most contested tiles; suppression is free under the declared cadence.
- **Evade into a coffin.** Time-to-impact is counted in ticks rather than radius,
  and a tile whose only exit is behind a facing-locked body is a trap.

## Reading a match

Decision debug text narrates the doctrine: `rooting: N covered objective tiles`,
`on overwatch: no relief on the surface`, `on overwatch: our hold holds for 31:
weight is pressure`, `on overwatch: contest arithmetic: relief 1 vs 1`,
`front rotated: unrooting after N rooted ticks`, `holding the scoring surface`,
`suppressing the objective lane`, `turning to travel East`, `slipping the shot
West`.

## Files

| File | Role |
| --- | --- |
| `IronRoot.cs` | the policy: roles, tenure gates, stations, threat response |
| `RatchetClock.cs` | whose progress is real this tick, inferred from published fields |
| `ContractLens.cs` | everything the doctrine knows, parsed from the contract only |
| `Kinematics.cs` | what a step costs when facing is coupled to movement |
| `FortressPlan.cs` | covering-tile selection, firing lanes, enemy lane sweep |
| `Gunnery.cs` | fire control: turret headings, straight guns, curved intercepts |
| `ArenaGeometry.cs` | rays, line of fire, flood fill, ordered first steps |
| `ArenaBasics.cs` | the current template helpers, synced verbatim |
