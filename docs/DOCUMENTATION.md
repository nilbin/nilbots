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
  opened Frontline-alpha replay v2 as separate contract generations, and
  records the narrow off-by-default hosted replay-v3 Labs checkpoint.
- [`COMPETITION-PERSISTENCE-PLAN.md`](COMPETITION-PERSISTENCE-PLAN.md) —
  additive playlist/ladder/series migration, pinned Duel compatibility, and
  reveal-time settlement. Match-level normalized result/score persistence is
  implemented for setless Labs; normalized generic series and ranked
  settlement remain planned. It records the existing rating-publication
  secrecy gap and the tests required to close it before generic ranked
  admission.
- [`FRONTLINE-IMPLEMENTATION-PLAN.md`](FRONTLINE-IMPLEMENTATION-PLAN.md) —
  package order and current code boundary. Packages 0–7 plus Package 8's
  local runner/evaluator slice are implemented: canonical per-life
  runtime/observation, replay v2, replication, fabrication, Anchor, actor
  SDK/Guest, protocol/configuration 1.0, canonical WASM life instances,
  viewer/mobile mirrors, reference doctrines, and descriptive dynamics.
  A separate minimal hosted generic Labs admission/replay-v3 path is also
  implemented; broad hosted formats, ranked competition, and the independent
  product verdict remain.
- [`FRONTLINE-REWRITE-PLAN.md`](FRONTLINE-REWRITE-PLAN.md) — exploratory
  gameplay and architecture envelope, including the implemented
  runtime/replication/Anchor checkpoint. Numeric values remain experiment
  arms, not balance verdicts.
- [`EXPERIMENTAL-FRONTLINE.md`](EXPERIMENTAL-FRONTLINE.md) — concise
  player/bot contract and local CLI instructions for the frozen alpha, plus
  the exact boundary of its distinct hosted generic Labs successor. Neither
  is the shipped player guide or a ranked availability claim.
- [`FRONTLINE-LABS-RULES.md`](FRONTLINE-LABS-RULES.md) — standalone,
  player-facing contract for immutable hosted Labs playlist v1 and its exact
  local generic runner. It does not require authors to infer which frozen
  Frontline-alpha mechanics happen to remain similar.
- [`FRONTLINE-LABS-BOT-AUTHOR-PACKET.md`](FRONTLINE-LABS-BOT-AUTHOR-PACKET.md)
  — frozen common information and DX-reporting budget for the first
  independently authored generic calibration cohort.
- [`FRONTLINE-LABS-COHORT-BASELINE.md`](FRONTLINE-LABS-COHORT-BASELINE.md) —
  pre-registered seeds, mirrored matrix, outcome/dynamics gates, replay review,
  artifact retention, and one-mechanism tuning rule for the exploratory Labs
  v1 balance pass.
- [`BOT-CAPABILITY-AND-SOLVABILITY.md`](BOT-CAPABILITY-AND-SOLVABILITY.md) —
  eight cumulative individual tiers, a separate six-grade coordination axis,
  practical 1v1/2v2/3v3 solvability targets, and the static gates that precede
  balance matches.
- [`BOT-QUALIFICATION-SUITE.md`](BOT-QUALIFICATION-SUITE.md) — replay-native
  T/C probe contract and scenario families. Its mirrored T4 entry component
  is implemented; cumulative T1–T8/C0–C5 qualification remains in progress.
- [`FRONTLINE-DUEL-THEORY.md`](FRONTLINE-DUEL-THEORY.md) — executable
  projectile chronology, local payoff matrices, and map-wide one-bend
  last-mile classification for the current duel-depth experiment.
- [`NILBOTS-BALANCE-LAB.md`](NILBOTS-BALANCE-LAB.md) — mode-independent
  candidate identity, layered rejection pipeline, tiered/full-cross-play
  population model, vector metrics, holdout/adversarial guardrails, and the
  implemented factorial orchestration slice.
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
definitions and emits replay 3. Frontline Labs exposes only one feature-gated,
setless, unranked H2H App/server path between two eligible submitted bots and
reuses the direct match viewer. The flag defaults off, and the values remain
experimental/unvalidated.

Historical `play`, broad generic matchmaking, FFA/2v2, series, seasons,
ratings, independently authored product evaluation, and ranked ladders still
expose only the supported historical product paths or remain planned.
Protocol/configuration 0.1 remains the exact shipped Duel contract.

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
