# LedgerFly — the attrition banker

Class: **fabricator**. Lineage: `ledger-fly-v1`. Role: verdict-doctrine,
target cumulative T4.

## The doctrine in one line

Bodies are currency, the prime is the bank, and the bank does not go to the
front.

## What that means tick by tick

**The prime is the bank, not a duellist.** LedgerFly identifies its economy
anchor from the contract, not from a slot number: the unit whose lifecycle
assignment returns automatically after destruction is the bank. Losing it
stops fabrication for the whole return delay, so it holds a standoff behind
the exchange, keeps the approach inside its facing quadrant, and spends its
ticks on the books. It walks onto an objective only when nobody else is
holding the line, or when the clock has already decided the match.

**Companions are lent, not spent.** Fabrication is reactive. The ledger tracks
three numbers rebuilt from observations every tick: bodies we have lost since
the last queue, field bodies we currently hold, and the best estimate of what
the other side is fielding. A queue settles a debt — a loss, or a deficit the
opponent has opened. When we already field more bodies than they do and the
objective is ours, the Ready slot stays unspent: an unqueued slot is a fast
rebuild banked for the exchange that is still coming. An eager fabricator
spends its slots on the unlock tick; this one spends them on the exchange.

**Replacements land where the last exchange happened.** The contract declares
the placement offsets and the order the host walks them, so LedgerFly replays
that rule locally before it queues. It tracks the last exchange from damage,
destruction, and hostile attack events, and if a different facing would drop
the child at least two tiles closer to that spot, it spends one tick rotating
first. On the class arm the child materialises beside the prime in open field;
on the pad-bound base arm the same code simply walks back to the declared
source region and queues there. No coordinate, offset, or unlock tick is
hard-coded.

**Trade children for bodies at favourable rates.** Target priority is: what we
can kill this shot, then the opposing bank (killing their prime pauses their
economy the same way losing ours pauses ours), then lowest health, then
nearest. Every shot is simulated against the declared projectile geometry
first — range, travel, strict diagonal corners, wall termination, and legal
bend programs — so a cooldown is never spent on a bolt a wall would eat. When
no lane exists the body turns into one; when it is the one holding ground it
suppresses the contested lane rather than conceding it.

**Let the fast rebuild win the long clock.** The bank never Splits (that
destroys the bank) and never Anchors (that removes objective weight). It wants
a long, even attrition game, because its rebuild clock is shorter than the
value of the bodies it trades away.

## Contract-driven, not arm-driven

Everything the doctrine needs is read at `StartLife` or from the per-tick
legality mask: the ordered objective regions and this team's advance
direction, the economy anchor and its return spawn, the fabrication route with
its source region, output region, required and forbidden tile tags, and
declared candidate offsets, the form catalog (health, vision, cooldown, range)
for both sides' forms, the shot language of the current form (absolute
eight-way heading, facing-relative shot program with bends, or a payload-free
straight bolt), the collision policy that says whether our own bolts block our
own bodies, the tick cap, the capture threshold, and the timeout-ranking score
channel. Actions are selected by contract kind and stable ID, paired with the
numeric code from that tick's legality entry. Arms with no fabrication route,
automatically activated companions, or no bend envelope fall through the same
code without a special case, and any unexpected state resolves to a legal
action rather than a fault.

## Files

| File | What lives there |
| --- | --- |
| `LedgerFly.cs` | the decision ladder and the banker/trader split |
| `Ledger.cs` | losses owed, body counts, and the last-exchange anchor |
| `MatchLens.cs` | every contract fact resolved once per life |
| `FabricationRoute.cs` | local replay of the declared placement rule |
| `Gunnery.cs` | simulated straight, aimed, curved, and suppressing fire |
| `Field.cs` | blocking, bolt threat, gun coverage, and pathing |

## Running it

```bash
nilbots experiment frontline-labs --bot . --opponent . \
  --runtime in-process --seed 7
nilbots build . --no-cache
nilbots experiment frontline-labs qualify --bot out/bot.wasm \
  --suite frontline-qualification-5 --out evidence/t4
```

Both entrants declaring a class resolve the arm from their manifests; this
project declares `"class": "fabricator"`.
