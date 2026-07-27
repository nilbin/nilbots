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

- [`FRONTLINE-IMPLEMENTATION-PLAN.md`](FRONTLINE-IMPLEMENTATION-PLAN.md) —
  package order and current code boundary. Packages 0–6 are implemented on
  the internal experimental path: canonical per-life runtime/observation,
  replay v2, replication, fabrication, Anchor, and viewer/mobile mirrors.
  SDK/protocol vNext and product admission remain later packages.
- [`FRONTLINE-REWRITE-PLAN.md`](FRONTLINE-REWRITE-PLAN.md) — exploratory
  gameplay and architecture envelope, including the implemented
  runtime/replication/Anchor checkpoint. Numeric values remain experiment
  arms, not balance verdicts.
- [`EXPERIMENTAL-FRONTLINE.md`](EXPERIMENTAL-FRONTLINE.md) — concise
  player/bot contract for the frozen internal experiment. It is not the
  shipped player guide or an SDK/protocol availability claim.
- [`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md) — shared proposal for
  canonical actor observations, replay v2, datasets, and bounded model assets.
  Its internal Frontline observation/replay seam is implemented; dataset,
  corpus, model-asset, inference, and public-product packages remain planned.
- [`EVALUATION-METHODOLOGY.md`](EVALUATION-METHODOLOGY.md) — required evidence
  policy before any rules experiment is promoted.

Frontline remains unshipped despite having an internal contract and viewer.
Protocol 0.1, the current SDK/guest, CLI/App match selection, server admission,
canonical WASM execution, and ranked ladders still expose only the supported
historical product paths.

## Current technical and product references

- [`REPLAY-FORMAT.md`](REPLAY-FORMAT.md) — replay version 1. Preserve its
  semantics and hashes. Its status note points to the separate internal
  observation-complete replay-v2 experiment without mutating the v1 contract.
- [`WASM-DEVELOPMENT.md`](WASM-DEVELOPMENT.md) — current controlled
  NativeAOT/WASM build and runtime workflow.
- [`ARENA-VISUALS.md`](ARENA-VISUALS.md), [`AUDIO-DESIGN.md`](AUDIO-DESIGN.md),
  and [`COSMETICS-ENTITLEMENTS.md`](COSMETICS-ENTITLEMENTS.md) — current
  presentation/content contracts and implementation status.
- [`USER-NOTIFICATIONS.md`](USER-NOTIFICATIONS.md) — shipped entitlement
  notification behavior.

## Active follow-on plans

- [`BACKEND-MAINTAINABILITY-PLAN.md`](BACKEND-MAINTAINABILITY-PLAN.md)
- [`DEPLOYMENT-SCALING-PLAN.md`](DEPLOYMENT-SCALING-PLAN.md)
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
