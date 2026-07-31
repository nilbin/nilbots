# GateStone — wave 8

A **bulwark** that keeps one ledger and settles every decision in it.

The ledger's unit has been the same for five waves: ONE TICK OF OBJECTIVE
WEIGHT. A body on the point is worth its weight per tick, and it is worth
exactly zero while the redeploy pause runs or while a completed capture would be
spent inside an enemy hold. Every trade this bot makes — pick up the turret's
faster gun, raise the shield, walk home for a companion, step out of a lane — is
that one number against something else.

Wave 8 is the wave that tried to replace the unit and did not.

## What the channel changed, and what it changed here

The capture channel splits presence in two: a **claim** that counts only bodies
which did not change tile this tick, capped at two bodies of surplus, and a
**denial** that counts everybody standing there. I rebuilt the whole ledger on
that split — the marginal progress a body's stillness or presence buys, this
tick, with the cap, the erosion multiple and the damage interrupt all read from
`gameMode.capture`. It is a faithful reading of the arm, and over eighteen games
it played **worse** than the weight ledger it replaced. The code is in
`StoneChannel`, the switch is `StoneDoctrine.ChannelArithmetic`, and the switch
is off with its number and its reasoning beside it.

What the channel *did* change here is fire control. At a threshold of 8, a
single point of damage to a body of the **controlling** team standing on the
objective takes back an eighth of a capture — far more than the same bolt is
worth as health. So this bot shoots the body on the point hardest, and prices
that premium from `claimInterrupt.revertPerDamagePoint` rather than from a
constant, so it collapses to the old flat presence bonus on a ruleset that
declares no interrupt.

## What it does

- **Fights at the objective and banks what falls there.** The assay pays at the
  tile with no transport, so a doctrine that stands where the corpses fall
  collects without a detour. A dedicated errand needs the free window to be
  genuinely free *and* long enough to walk; a loaded body never anchors, because
  a zero-weight form drops the whole load on the floor.
- **Buys upgrades out of ticks that were already worth nothing** — from under
  the shield, from a turret with an empty lane, from a body whose gun is cold —
  one body per tick, elected in muster order from shared state so the second
  never collides with the first.
- **Leases the turret rather than selling itself to it.** Anchor when the point
  pays nothing and the eight-headed cadence-1 gun pays something; hand the
  weight back the tick the point starts paying again. The wave's largest single
  measured gain is a four-line repair here: a turret asks "does the gate need a
  body?" by pricing the **mobile body it would become**, not its own objective
  weight, which the contract declares as zero — so wave 6 asked that question
  every tick and always answered no.
- **Reads everything from the resolved contract.** The guard route, the fortify
  route, its reversibility, the health-transfer policy, the bend and aim
  envelope, the capture policy, the interrupt, the whole economy, the lifecycle
  clocks and the movement coupling. The same artifact plays `bastion`, `siege`,
  `forge`, `swell` and the classless qualification profile with no arm-specific
  branch.

## What it refuses to do

- **It never buys gun range.** The `edge` track aborts the match on this build —
  see `DX.md`, where the same failure is reproduced by the pre-registered
  control arm with a bot that contains no economy code. That is a platform
  defect, and it costs this class the purchase the arm most obviously designed
  for it. The refusal is written against the track's declared *effect*, so it
  lifts by itself on any ladder that does not move a bolt.
- **It does not stand and eat bolts to protect a claim.** The contract prices the
  interrupt at exact parity with the stillness gate, so a sidestep and a wound
  cost the same progress and the sidestep keeps the health.
- **It does not screen.** Parking a body in the firing lane to the channeller is
  the arm's headline pattern; it measured negative for a three-slot class and
  was cut.

## Reading it

`StoneDoctrine` names one switch per rule, and a `false` there means *built,
measured, refused* — with the number. Rules that lost are argued out in the
code, at the call sites where the next wave would otherwise derive them again.
`StoneChannel` is the channel arithmetic, `StoneScrap` the economy,
`StoneContract` everything the bot is allowed to believe about the ruleset,
`StoneGround` stations and routing, `StoneCrew` the wave-6 coordination layer,
`StoneAim` fire control.
