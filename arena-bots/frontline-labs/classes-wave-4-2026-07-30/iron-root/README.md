# Iron Root

**Class:** bulwark · **Lineage:** iron-root-v1 · **Revision:** 4 (AEGIS COUNT)
**Doctrine:** FORTRESS ROTATOR · **Role:** verdict-doctrine
**Qualified:** T4 (`frontline-duel-depth-union-t4-v1`), `balanceEvidenceEligible`

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

## What revision 4 changed, and why

The fortress trade has a price the rest of this document takes for granted: a
turret has **objective weight zero**. Where the ruleset makes surplus weight
scale capture pressure, that price is the match, so revision 3's own tenure gate
refused every root on the phase-2 baseline — correctly — and the doctrine spent
five hundred ticks as a plain duelist on a contested tile that paid nobody. Its
replays on that level read: **zero roots, zero advances, zero holds, twenty-one
deaths a side, nought all.**

The **aegis shell** is the trade the fortress cannot be. It keeps objective
weight, so it holds ground instead of leaving it. It cannot move, shoot or
rotate. And a hostile bolt arriving inside its facing arc dies there and is
relaunched from this tile along the exactly reversed heading **under our
ownership** — so an opponent that answers a contested objective with fire is
shooting itself, while the tile never changes hands.

Four rules, all read from the contract:

- **Raise it against the muzzle, not the bolt.** A windup-one stance requested on
  tick *t* is guarding from *t+1*, and bolts here cross two tiles a tick at a
  duelling range of two to four — so every bolt lands the tick after it is fired
  and a shield raised at the sight of one is *always* late. An enemy's cooldown
  is redacted; its **cadence is not**. The attack is a published event and the
  cooldown is a declared number, so "that muzzle can fire now" is a subtraction.
- **Raise it in the cooldown shadow.** The gun declares a cooldown; the two
  routes declare their windups. When entry plus exit fits inside the idle window,
  the whole cycle is spent on ticks the gun could not have used, so the shield
  costs no fire at all.
- **A kill outranks armour.** Everything else about the trade is a wash on damage
  — a turned bolt costs the shooter exactly what our own bolt would — so the one
  shot never given up is the one that removes a body, because removing the last
  body on a contested surface is what becomes territory.
- **One unearned stance, then fight.** A shield that turned nothing was facing a
  muzzle that declined to fire. Raising against it again before its declared
  cadence comes round asks the same question for the same two ticks; two of these
  doctrines doing that livelocked into 223 stance flickers a match.

Read from the other side, the same facts price fire control. A bolt that
**lands** beats a bolt that is **turned**; a turned bolt still spends a third of
the arc's declared budget, and the third shatters it into a forced return whose
exit and re-entry windups are the punish window; and the only shot held back is
one whose own return would kill the shooter.

Everything the observation publishes is now asked rather than reconstructed: the
hold's owner and clock, each bolt's own cadence and damage, the reserved spawn
tiles, both declared classes, both slot counts.

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

## One artifact, every cell

Nothing in the source names an arm or a class. Forms, routes, windups, budgets,
guards, projectile counts, reach, cadence, damage, objectives, unlock ticks, slot
counts, tile legality, movement coupling, hold clocks, control arithmetic and
action codes are read from the resolved contract and the current legality mask.
Canonical contracts omit inert fields, so a rule about something the contract
does not declare is *provably* inert rather than merely usually quiet.

| Phase-2 cell | What the doctrine becomes |
| --- | --- |
| `keel` (kit off, striker-only) | revision 3, byte for byte — 624 decisions and body states identical |
| `veer` (kit off, universal bend) | revision 3, byte for byte — 635 decisions and body states identical, plus the 18 off-axis tiles the bend reaches |
| `helm` (kit on, striker-only) | the shell: raised against the muzzle, cycled inside the gun's cadence |
| `rig` (kit on, universal bend) | the same, and every bolt priced by its arrival heading |

Measured against this lineage's own rebuilt revision 3 over 40 WASM cells (four
cells × five seeds × both sides): `keel` 0–0–10 at +0.0, `helm` **10–0–0 at
+55.4**, `veer` 0–0–10 at +0.0, `rig` **7–3–0 at +17.8**. Removing the stance
reproduces revision 3 on all four arms, so the shell is not part of the gain, it
is all of it. `DX.md` has the ablation table and the honest reading of the `rig`
variance.

The other two class skills are handled but not owned: a bulwark chassis declares
no volley route and keeps three slots, and `--skills kit` on a bulwark mirror
resolves to the shell alone. Both are read from the contract anyway — the slot
count from the topology's own list, the fan width from the declared projectile
count — and probes against copies of this source declaring the other two classes
run the asymmetric five-slot topology and the volley forms without a fault.

| Earlier level | What the doctrine becomes |
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

- **Raise the shield over a killing shot**, or against a muzzle that has already
  declined to fire at it once.
- **Poke a guard it cannot break.** A guarding form with no declared
  `automaticReturn` budget never shatters, so a bolt into its arc buys nothing
  and the arc is simply a tile not to shoot.
- **Feed an arc when the return would be lethal to the body feeding it.**
- **Treat a returned bolt as friendly.** A deflection belongs to the deflecting
  team, so a bolt this doctrine fired into an enemy arc comes back as an ordinary
  hostile projectile and is dodged like one.
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

Decision debug text narrates the doctrine. New in revision 4: `shield up in the
cooldown shadow: turning N`, `shield up over a shot: turning N`, `shield up:
returning N`, `dropping the shield: nothing to turn, work to do`, `dropping the
shield: the arc does not cover this one`, `dropping the shield: nothing fired in
N`, `bent past the guard arc on target (x,y)`, `feeding the guard on target
(x,y)`, `casting: N bodies inside the fan`.

Carried forward: `rooting: N covered objective tiles`,
`on overwatch: no relief on the surface`, `on overwatch: our hold holds for 31:
weight is pressure`, `on overwatch: contest arithmetic: relief 1 vs 1`,
`front rotated: unrooting after N rooted ticks`, `holding the scoring surface`,
`suppressing the objective lane`, `turning to travel East`, `slipping the shot
West`.

## Files

| File | Role |
| --- | --- |
| `IronRoot.cs` | the policy: roles, the shell cycle, tenure gates, stations, threat response |
| `RatchetClock.cs` | revision 3's hold inference, retained only as a contradiction check now that the observation publishes the hold |
| `ContractLens.cs` | everything the doctrine knows, parsed from the contract only — including which routes lead to a guard, a fan, or a zero-weight form |
| `Kinematics.cs` | what a step costs when facing is coupled to movement |
| `FortressPlan.cs` | covering-tile selection, firing lanes, enemy lane sweep |
| `Gunnery.cs` | fire control: turret headings, straight guns, curved intercepts |
| `ArenaGeometry.cs` | rays, line of fire, flood fill, ordered first steps |
| `ArenaBasics.cs` | the current template helpers, synced verbatim |
