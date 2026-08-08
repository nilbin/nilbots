# march-wall — THE LANE IS THE WALL, PRICED

**Class:** bulwark · **Lineage:** march-wall-v1 (revision 3) · **Role:**
verdict-doctrine · **Target:** cumulative T4

Revision 2 fixed the geometry: a bulwark's gun fires along its facing, so the
wall is the set of straight lanes our guns close, not the tiles we stand on.
That is unchanged and it still decides most ticks.

What revision 2 got wrong was not a rule, it was an assumption underneath every
rule — that a tick of presence is worth the same as any other tick of presence.
On a mean-reverting frontline that is true: everything comes back, so the only
question a body ever has to answer is whether it survives to keep standing
there. Three declared policies each make it false, and revision 3 reads all
three.

## What changed, and why

**A hold turns ground into a clock.** `capture.ratchetHoldTicks` protects a
completed advance, and a capture completed inside somebody else's hold is
*spent* — the claim resets and the objective does not move. So "how much is this
claim worth" stops being a constant. Inside our own hold the answer is *more
than usual*: the front cannot come back, so the ticks of the hold are exactly
the ticks to spend crossing under fire, and a standoff on protected ground is a
standoff we are paying for by the tick. Inside theirs the answer is *nothing at
all* until the clock runs shorter than the capture does — and a mutual null on
the objective is then not a stalemate to force open, it *is* the denial, so the
doctrine stops buying it with health.

The hold's start is not an observation field. It is derived two ways: an advance
is a change in the active position index, and its sign against our declared
advance delta names the team that made it; and on a life's first tick, when
there is no history at all, `controlResumesAtTick` is the capture tick plus one
plus the declared redeploy pause, so a live pause is a timestamp. A life that
appears after that pause lapses genuinely cannot see the hold, and the doctrine
reads an unnamed hold as the opponent's — the reading that never spends a body
on a claim it might not be able to bank.

**A control policy decides whether the objective is a switch or an election.**
When `capture.controlPolicy` scales gain with surplus objective weight, a
weighted body standing on the objective is a vote and being outweighed erodes a
claim we already own; a body that walks off to extend the wall is a vote
withdrawn, and it does not walk. When control is binary, one body of positive
weight nulls any number of opposing bodies, the second body was never adding to
the claim, and the same walk is free. One doctrine cannot answer that without
reading the policy, which is the whole point of the field.

**A return address decides what a body is scarce for.** When
`lifecycle.automaticReturnPlacement` rallies arrivals onto our own-side
objective, a dead scorer is back beside the fight on its declared clock, so the
roster no longer has to carry insurance against losing its one presence. The
fortification ration — never fewer capturing bodies than the other side has
shown us — relaxes to one, and the surplus body becomes the best gun this class
owns: seven health, eight tiles, eight headings, the fastest cadence on the
field. On a contract that returns bodies home the old ration stands unchanged.

## What is deliberately not here

Five further readings of those same three fields were implemented and measured
against the rebuilt revision 2, then removed. Three lost outright, one was
exactly inert, and one never fired at all. All five are recorded in `DX.md`
with their numbers, because every one of them reads as obviously correct —
including the two that sound most like this class's identity: *"a durable body on ground it cannot lose
should stand and take the hit"*, and *"a death that puts you back at the front
is a death you should be willing to trade for"*.

The revision does nothing whatsoever on the arms that declare none of the three
fields. That is not a claim, it is measured: on the unmodified control and on
the numbers-only arm, revision 3 and revision 2 produce **bit-identical
matches** across sixteen matches each.

## Movement arms

Facing is this chassis' entire firing envelope, so the movement profile's
declared facing coupling is read before any step is planned; this is unchanged
from revision 2 and every measurement cell here runs `facing-locked`.

- **face-movement-direction** — every candidate step is scored on the facing it
  would leave us holding.
- **facing-locked** — the movement mask offers only the current facing, so every
  route falls back to turning into the step, which makes rotation a movement
  primitive rather than an aiming tweak.

## Contract-driven, not arm-driven

Nothing here names a form, an action code, a map tile, a class, or a pendulum
token. A form is "fortified" when its declared action mask contains no movement
action. The hold, the control policy and the return placement are read through
the scaffold's `Capture()`, `ObjectivePresence()` and `ArrivalsRallyForward()`,
and an absent value is treated as a real answer rather than a gap: no declared
hold means the front can come straight back, and a control policy that does not
scale means the second body adds nothing. Firing envelopes for both sides are
still replayed from the declared aim, bend and travel rules through the SDK's
own path preview.

## Reading it

- `MarchWall.cs` — the doctrine ladder for a turret and for a mobile body.
- `Pendulum.cs` — whose hold is live, how much is left, and whether a claim
  started now can be banked.
- `Lane.cs` — what a gun could reach from a pose we do not hold yet.
- `ContractView.cs` — one immutable reading of the resolved contract.
- `AnchorPlanner.cs` — where the next wall segment goes.
- `FireControl.cs` — every tile this body can put a bolt on this tick.
- `Threat.cs` — where hostile bolts are going to be, and how far they can go.
- `Navigation.cs`, `Geometry.cs` — stepping and tile geometry.
- `ArenaBasics.cs` — the shared template helper, synced verbatim.
