# Still Water — patient interceptor (striker)

Lineage `still-water-v1` · revision 8 · class `striker` · role `verdict-doctrine` ·
Frontline Labs classes arm, wave 8 (the candidate game plus the CAPTURE CHANNEL
and the SCRAP economy — `bastion` where both are present, `siege` and `forge`
where one is, `swell` where neither is).

Artifact `out/bot.wasm` sha256
`37cf30e5d6f1ebdeec93b0b9490124e5351a8f6f6483fec09c5ab85817af90b7`, built on
CLI 0.9.27 / SDK 0.10.10. T4 on `frontline-qualification-5` — all six probes
PASS, exit 0.

## The idea in one sentence

Unchanged since revision 5: do not walk into the duel; stand one bend's reach
behind the contested point, put the gun across the approach, and take the ground
last but never later than the clock can still pay for.

What revision 8 changes is what "the gun across the approach" is worth. On a
channel ruleset that gun is no longer only a way of removing bodies. It is the
only verb this doctrine owns that moves the objective number **without standing
on the tile**.

## What revision 8 is about

Two arms landed at once, and between them they re-price every positional
argument this lineage has ever measured. All of it is contract data.

| what the contract now says | where it is read | what revision 7 assumed |
| --- | --- | --- |
| claim weight counts only bodies that did not change tile; denial weight counts all of them | `capture.controlPolicy` + `capture.stationaryGainMultiplierCap` | presence was presence, moving or not |
| damage to the CONTROLLING team's bodies **on** the objective reverts that team's work on the current run | `capture.claimInterrupt` (`kind`, `revertPerDamagePoint`, `scope`, `granularity`) | damage bought health, never ground |
| a standing enemy claim erodes at a declared multiple of the rate a fresh one builds | `capture.opposingErosionMultiplier` | erosion cost the same as capture |
| a capture costs 8, not 15 | `capture.threshold` | read, but everything budgeted against it moved |
| loose scrap, a carried load, a home bank, and a typed store | `gameMode.scrapEconomy` | did not exist |
| the store is a player verb with an action cost | `scrapEconomy.purchaseMode`, action `invest` | did not exist |
| both teams' banks and tier vectors, every live pile, every visible body's load | `mode.scrapTeams`, `mode.scrapPiles`, `carriedScrap` | did not exist |

Every one of those fields is **inert-omitted** on a ruleset without its arm, so
the whole pass branches on presence. Measured consequence: on the `swell` cell,
where neither arm is declared, this artifact reproduces revision 7 on 33 of 33
paired matches on one seed set and on 53 of 55 on a disjoint one — and the two
that differ are the one wave-8 rule that is deliberately not gated on an arm
(never feed a mirror, below).

## The doctrine headline

**A bolt onto the point is territory, and what a body is worth to the point is a
declared number.**

Three consequences, and they are the whole pass:

1. **The interrupt is the premier verb.** One bolt landing on a body of the
   controlling team, standing on the active objective, takes its damage class
   off that team's whole run. Against a threshold-8 channel a salvo fan across
   three spread controllers is up to six — three quarters of a capture — from
   outside the point. So a shot that reverts a live run fires on its own
   account: it is not competing with a better guess, it is competing with one
   step of route. (The MECHANIC is where most of the pass's margin comes from.
   The explicit RULE that fires for it measures exactly zero, because the
   wave-5 shot ladder already fired at those bodies unaided — see `DX.md`. Both
   facts are true and neither is allowed to stand in for the other.)
2. **Stillness captures; presence denies.** The tile a body ends the tick on is
   the whole rule — rotating, shooting, entering a stance and investing all keep
   it. So the ladder pays a body for standing on ground it is taking, and pays
   it nothing for standing on ground it is only denying, because denial counts
   a body that keeps walking and dodging just the same.
3. **A body is spare only when the contract says so.** `stationaryGainMultiplierCap`
   declares that surplus past the ceiling buys no capture speed at all — so the
   marginal body is free, and the deposit lane is worth its walk. Where no cap
   is declared and net weight scales the rate, every body on the point IS rate,
   nothing is spare, and one tile of detour for one scrap is a bad trade. Both
   the courier and the assay are priced off that one field.

That third rule is the one that stops this being a bastion bot. It was found by
measurement: an ungated economy costs about 26 territory a match against the
fabricator cohort where no cap is declared, and buys about 5 where one is.

Measured attribution, leave-one-out from the shipped whole over 132 paired
matches: stillness is worth **+5.18 ± 1.51** overall and **+18.42 ± 5.15 in the
bastion cell alone**; the four economy rules removed together are worth
**+1.85 ± 1.01**, of which **+7.64 ± 3.67** is bastion. The interrupt rule
itself measures exactly zero, and `DX.md` says at length why that is an honest
zero rather than a quiet one.

## Never feed a mirror

The pass's largest single positional repair is not about either new arm. A
guarded form returns the bolt that arrives inside its facing quadrant, from its
own tile, along the exactly reversed heading, owned by its team, carrying the
damage class of the bolt it returned. Revision 7 priced that as a **score
penalty**, and a penalty loses to a large enough prediction: measured, this
lineage stood in a corridor and shot itself to death against an aegis shell —
three bolts, no reply, dead on tick 16, the objective free for the rest of the
match.

