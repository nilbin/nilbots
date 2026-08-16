# Tapline-8 bank-triggered Well recharge report

Date: 2026-08-16  
Implementation checkpoint: `464ec9ea`

## Outcome

`tapline-8` / `arc-relay-tapline-08` is the immutable local, editor, CLI and
exhibit `Current` Arc Relay profile. It preserves Tapline-7's accepted
`arc-relay-tapline-02` map, 1/2/4 scoring, seed-jittered initial openings,
combat/signature stack and 600-tick horizon.

After a Core is banked into either reactor, its source Well starts one public
46-tick recharge and births source ordinal N+1 exactly at `bank tick + 46`.
Pickup, carrying, handoff, Arc Toss, landing, drop and enemy reacquisition do
not start or advance a separate supply clock. While that source's Core is
active anywhere in the world, the Well has no next-birth countdown. Tapline-8
has only the three initial scheduled births; it cannot enter the historical
pending-beat path and has no later global 65-tick births.

## One public clock

- The canonical rules contract adds the optional positive
  `bankTriggeredWellRechargeTicks`; it is absent from all historical canonical
  rules bytes.
- `nextScheduledBirthTick` is the effective public due tick: initial opening
  before source ordinal 0, the exact recharge due tick after a bank, and null
  while the active Core exists.
- A bank emits `CoreBanked` and `WellChanged(pendingCharge: false,
  rearmCompletesAtTick: due)`; the due birth emits `CoreBorn` and
  `WellChanged` with the new outstanding Core and no running countdown.
- Replay validation rejects pending charge in Tapline-8, a countdown alongside
  an active Core, or a recharge whose two public due fields disagree.
- `well-ticks-until-birth` reads the same effective field. Programs and their
  generated Instructions therefore see the opening/recharge countdown and a
  non-finite value while the source Core remains active.
- Canvas2D and WebGL draw one opening/recharge ring from the same field. The
  event feed calls a banked Well “recharging” rather than “ready”.

## Immutable identities

| Contract | Tapline-7 (unchanged) | Tapline-8 |
| --- | --- | --- |
| ruleset | `arc-relay-tapline-07` | `arc-relay-tapline-08` |
| rules fingerprint | `7a237424735e352291816077591eb6e4c93ba51dfea46979accd84c16e9a5b2a` | `33cb4b5ed13603f9568808f0619c9cd18cb2e0a6fc8b58a6c1b6ecc1fe958f83` |
| map | `arc-relay-tapline-02` | `arc-relay-tapline-02` |
| map fingerprint | `e536d04deff7e53296d10f2b93c38741a6a3e465a03e779a84336ba0ae1df519` | same |
| scheduled source births | `50 / 65 / 570` before seed jitter | one initial opening only |
| bank recharge | absent | `46` ticks |

The focused historical test pins the Tapline-7 fingerprints, schedule and
absence of the new canonical property. Initial Tapline-8 births are compared
against Tapline-7 for the same seed and are identical.

Hosted entrant playlist v24 remains immutable Tapline-6. No hosted playlist,
rating book, frozen stock directory, release or deployment changed here.

## SDK and controlled evidence

The additive strict rules reader moves SDK `0.10.13` → `0.10.14`; CLI/package
moves `0.9.63` → `0.9.64`. The controlled compatibility artifacts are:

- tactical build input:
  `c41d6bb60b353a05a8dc7f45dbd449b949adf31d3e3822620cb7e04826b99311`;
- tactical WASM:
  `e837ab52ae5aadb86832cccef86de4113e8f83739e3e23d476a6115d7b4c180e`
  (`5,884,130` bytes);
- shared built-in guest:
  `42bbd7d9e4fae781f3938b314fe7fa6366d5435e10fc68ed997994698d86601f`.

A real Tapline-8 seed-9001 Demolition-vs-Lockbox cell completes at tick 266
in both hosts. The outer replay hashes differ by runtime provenance, while the
gameplay-state digest is identical:
`a8c4251062c9b07fef7cdbad55c622052041f7cc2918fde804508095a349b037`.

`publish-cli` owes tag `cli-v0.9.64` before any release.

## Validation

- solution build: passed, zero warnings/errors;
- Engine: 1,539 passed, including exact opening, either-reactor bank, every
  intermediate countdown tick, exact due birth, duplicate suppression,
  prolonged carry, toss/land/drop/steal non-triggers and Tapline-7 bytes;
- SDK: 87 passed; tactical mind: 32 passed; Guest: 36 passed;
- Runtime.Wasm: 69 passed; Determinism: 17 passed;
- release-only current tactical WASM parity: passed on Tapline-8;
- web: 587 passed; production and hosted Vite builds passed;
- generated API clients: zero diff;
- phone browser smoke: Chrome at 390×844 loaded the gallery and 3D replay,
  advanced playback, and logged no errors.

One unrelated CLI scene remains red: 254 tests pass and
`AlliedTrafficDebtOpensTheLongerDetourInsteadOfWaitingForever` fails. The same
test was reproduced unchanged on the clean starting commit `4faa486a`; its
Tapline-6 scene is not affected or weakened in this change.

## Review exhibit

[Open the six-match phone gallery](https://printable-implementing-statutes-developer.trycloudflare.com/)

The exhibit is Rising Standard v1 against Tempered Standard v2 on Tapline-8,
using the standard side-seed cells. Every cell passed movement sanity and felt
eligibility and ended by reactor destruction:

| Cell | Rising Standard result | End tick |
| --- | --- | ---: |
| `w-9001` | loss | 369 |
| `w-9002` | win | 349 |
| `e-9005` | win | 365 |
| `e-9009` | loss | 358 |
| `w-9012` | loss | 369 |
| `e-9004` | loss | 359 |

These are exhibit records, not a balance conclusion. In `w-9001`, the public
timeline provides a direct visual check: banks at ticks 75 and 86 lead to the
same source Wells birthing at ticks 121 and 132, exactly 46 ticks later.
