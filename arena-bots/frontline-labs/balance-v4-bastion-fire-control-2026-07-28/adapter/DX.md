# Adapter v2 remediation DX

## Scope and frozen inputs

This directory is a new repaired entrant. Baseline v1 was not edited:

- v1 `Adapter.cs` SHA-256:
  `87001b5b129f5c24b15e45b0d0302d757d683b15c603403b46bf1b5a1b74600f`
- v1 `out/bot.wasm` SHA-256:
  `d2549f94b31068730d889148f35400bf017a9b71a37013aedc9583e135945b42`

No opponent source or aggregate balance result was inspected. Opponents were
used only through their preserved v1 WASM artifacts. Exactly three local,
unranked, exact-`frontline-labs-1` WASM smoke runs were executed.

## Implementation and complexity

The v1 policy constructed LINQ pipelines, sorted observations, built hash
sets, and allocated BFS queues on active ticks. V2 moves static work into
`StartLife` and uses:

- cached form/attack and action-role catalogs;
- precomputed walkable, objective, and legal support masks;
- fixed-size per-life queue, seen, blocked, and first-step arrays;
- integer generations to reuse those arrays without clearing them each tick;
- direct bounded loops over observed actors, scores, legalities, and
  constraints;
- legality-derived action codes and target values.

The explicit loops make the C# file longer (1,165 lines), but the WASM artifact
fell from 3,167,918 bytes to 3,016,683 bytes, a reduction of 151,235 bytes
(about 4.8%). More importantly, the tick path has bounded reusable storage and
no map-sized allocation.

The CLI exposes peak completed-tick fuel only in fault summaries. Observed
Adapter peaks were 6.6–7.0M against a 200.0M limit, so neither revealed fault
was fuel exhaustion. Successful-actor peak fuel was not printed, so no stronger
fuel claim is available.

## Repair ledger

Total repairs: 3.

1. Runtime remediation: replaced the allocation-heavy v1 hot path and made
   Fabricate/Split depend on current `Available` state and typed constraints.
   This matches the corrected Engine legality for off-region fabrication and
   insufficient Split slots.
2. Mechanical repair: the first controlled build found one C# local-name
   shadowing error in `TryFabricate`; one local was renamed. No doctrine change.
3. Runtime remediation: smoke 1 revealed an SDK `dueTick` exception after
   Adapter requested a one-tick Transform. The optional Transform branch and
   its dead support-selection code were removed.

The final controlled build succeeded with:

- source SHA-256:
  `8b40e8771629e96ed99636a08a6810c7f7629452a8dc2cf13f1c3d289f4b0ea9`;
- artifact SHA-256:
  `d4dcc5edcd711e87bdd3153f3f75f9132c3d0b1f74d01b2897045ff782299161`;
- SDK 0.10.0;
- NativeAOT-LLVM 10.0.0-rc.1.26306.1;
- generic actor protocol 1.0.

## Bounded smoke outcomes

| Run | Opponent | Seed | Adapter artifact | Outcome | Runtime observation |
| --- | --- | ---: | --- | --- | --- |
| 1 | pressure v1 | 104729 | intermediate `316de871…` | pressure win, fault eligibility at tick 194 | Adapter self-requested Transform at tick 193; the following observation rejected `startedTick == dueTick`. Adapter peaks: 6.9–7.0M/200M. Replay hash `11bc3e5b…`. |
| 2 | fabricator v1 | 130363 | final `d4dcc5ed…` | Adapter win, fault eligibility at tick 334 | Adapter completed without a reported runtime fault. Fabricator faulted on SDK life-spawn lineage decoding; reported opponent peaks 4.9–13.3M/200M. Replay hash `04b174ff…`. |
| 3 | bastion v1 | 155921 | final `d4dcc5ed…` | draw, fault eligibility at tick 144 | Bastion transformed team 1 unit 1 at tick 143. The event carried `startedTick=143,dueTick=143`; both bastion and the observing Adapter faulted during SDK decoding. Adapter peak: 6.6M/200M. Replay hash `cc1b1ed9…`. |

