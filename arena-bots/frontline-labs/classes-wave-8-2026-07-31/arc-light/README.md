# arc-light

A Frontline Labs **striker**, class doctrine *flank-and-collapse skirmisher*.
Wave-8 revision of the wave-7 entrant of the same name. The fan doctrine is
unchanged; what is new is that this artifact now knows what a capture *is*.

Qualified **T4** on `frontline-qualification-5`
(`frontline-duel-depth-union-t4-v1`), `balanceEvidenceEligible: true`.

## The headline

**The channel turns one bit per body into the whole capture, and this doctrine
had that bit backwards.** Under `--capture channel` a team's claim weight counts
only the bodies whose tile did **not** change this tick, while denial weight
counts every body. A skirmisher is the least stationary chassis in the game, so
every discretionary step it takes on the point — unmasking a lane, kiting off a
bearing, clearing a sibling's path — used to cost a full tick of capture that no
part of the doctrine was pricing. And the same asymmetry is a gift in the other
direction: because denial counts movers, a body **defending** the point can dodge
every tick for free. So the wave is one sentence: **freeze when the claim is
yours, kite when it is theirs.**

Four consequences, all read from `rules.gameMode.capture` and none from a rule
card, so the same source plays a cell with no channel at all:

- **Stillness is the capture.** While holding this tile buys strictly more gain
  (or strictly more erosion, at the declared multiple) than stepping would, the
  whole discretionary half of the router is refused. Rotating, shooting, casting
  and investing stay available, because stillness is positional, not
  intentional — none of them changes the tile.
- **A channeler is prey.** The interrupt is scoped to controlling-team bodies
  standing on the active objective and it reverts the whole *run*, so a bolt into
  one is worth its damage in progress as well as in health. Against the declared
  threshold of 8 one ordinary bolt is an eighth of a capture and a three-lane fan
  of damage-2 bolts is three quarters of one. Measured: interrupt landings
  against a bulwark go **2.8 → 11.2 per match**.
- **Do not eat a bolt while you channel.** Staying is worth `gain − damage ×
  revertPerDamagePoint`; stepping to another tile of the same objective is worth
  the gain without this body's claim weight, with no revert. Take the larger.
- **Read the enemy's tier vector.** An edge tier means an enemy gun reaches one
  tile further than its profile declares, and every lane, bearing and escape
  answer derived from the declared number is then wrong on the last tile — the
  only tile a skirmisher stands on.

On the economy this doctrine is deliberately small. A dedicated harvester spends
a quarter of a three-body team's ticks, and under the channel that body is the
difference between holding and not; so there is no harvester, only a **detour
budget** — a pile is taken when reaching it adds at most a third of a tile per
scrap to the walk this body was already making. Corpses fall where a
flank-and-collapse doctrine is already standing, and the assay pays in full at
the tile.

**The purchase verb is shipped off, and not because it was not worth it.** See
`DX.md`: `invest` on the gun-travel track aborts the whole match with an engine
invariant failure, and the only track this doctrine wanted was gun travel.

## Headline results

Cell **`bastion`** (`--capture channel --economy scrap`), facing-locked, wave
game otherwise as wave 7; 4 disjoint seeds a leg; opponents are the wave-8
baseline artifacts. `x` is interrupt hits landed per match.

| leg | my rebuilt wave-7 self | this build |
| --- | --- | --- |
| striker mirror vs my own wave 7 | — | **0-0-4 draw @499t** |
| `striker-vs-striker` vs still-water | 0-4-0, −16.00 @330t | 0-4-0, **−14.00** @320t |
| `striker-vs-striker` vs vector-edge | 0-4-0, −16.00 @196t | 0-4-0, −16.00 @**231t** |
| `bulwark-vs-striker` vs iron-root | 1-3-0, −10.00 @370t, x2.8 | 1-3-0, **−6.00** @**479t**, **x11.2** |
| `bulwark-vs-striker` vs march-wall | 0-4-0, −2.00 @499t | 0-4-0, −2.00 @499t |
| `fabricator-vs-striker` vs spark-line | 0-4-0, −16.00 @334t | 0-4-0, **−12.50** @355t |

Records are unchanged on five of six legs and the wave shows up as **territory
and survival**, which is what a channel arm scores. The `siege` cell (channel,
no economy) reproduces every one of these numbers exactly, which is itself the
finding that the economy is nearly inert for this doctrine.

In the arms-absent cell `swell` this artifact reaches the identical tick as the
wave-7 build it replaces (breach at 370 against still-water, seed 11), because
with no capture channel and no economy declared every wave-8 rule short-circuits
on an absent contract block. It is not tuned for `bastion`; it is tuned for a
contract it reads.

This is an honest, modest wave. It does not turn a losing cohort position into a
winning one; it stops the doctrine from paying for a mechanic it did not read.

## Building

```bash
nilbots build <this directory> --no-cache
nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm --suite frontline-qualification-5 --out evidence/t4
```
