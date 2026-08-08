# LedgerFly — the attrition banker (revision 6)

Class: **fabricator**. Lineage: `ledger-fly-v1`. Role: verdict-doctrine,
target cumulative T4. Budget: a coordination pass over multi-body play;
the doctrine is not reopened.

## The doctrine in one line

A body is worth what its own slot's rebuild clock costs in capture-ticks — so
quote health, bodies and ground in the same unit, and never sell a body for less
ground than it costs to replace.

## What changed, and why

Nothing above changed. Revisions 3, 4 and 5 fixed the unit of account, the price
of a tick, and the exchange rate between bodies and ground; this revision adds
the line those books never had, which is the tick this team loses **to itself**.

A body whose step is refused because a *sibling* wanted the same tile pays a full
tick and the ledger records nothing. Measured on the rebuilt wave-5 artifact
playing itself over 24 matches: **230 of its 250 refused steps were caused by one
of its own bodies**, nearly all of them two or three bodies stepping onto one tile
of the contested region — the same pair re-colliding every third tick for as long
as the exchange lasted. That is a debit this doctrine was already equipped to
price. It simply had no account for it.

## The coordination layer

The mechanism is stated once because everything below depends on it. There is no
shared memory between our bodies and a life never sees an ally's action — but
every life of a team receives the **same frozen observation**. So a plan computed
from observation and contract alone, with no private memory and no
`context.Random`, is computed identically by every one of our bodies. That is
common knowledge, and it deconflicts without a channel. `Convoy.cs` is that plan.

**Right of way is priced, not arbitrary** — the written rule the coordination bar
asks for. Between two of our bodies that want one tile:

1. fewer ticks of route remaining to its own berth (it clears the tile sooner, so
   asking *it* to detour is the dearer yield);
2. then the larger declared replacement cost — a slow-refilling slot is worth more
   ticks, and the bank carries every pipeline clock it feeds on top of its own
   return;
3. then unit id, then life id — a total order, so two of our bodies can never both
   believe they have right of way.

| line | what it does | measured |
| --- | --- | --- |
| **congestion** | one berth of the contested region per body, and a dearer sibling's route reserves its tiles for the ticks it needs them — refused this tick and next, priced after | **+17.31 margin** against the predecessor; cuts self-obstruction to a quarter against every opponent |
| **corridor** | a one-tile corridor cannot be shared: the body with right of way owns the corridor tiles its route crosses (corridors derived from the map, never from coordinates) | +7.06 margin against the predecessor, −1 point of 64 on the opponent stable, halves the remainder of the self-obstruction |
| **traffic** | no body of ours stands on, and no fabrication lands on, the tile our own next arrival needs | meets the bar; measured at **exactly zero** effect, not claimed |
| **spacing** | two of our bodies are not left where one declared fan spread, or one deflection sent back down our own lane, covers both — at strict tie-break weight | meets the bar; **zero** at tie-break weight and measurably *worse* at any weight that moved anything, not claimed |

A yield never immobilises a body: every route search drops the yields as its last
resort, and a dodge prices them instead of obeying them, because the ledger has
never rated a tile above a body.

## Measured, honestly

Candidate versus its own **rebuilt revision-5 source** on the deck game, both
sides, 24 seeds per side: **48W 0L 0D**, paired mean territorial margin **+20.00**
against a null of 23W 23L 2D / **+0.00**. Confirmed on 6 seeds per side under the
controlled WASM runtime — identical winners, margins and end ticks, and this wave
identical replay hashes too.

Two sentences of context that matter more than that record. Seeds move almost
nothing in this cell (48 matches of one pairing contained two distinct stories), so
per-rule attribution is measured against an **opponent stable** — the predecessor
plus three sparring variants built from its source — where the coordination layer
is margin-neutral overall and its reliable product is a **90 % cut in
self-obstruction** (5.86 → 0.60 self-inflicted refused steps per 1000 decisions,
choke stalls 10 → 1). And against a copy of *itself* the same cut is only about
half, with the remainder displaced from the objective region into the corridors.

`DX.md` carries every number, the two rules that measured at exactly zero, the
three rule versions that measured *worse* and were repaired rather than shipped,
the head-to-head instrument that looked authoritative and was not, and the
opponent this revision loses to.

## What it still never does

It never Splits, never Anchors, never enters an irreversible stance, and never
hard-codes a slot count, an unlock tick, a rebuild clock, a hold length, a
capture threshold, a fan width, a deflection threshold, a bend depth, an aim
bound, or a corridor coordinate. `Standoff` remains the only tuned constant in
the bot; the coordination layer adds a horizon and a search depth in ticks, and
nothing else.
