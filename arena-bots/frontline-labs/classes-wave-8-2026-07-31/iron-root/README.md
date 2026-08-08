# Iron Root

**Class:** bulwark · **Lineage:** iron-root-v1 · **Revision:** 7 (GARRISON)
**Doctrine:** FORTRESS ROTATOR · **Role:** verdict-doctrine
**Qualified:** T4 (`frontline-duel-depth-union-t4-v1`), `balanceEvidenceEligible`

A bulwark does not win by out-shooting anyone. It wins by making one piece of
ground expensive to stand on for longer than the opponent can afford to keep
paying, and then moving that expense forward.

Revision 7 keeps that and repairs the sentence underneath it. Under the channel,
**holding ground is no longer standing on it — it is standing STILL on it, and
not being shot while you do.** Two words changed and every presence decision in
the doctrine changed with them.

## The headline is a refutation, and it is stated first

> The class's advertised leverage on this arm is that *the turret finally has a
> job*: a gun on cooldown 1 that reverts an enemy channel. **Measured, that is
> backwards, and it is backwards for a reason that generalises.**

A body of positive objective weight standing on the surface subtracts from the
enemy's gain multiplier **unconditionally** — no line of sight, no vision, no
cooldown, nothing to dodge and no arc to turn it aside. A rooted gun's revert is
the *same size* and is conditional on every one of those, and it costs the weight
to buy. So the turret is not this class's recapture denial. **The body is.**

The measurement is not close. Rooting for denial, gate written permissively:
487 anchored ticks across sixteen mirror cells bought **five** points of denial
and cost **14.5 points of territory a cell**. Gate rewritten to demand a live,
visible, clear-ray body of the claiming team on the surface: it then fires almost
never and *still* trails, because vetoing is not free either. The rule ships
**off**, with both numbers, in `Channel.cs`.

What survived from it is the half that never spends weight to find out: the
interrupt's own scope, read back as a **target priority**. A bolt into a body of
the claiming team standing on the region reverts that team's run; the identical
bolt one tile off it reverts nothing. That is fire control, not a bargain.

## What revision 7 changed: one idea, eight rules

> **Progress is a health bar. Damage on the point and capture progress are the
> same currency, so every rule that used to price a tile in presence now prices
> it in rate.**

### G1 · Stillness is the capture — and it outranks the post

A body whose tile did not change is the only body that pays into a claim. So a
body already paying is *stationed by definition*, whatever the ranking thinks of
its tile: walking to a better post costs a tick of gain and buys a proxy for
gain. The lock is a rate subtraction, not a flag, which is why it **releases
itself** the moment the step is free — at the declared cap the third stationary
body's departure costs nothing and the same arithmetic says so.

### G2a · Root for denial — **refuted, shipped off, measured twice**

See above. `Channel.cs` carries the full case and both gates.

### G2b · The interrupt tells fire control which body is the score

Kept. Inert on any ruleset that declares no interrupt.

### G2c · Root-exit on weight demand — **refuted, shipped off**

Leaving the turret the moment the surface could build or erode reads correct and
costs 12.1 a cell: it pulls guns off positions that were still paying. Bundled
with G2b it measured as one −5.4 rule; split, the two halves have opposite signs.

### G3 · Screen the channeler

A body on the firing line and **off** the region eats a bolt that would have
reverted the run — free, because only damage *on* the region reverts anything and
allied projectiles pass through. Gated on the declared interrupt scope.
**Measured exactly inert on every cell tried** and labelled that way.

### G4 · Cap discipline

Surplus stops paying at the declared cap, so while my team is building, a body
past `enemy denial + cap` buys no speed and adds one more place a leaked bolt can
revert the whole run through. While *they* build, every body on the surface
subtracts from their multiplier and the list is not cut at all — the sign flips
with who is claiming. **Measured exactly inert**, and G1 is why: a body the lock
has already pinned cannot be moved by a change to where it "should" stand.

### G5 · Erosion urgency

An enemy claim erodes at the declared multiple, so two ticks of control can undo
eight of theirs. **Measured exactly inert**, and provably: with G2a and G2c off,
its only remaining consumer is G4, which is itself inert.

### G6 · The interrupt prices the shield

Revision 6 valued a deflection in health. For the body actually channelling it is
worth health **and** progress, so the arc is not armour — it is the claim. The
shell's budget is three deflections, which is about two ticks short of a full
channel at the declared threshold: that arithmetic is in the source rather than
discovered in a loss. Worth **+5.8 a cell**; the envelopment refusal above it is
untouched, so against numbers the answer is still the gun.

### G7 · Spend the bank

