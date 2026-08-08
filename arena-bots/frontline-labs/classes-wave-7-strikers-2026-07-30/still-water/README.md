# Still Water — patient interceptor (striker)

Lineage `still-water-v1` · revision 7 · class `striker` · role `verdict-doctrine` ·
Frontline Labs classes arm, wave 7 strikers (the `swell` game: keel + kit +
universal bend + `wane` + launch offsets + open placement + ticking cooldown +
the re-armed salvo, all facing-locked).

Artifact `out/bot.wasm` sha256
`5f32c2cc40ae72a984f2224e3659fb0341246b91b746b80452e497effa8f816d`, built on
CLI 0.9.25 / SDK 0.10.8. T4 on `frontline-qualification-5` — all six probes PASS,
exit 0.

## The idea in one sentence

Unchanged since revision 5: do not walk into the duel; stand one bend's reach
behind the contested point, put the gun across the approach, and take the ground
last but never later than the clock can still pay for.

## What revision 7 is about: the fan's price list changed, and only the price list

The volley was re-armed. Three declared prices moved at once and a fourth
appeared, and revision 6 was priced against all four of the old ones:

| what the contract now says | where it is read | what revision 6 assumed |
| --- | --- | --- |
| the fan bolt deals **2**, the mobile bolt **1** | both `attackProfiles[].projectile.damagePerHit` | one damage class for both |
| the fan's `cooldownTicks` is **1**, the gun's **2** | the two profiles | the stance taxed the gun |
| the entry windup is **1** | `sameLifeTransitions[].windup.durationTicks` | a two-tick blind commitment |
| the entry route declares `cooldownTicks: 8`, scoped to the UNIT SLOT | the route, plus the live clock on `self.routeCooldowns` | no such price existed |

Revision 7 changes **nothing else**. Every positional argument revisions 5 and 6
measured is carried forward byte-identical: the five-family interception table,
the aim-widened standoff band, cover quality, the convoy right-of-way convention,
the lane claim, choke precedence. The whole pass lives in `Salvo.cs` and in the
cast ledger it re-prices.

## What the fan is for now

**It is the same verb, released from a bar that was quoting stale prices.**

Revision 6 made a fan clear a margin of `1 + 0.15 × lanes` over the bolt it
displaced — a tax for displacing a gun that can guess down several lanes for one
tick — and floored it at the fan's raw bolt count. Both numbers encoded the arm
they were measured on. Read as arithmetic instead of as constants:

- the breadth tax is owed only on the lanes the fan **gives up**, and a
  three-bolt fan gives up none of a three-lane gun's; it is zero here and
  reappears by construction on a narrower fan;
- what the fan does still owe is **tempo** — the ticks it spends unable to walk
  beyond the one tick the displaced bolt also costs (the two windups), plus
  whatever the stance gun's cooldown adds over the mobile gun's. On this arm that
  second term is zero, so a cast costs the gun nothing at all;
- the floor was the bolt **count**, which silently assumed each fan bolt was worth
  one mobile bolt. Divided by the declared damage ratio it becomes what it always
  meant, and reverts to revision 6's number wherever the two guns hit alike.

That re-derivation is the whole shipped change. It is worth **+6.82 ± 1.53 mean
territory per match over the rebuilt predecessor across 220 paired matches**
(91-125-4 against 69-146-5). Fan usage goes from 325 entries to 439 — a 35%
increase, not a spree, and the spree was measured and rejected (below).

### Whom to fan: the threshold was already in the valuation

The doubled bolt buys exactly one thing a patient doctrine cares about — a
**threshold**. A body whose health is at or below the fan's damage and above the
gun's is one the fan removes and the bolt merely wounds, and this lineage converts
damage into TIME, so removing it is worth the slot's whole absence clock.

Nothing had to be added for that. `EnemyForecast.Pressure` has taken the *firing
profile's own* `damagePerHit` since revision 5, so re-arming the bolt moved the
kill threshold by itself: every striker body at 2 health, and a fabricator prime
at **full** health, became a body the fan finishes. What was blocking those casts
was the bar, not the target selection.

