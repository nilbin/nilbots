# Iron Root

**Class:** bulwark · **Lineage:** iron-root-v1 · **Doctrine:** FORTRESS ROTATOR
**Role:** verdict-doctrine · **Qualified:** T4 (`frontline-duel-depth-union-t4-v1`)

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

Every other body stays **mobile**. One holds the scoring surface, because
territory is the only currency; the rest take ranked overwatch posts that see
the surface without crowding it. They contest rather than concede — a contested
objective pays nobody, and a body already standing on the tile is worth more
than a body that stepped off it to be safe.

When the front rotates and the lanes stop crossing anything that matters, the
fortress spends its **one return**, walks to the new line, and roots again.

## What it will not do

- **Root without relief.** A fortress has zero objective weight. Until a
  companion exists, or the contract says one arrives within the windup plus a
  settling period, the would-be fortress plays as a screen instead.
- **Root into a line that is about to move.** When a capture is within a few
  points of completing — whoever is completing it — the ground is about to stop
  being the ground. Waiting costs three ticks; rooting there costs the return.
- **Root because it feels safe.** The windup is *priced*, not feared. Every
  visible muzzle is checked for whether it can occupy a tile with a real firing
  lane onto us in time, at its own declared cadence, and the expected damage is
  compared against health. A stalemate buys a point of that budget, because the
  stalemate is what is being paid for.
- **Concede a lane.** An idle rooted gun keeps firing down the lane that crosses
  the most contested tiles; suppression is free under the declared cadence.
- **Evade into a coffin.** Time-to-impact is counted in ticks rather than
  radius, and a tile with no perpendicular exit is left early — a walled duel
  lane kills on the tick you run out of room, not the tick the bolt gets close.
  Evasion that is available on the scoring surface is taken there.

## Contract-driven, not rule-driven

Nothing in the source names a rule. "Fortress" means *a form whose own action
mask contains no movement action*; the anchor route, its windup, and whether it
is reversible at all are read from the same-life transition catalog; objective
tiles, transform-legal tiles, reach, cadence, projectile geometry and companion
timing come from the resolved contract; every action code comes from that tick's
legality mask.

The consequence is that the doctrine survives contracts it was not written for:

| Contract | What the doctrine becomes |
| --- | --- |
| bulwark class arm | prime is the fortress, three-tick windup, one return |
| base Labs v1 | prime cannot anchor, so it walks home and **fabricates** a child; the child roots on a one-tick windup, irreversibly |
| duel-depth union arms | automatic companions and one-bend guns; same roles, curved intercepts instead of straight ones |
| a contract with no anchor route | every body simply screens |

## Reading a match

Decision debug text narrates the doctrine: `rooting: N covered objective tiles`,
`on overwatch: windup would cost 3`, `on overwatch: front about to rotate`,
`front rotated: unrooting to re-fortify`, `suppressing the objective lane`,
`holding the scoring surface`, `slipping the shot West`.

## Files

| File | Role |
| --- | --- |
| `IronRoot.cs` | the policy: roles, anchor/mobilize gates, threat response, stations |
| `ContractLens.cs` | everything the doctrine knows, parsed from the contract only |
| `FortressPlan.cs` | covering-tile selection, firing lanes, enemy lane sweep |
| `Gunnery.cs` | fire control: turret headings, straight guns, curved intercepts |
| `ArenaGeometry.cs` | rays, line of fire, flood fill, first steps |
