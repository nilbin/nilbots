# march-wall — A WALL THAT STANDS STILL IS A WALL THAT TAKES GROUND

**Class:** bulwark · **Lineage:** march-wall-v1 (revision 7, wave 8) · **Role:**
verdict-doctrine · **Target:** cumulative T4 · **Cells:** swell / siege / forge /
bastion, facing-locked — in a bulwark mirror those spell themselves
`…-sail-open-facing-locked`, `…-mantlet-…`, `…-forge-…` and `…-smithy-…`,
because the salvo arm is inert-omitted where no striker is on the board.

Six revisions of this lineage said the same sentence in six ways: **the wall is
the set of straight lanes our guns close, not the tiles we stand on.** Revision 2
found that geometry, 3 priced the pendulum, 4 spent the kit, 5 spent the open
ground and the reversible door, and 6 discovered that there are three of us and
that we were each written as if alone. All of it still decides most ticks and
none of it changed.

Revision 7 is about the one thing that did.

## The rule that inverted

`--capture channel` does not add a capability beside the front; it rewrites what
taking the front IS. Your claim weight counts only your bodies on the objective
**whose tile did not change this tick**. Your denial weight counts all of them.
And hostile damage to a body of the *controlling* team standing *on* the
objective reverts that team's whole run by the damage taken.

Every previous revision of this wall could reposition on the point for free.
Presence was binary: a sidestep inside the region cost nothing, so the doctrine
grew four separate reasons to take one — spacing, closing a lane, yielding to a
sibling, walking to a better tile. Each is right on its own. **All four are wrong
on a channel**, and the measurement is not subtle: the shipped source with the
one clause that refuses them is 15-0-0 against the wave-6 self; the same source
with that clause off is 0-0-15.

So: **HOLD, and do everything that is not a step.**

- **Shoot.** Firing never breaks stillness. And when the *other* team is the
  controller, a bolt landing on a body of theirs standing on the point reverts
  their run at whole-run granularity — one hit undoes a capture window. That is
  the class's defensive case restated: a turret is cooldown 1 at travel 8 on
  eight absolute headings, so one covering gun with a clear heading is a capture
  veto rather than merely a nuisance.
- **Guard.** A raised AEGIS SHELL cannot move, which under this policy means it
  is stationary *by construction*, and its objective weight is 1. It channels
  while it deflects. On `--stance-ground open` it may rise on the point itself.
  This is the single best interaction the chassis owns on this arm.
- **Dodge, but only inside the region.** A bolt that lands on the controller
  costs progress equal to its damage, and a salvo bolt carries two. Giving up one
  tick of gain to avoid two points of revert is a trade. Giving up the region is
  not.
- **Invest.** A body standing still with no shot has an action the board is not
  using.

## The escort, and when not to bother

A screen is a body standing on the firing line to your channeler, **off** the
objective. It works because the collision model already did it — bolts stop on
the first enemy body and pass through allies — and because damage off the
objective reverts nothing. Nothing was added for this; the arm gave an existing
behaviour a purpose.

Whether to screen or to stack is arithmetic the contract publishes, not taste.
Surplus stationary weight scales the gain to a declared cap, so against a broken
defence a second body on the point halves the capture and the answer is to STACK.
Against a live one the extra body is extra revert surface and the answer is to
SCREEN. Both halves are read: `stationaryGainMultiplierCap` and whether any
hostile gun's declared travel covers a tile of the contested region.

## The economy: the wall does not go shopping

**Scrap never moves a body of this wall.** Not one tile, not for a six-scrap
deposit, not for the corpse two tiles behind it. That is a doctrine statement and
it is the single most expensive thing this pass learned.

What is left is everything the economy gives you for free, which turns out to be
almost all of it. The assay pays in full at the tile with no transport, so the
wall banks by walking over piles it was standing on anyway — and under the
channel and the salvo the corpses fall exactly where a wall is standing. A load
banks by itself on the home pad: no action, no cost, no decision. A loaded enemy
carrier is ranked as the target its published `carriedScrap` says it is, because
killing one drops its whole load plus its wreck on a single tile. And tiers are
bought straight off the `upgrade-track` mask by whichever body has nothing else
to do with the tick — in practice an anchored turret or a raised shell, neither
of which has a step to give up. The track is chosen by the gap each declared
effect closes: travel against the longest gun the other side fields, spawn health
against what a screen has to eat, sight against our own travel.

Two spending routines were written, measured, and cut, in that order:

1. **An elected quartermaster** walking to the published deposit metronome. The
   schedule is static contract data and 48 scrap is three tiers, so the idea was
   sound. Under the channel two moving defenders hold three still attackers, and
   the elected body leaves around tick 80–120 and the front runs light for the
   rest of the match.
2. **A two-tile doorstep detour**, which looked harmless and survived the first
   cut. It went **0-1-13** against this lineage's own wave-6 predecessor on the
   cell that carries the economy and NOT the channel — and removing it made the
   same source **8-0-6**. The mechanism is the embarrassing part: on a
   channelling ruleset the stillness gate sits above salvage and quietly took the
   tick before salvage could spend it, so a rule that traded ground for currency
   looked safe for as long as another rule was covering for it.

`DX.md` carries both numbers. The surviving rule is the one that cannot make that
mistake, because it cannot move.

## The team draw

Wave 6 built every coordination rule as a pure function of the frozen shared
observation — the only substrate a team without a channel has — and then fed it
a tie-break drawn from `context.Random`, which is per LIFE. Its author noticed
and kept `Column` away from the helper entirely. `context.TeamRandom` retires the
workaround: identical draws for every teammate at the same tick, re-derived per
tick so a body born mid-match agrees on its first, and unrelated to the
opponent's stream so the order stays unpredictable from the other side.

## Reading it

- `Channel.cs` — **new.** What taking ground is: the stillness gate, the
  stack-versus-screen arithmetic, the interrupt, and the screening geometry.
- `Economy.cs` — **new.** The ladder, priced from the contract's declared effects
  against both sides' declared envelopes. Nothing in it moves a body.
- `MarchWall.cs` — the doctrine ladder for a mobile body, a turret and a shield.
  `HoldTheChannel` and `Escort` are new; the rest is revision 6's.
- `Column.cs` — the march order: precedence, routes, chokes, arrivals, spacing.
- `Navigation.cs` — stepping, route prediction, corridor runs, and the team draw.
- `AnchorPlanner.cs` — where the next segment goes.
- `Cycle.cs` — what a fortify round trip costs, from the declared health policy.
- `Stance.cs` — what a stance is, its budget, and which bearings an arc covers.
- `Pendulum.cs` — the published hold.
- `Lane.cs`, `FireControl.cs` — firing geometry from a pose we do not hold yet.
- `Threat.cs` — where hostile bolts are going, when, and what they cost. It
  already priced per-projectile `damagePerHit`, which is why the salvo's damage-2
  fan needed no new code to be feared correctly.
- `ContractView.cs` — one immutable reading of the resolved contract.
- `Geometry.cs`, `ArenaBasics.cs` — tile geometry and the shared template helper,
  synced verbatim from `templates/botarena-generic-actor/`.

Nothing here names a form, a class, a transition, a tile or a map. The channel is
`controlPolicy`, the economy is a nullable contract block, a fan is
`projectilesPerAttack > 1`, a guard declares itself. On a ruleset with no channel
and no economy this artifact is behaviourally revision 6 plus the team draw.
