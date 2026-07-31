# LedgerFly — the attrition banker (revision 8)

Class: **fabricator**. Lineage: `ledger-fly-v1`. Role: verdict-doctrine,
target cumulative T4. Budget: one doctrine pass integrating the capture channel
and the scrap economy over the wave-6 coordination layer; the doctrine itself is
not reopened.

## The doctrine in one line

A body is worth what its own slot's rebuild clock costs in capture-ticks — so
quote health, bodies, ground **and now scrap** in the same unit, and never sell
one for less of another than it costs to replace.

## What the new game changed, and what it did not

Nothing above changed. What changed is **which ticks are convertible**.

Under `--capture channel` a capture counts only the bodies that **did not change
tile** this tick; denial counts all of them; gain scales with the surplus and
stops at a declared cap; and damage to a controlling body **standing on the
objective** reverts that run's work. Under `--economy scrap` there is a fixed
pot of loose resource on the map and a short, capped ladder to spend it on.

So the wave-8 headline is one sentence:

> **Claim is stillness, denial is presence — and the tick you spend standing
> still is the most expensive tick this doctrine has ever had to price.**

Standing still on the point is now the only way to take ground, and it is also
the way to be shot, and being shot on the point costs the whole run rather than
one body's share. Every line below is that trade, priced.

## The lines, and what each is answerable for

Measured against the rebuilt wave-6 artifact and the wave-8 baseline cohort;
full tables, nulls and seed counts in `DX.md`.

| line | what it does | measured |
| --- | --- | --- |
| **still** | while this body's stillness is actually buying progress, every discretionary step — footwork, berth-shuffling, staging — is refused | **+28.0** territorial margin in the primary cross-class cell; **−21.3** in a fabricator mirror with the economy absent. The largest line in the revision, in both directions |
| **escort** | berths on the region are capped at **their weight plus the declared cap**, because gain is `min(cap, claim − denial)`; the surplus body is planned onto the ring *off* the region, on the firing lanes into it, where damage reverts nothing | 0.0 on margin; it is what keeps a body from parking where it converts nothing |
| **interrupt** | a claimer with a bolt inbound steps **inside** the region rather than eating it — the revert costs the run, the step costs one tick, and denial counts movers. The exact inverse of the wave-5 rule, on the arm that declares an interrupt | 0.0 on margin |
| **invest** | one designated body converts bank into tiers, ordered by the contract's declared **effects** with a published stopping condition for each: plate until one declared enemy contact can no longer delete a fresh prime, then sight until we see as far as we shoot, then reach, then the remainder | 0.0 on margin; buys 1–2 tiers a match on assay income alone |
| **lethal** | a declared hit that meets this body's health is priced like a coordination yield, not like a wall — the first version made it a wall and cost a fifth of a match | 0.0 after the repair; **−21.3** before it |
| **courier** — **shipped OFF** | complete, gated, and measured: **−22.3** against the wave-8 bulwark artifact, **+6.0** in the fabricator mirror, 0.0 everywhere else. A body sold for less ground than it costs to replace is a loss | the line stays in the source as the instrument that measured it |

## The economy this bot actually runs

It does **not** go to the deposits. It banks the **assay** — stepping onto a
wreck pays one scrap at the tile with no transport — and spends it. That is
about nine scrap a match, which funds the plate tier that turns a one-bolt kill
on a two-health prime into a two-bolt kill, and usually the optic tier that
closes the gap between what this chassis sees and what it shoots. It reads
`purchaseMode`, so on the control level where the bank buys by itself the
purchase routine is simply absent.

## What it still never does

It never Splits, never Anchors, never enters an irreversible stance, and never
hard-codes a slot count, an unlock tick, a rebuild clock, a hold length, a
capture threshold, a gain cap, an erosion multiplier, a revert rate, a fan
width, a deflection threshold, a bend depth, an aim bound, a deposit address, a
tier price, a track name, or a corridor coordinate. `Standoff` remains the only
tuned constant in the bot.