The store's verb costs the casting body its action, so the cheapest caster is a
body whose action was already a wait — and this doctrine manufactures those:
a channeler holding still, a shell with nothing inbound, a rooted gun with no
line. The purchase therefore lives inside the idle fallback and needs no
schedule. **The doctrine's own idleness is the budget.**

Track choice is derived, and one derivation was corrected by measurement: where a
claim interrupt is declared, the **ceiling** track goes first, because damage is
progress and the body's standing time on the point *is* the claim. The gap model
preferred sight and measured a point a cell worse.

**The gun-travel track is declined, and that is a defect workaround, not
doctrine.** Buying it aborts the match — `A retained projectile must preserve its
exact resolved committed path`, no result and no replay — on both runtimes and
every seed tried. There is no observation a bot can gate on; declining while this
body's own `VisibleProjectiles` is empty still aborts eight seeds out of eight.
Full repro in `DX.md`.

### G8 · Take the pile under the boot, never the walk across the map

The assay pays in full at the tile with no transport, so a pile one step away is a
banked unit for a tick already being spent walking. What this doctrine refuses is
the other half: the deposits are sixteen ticks from home in a lane the front
cannot see, and the arm's own arithmetic says a body-light front is a lost front.

## Measured

Every rule is a switch in `Channel.cs` and each was built and swept alone. With
all eight off the artifact is **decision-for-decision identical** to the rebuilt
wave-6 predecessor — 698 of 698 team decisions on a checked cell — so the
attributions in `DX.md` are differences, not estimates.

Sixteen bastion mirror cells against that rebuilt predecessor: **16–0–0, +14.8
territory a cell**, against a pairing that was **0–0–16 all draws** before.

## One artifact, every cell

Nothing in the source names an arm, a class, a map, a track or a coordinate. The
channel is recognised by its declared control policy; the cap, the erosion
multiple and the interrupt are the contract's own fields and are **absent** on a
ruleset without them, which makes every rule above provably inert there rather
than merely quiet. The economy is recognised by a declared block and its verb by
a declared purchase mode.

| The game declares | What GARRISON becomes |
| --- | --- |
| no channelling control policy | G1–G6 are inert; the wave-6 doctrine plays unchanged |
| a channel with no interrupt | G3 and G6's progress clause go quiet; G1 still binds |
| a channel with an uncapped surplus | G4 wants the whole surface, every tick |
| no declared economy | G7 and G8 are inert and the idle fallback is a plain wait |
| `automatic-greedy-declared-order` | the store buys itself; the purchase routine is skipped |
| a track that is not offered this tick | never submitted — the mask prices the ladder |
| one body alive | the claim arithmetic is one body's, and still correct |

## What it will not do

Carried forward: raise the arc against numbers, or over a killing shot, or
against a muzzle that has declined once; root while it is the margin; cycle at
partial health for no reason; flicker; poke a guard it cannot break; treat a
returned bolt as friendly; freeze in a lane its own bodies need; wait in the open
to be polite; stand where its own reinforcement is about to appear.

New in revision 7:

- **Step off a claim its own stillness is paying for.**
- **Give up objective weight to buy a conditional revert.** Measured, twice.
- **Send a body across the map for scrap while the front is live.**
- **Buy a tier that rewrites a live projectile's envelope.**
- **Price a purchase.** The mask does that; guessing earns a `Blocked`.

## Reading a match

New in revision 7: `channelling: N still against M`, `arc over the claim: turning
N at C against T`, `banking the assay at (x,y)`, `banking a tier of <track>:
closing a declared gap of N`, `I am N of the claim: weight beats the gun`,
`the gun does not pay the weight: costs C, pays P/T`.

## Files

| File | Role |
| --- | --- |
| `IronRoot.cs` | the policy: roles, both cycles, tenure gates, stations, threat response, and every rule's decision point |
| `Channel.cs` | **new** — the eight switches, the declared capture arithmetic, and this tick's claim/denial/stillness state |
| `Salvage.cs` | **new** — the declared economy, the store's verb, and the pile detour |
| `Traffic.cs` | corridor geometry, route cost, wall cost, corridor runs, arrival tiles |
| `ContractLens.cs` | everything the doctrine knows, parsed from the contract only |
| `Gunnery.cs` | fire control: turret headings, the aim envelope, straight guns, curved intercepts |
| `FortressPlan.cs` | covering-tile selection, firing lanes, enemy lane sweep |
| `Kinematics.cs` | what a step costs when facing is coupled to movement |
| `ArenaGeometry.cs` | rays, line of fire, flood fill, ordered first steps |
| `ArenaBasics.cs` | the template helpers this doctrine calls, pruned |
