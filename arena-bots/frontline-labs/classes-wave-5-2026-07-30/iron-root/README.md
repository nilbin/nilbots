# Iron Root

**Class:** bulwark · **Lineage:** iron-root-v1 · **Revision:** 5 (OPEN ROOT)
**Doctrine:** FORTRESS ROTATOR · **Role:** verdict-doctrine
**Qualified:** T4 (`frontline-duel-depth-union-t4-v1`), `balanceEvidenceEligible`

A bulwark does not win by out-shooting anyone. It wins by making one piece of
ground expensive to stand on for longer than the opponent can afford to keep
paying, and then moving that expense forward.

## The idea

Territory is the only currency, so bodies hold the scoring surface and contest
rather than concede. One body may convert into a tough omnidirectional gun that
cannot capture anything — that is the whole bargain, and the price is the only
quantity that decides the match. A hostile bolt arriving inside a raised arc dies
there and is relaunched from that tile back down the ray the shooter is standing
on, under our ownership, so an opponent that answers a contested objective with
fire is shooting itself while the tile never changes hands.

## What revision 5 changed, and why

Three declarations moved, and each of them turned a correct revision-4 rule
backwards.

### The placement tag stopped binding, so ask the route

Every same-life route now declares an **empty** `forbiddenTileTags`, while the
map still publishes 112 tiles tagged `transition-placement-forbidden`. Both facts
are true at once and only the second one binds. Revision 4 asked the map, so it
refused to armour or root on the entire scoring surface and the central corridor
— a third of the walkable board, and exactly the third worth standing on. Asking
the route gets that back, and behaves identically on every stricter arm where the
tag reappears in a route's own list.

Measured: **+85.6 points of territory per cell**, the largest single effect this
lineage has ever recorded. A shell can now guard the point it is capturing.

### The mobile gun got 45 degrees, and this chassis sees them all

The attack profile declares a ±1 initial aim offset — an ordinary aim-only
program with no bends. Rotation sets an absolute **cardinal** facing while
projectile headings are **eight-way**, so at zero offset exactly half of every
ring is unreachable however the body turns. A ±1 envelope hands back all four
diagonals.

No chassis gains more, and the reason is not the gun: the bulwark's vision is
**omnidirectional**, so it has always seen the diagonal bodies it had to walk
onto a lane to answer. Now it shoots them where it stands. The same declared
envelope is read defensively — a gun that can do this to us is any gun whose
profile says so — which widens every "does that muzzle bear on me" test in the
doctrine by exactly one octant.

Measured: **+62.6 per cell**, with 9–27 diagonal launches a match.

### The turret is a cycle, and a cycle is about leaving

`irreversibleForLife` is false and a route back exists, so anchor ⇄ mobilize is
unlimited for the life. Health maps by ratio with a floor of one in both
directions and no entry heal, so a full-health lap is exactly free (4/4 ⇄ 7/7,
5/5 ⇄ 7/7) and a partial-health lap pays one health **every** round trip.

The obvious reading is "commit more freely". It is the losing one, and it cost
**47 points of territory per cell** when this revision tried it: a root taken on
a thin case is a body that is not scoring for as long as the case is thin, and
being able to undo it later does nothing about those ticks. So the bar for taking
a root is unchanged, and what reversibility actually buys is spent at the other
end of the route:

- **weight on demand** — stand as a gun while the ground does not need weight,
  and *be* the weight the tick the mode's own arithmetic says it does;
- **follow the front immediately** rather than hoarding a one-use return;
- **give a root up** the tick it stops paying, because another one is available.

A lap below full health is a purchase rather than a reflex, and a completed leg
buys silence for one full declared cycle so two of these cannot flicker at each
other.

### Shell discipline: against poke raise the arc, against numbers root the gun

The lab measured the arc as **opponent-shaped** — strong against a muzzle, a trap
against numbers — and the mechanism is entirely in the form's own declarations.
The arc covers one quadrant, the stance cannot move or shoot, and the clause that
does the damage is that it cannot **rotate**: the protected quadrant is chosen
before the shield rises, so every body that gets outside it is hitting an
immobile target for free.

So the rule is a count, not a class name. How many hostile sources bear on this
tile inside the frozen window, and how many can one quadrant answer? Counted per
body rather than per bearing, because one body given long enough reaches several
bearings and counting bearings would decline every shield ever. Numbers have a
declared form too — more unit slots, rebuilding faster — read per side from the
topology and from each slot's lifecycle profile, so it generalises past the three
classes that exist.

And the refusal is handed to something. A turret is omnidirectional, tougher, and
faster; it answers three bearings as easily as one. It roots *where it stands*
rather than at the best site on the map, and it still pays the full turret
bargain — a first draft that skipped that gate lost ten cells out of ten by sixty
points each.