Smoke artifacts are under `smoke/pressure-s104729`,
`smoke/fabricator-s130363`, and `smoke/bastion-s155921`.

## Remaining bot-external blocker

Final Adapter no longer creates the invalid same-life transition event and did
not fault in the fabricator smoke. It can still fault before `Tick` when another
participant's visible Transform event contains equal started/due ticks. The
smoke replay confirms the event belongs to team 1. Because observation decoding
constructs SDK records before bot code receives the context, no action check,
exception guard, or event filter inside `Adapter.cs` can intercept it.

Fixing that invariant requires an Engine/SDK/runtime change, which was
explicitly outside this remediation directory. Consequently the final artifact
is repaired against self-induced faults and corrected Fabricate/Split
availability, but cannot truthfully be claimed fault-free against a
transforming opponent on the current runtime.

## Framework SDK repair validation — 0.10.1

The preceding blocker section is the preserved SDK 0.10.0 historical record.
SDK/Guest 0.10.1 subsequently repaired the same-tick FormTransition invariant,
and CLI 0.9.2 exposed that updated controlled toolchain. This is a framework
SDK repair, not a fourth Adapter repair:

- `Adapter.cs` was byte-for-byte unchanged at
  `8b40e8771629e96ed99636a08a6810c7f7629452a8dc2cf13f1c3d289f4b0ea9`;
- the bot repair ledger remains three;
- only Adapter's SDK metadata and player-facing records changed;
- the controlled build reported `Cache: miss (compiled)` with key
  `3914ccda0efab2ae8f76a9d0f2d458a330adb59d6673aec28b2c6f6052427945`;
- the SDK 0.10.1 artifact is
  `d01f26f41d870bd842c12372748e3e63770a77a0c2f6d2443637ca2e43283557`.

One new exact WASM smoke was run against the preserved v1 Bastion artifact:

| Opponent | Seed | Toolchain | Outcome | Runtime observation |
| --- | ---: | --- | --- | --- |
| bastion v1 | 155921 | CLI 0.9.2 / Adapter SDK 0.10.1 | Adapter win, fault eligibility at tick 144 | Bastion again transformed team 1 unit 1 at tick 143 with `startedTick=143,dueTick=143`. Adapter accepted the visible event and did not fault. Preserved SDK 0.10.0 Bastion faulted two actors (`invalid terminal reply`; `exited before its life ended`) at 6.9–7.0M/200M peak completed-tick fuel. Replay hash `33b76097…`. |

The new smoke is preserved under
`smoke/bastion-s155921-sdk-0.10.1`; the earlier
`smoke/bastion-s155921` failure remains untouched. This validates the
framework fix for Adapter without changing its doctrine or source.

## Framework SDK repair promotion — 0.10.2

The first cross-doctrine SDK 0.10.1 smoke exposed a second player-external
decoder mismatch. An opponent Split created visible replicas while sensor
policy correctly redacted both the enemy parent identity and its private
operation handle. SDK 0.10.1 rejected that canonical `LifeSpawned` shape before
the observing Fabricator's `Tick` method ran.

SDK/Guest 0.10.2 accepts the fully redacted transition-spawn lineage while
retaining the public transition ID and still rejects a disclosed parent
without its operation handle. This is the second framework SDK repair, not a
fourth Adapter repair:

- `Adapter.cs` remains byte-for-byte unchanged at
  `8b40e8771629e96ed99636a08a6810c7f7629452a8dc2cf13f1c3d289f4b0ea9`;
- the bot repair ledger remains three;
- the controlled build reported a cache miss with key
  `6a534e0a79ae51682e9df8b1fbd6907f8c3a9f2c2cf8016722dd17c4198a6120`;
- the SDK 0.10.2 artifact is
  `dd5ef784414250847fb750b9f3cc41d018f7cd5898e8aa233b54235ca68f21ca`.

The compact SDK 0.10.2 cohort, rather than another one-opponent smoke, is the
final runtime validation for this promotion.
