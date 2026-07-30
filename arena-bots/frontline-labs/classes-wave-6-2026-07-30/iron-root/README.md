# Iron Root

**Class:** bulwark · **Lineage:** iron-root-v1 · **Revision:** 6 (CLEAR LANE)
**Doctrine:** FORTRESS ROTATOR · **Role:** verdict-doctrine
**Qualified:** T4 (`frontline-duel-depth-union-t4-v1`), `balanceEvidenceEligible`

A bulwark does not win by out-shooting anyone. It wins by making one piece of
ground expensive to stand on for longer than the opponent can afford to keep
paying, and then moving that expense forward.

Revision 6 changes nothing about that. It fixes the thing a fortress doctrine is
uniquely able to get wrong: **the two forms that make this class strong are both
walls, and a wall does not know whose side it is on.**

## The idea, unchanged

Territory is the only currency, so bodies hold the scoring surface and contest
rather than concede. One body may convert into a tough omnidirectional gun that
cannot capture anything — that is the whole bargain, and the price is the only
quantity that decides the match. A hostile bolt arriving inside a raised arc dies
there and is relaunched from that tile back down the ray the shooter is standing
on, under our ownership, so an opponent that answers a contested objective with
fire is shooting itself while the tile never changes hands.

Revision 5's three readings all stand and none of them moved: placement is asked
of the **route** and not the map; the ±45° launch offset is exploited offensively
and respected defensively; the turret is a **rental** whose entry bar is unchanged
and whose exit is cheap. Shell discipline — *against poke raise the arc, against
numbers root the gun* — is unchanged.

## What revision 6 changed: one idea, six rules

> **A rooted turret and a raised shell are walls for BOTH teams. Placement must
> keep one clear lane for my own traffic — or wall a lane deliberately, as a gate,
> with my own bodies already on the right side of it.**

Revision 5 could not think this thought. It had no notion of a corridor, no notion
of a route's cost, and no notion of where a sibling was going. What it had instead
was a *reactive* blacklist that learned a tile was unusable by walking into it and
losing the tick — so it discovered a sibling existed only after the sibling had
already cost it a move.

### C1a · Clear lane — refuse to wall your own traffic

Before freezing on a tile — shell or turret — ask the map what walling it costs
each body that wants the objective, on both sides. It is a subtraction of two
route searches, not a heuristic.

- costs one of my own bodies **two steps or more** → refuse; there is another tile
- in between → ordinary tile, ordinary rules

The same question is asked every tick from *inside* both forms, so a body that
becomes a wall later — because a companion unlocked, or the front rotated —
leaves. Entry gating alone would have fixed the first thirty ticks of a
ninety-seven-tick plug, and this exit clause is the only rule in the layer that can
evict an already-frozen body: the stance's own budget cannot, because that budget
is spent by the *enemy's* decision to fire.

### C1b · The deliberate gate

Costs mine **nothing** and the opposition **two or more** → this is the best tile
on the board, the wall itself is the contribution, and the coverage floor relaxes
so the doctrine may take it. Measured **exactly inert** on the pairing available
to it, and labelled that way in `DX.md` rather than implied to work.

### C2 · Right of way: route around, never wait in the open

Precedence is **screen rank** — the body's index among the team's screening bodies
in canonical actor order, which every life derives from the same frozen
observation. A junior adds the senior's tile and every tile a shortest first step
of the senior's could use to its own obstacle set, then routes again. If a way
round exists it takes it and loses nothing.

The claim is a **union** rather than a prediction, and it has to be: two lives
choose between equal routes with per-life state, so no life can know which step a
sibling actually picked.

### C3 · Choke precedence: the only rule allowed to spend a tick

Inside a 1-tile corridor there is no way round, the senior cannot step aside
either, and two bodies meeting in the middle is the one collision on this board
that cannot resolve itself. So a junior forced into a corridor a senior has
claimed **waits at the mouth** — and the whole corridor run is reserved, not just
the entry cell. A body already inside a run outranks one outside it, because it
has one way out.

### C4 · Do not rally into my own traffic

This chassis does not fabricate — its companions are automatic, and the
contract's fabrication list is empty, so that half of the bar is inert here and is
labelled inert rather than dressed up. The live half is the arrival: a forward
rally takes *the rear-most FREE tile* of its region, so standing on that tile
makes my own reinforcement appear one tile deeper into the fight. Both the due
tick and the region are read from the contract, and the tile is removed from the
post list rather than ranked down.

