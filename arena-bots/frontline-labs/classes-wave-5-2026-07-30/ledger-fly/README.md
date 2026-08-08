# LedgerFly — the attrition banker (revision 5)

Class: **fabricator**. Lineage: `ledger-fly-v1`. Role: verdict-doctrine,
target cumulative T4. Budget: one strategic revision; mechanical repairs free.

## The doctrine in one line

A body is worth what its own slot's rebuild clock costs in capture-ticks — so
quote health, bodies and ground in the same unit, and never sell a body for less
ground than it costs to replace.

## What changed, and why

Revision 3 fixed the *unit of account* (convertible objective-ticks). Revision 4
priced a tick by what could keep standing on it (shape, blood, throughput). Both
are intact and this revision does not reopen them. What neither could know is the
**exchange rate**, because the contract had not moved it yet: a slot that refills
in about the time a capture takes makes a body-for-ground trade roughly free, and
a slot on a slower clock makes the same trade a loss. This arm's roster declares
exactly that — ordinary children rebuild slower than a capture completes, and the
late slot slower still — so revision 5 reads both clocks and settles every trade
against them.

| Contract fact | What it changes | What LedgerFly does about it |
| --- | --- | --- |
| `capture.threshold` / `gainPerSoleTeamTick` | one capture has a price in TICKS | `MatchLens.ConversionTicks` is that price, and it is the denominator of everything below |
| each slot's own lifecycle profile `delayTicks` | a body has a price in ticks too, and two slots of one team need not agree | eating a bolt costs `damage / maxHealth` of this slot's clock and must be paid for with at least that many ticks of published claim; exposure on a tile is priced at the same rate |
| the ENEMY's lifecycle assignments | a kill removes a body for exactly as long as the slot it came from declares | the firing order prefers the kill that empties the slowest slot to refill — their late child over their early one — after the bodies it can finish this tick and the throughput asset |
| `shotProgram.minInitialAimSteps` / `maxInitialAimSteps` + `aimOnlyProgram` | a bolt may launch 45° off facing with ZERO bends: three straight rays out of one facing | `Gunnery` fires the plain diagonal in aimed fire and in suppression, which under `facing-locked` answers an off-lane contact without spending the rotation that would also cancel the step |
| form `objectiveWeight` on an OBSERVED body | a fortified body has left the ledger — it holds no ground and is the most durable thing on the board | it goes last in the firing order (still a target, never a priority), which is the same weight test that stops this doctrine fortifying its own bodies |

## Fortification, now that it is free

Where the arm makes anchoring reversible without limit and legal anywhere, every
argument against it disappears except the one this doctrine was always making:
**a zero-weight body scores nothing.** `Stances.cs` therefore gates on weight and
not on reversibility (`irreversibleForLife` is read, not assumed), so the same
source refuses an Anchor whether it is a one-way trade or a free round trip, and
declines to spend bolt after bolt on an opposing body that has taken itself out
of the count. Measured against an anchoring sparring copy of this same source:
30 anchors and 17 mobilizes in one match, one life cycling three times, 13 of
those anchors standing on objective tiles that score nothing for it.

## Measured, honestly

Candidate versus its own **rebuilt revision-4 source** on the wave-5 game, both
sides, twelve seeds per side (24 matches in-process; six seeds per side confirmed
under the controlled WASM runtime, with identical outcomes and identical accepted
decision streams).

| | record |
| --- | --- |
| candidate vs rebuilt predecessor | **13W 11L 0D** |
| the same pair mirrored against itself (the null) | 12W 12L 0D |
| leave-one-out: no aim-only diagonals | 9W 15L — worse than the predecessor |
| leave-one-out: eat-the-bolt unpriced | 11W 13L |
| leave-one-out: revision-4 firing order | 12W 12L |
| leave-one-out: flat exposure premium | 13W 11L — no effect at all |

This arm is strongly side-biased (team 1 wins 9 of 12 seeds whichever artifact
holds it), which is why every record is paired across sides: the bias cancels and
a neutral change scores exactly 12-12. `DX.md` carries the detail, the two
readings that measured worse and were deleted, and the reasons to distrust these
numbers.

## What it still never does

It never Splits, never Anchors, never enters an irreversible stance, and never
hard-codes a slot count, an unlock tick, a rebuild clock, a hold length, a
capture threshold, a fan width, a deflection threshold, a bend depth, or an aim
bound. `Standoff` (3 tiles, adjusted by declared reasons) remains the only tuned
constant in the bot.