Measured: **0 against a poking predecessor, +21.3 against numbers.** Removing it
in the fabricator cell doubles shells raised (16 → 32), doubles ticks frozen
(56 → 105), doubles deaths (7 → 13), and turns a breach win into a max-ticks
grind.

## Measured

Against this lineage's own wave-4 source rebuilt from the frozen tree, on the
crew game (`--pendulum keel --skills kit --bend universal --aim offset
--stance-ground open --movement facing-locked`), WASM, both sides, three seeds:

| | W–L–D | margin / cell | how it ended |
| --- | --- | --- | --- |
| bulwark mirror vs rebuilt wave-4 | **6–0–0** | **+60.0** | base-breach, all six |
| vs own source as a fabricator (`wane`) | 3–0–0 | +60.0 | base-breach |
| vs own source as a striker | 3–0–0 | +46.0 | max-ticks |

Neither artifact consults `context.Random`, so seeds buy nothing on a fixed map
against a fixed opponent: read that headline as "wins both sides by breach", not
as six independent results. `DX.md` has the ablation table, the two rules this
revision shipped backwards, and the honest reading of everything above.

## One artifact, every cell

Nothing in the source names an arm or a class. Forms, routes, windups, health
transfer policies, reversibility, placement legality, aim envelopes, reach,
cadence, objectives, slot counts, rebuild economies, tile legality,
movement/facing coupling, hold clocks, control arithmetic and action codes are
read from the resolved contract and the current legality mask. Canonical
contracts omit inert fields, so a rule about something the contract does not
declare is *provably* inert rather than merely usually quiet — which is why this
artifact passes the qualification suite's classless, skill-less, aim-less union
profile unchanged.

| The game declares | What the doctrine becomes |
| --- | --- |
| no guard form | the fortress and the screen, revision 3's shape |
| a guard form, strict placement | revision 4: the arc raised against the muzzle in the gun's own cooldown shadow |
| a guard form, open placement | the arc raised on the ground it is holding |
| an irreversible anchor | one root, bought with a full tenure |
| a reversible anchor | the same bar to root, and a gear change to leave |
| a ±1 aim envelope | four more headings out, and one more octant of threat in |
| more enemy slots, rebuilt faster | the arc declines and the gun roots instead |

## What it will not do

- **Raise the arc against numbers**, or over a killing shot, or against a muzzle
  that has already declined to fire at it once.
- **Root while it is the margin** — while the weight on the tile without this
  body would no longer hold the ground. Being shot at does not suspend that.
- **Cycle at partial health for no reason.** Each lap below full costs a health
  that never comes back.
- **Flicker.** A completed leg of either cycle buys silence for that cycle's own
  declared cost.
- **Poke a guard it cannot break**, or feed one whose return would be lethal.
- **Treat a returned bolt as friendly** — a deflection belongs to the deflecting
  team.
- **Spend a rotation to gain nothing.** With a three-heading envelope the current
  facing is scored as the incumbent and wins ties.
- **Concede a lane.** An idle rooted gun keeps firing down the lane crossing the
  most contested tiles.

## Reading a match

Decision debug text narrates the doctrine. New in revision 5: `renting the gun on
the point: N tiles covered`, `renting the gun: N covered objective tiles`,
`weight wanted: unrenting the gun after N ticks`, `I am the margin: N vs M
without me`, `a lap costs N health and nothing is shooting`, `just cycled`, `this
tile is not a legal anchor`.

Carried forward: `shield up in the cooldown shadow: turning N`, `shield up over a
shot`, `dropping the shield: nothing to turn, work to do`, `bent past the guard
arc on target (x,y)`, `feeding the guard on target (x,y)`, `front rotated:
unrooting after N rooted ticks`, `holding the scoring surface`, `suppressing the
objective lane`, `aimed fire on target (x,y)` (the diagonal launches).

## Files

| File | Role |
| --- | --- |
| `IronRoot.cs` | the policy: roles, the shell cycle, the turret cycle, tenure gates, stations, threat response |
| `ContractLens.cs` | everything the doctrine knows, parsed from the contract only — route placement legality, reversibility, health transfer, rebuild economies, guard/fan/zero-weight classification |
| `Gunnery.cs` | fire control: turret headings, the aim envelope, straight guns, curved intercepts |
| `FortressPlan.cs` | covering-tile selection, firing lanes, enemy lane sweep widened by the declared aim envelope |
| `Kinematics.cs` | what a step costs when facing is coupled to movement |
| `ArenaGeometry.cs` | rays, line of fire, flood fill, ordered first steps |
| `ArenaBasics.cs` | the template helpers this doctrine calls, pruned (see `DX.md` friction #3) |