A separate structural override — waive the two-body rule whenever a ray kills —
was built, measured over the same 220 matches, and **rejected**: −0.24 ± 1.72
territory on a worse record (85-132-3) while changing 72 of 220 pairings. It fires
a great deal and converts none of it. `Salvo.KillThreshold` retains it as a
switch.

### Tempo: the clock is read, and the mask turns out to enforce it already

`self.routeCooldowns` publishes the exact tick the entry reopens, and the bot
reads it for itself and for allies. Its measured attribution is **exactly zero, on
0 of 220 pairings** — and the reason is a fact about the platform rather than
about the doctrine. On all 420 mobile life-ticks in the sample where the clock was
live, `transform` came back `available: false`: **the legality mask already
enforces the route cooldown**, so a mask-driven bot is correct about the clock for
free.

There is a trap beside it. On those same ticks the action's `form-target`
constraint **still lists the held stance** among its `allowedFormIds`. A bot that
reads the argument domain without reading `Available` requests a refused route
every tick of the window.

## How a tick is spent

Unchanged from revision 6 except for step 3's price.

1. A bolt that will cross this tile during the coming resolution outranks
   everything, and the step that answers it must still be alive three ticks later.
2. Companions, whichever way the contract hands them over.
3. The cast ledger — now with the bar re-derived from the declared windups,
   cooldowns and damage, and with a fan that would feed a projectile guard priced
   for what the guard sends back.
4. The gun, if the trajectory arrives on a tile some prediction actually names.
5. Otherwise the feet, under the convoy conventions.

## Postures

Unchanged from revision 6. **Cast** now reads: a fan answers two bodies, seals a
contested point, or guards the point this body already holds — and clears a bar
computed from what the two guns and the two windups actually declare.

## Headline results

220 paired matches per configuration: 8 cohort opponents × 20 seeds, with
same-class cells played from **both sides**, because a mirror-symmetric map still
carries a team-side bias that has to cancel rather than be credited. Errors are
standard errors over the 220 pairings; seeds are not independent trials of a
random process, and what the spread describes is variation across pairings.

| cell | revision 6 | revision 7 |
| --- | --- | --- |
| striker (vector-edge, arc-light, own r6) | +2.58 ± 4.88, 59-56-5 | **+3.37 ± 4.86, 61-55-4** |
| bulwark (iron-root, march-wall, gate-stone) | −60.00 ± 0.00, 0-60-0 | −60.00 ± 0.00, 0-60-0 |
| fabricator (spark-line, ledger-fly, `--five-slots wane`) | −21.15 ± 7.73, 10-30-0 | **+14.00 ± 7.15, 30-10-0** |
| all | −18.80 ± 3.50, 69-146-5 | **−11.98 ± 3.56, 91-125-4** |

Two caveats, both at length in `DX.md`:

- **The gain is one cell.** Against spark-line the pass turns 0-20 (−42.30) into
  20-0 (+28.00). Head-to-head against its own rebuilt predecessor it is
  **17-19-4 at −2.55 ± 6.85**, indistinguishable from the +0.00 ± 6.78 the
  predecessor scores against itself over the same 40 two-sided pairings. The
  mirror is a wash and is reported as one.
- **The bulwark cell is a structural loss the fan cannot touch.** All 60 matches
  end in a base breach between ticks 100 and 300, in every one of the fourteen
  configurations measured, with the stance entered 20–80 times and nothing moving.
  It is a posture problem — the doctrine holds its station on its own last
  objective while the bulwark grinds forward — not a stance problem, and it is
  outside a fan-integration budget.

## Contract discipline

Revision 6's list stands, plus: the two guns' `damagePerHit` and `cooldownTicks`,
both windup durations, the fan's bolt count and spread policy, each route's
`cooldownTicks` and the live `routeCooldowns` clock for self and allies, the
opposing catalog's worst declared bolt, and every projectile guard with its
deflection budget. The numbers 2, 1, 8 and 3 appear in this README and in
comments; none of them is a literal in a decision path.

## Files

Revision 6's table stands. One file is new:

| File | What it holds |
| --- | --- |
| `Salvo.cs` | the wave-7 pass: seven rules as switches with their measured attribution, the re-derivation of what a stance costs from the declared windups/cooldowns/damage, and the published route-cooldown clock for self and allies |
