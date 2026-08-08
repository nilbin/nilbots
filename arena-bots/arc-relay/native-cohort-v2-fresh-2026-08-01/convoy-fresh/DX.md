# ConvoyFresh author evaluation

Completed before any match outcome was opened. No match was run during this
fresh-author pass.

## Public authoring experience

The participant-scoped API makes convoy coordination direct: the carrier,
catchers, and screens can be assigned once over the complete body set, and a
receiver can be explicitly held before the source submits `handoff-core`.
Stable unit IDs make the ladder survive respawns without treating a life ID as
a role.

The most useful public surfaces were the per-body typed legality mask, the
mode's visible Core carrier identity and relocation clock, public Well clocks,
participant-relative region bindings, map rows, body class IDs, and visible
spawn reservations. Together they are enough to avoid importing engine facts.

The largest authoring cost was action diversity. Movement and gunfire use
projectile-heading constraints, rotation and Prism Wall use cardinal direction,
handoff/repair/paint use stable-unit constraints, and several signatures use
position constraints. Keeping one helper per constraint family made malformed
commands avoidable.

## Doctrine commitments frozen before build

- Eight distinct classes: Relay, Repulsor, Palisade, Patchbay, Hush, Towline,
  Sunder, Lantern.
- Primary route: earliest-producing Well, north-biased equal-length path.
- One-way catch ranks: Relay 0, Repulsor 1, Palisade 2, Patchbay 3.
- Three convoy screens and one off-route Well picket.
- No same-or-lower-rank handoff, no receiver farther from home, and at most one
  submitted handoff per Core/source/receiver tuple.
- No strategy pass after a match; no adversary or outcome data was available.

## Repairs

Two doctrine-neutral repairs followed source freeze:

1. C# preserved the `UnitTarget?` nullable wrapper after the explicit null
   guard; the two argument constructors now receive `.Value`.
2. Filtered fault evidence exposed `UnitTarget`'s value-type default `(0,0)`
   escaping `FirstOrDefault(predicate)` when no constrained target matched.
   Target lookups now cast only actual matches to nullable before selecting.

The pre-repair source hashes and exact explanations are archived under
`repairs/`. No role, route, composition, target ordering, action priority, or
handoff condition changed.
