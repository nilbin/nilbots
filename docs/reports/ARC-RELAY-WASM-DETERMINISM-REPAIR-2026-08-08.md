# Arc Relay WASM determinism repair

Date: 2026-08-08

Implementation checkpoint: `1107175f`

Branch: `main`

## Outcome

The current tactical-playbook artifact no longer faults at match start, hosted
Arc Relay playlist v8 pins a fresh immutable Wellwright stock artifact, and a
full real tactical MIND match now enforces byte-identical canonical replay
output between WASM and in-process hosts when participant provenance is held
equal. Playlist v7 and its broken historical bytes remain unchanged.

The repair changed no Arc Relay rules, map, sheet, composition, or gameplay
fingerprint. It changed the additive observation wire, controlled artifact
identity, hosted playlist identity, and toolchain versions required to carry
those bytes safely.

## Failure and first difference

At the starting revision, both tracked files were byte-identical:

| Artifact | SHA-256 | Observed result |
| --- | --- | --- |
| `tactical-playbook-mind-v1/bot.wasm` | `42288a911821f7893f34fb096cd8c17464f24da7a170761898f10e8b0497763b` | immediate participant fault; match ended before a tick; peak 0.0M fuel |
| frozen `ambush-warren-11-stock-v1/bot.wasm` | same | same stale bytes pinned by v7 |
| controlled rebuild of the same source | different, live artifact | completed the Hunter/Wellwright Warren match |

Command-by-command bisection found the first host difference at tick 15 for
team 0. The authoritative and in-process observation held four pending
declared strikes; an instrumented WASM guest observed zero. The object graph
already exposed `ModeObservationState.ArcRelay.PendingStrikes`, but
`GenericActorWireObservationCodec` did not encode or decode it. Collection
ordering, position equality/hashing, floating point, fuel exhaustion, and host
exception behaviour were ruled out before changing the codec.

Arc Relay mode payload field 7 now carries every pending strike's shooter,
resolve tick, origin, heading, optional target, and tile sequence. It is an
additive tagged field: old readers skip it, and profiles with no pending
strikes retain the empty/inert representation. SDK and Guest are now 0.10.13;
the negotiated profile and schema numbers remain unchanged.

## Parity proof and the provenance boundary

`ArcRelayMindRuntimeParityTests` compiles the committed Hunter and Wellwright
playbooks, resolves the real `ambush-warren-11` contract, and runs the shipped
tactical mind through both hosts for a complete match. It assigns identical
participant provenance to both executions and asserts equality of the entire
canonical replay hash. That test passes in the Runtime.Wasm suite.

An independent CLI smoke on seed 9001 produced:

| Runtime | Terminal fact | Outer replay hash |
| --- | --- | --- |
| WASM | Hunter wins, reactor destroyed at tick 348 | `31013ff9b510977eafb0325df49e36e3792f41514479a264a5833c0aa470b47c` |
| in-process | Hunter wins, reactor destroyed at tick 348 | `8e5d56989bc25803013725261ee639852b7c778f759c69b50537c18866569e14` |

Both replays have gameplay-state digest
`384a189642a5f0b658e1c23a49e6e9c97a525c043c2a97c35270d5fadf2c5875`.
The outer hashes intentionally differ because the diagnostic CLI records a
different `runtimeKind` and source/executable artifact identity. This is not a
remaining decision divergence. Cross-host gameplay comparisons must either
hold that participant provenance equal, as the regression does, or use
`scripts/arc-relay-state-digest.py`. Exhibits run in-process; ranked execution
runs WASM; claims must name their runtime.

## Artifact and playlist identity

The final controlled build ran on macOS through the platform-matched Docker
backend selected below `scripts/run-wasm-publish.sh`:

| Identity | Value |
| --- | --- |
| build input SHA-256 | `b4b9646298f6aa754f662fc2dfe6838775ff32231db850f9f03b2df72f33165d` |
| artifact SHA-256 | `7cf7e2f56fad1d80bec2250b0c309191b9b24ac2141bf36374116431c19adec3` |
| SDK / Guest | `0.10.13` / `0.10.13` |
| compiler | NativeAOT-LLVM `10.0.0-rc.1.26306.1` |
| build pipeline | `5` |

Both the current tactical source directory and frozen
`ambush-warren-11-stock-v2` carry these exact bytes and the same
`bot.build-receipt.json`. The v1 frozen artifact remains at its original hash.

Playlist v7 was not amended. The repository cannot prove that a shipped
playlist admitted or rated no production entrants, so the immutable-history
rule wins. Playlist v8 pins stock-v2 and closes v7 for new pairing. Ratings and
ranked-match counts copy v7→v8 because this is an execution repair on the same
rules/map line. PostgreSQL integration tests prove that continuity, while also
preserving #248's fresh-era semantics when a database crosses directly from
v6 without ever having seeded v7.

## Staleness guards

`nilbots build` now emits `out/bot.build-receipt.json`. The tracked-artifact
test recomputes the current project build-input key, hashes the committed WASM,
checks all pinned toolchain versions, and checks that frozen stock-v2 carries
the identical receipt and bytes.

That guard caught a second real defect during this pass: staged SDK/Guest DLLs
embedded the current Git commit in `AssemblyInformationalVersion`. Committing
an artifact therefore changed the next cache key even when no executable
source changed. `src/ToolchainAssembly.props` now excludes SourceRevisionId
from informational version; Debug and Release staged DLL hashes are
byte-identical, and the tracked receipt still passes after the commit that
tracks it. This controlled-input byte change minted build pipeline 5.

The shared guest stamp also omitted the imported
`src/ToolchainAssembly.props`, allowing `scripts/build-wasm-guest.sh` to claim
"up to date" after the file changed. Stamp format v6 includes it. The rebuilt
shared guest is
`d59ddbb6bb86fc4fc95fbd8f354c4c09eb815c8a8c1a21c38615c59b3352980c`.

## Size and optional compiler flags

| Artifact | Before | After | Change |
| --- | ---: | ---: | ---: |
| tactical/Wellwright WASM | 7,260,242 B | 7,934,239 B | +673,997 B (+9.28%) |
| shared built-in guest | 8,158,762 B | 8,207,630 B | +48,868 B (+0.60%) |
| tracked receipt | — | 341 B | +341 B |

The optional `StackTraceSupport=false` and `OptimizationPreference=Size`
flags were deliberately skipped. The first changes bot-visible exception text;
the second has measured fuel/runtime cost. Mixing those optional semantics and
a second whole-cache invalidation into a core artifact/parity repair would
weaken attribution. They remain a separate measured compiler experiment.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build BotArena.sln` | pass, 0 warnings / 0 errors |
| CLI suite | 295 passed |
| Engine suite | 1,435 passed |
| App suite with disposable PostgreSQL 16 | 282 passed, 3 unrelated opt-in integration tests skipped |
| Determinism suite | 17 passed |
| Runtime.Wasm suite | 68 passed, including full Arc Relay MIND hash parity |
| `bash scripts/generate-api-clients.sh` | pass; generated tree clean |
| `cd web && npm test` | 426 passed |
| `cd web && npm run build` | pass |
| final tracked WASM full match | completed at tick 348; no runtime fault |
| `git diff --check` | pass |

The three App skips are the separately gated networkless compiler-runner,
generic-actor controlled-builder, and S3 object-store integrations; all
PostgreSQL-gated tests ran against the disposable server. No release or deploy
workflow ran.

CLI/package version is 0.9.36. Before any deploy carrying this line,
`publish-cli` owes all three currently untagged release tags:
`cli-v0.9.34`, `cli-v0.9.35`, and `cli-v0.9.36`.
