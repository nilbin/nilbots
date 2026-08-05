# Ambush terrain checkpoint — the warren map (2026-08-05)

Owner direction: the game is too open — limited vision is key, dodging
is too easy, and running up behind a bot should be lethal for skilled
flankers. This checkpoint delivers the terrain half and locates the
remaining constraint in the rules.

## What exists now

- **Predation capability** (`4b7c5085`, sheet-controlled, no rules
  change): engagement `holdFire` (no focus, tracking, or chasing until
  a target enters the gate), custody `baitDrop` (arc-toss a Core into a
  pocket; a voluntary drop is re-collected from the dropper's own tile
  next tick start — the mechanism behind the old disposal-affordance
  REVISE), `ambush` stance (slip out of visible enemies' facing cones).
  Frozen sheets verified byte-identical in play.
- **`arc-relay-ambush-01` on the warren map** (`f22648ea`): counterflow
  plus 30 wall tiles in exact 180°-chiral pairs — sightline breakers
  across the open strips and two dead-end alcove pairs (pockets at
  (8,4)/(22,18) and (8,18)/(22,4)). Rules documents byte-identical to
  -03; only the map fingerprint mints, so any behavioral difference is
  terrain alone. All 191 protected tiles (routes, paths, anchors, pads,
  gate holes, well/reactor rings) verified clear; 507 open tiles
  mutually reachable. Chirality keeps the map asymmetric in feel — each
  side's alcoves sit on different lanes — but fair by construction.
- Warren layout re-pins, a warren twin of the stock baseline control,
  and `predator-v2`.

## Fairness and health

Stock mirrors, pooled: warren **9/12 west**, counterflow **9/11 west**
on overlapping seed sets — the west lean is the baseline's documented
hook residual, not the walls (an initial 6/6-west batch was seed
clustering; fresh seeds split 3/3). The warren tracks the established
-03 fairness envelope. Frozen degeneracy bars: warren mirrors fully
cohort-eligible, no trips, detectors untouched. Games are structurally
healthy (normal lengths, bank counts, no stuck pathing).

## Key rules facts established on the way

- Body vision is facing-quadrant only (range 7, omnidirectional only at
  the adjacent ring). "Remove vision behind the bot" is already true;
  the open map simply let the team union cover every rear.
- **Projectiles are team-vision-filtered**: a shot from behind an
  isolated bot is invisible and undodgeable by rule today. Openness is
  the dodge assist; walls remove it.

## Predator-v2 on the warren: 0/4, and why

Four-body trap: economy collapse (five bodies at a five-standable-tile
pocket while stock farms three wells; losses by ~260). Two-body trap
with six farmers: better games, still 0/4 — the alcove area itself
trades 17:7 **against**. The trap fires, the bait is taken, and the
exchange still loses, for two compounding reasons:

1. **A dead-end pocket corners its occupant too.** No escape lanes; once
   the spring happens the ambusher is the most findable body on the map.
2. **Kills are too slow to pay for surprise.** Three hits at fire
   interval 2 is ~6 ticks per kill; stock's converging bodies arrive
   inside that window, every time. Positional surprise buys ~2 free
   ticks and the fight then reverts to numbers, which stock always wins.

This is now a rules constraint, not a map or capability constraint.

## The decision this points at (owner's call)

The terrain and capability exist to make flanking skill matter; the
combat parameters don't reward it yet. Two levers, both already scoped:

- **Backstab lethality**: a rear-arc damage multiplier (impact heading
  inside the victim's blind arc). 1–2 hits to finish a flanked squishy
  makes every spring in this report a won exchange. Engine rules field →
  new mint beside (e.g. `arc-relay-ambush-02`), moderate work, same
  pattern as ripening.
- **Death punishment**: the return delay is already a profile parameter
  (`Return16`/`Return24` exist; forward-combat runs 20). Raising it is a
  one-line sibling mint and makes every kill buy real tempo.

Recommended sequencing: mint both onto the warren as one "predation
rules" arm (terrain alone is now measured and neutral, so a combined
arm A/Bs cleanly against `arc-relay-ambush-01`), re-screen predator-v2,
and only then decide whether the concept earns a full pre-registered
bar campaign.

Commits: `4b7c5085` (capability), `d1586144` (predator-v1 findings),
`f22648ea` (warren mint + fairness companions).
