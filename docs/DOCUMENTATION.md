# Documentation map

This index separates shipped truth, active work, and historical evidence. A
new experiment does not make an old ruleset, replay, or evaluation record
wrong; it changes which document is the current entry point.

## Start here

- [`PLAN-SUMMARY.md`](PLAN-SUMMARY.md) — current product and implementation
  status.
- [`DECISIONS.md`](DECISIONS.md) — append-only record of choices that are
  actually settled.
- [`PLAYER-GUIDE.md`](PLAYER-GUIDE.md) — shipped rules 0.5 player contract.
  Frontline has not replaced it.
- [`GAME-DESIGN.md`](GAME-DESIGN.md) — current design direction and retained
  backlog/rationale.

## Active Frontline and ML work

- [`GAME-MODE-ARCHITECTURE.md`](GAME-MODE-ARCHITECTURE.md) — active
  compatibility-first implementation plan for typed game modes, match formats,
  resolved contracts, Split/replication, Deathmatch/FFA proof cases, immutable
  playlists, and opaque ladders. It preserves official replay v1 and the
  opened Frontline-alpha replay v2 as separate contract generations.
- [`COMPETITION-PERSISTENCE-PLAN.md`](COMPETITION-PERSISTENCE-PLAN.md) —
  additive playlist/ladder/series migration, pinned Duel compatibility, and
  reveal-time settlement. It records the existing rating-publication secrecy
  gap and the tests required to close it before generic ranked admission.
- [`FRONTLINE-IMPLEMENTATION-PLAN.md`](FRONTLINE-IMPLEMENTATION-PLAN.md) —
  package order and current code boundary. Packages 0–7 plus Package 8's
  local runner/evaluator slice are implemented: canonical per-life
  runtime/observation, replay v2, replication, fabrication, Anchor, actor
  SDK/Guest, protocol/configuration 1.0, canonical WASM life instances,
  viewer/mobile mirrors, reference doctrines, and descriptive dynamics.
  Hosted product admission and the independent product verdict remain.
- [`FRONTLINE-REWRITE-PLAN.md`](FRONTLINE-REWRITE-PLAN.md) — exploratory
  gameplay and architecture envelope, including the implemented
  runtime/replication/Anchor checkpoint. Numeric values remain experiment
  arms, not balance verdicts.
- [`EXPERIMENTAL-FRONTLINE.md`](EXPERIMENTAL-FRONTLINE.md) — concise
  player/bot contract and local CLI instructions for the frozen experiment.
  It is not the shipped player guide or a ranked/server availability claim.
- [`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md) — shared proposal for
  canonical actor observations, generic replay 3, datasets, and bounded model
  assets. Replay 2 remains the frozen Frontline-alpha proof; generic actor
  SDK/Guest delivery, neutral gameplay host, and replay 3 are implemented,
  while dataset, corpus, model-asset, inference, and public-product packages
  remain planned.
- [`EVALUATION-METHODOLOGY.md`](EVALUATION-METHODOLOGY.md) — required evidence
  policy before any rules experiment is promoted.

Frontline remains unshipped despite having an experimental contract and
viewer.
The actor SDK/Guest, protocol/configuration 1.0, canonical per-life WASM
runner, local experimental CLI, and descriptive evaluator are implemented.
The additional generic actor-match profile and SDK/Guest boundary are also
implemented. Its neutral Engine host now runs typed Deathmatch and Frontline
definitions and emits replay 3, but no generic mode is yet selectable through
public App/server admission.
Historical `play`, App/server admission, independently authored product
evaluation, and ranked ladders still expose only the supported historical
product paths. Protocol/configuration 0.1 remains the exact shipped duel
contract.

## Current technical and product references

- [`REPLAY-FORMAT.md`](REPLAY-FORMAT.md) — replay version 1. Preserve its
  semantics and hashes. Its status note points to the separate locally emitted
  observation-complete replay-v2 experiment without mutating the v1 contract.
- [`WASM-DEVELOPMENT.md`](WASM-DEVELOPMENT.md) — current controlled
  NativeAOT/WASM build and runtime workflow.
- [`RUNTIME-PROTOCOL.md`](RUNTIME-PROTOCOL.md) — simultaneously supported
  duel protocol/configuration 0.1 and internal actor
  protocol/configuration 1.0, including framing, limits, negotiation, and
  per-life sandbox ownership.
- [`ARENA-VISUALS.md`](ARENA-VISUALS.md), [`AUDIO-DESIGN.md`](AUDIO-DESIGN.md),
  and [`COSMETICS-ENTITLEMENTS.md`](COSMETICS-ENTITLEMENTS.md) — current
  presentation/content contracts and implementation status.
- [`USER-NOTIFICATIONS.md`](USER-NOTIFICATIONS.md) — shipped entitlement
  notification behavior.

## Active follow-on plans

- [`BACKEND-MAINTAINABILITY-PLAN.md`](BACKEND-MAINTAINABILITY-PLAN.md)
- [`DEPLOYMENT-SCALING-PLAN.md`](DEPLOYMENT-SCALING-PLAN.md)
- [`POSTGRESQL-OPERATIONS-PLAN.md`](POSTGRESQL-OPERATIONS-PLAN.md)
- [`NOTIFICATIONS-PLAN.md`](NOTIFICATIONS-PLAN.md)
- [`VIEWER-PLAN.md`](VIEWER-PLAN.md)

Each file states what is implemented versus deferred. Do not read a planned
phase as shipped behavior.

## Historical design and evidence

- [`RULES-0.3-DESIGN.md`](RULES-0.3-DESIGN.md) and
  [`RULES-0.5-DESIGN.md`](RULES-0.5-DESIGN.md) preserve the candidate reasoning
  behind shipped historical rules.
- Every `DX-FINDINGS-*.md` file is a dated experiment or authoring record.
  Its observations stay as written even when a later ruleset supersedes the
  tested candidate.

When status changes, update this index and `PLAN-SUMMARY.md`, append the
settled choice to `DECISIONS.md`, and update the exact player/format contract
that shipped. Do not rewrite historical evidence to match the new present.