### C5 · Spacing: one muzzle, one target

Between posts that are otherwise equal on presence, cover and heat, prefer the one
that is **not** already under a muzzle covering another of my bodies. Not just a
volley fan — every mobile gun here declares a ±1 launch offset, so any muzzle
covers three headings without rotating, and the rule is read from the enemy's own
profile so it binds on arms with no volley in them at all.

Second half, and the sharper one: a deflection returns the bolt along the
**exactly reversed heading**. This doctrine feeds arcs deliberately, so a fed bolt
comes back down the lane it left on — through me, then through whoever of mine is
stacked behind me. So the *feed* is refused when a sibling is on the return lane;
flanking the arc, which always works, stays available.

## Measured

Every rule is switchable and each was built and swept alone. With all six off the
artifact is **decision-for-decision identical** to the rebuilt wave-5 predecessor
— 706 of 706 team decisions on a checked cell — so the attributions below are
differences, not estimates. See `DX.md` for the table, and for the one rule this
revision shipped too eagerly and had to redesign after it measured a loss.

## One artifact, every cell

Nothing in the source names an arm, a class, a map or a coordinate. Corridors are
derived from the resolved map; route costs from the map; precedence from the
canonical identity order; arrival tiles and due ticks from the lifecycle
declarations; launch envelopes from the enemy's own attack profile. Canonical
contracts omit inert fields, so a rule about something the contract does not
declare is *provably* inert — which is why this artifact passes the qualification
suite's classless, skill-less, aim-less union profile unchanged.

| The game declares | What the coordination layer becomes |
| --- | --- |
| a map with no 1-tile corridors | C1 and C3 never fire; C2 still reorders equal routes |
| a corridor between me and the point | refuse to freeze in it; route around it |
| a corridor between THEM and the point | gate it deliberately, and relax the site floor to do it |
| one body alive | every rule is inert; there is no sibling to coordinate with |
| a map whose objective chain is tight | C4 binds; on this map it provably cannot |
| a forward rally | the arrival tile is vacated before the arrival |
| a home rally | the same reader resolves to the spawn anchor |
| a volley in the cell | C5's envelope widens to the declared fan by the same reader |

## What it will not do

Carried forward: raise the arc against numbers, or over a killing shot, or against
a muzzle that has declined once; root while it is the margin; cycle at partial
health for no reason; flicker; poke a guard it cannot break; treat a returned bolt
as friendly; spend a rotation to gain nothing; concede a lane.

New in revision 6:

- **Freeze in a lane its own bodies need.** Not on entry, and not by staying.
- **Wait in the open to be polite.** Yielding costs a certain tick; out where a
  route exists it goes around instead. Waiting is reserved for corridors.
- **Stand where its own reinforcement is about to appear.**
- **Feed an arc with a sibling behind it on the return lane.**

## Reading a match

New in revision 6: `gating the lane: costs them N, costs us 0, M covered`,
`rooting here walls my own lane by N`, `dropping the shield: clearing a lane my
own traffic needs by N`, `unrooting: this gun walls a lane my own traffic needs by
N`.

Carried forward: `renting the gun: N covered objective tiles`, `weight wanted:
unrenting the gun after N ticks`, `shield up in the cooldown shadow: turning N`,
`dropping the shield: nothing to turn, work to do`, `bent past the guard arc on
target (x,y)`, `feeding the guard on target (x,y)`, `front rotated: unrooting
after N rooted ticks`, `holding the scoring surface`.

## Files

| File | Role |
| --- | --- |
| `IronRoot.cs` | the policy: roles, both cycles, tenure gates, stations, threat response, and the five coordination rules' decision points |
| `Traffic.cs` | **new in revision 6** — corridor geometry, route cost, wall cost, corridor runs, arrival-tile resolution, and the rule switches |
| `ContractLens.cs` | everything the doctrine knows, parsed from the contract only |
| `Gunnery.cs` | fire control: turret headings, the aim envelope, straight guns, curved intercepts |
| `FortressPlan.cs` | covering-tile selection, firing lanes, enemy lane sweep |
| `Kinematics.cs` | what a step costs when facing is coupled to movement |
| `ArenaGeometry.cs` | rays, line of fire, flood fill, ordered first steps |
| `ArenaBasics.cs` | the template helpers this doctrine calls, pruned |
