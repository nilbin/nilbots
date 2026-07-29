# Iron Root

**Class:** bulwark · **Lineage:** iron-root-v1 · **Revision:** 2 (TENURED ROOT)
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
territory is the only currency, and they contest rather than concede — a
contested objective pays nobody, and a body already standing on the tile is
worth more than a body that stepped off it to be safe.

When the front rotates and the lanes stop crossing anything that matters, the
fortress spends its **one return**, walks to the new line, and roots again.

## What revision 2 changed, and why

Revision 1 went 15–39 across the wave-1 class factorial. Reading back its own
replays, the fortress was not being punished — it was being **wasted**:

| Measured over 54 wave-1 matches | |
| --- | --- |
| median rooted tenure | **12–25 ticks** |
| kills a turret scored before it stopped being one | **0.4** |
| damage a turret actually absorbed | **0.7–1.4** |
| roots that ended in an immediate return | **~4 in 5** |
| ticks a match spent standing on a distant overwatch post | **~280** |
| decisions that were a wait | **51%** |

A turret nobody shoots at, that kills nothing, and that un-roots twenty ticks
later has spent a body's objective weight, two windups, and an irreversible
route on nothing at all. Meanwhile the opponent out-scored this doctrine on
uncontested presence in every single class matchup.

So the doctrine keeps its shape and re-prices it. **A root must buy a tenure.**

- **Root only with relief in place.** Not "a companion exists", not "one is due
  in a while" — an allied mobile body already standing on the scoring surface,
  or one step from it. A fortress is the screen's asset; the screen has to
  exist first.
- **The return is a rotation, not a reflex.** It is spent when coverage has
  been zero for the whole declared redeploy pause — a front that genuinely
  moved — or on a last call when only a body physically standing on the surface
  can still change the result. Never on a one-tick relief gap.
- **The cheapest body roots.** Shortest declared windup first, and then the
  body the contract does *not* renew automatically. The renewable body is worth
  more standing on the objective than standing still beside it. Revision 1 had
  this exactly backwards.
- **Stations are the surface and its ring.** Every mobile body takes a distinct
  scoring tile, preferring the tiles an allied fortress actually covers, and
  only overflows to posts one step off. Suppression becomes territory only when
  somebody is standing under it. (Spreading bodies between surface and ring was
  tried and measured: it cost nine wins, because actors block actors and a full
  surface is ground the opponent cannot walk onto at all.)
- **Durability is spent on ground.** A body holding the surface eats the bolt
  and keeps the tile; it only steps off when the hit would leave it too thin to
  hold anything, or while the mode's own pause says presence pays nothing.

Measured against a fixed sparring set across three classes, three maps and
three seeds: **9–18 → 14–9–4**, and a median rooted tenure of 4 → 60 ticks.

## Movement and facing coupling

The contract may couple facing to movement, and this doctrine reads which arm
it is in from the movement profile rather than assuming one.

| Declared coupling | What the doctrine does |
| --- | --- |
| `preserve-facing` | a step is a free strafe; turning is a separate tick |
| `face-movement-direction` | travel *is* aim, so a body that still has walking to do never spends a tick rotating — the next step lays the muzzle for free |
| `facing-locked` | a route that turns a corner costs a rotation per corner, so the body turns onto its route deliberately; leaving a tile sideways costs two ticks, so evasion is priced at two and the gun trades instead |

The same reading re-prices the windup from the other side. A muzzle only
punishes a root if it can reach a firing lane **and be pointed down it** before
the transition completes. Where a step turns the body, an enemy that walks into
a lane arrives facing its own travel direction and owes a rotation; where a body
may only move where it faces, it owes one turn to travel and another to aim. An
enemy dodging covering fire pays in exactly the currency the windup is spent in
— so a forward root is genuinely cheaper on the coupled arms, and the
arithmetic says so instead of the doctrine assuming it.

That coupling reading is also a repair. Searching a route only over the
directions the legality mask offers throws away every route that is not already
straight ahead, and revision 1 consequently waited 78% of its ticks and never
reached an objective under `facing-locked`. Routes are now searched over every
cardinal and the body turns onto the one it wants.

## What it will not do

- **Root without relief.** A fortress has zero objective weight. Until an
  allied body is actually holding the scoring surface, the would-be fortress
  plays as a screen instead.
- **Root into a line that is about to be taken from us.** A front about to move
  *away* is the rotation this doctrine is named for; a front the opponent is
  about to take strands the fortress behind the line.
- **Root because it feels safe.** The windup is *priced*, not feared, under the
  coupling arithmetic above, and a stalemate buys a point of that budget
  because the stalemate is what is being paid for.
- **Concede a lane.** An idle rooted gun keeps firing down the lane that crosses
  the most contested tiles; suppression is free under the declared cadence.
- **Evade into a coffin.** Time-to-impact is counted in ticks rather than
  radius, and a tile whose only exit is behind a facing-locked body is a trap
  even though the map shows an exit.

## Contract-driven, not rule-driven

Nothing in the source names a rule. "Fortress" means *a form whose own action
mask contains no movement action*; the anchor route, its windup, and whether it
is reversible are read from the same-life transition catalog; objective tiles,
transform-legal tiles, reach, cadence, projectile geometry, capture threshold,
redeploy pause, movement/facing coupling and companion timing come from the
resolved contract; every action code comes from that tick's legality mask.
Every direction tie-break uses the shared mirror-fair ordering rather than an
absolute preference, which is a measured team-side bias on a symmetric map.

The consequence is that the doctrine survives contracts it was not written for:

| Contract | What the doctrine becomes |
| --- | --- |
| bulwark class arm | the child roots on its one-tick windup, the prime screens and renews |
| base Labs v1 | prime cannot anchor, so it walks home and **fabricates** a child; the child roots, irreversibly |
| duel-depth union arms | automatic companions and one-bend guns; same roles, curved intercepts instead of straight ones |
| a movement-coupled arm | the same policy, with turning priced into routes, evasion and windups |
| a contract with no anchor route | every body simply screens |

## Reading a match

Decision debug text narrates the doctrine: `rooting: N covered objective tiles`,
`on overwatch: no relief on the surface`, `on overwatch: they take the line in
4`, `on overwatch: windup would cost 3`, `front rotated: unrooting after N
rooted ticks`, `last call: unrooting to put a body on the surface`,
`turning to travel East`, `suppressing the objective lane`, `holding the
scoring surface`, `slipping the shot West`.

## Files

| File | Role |
| --- | --- |
| `IronRoot.cs` | the policy: roles, tenure gates, stations, threat response |
| `ContractLens.cs` | everything the doctrine knows, parsed from the contract only |
| `Kinematics.cs` | what a step costs when facing is coupled to movement |
| `FortressPlan.cs` | covering-tile selection, firing lanes, enemy lane sweep |
| `Gunnery.cs` | fire control: turret headings, straight guns, curved intercepts |
| `ArenaGeometry.cs` | rays, line of fire, flood fill, ordered first steps |
| `ArenaBasics.cs` | the current template helpers, synced verbatim |
