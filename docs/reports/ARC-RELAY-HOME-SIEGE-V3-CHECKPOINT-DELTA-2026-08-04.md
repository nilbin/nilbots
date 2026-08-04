# Arc Relay Home Siege v3 — checkpoint delta

Date: 2026-08-04

Branch: `codex/arc-strategy-ladder`

Accepted strategy report: `docs/reports/ARC-RELAY-HOME-SIEGE-V3-9-2-CORRECTION.md`

## Post-report delta

Two presentation/art commits landed after the v3 correction report:

| Commit | Change | Gameplay boundary |
| --- | --- | --- |
| `386ff4e7` | Inventoried signature props and gave loose/in-flight Cores a shared neutral white-lilac palette in Canvas2D and WebGL. | Presentation only. “Neutralize loose Core” means **remove team-looking color from an unpossessed or airborne Core**. It does not change Core ownership, pickup, drop, flight, Well, scoring, visibility, timing, or simulation state. A carried Core still uses its authoritative carrier's team color. |
| `14b4e4da` | Added compact KTX2 GLBs for Minesmith's active Trip Node and Nest's active Sentinel Seed, with lazy manifest loading and procedural fallbacks. | Presentation only. The renderer instantiates props from authoritative signature state. Sentry visual yaw follows the latest shot already at the playhead; it does not acquire a target or affect a shot. |

The gameplay-adjacent movement is therefore limited to **how existing replay
facts are communicated**: neutral possession semantics for Cores, and physical
models replacing procedural placeholders for two already-existing signatures.
No engine, rules, map, SDK, wire contract, match driver, stock/tactical WASM,
sheet, eligibility bar, canonical serializer, or fingerprint implementation was
changed by either commit.

No frozen Home Siege artifact or evidence was edited. `14b4e4da` adds separate
art-review screenshots and provider provenance under `art/`; those are new
presentation evidence, not mutations of a canonical replay, match record,
cohort, scorecard, freeze, or strategy result. The accepted v3 playbook,
layout, ATP, tactical WASM, parity-control WASM/sheet, and v4 bars retain the
hashes printed in the correction report.

## Pre-recognizer variant freeze

Before any Stage 2 recognizer authoring or outcome observation, this checkpoint
adds two unplayed, evaluation-grade generalization probes:

- `home-siege-v3-south-mirror`: a true south-lane mirror, including the south
  Well conversion condition and assignment-correct south breach/return routes;
- `home-siege-v3-four-down-double-relay`: four confirmed unavailable enemies
  for the approach conversion threshold, with a second Relay replacing
  Kestrel.

Their authoring sheets, exact-bound layouts, compiled ATP1 artifacts, hashes,
byte sizes, and no-outcome boundary are frozen in
`arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/evidence/home-siege-v3-recognizer-prefreeze.json`.
They have not been screened, matched, or accepted. Their later value is as
unseen tests of whether a separately authored recognizer generalizes beyond the
exact accepted north-lane v3 input.

## Branch posture

`codex/arc-strategy-ladder` diverged from `codex/game-redesign` at
`2ff34f16005da75ddff4e6289c817fa37d57d303` (`Tighten Arc Relay strategy
ladder gates`). The `codex/game-redesign` branch ref still points at that exact
commit, so its committed post-divergence file set is empty and the intersection
of files modified by both committed branch ranges is **none**.

The `game-redesign` worktree currently has untracked preflight logs/replay data,
one class-sheet screenshot, and several `sandbox-arc-relay-*` smoke/viewer
directories. They are worktree-local outputs, not commits on that branch, and
none is part of this checkpoint. No merge was performed.
