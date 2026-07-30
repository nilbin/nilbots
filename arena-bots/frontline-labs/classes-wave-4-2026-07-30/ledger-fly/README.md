# LedgerFly — the attrition banker (revision 4)

Class: **fabricator**. Lineage: `ledger-fly-v1`. Role: verdict-doctrine,
target cumulative T4. Budget: one strategic revision; mechanical repairs free.

## The doctrine in one line

A body's contribution is the objective-ticks it can keep standing for — so buy
them with shape and with blood, and keep the bank alive to sell more.

## What changed, and why

Revision 3 fixed the *unit of account*: convertible objective-ticks rather than
bodies, priced from the hold, the control policy, and the arrival placement.
That is intact, and this revision does not reopen it. What revision 3 could not
know is that **weight is not a scalar.** Two bodies on the objective are one
asset when they stand on the same firing lane and a different asset when they
stand on two bearings, and a body that steps out of a lane to dodge a bolt it
would have survived has just handed the other side the only configuration that
erodes a claim. Three contract facts price that, and every one of them is a
field:

| Contract fact | What it changes | What LedgerFly does about it |
| --- | --- | --- |
| projectile geometry + `volley.projectileCount` + `projectileGuard` | a bolt is a ray that stops on the first body, a fan is three lanes at once, an arc is a fixed quadrant that never tracks | every contested-tile choice breaks its ties on **bearing dispersion**: don't share a clear ray with an ally inside the enemy's own declared reach, don't stand in a lane something is already aimed down, and prefer a bearing onto the objective nobody is covering |
| `capture.decayClock` + `damagePerHit` + the bolt's exact arrival tick | contested ticks may PRESERVE a claim, so only an enemy standing alone erodes it | a body that survives the hit and whose removal would flip control **eats the bolt and keeps the tile**; the bank never does |
| the slot roster's own lifecycle profiles | slots that only ever fill through an explicit fabrication are pipelines the bank feeds by hand, and late ones rebuild on a slower clock | the bank's standoff grows with its **pipeline count**, it queues the slowest-rebuilding Ready slot first, and it declines the two *discretionary* reasons to join the objective while the pipeline is deep — but only where the front is armed with a bend |

## The skills, priced on both sides

`Stances.cs` defines a stance as **any same-life route into a form that keeps
its objective weight and adds a fan or a guard.** That gate is the banker's
whole opinion about fortification: a route into a zero-weight body is not a
stance, it is a body deleted from the ledger. It admits the kit's volley and
aegis stances and rejects an Anchor into a turret — from the same `transform`
action, on the same contract, with no name anywhere in the source.

- **Volley.** Enter only when the gun will be loaded by the time the fan is up
  and the spread is already earning; one entry buys one cast and the engine
  takes the return itself.
- **Aegis.** Raised across an *approach*, never on the objective — the shell's
  route forbids `transition-placement-forbidden` tiles and every objective tile
  carries that tag, so the arc is a chokepoint plug rather than a capture
  holder. It goes up before the dodge rung (or the dodge always wins the tick),
  holds while anything inside the arc can still shoot, and stays down for a full
  round trip after it drops.
- **Facing one.** A lane whose bolt *arrives* inside a visible arc is refused —
  the return is owned by the guard's team and flies the exact reverse — unless
  it is the bolt that reaches the declared deflection threshold, which shatters
  the shield instead of being handed back. Fan lanes are threat before a bolt
  exists. A deflected return needs no special case: every dodge and blocking
  test in this bot keys on `OwnerTeamId`.
- **Five slots.** Counted from `unitSlots`, unlocked from the slots' own
  lifecycle assignments, rebuilt on the clocks their own profiles declare.

## The hold is read, not derived

Revision 3 inferred the ratchet hold's owner from the sign of the front's
displacement and could not recover it across a death. `holdOwnerTeamId` and
`holdEndsAtTick` are now published together on the mode observation, so
`Ratchet.cs` asks. The derivation survives as a **fallback only**, for a
contract that declares a hold duration and publishes no live clock, and
`Ratchet.OwnerRead` reports which channel answered.

Class is read the same way: `Topology.Teams[].ClassId` and the per-body
`classId`, never a form-ID prefix.

## Measured, honestly

Candidate versus its own **rebuilt revision-3 source**, all four registered
phase-2 cells, `facing-locked`, `fabricator-vs-fabricator`, both sides, twelve
seeds per side (controlled WASM runtime confirms six).

| cell | spelled | candidate record |
| --- | --- | --- |
| `keel` | `--pendulum keel` | 12W 12L 0D — side-determined |
| `helm` | `+ --skills kit` | 13W 11L 0D — near neutral |
| `veer` | `+ --bend universal` | **24W 0L 0D** |
| `rig` | `+ kit + bend` | **24W 0L 0D** |

`DX.md` carries the per-cell detail, the ablations that attribute the wins, the
two readings that measured to nothing, and the reasons to distrust the seed
counts.

## What it still never does

It never Splits, never Anchors, never enters an irreversible stance, and never
hard-codes a slot count, an unlock tick, a hold length, a fan width, a
deflection threshold, or a bend depth. `Standoff` (3 tiles, adjusted by declared
reasons) remains the only tuned constant.
