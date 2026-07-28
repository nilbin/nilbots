# Adapter — Frontline Labs remediation v2

Adapter keeps the original doctrine: it changes priorities from current allied
and visible enemy bodies, territorial score, active objective state, remaining
time, and the actions that the resolved contract makes legal. It has no fixed
opening and does not assume team, unit, form, objective, or map counts.

This is a revealed-results remediation entrant, not blind balance evidence.
Compared with baseline v1, its hot path avoids per-tick LINQ, sorting,
temporary hash sets, and newly allocated pathfinding collections. Forms,
attacks, action roles, objective masks, support masks, and fixed-size BFS
buffers are cached once per body life. The resulting WASM is 151,235 bytes
smaller than v1.

Fabricate and Split are selected only when the current action entry is
`Available`; typed unit targets must also name a currently Ready allied slot.
Their numeric codes are always copied from that same legality entry. The
stable action IDs themselves are discovered from the contract's action and
transition catalogs.

The final v2 policy deliberately does not request same-life Transform. The
initial SDK 0.10.0 smoke history revealed that one-tick form transition events
carry `startedTick == dueTick`, which that SDK rejected before `Tick` ran.
Avoiding self-requested Transform removed the bot's self-induced instance of
the fault, but the historical SDK 0.10.0 artifact still faulted when an
opponent made such an event visible.

SDK/Guest 0.10.1 repaired that framework invariant. A later cross-doctrine
smoke then exposed another SDK decoder mismatch: an enemy transition-created
life may be visible while observation policy redacts its parent and operation
handle. SDK/Guest 0.10.2 accepts that canonical privacy-redacted shape.
Adapter's source and bot repair count did not change for either framework
update.

## Frozen hashes

- `Adapter.cs` SHA-256:
  `8b40e8771629e96ed99636a08a6810c7f7629452a8dc2cf13f1c3d289f4b0ea9`
- Historical SDK 0.10.0 `out/bot.wasm` SHA-256:
  `d4dcc5edcd711e87bdd3153f3f75f9132c3d0b1f74d01b2897045ff782299161`
- Historical SDK 0.10.1 `out/bot.wasm` SHA-256:
  `d01f26f41d870bd842c12372748e3e63770a77a0c2f6d2443637ca2e43283557`
- Current SDK 0.10.2 `out/bot.wasm` SHA-256:
  `dd5ef784414250847fb750b9f3cc41d018f7cd5898e8aa233b54235ca68f21ca`

All controlled NativeAOT-LLVM WASM builds succeeded. The initial three
bounded remediation smokes plus one exact framework-repair validation smoke
are documented in `DX.md`. Their outcomes are remediation evidence only.
