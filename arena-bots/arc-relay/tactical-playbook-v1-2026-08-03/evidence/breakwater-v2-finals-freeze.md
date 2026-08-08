# Breakwater v2 finals — pre-outcome freeze

Frozen 2026-08-05, before any finals game. Follows the v1 freeze
discipline; the v1 finals verdict (7/8, DECISIONS #206) reclassified
four-down-double-relay as an open development opponent, and v2 was tuned
against it. This freeze adds a NEW unseen holdout.

## Candidate

- Playbook `breakwater-v1` (v2 config: release-only freshnessTicks 18 on
  `approach-clear`; no side overrides), sha256
  `5979497f9f1564a68eb0b7d6f0f7221e5d234613c6f1e3dd16f8964c8a294a32`
- Layout sha256
  `a5b9b272c8af6b5ed613662c8bb79a8a8870ebbdcda71062bbd7cc56326e01ef`
- Executor artifact `f97792b9…` (unchanged since the v1 finals).

## Registered pass bar

All wins by elimination; max-tick decisions fail the cell.

| Cell | Bar |
|---|---|
| vs frozen siege west / east | W 3-2+ / W 3-1+ |
| vs parity west / east | W 3-1+ / W 3-1+ |
| vs south-mirror west / east | W / W |
| vs four-down-double-relay west / east (open dev opponent) | W / W |
| vs double-kestrel (UNSEEN holdout), both orientations | W / W |
| false-positive read vs 3 non-siege | zero fortify latches |

## Holdout

`home-siege-v3-double-kestrel.json`, sha256
`5b8d67451c634c2ca8933a89f00c74c619d438bb7d10f82022f842832c5a9bc2` —
frozen siege with one repulsor swapped for a second kestrel (fast-flank
profile). Authored blind; pairing fingerprints minted via
`--print-contract` only (`79425650…` west, `4303bef7…` east); **no game
against any Breakwater has ever been played.** The candidate meets it
through its side-keyed wildcard bindings.

## Protocol

WASM only, zero faults; games seed-invariant (dev evidence: 424242 and
888001 outcome-identical). One evidence game per cell. False-positive
opponents and their hashes as in the v1 freeze. In-process results are
analysis, never evidence.