A returned bolt is not a worse shot. It is a self-inflicted hit. Revision 8
refuses the trajectory outright unless the contact is the one that spends the
guard's declared deflection budget, and it applies the test to the FIRST enemy
body the lane crosses rather than to the tile the plan was aiming at — because a
projectile stops on the first enemy actor, whatever the planner had in mind.

## How a tick is spent

1. A bolt that will cross this tile during the coming resolution outranks
   everything, and the step that answers it must still be alive three ticks later.
2. Companions, whichever way the contract hands them over.
3. The cast ledger — now with a fan that is scored for what it reverts as well
   as for what it hits.
4. **The store**, on a tick the ladder was going to spend on nothing. A purchase
   costs the action but not the tile, so a body channelling a point can buy while
   it channels.
5. The gun — if the trajectory arrives on a tile some prediction names, **or if
   it reverts a live enemy run**.
6. Otherwise the feet, under the convoy conventions, with the channel arithmetic
   and the assay priced onto every candidate tile.

## Reading the store

Nothing in the purchase path names a track. The legality mask says which tiers
are legal right now — affordable out of the standing bank, under their own max
tier, inside the team's total cap — and each is ranked by what its **declared
effect** would close against the numbers the two form catalogs publish:

- a sight tier is worth the gap between what this chassis SEES and what it
  SHOOTS, and nothing past it: a doctrine that only fires at bodies a prediction
  names is bounded by vision, not by reach (striker: range 8, vision 6, so two
  tiers of `optic` are exactly the gap, and that is what it buys);
- a travel tier is worth a lot while it buys the opening shot against the
  opposing catalog's best declared reach, and little once that margin is already
  there;
- a health tier is bought in **hits**, not in points: a tier that does not change
  how many of the opposing catalog's worst declared bolts this chassis survives
  has bought nothing. Against a two-damage fan a three-health striker gains no
  hit from a fourth point — which is the same arithmetic that makes the tier
  compulsory for a two-health fabricator prime, read from the other side.

## Headline results

Against its own predecessor's source rebuilt on the current toolchain, over two
disjoint seed sets, all four cells, both team sides on every same-class pairing.
Errors are standard errors over the pairings; seeds are not independent trials
of a random process, and what the spread describes is variation across pairings.

| cell | seeds 1–3 (n=33) | seeds 11–15 (n=55) |
| --- | --- | --- |
| **bastion** | 13-20-0 vs 6-25-2 · **+8.97 ± 4.05** | 19-36-0 vs 12-41-2 · **+4.76 ± 4.05** |
| siege | 7-25-1 vs 6-25-2 · +1.33 ± 2.51 | 12-43-0 vs 12-41-2 · −0.62 ± 0.91 |
| forge | 11-22-0 vs 11-22-0 · −0.24 ± 1.34 | 19-34-2 vs 17-36-2 · **+4.51 ± 2.39** |
| swell | 11-22-0 vs 11-22-0 · +0.00 ± 0.00 | 18-35-2 vs 17-36-2 · +1.85 ± 1.41 |
| all | **+2.52 ± 1.27** | **+2.63 ± 1.25** |

Three caveats, all at length in `DX.md`:

- **The gain is the channel.** `siege` is a wash on both sets and `swell` is
  where the artifact is supposed to be — and is — its predecessor.
- **The bulwark cell is the first this lineage has ever scored in**, at
  **+16.00 ± 8.00** and **+16.00 ± 6.05** on the two sets: 0-15 becomes 3-6 and
  5-10. Denial that counts a moving body, and a gun that stops shooting itself.
- **The fabricator cell is an honest regression on this arm.** Revision 7 beat
  the fabricators 9-1 without the channel and still does; under the channel both
  revisions lose the cell and this one loses it by more (−6.4 to −10.7). Five
  slots reach the stationary-surplus ceiling with bodies to spare, and every
  rule that makes a body worth more on the point makes the body I do not have
  hurt more. There is no fix inside this budget and none is claimed.

On the shipped configuration the interrupt is doing the work the doctrine says
it is: over 55 bastion matches it reverts **523 points of enemy progress against
215 taken**, on a threshold of 8.

## Contract discipline

Revision 7's list stands, plus the whole of `gameMode.capture`'s channel block
and `gameMode.scrapEconomy`. The numbers 8, 2, 4, 6, 10, 120, 200, 280 and 360
appear in this README and in comments; none of them is a literal in a decision
path. Deposit addresses, the deposit schedule, pile expiry, carry capacity, the
assay, the bank regions, every track's effect/magnitude/max-tier/price, the
interrupt's rate and granularity, and the surplus cap are all read.

## Files

Revision 7's table stands. Two files are new:

| File | What it holds |
| --- | --- |
| `Channel.cs` | the capture pass: seven switches, the per-tile claim/deny arithmetic, the escort geometry, the interrupt's declared bound, and the one field that decides whether a body is spare |
| `Ledger.cs` | the economy pass: four switches, the contract read, the effect-driven track ranking, and what a tile is worth when there is scrap on it |
