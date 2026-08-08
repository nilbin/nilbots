# LedgerFly — the attrition banker (revision 3)

Class: **fabricator**. Lineage: `ledger-fly-v1`. Role: verdict-doctrine,
target cumulative T4. Budget: one strategic revision; mechanical repairs free.

## The doctrine in one line

Bodies were never the currency — convertible objective-ticks were, and the
contract tells you the exchange rate before tick 0.

## What changed, and why

Revision 2 fixed *when* the bank lends: solvency against the opposing slot
capacity the contract declares, rather than against the handful of enemies a
90° cone happens to catch. That rule is intact and this revision does not touch
it.

What revision 2 could not know is that a tick of sole presence is not always
worth the same thing. On a mean-reverting frontline it always is: every capture
advances, one body nulls any number, and a death is a walk home. The
counterweight arms break all three, and every one of them is a field in the
resolved contract:

| Contract fact | What it changes | What LedgerFly does about it |
| --- | --- | --- |
| `capture.ratchetHoldTicks` + the inferred hold owner | inside somebody else's hold a completed capture is **spent** — claim reset, front unmoved | the objective is priced as denial, not as ground, while their hold runs; inside our own hold the front cannot come back, so the bank leaves its standoff and commits |
| `capture.controlPolicy` | surplus objective weight **scales** capture pressure instead of one body nulling any number | presence is denominated in weight: the bank joins the objective while our weight is not yet clear of theirs, because that marginal body is the difference between capturing at 2/tick and being eroded |
| `lifecycle.automaticReturnPlacement` | arrivals land on the own-side objective beside the fight, not at the spawn | a death costs the return clock and no walk, so the caution premium that set the bank's standoff is repriced down |

**Nothing in the source names an arm.** Every one of those is read at
`StartLife` through the template's `Capture`, `ArrivalsRallyForward`,
`ExpectedArrivalTiles`, and `ObjectivePresence` readers. On a contract that
declares no hold, spawn-anchored arrivals, and binary control, revision 3
resolves to revision 2's doctrine **exactly** — measured as zero differing
decisions across a full 500-tick match.

## Inferring the hold

The hold length is a contract field. Its *start* and *owner* are not in the
observation schema at all, so `Ratchet.cs` derives them:

- an advance is the tick the active position index moves;
- its direction against this team's declared objective-index delta says whose
  it was;
- the contract publishes its own redeploy arithmetic
  (`capture tick + 1 + pause`), so `ControlResumesAtTick` recovers the exact
  completion tick rather than the tick we happened to notice on.

Private memory is life-scoped, so a body created inside somebody else's hold
can prove that an advance happened recently and cannot prove whose. That case
reports "no hold known" and plays the baseline rather than guessing.

## What that means tick by tick

**The prime is the bank, not a duellist** — until the contract says otherwise.
LedgerFly identifies its economy anchor from the lifecycle assignment that
returns automatically, holds a standoff behind the exchange, and spends its
ticks on the books. It now leaves that standoff for four readable reasons:
nobody else is holding the line, the clock has decided the match, surplus
weight converts and ours is not yet clear of theirs, or our own ratchet is live
and arrivals rally forward so committing cannot cost ground.

**Solvency is read from the contract, not from the vision cone.** Unchanged
from revision 2: the lending test is `active bodies < declared opposing
capacity + 1`, and an unlocked enemy slot counts whether or not we have ever
seen it.

**Replacements land where the last exchange happened.** The contract declares
the placement offsets and the order the host walks them, so LedgerFly replays
that rule locally before it queues, and buys the facing by stepping into it
where a step turns the body and by rotating where it does not.

**Forward is chain-derived, not spawn-derived.** Which end of an objective
region is "deep" comes from the ordered chain and this team's index delta. On a
rallying contract, distance-from-spawn stops meaning depth into their ground.
The spawn anchor is still what answers "which half of the map is mine" — that
is a different question from "where does my next body appear", and they have
different answers once arrivals move.

**Trade children for bodies at favourable rates.** Target priority is what we
can kill this shot, then the opposing bank, then lowest health, then nearest.
Every shot is simulated against the declared projectile geometry first.

**The empty gun still owes the team a tick** — footwork inside the region we
are contesting, or a turn onto a lane worth suppressing when nothing is in
sight.

**Let the fast rebuild win the long clock.** The bank never Splits (that
destroys the bank) and never Anchors (that removes objective weight).

## Movement arms

Facing coupling is read from the form's declared movement profile; the field is
optional and its absence means *preserve facing*.

| Arm | What LedgerFly does differently |
| --- | --- |
| preserve-facing | the measured baseline; a step is a free strafe |
| move-sets-facing | retreat is repriced — the standoff shrinks by a tile, steps that turn away from the exchange carry an explicit cost, and a placement facing is bought by walking into it |
| facing-locked | routes are planned on the map geometry and paid for at emit time: move when the mask offers the step, rotate into it when it does not. Ties break toward the current facing so two equally short routes cannot make the body oscillate. Evasion stays inside the mask — a rotation is not a dodge |

## Contract-driven, not arm-driven

Everything the doctrine needs is read at `StartLife` or from the per-tick
legality mask: ordered objective regions and this team's advance direction, the
economy anchor and its return spawn, the automatic-return placement policy, the
capture policy (threshold, gain, decay clock, redeploy pause, hold length,
control policy), the fabrication route with its source region, output region,
tile tags and declared candidate offsets, opposing unit slots and their unlock
ticks, the form catalog for both sides, the shot language of the current form,
the collision policy, the tick cap, and the timeout-ranking channel. Actions are
selected by contract kind and stable ID, paired with the numeric code from that
tick's legality entry. Arms with no fabrication route, automatically activated
companions, no hold, or no bend envelope fall through the same code without a
special case, and any unexpected state resolves to a legal action rather than a
fault.

Direction ties never break on an absolute compass preference. Every search takes
its order from the template's `OrderedDirections`, because a shared absolute
preference hands the advancing side a systematic edge on a mirror-symmetric map.

## Files

| File | What lives there |
| --- | --- |
| `LedgerFly.cs` | the decision ladder, the banker/trader split, and the commit test |
| `Ratchet.cs` | the pause/hold clock and whose advance it protects |
| `Ledger.cs` | losses owed, solvency target, and the last-exchange anchor |
| `MatchLens.cs` | every contract fact resolved once per life |
| `Kinematics.cs` | what a step costs under each movement facing coupling |
| `FabricationRoute.cs` | local replay of the declared placement rule |
| `Gunnery.cs` | simulated straight, aimed, curved, and suppressing fire |
| `Field.cs` | blocking, bolt threat, gun coverage, and pathing |
| `ArenaBasics.cs` | the generated template helper, carried unmodified |

## Running it

```bash
nilbots experiment frontline-labs --bot . --opponent . \
  --movement facing-locked --pendulum ratchet --runtime in-process --seed 7
nilbots experiment frontline-labs --bot . --opponent . \
  --movement facing-locked --pendulum ratchet-contest --runtime wasm --seed 7
nilbots build . --no-cache
nilbots experiment frontline-labs qualify --bot out/bot.wasm \
  --suite frontline-qualification-5 --out evidence/t4
```

Both entrants declaring a class resolve the arm from their manifests; this
project declares `"class": "fabricator"`.
