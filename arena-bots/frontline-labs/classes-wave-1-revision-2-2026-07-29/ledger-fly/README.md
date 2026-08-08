# LedgerFly — the attrition banker (revision 2)

Class: **fabricator**. Lineage: `ledger-fly-v1`. Role: verdict-doctrine,
target cumulative T4. Budget: one strategic revision; mechanical repairs free.

## The doctrine in one line

Bodies are currency, the prime is the bank — and a bank that will not lend is
just a vault.

## What changed, and why

Wave 1 said *companions are lent, not spent*. Its own replays say that rule
cost more than it saved. The lending test was denominated in enemies the body
could currently see; a facing-quadrant sensor was blind on a third to a half
of all ticks and the estimate floored at one, so the ledger declared itself
solvent while the other side quietly fielded more. Measured over the wave-1
matches: a Ready slot sat unspent for 40–160 slot-ticks per match, the other
side outnumbered us in 30–43 % of ticks, and we out-numbered them almost never.
Against ranged pressure that is the whole loss — fewer bodies means fewer guns,
and the side with more guns wins the exchange that decides the objective.

**Revision 2 reprices solvency against declared capacity.** The contract says
how many opposing unit slots exist and when each unlocks; that number does not
depend on what a 90° cone happens to catch. The bank now targets one body more
than the opposition can legally field and pays on the earliest tick the
fabrication route allows. Everything else about the banker survives: it still
does not duel, still stands off behind the exchange, still drops replacements
where the last exchange happened, and still never Splits or Anchors.

## What that means tick by tick

**The prime is the bank, not a duellist.** LedgerFly identifies its economy
anchor from the contract, not from a slot number: the unit whose lifecycle
assignment returns automatically after destruction is the bank. Losing it stops
fabrication for the whole return delay, so it holds a standoff behind the
exchange, keeps the approach inside its facing quadrant, and spends its ticks
on the books. It walks onto an objective only when nobody else is holding the
line, or when the clock has already decided the match.

**Solvency is read from the contract, not from the vision cone.** The ledger
still tracks what we owe and what we can see, but the lending test is
`active bodies < declared opposing capacity + 1`. An unlocked enemy slot counts
whether or not we have ever laid eyes on it.

**Replacements land where the last exchange happened.** The contract declares
the placement offsets and the order the host walks them, so LedgerFly replays
that rule locally before it queues. It tracks the last exchange from damage,
destruction, and hostile attack events, and if a different facing would drop
the child at least two tiles closer, it buys that facing — by *stepping* into
it when the movement profile turns the body on a step, and by rotating when it
does not.

**Trade children for bodies at favourable rates.** Target priority is: what we
can kill this shot, then the opposing bank, then lowest health, then nearest.
Every shot is simulated against the declared projectile geometry first — range,
travel, strict diagonal corners, wall termination, legal bend programs — so a
cooldown is never spent on a bolt a wall would eat.

**The empty gun still owes the team a tick.** Fire cooldown means roughly every
second combat tick has no shot in it. Those ticks are now spent on footwork
inside the region we are contesting — one step to the least-covered tile, never
off the objective — or, when nothing is in sight, on turning onto a lane worth
suppressing. Coverage counts remembered contacts as well as visible ones,
because the gun that just shot us is usually the one we cannot see.

**Let the fast rebuild win the long clock.** The bank never Splits (that
destroys the bank) and never Anchors (that removes objective weight). It wants
a long, even attrition game, because its rebuild clock is shorter than the
value of the bodies it trades away.

## Movement arms

Facing coupling is read from the form's declared movement profile; the field is
optional and its absence means *preserve facing*.

| Arm | What LedgerFly does differently |
| --- | --- |
| preserve-facing | the measured baseline; a step is a free strafe |
| move-sets-facing | retreat is repriced — the standoff shrinks by a tile so the bank keeps the exchange in its quadrant instead of backing out of its own vision; steps that turn away from the exchange carry an explicit cost; and a placement facing is bought by walking into it rather than by spending a rotation |
| facing-locked | routes are planned on the map geometry and paid for at emit time: the first step is taken when the mask offers it and rotated into when it does not. Ties break toward the current facing, so two equally short routes cannot make the body rotate back and forth forever. Evasion and footwork stay inside the mask — a rotation is not a dodge |

## Contract-driven, not arm-driven

Everything the doctrine needs is read at `StartLife` or from the per-tick
legality mask: ordered objective regions and this team's advance direction, the
economy anchor and its return spawn, the fabrication route with its source
region, output region, tile tags and declared candidate offsets, opposing unit
slots and their unlock ticks, the form catalog (health, vision, cooldown,
range, movement facing coupling) for both sides, the shot language of the
current form, the collision policy, the tick cap, the capture threshold, and
the timeout-ranking channel. Actions are selected by contract kind and stable
ID, paired with the numeric code from that tick's legality entry. Arms with no
fabrication route, automatically activated companions, or no bend envelope fall
through the same code without a special case, and any unexpected state resolves
to a legal action rather than a fault.

Direction ties never break on an absolute compass preference. Every search
takes its order from the template's `OrderedDirections` — advance first,
retreat last, laterals decided by this life's deterministic random stream —
because a shared absolute preference hands the advancing side a systematic edge
on a mirror-symmetric map.

## Files

| File | What lives there |
| --- | --- |
| `LedgerFly.cs` | the decision ladder and the banker/trader split |
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
  --runtime in-process --seed 7
nilbots experiment frontline-labs --bot . --opponent . \
  --movement facing-locked --runtime in-process --seed 7
nilbots build . --no-cache
nilbots experiment frontline-labs qualify --bot . \
  --suite frontline-qualification-5 --out evidence/t4
```

Both entrants declaring a class resolve the arm from their manifests; this
project declares `"class": "fabricator"`.
