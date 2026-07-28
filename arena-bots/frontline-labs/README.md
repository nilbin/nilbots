# Frontline Labs bot cohorts

This is the permanent, neutral archive for independently authored Frontline
Labs bots. It is separate from `champions/`: cohort entrants are calibration
evidence and reusable starter opponents, not ladder title holders.

The current population/pacing verdict and four preserved causal arms are in
[`BALANCE-ITERATION-2026-07-28.md`](BALANCE-ITERATION-2026-07-28.md). Baseline
v2 remains the calibration control; no candidate in that iteration mutates or
relabels immutable `frontline-labs-1`.

Authors receive the common
[`FRONTLINE-LABS-BOT-AUTHOR-PACKET.md`](../../docs/FRONTLINE-LABS-BOT-AUTHOR-PACKET.md)
and player contract
[`FRONTLINE-LABS-RULES.md`](../../docs/FRONTLINE-LABS-RULES.md), plus only
their assigned doctrine sentence.

Each generation lives at `<cohort-id>/` and retains every entrant, including
losers and every mechanically repaired revision. Each entrant directory
contains source, project metadata, `botarena.json`, README, `DX.md`, the final
`bot.wasm`, and its local manifest. `DX.md` is frozen after source authorship
and before standings, opponent source, or replays are disclosed; it may report
mechanical repairs but must not become an extra strategy iteration.

The cohort root contains a `cohort.json` conforming to
[`cohort.schema.json`](cohort.schema.json). Start from
[`cohort.example.json`](cohort.example.json), expand it to all four doctrines,
replace every placeholder, and record the exact checked-out engine commit and
contract fingerprints, WASM SHA-256 values, and deterministic source-tree
identities. `sourceTreeSha256` is `sha256:` plus the driver hash of every
non-WASM authored file's relative path, executable bit, length, and bytes;
`.git`, compiler outputs, caches, and generated `smoke/` or `evidence/`
directories are excluded. `sourceRevision` retains the authoring-time revision
identity independently. Entrant `root`, `artifact`, and `dxReport` paths are
relative to that cohort's `cohort.json`; for example, use `pressure`,
`pressure/bot.wasm`, and `pressure/DX.md`.

The blind authoring budget remains separate from later tuning. A post-reveal,
pre-registered population arm records `balancePasses` at both cohort and
entrant level, preserves the prior cohort, and changes only the declared
entrant policies. `repairPasses` remains reserved for mechanical
compile/contract/fault corrections.

Canonical evidence belongs under
`<cohort-id>/evidence/<run-id>/`. The cohort driver copies every source,
DX report, and artifact into the run before launching matches, then retains
every command, log, replay, verification result, and W/D/L table. Never reuse
an evidence directory; `--resume` adds immutable attempt directories and
re-runs `nilbots verify` for previously accepted replay bytes. `run.json`
records both the Git commit and a deterministic relevant-source worktree
identity, including whether that checkout was dirty.

```bash
python3 scripts/labs-cohort-drive.py \
  --manifest arena-bots/frontline-labs/<cohort-id>/cohort.json \
  --output arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm

python3 scripts/labs-replay-eval.py \
  --group baseline=arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/matches \
  --json arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/dynamics.json

python3 scripts/replay-review-sample.py \
  arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/matches \
  --count 12 --seed 20260724 --blind-identities \
  --copy-selected arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/blind-review \
  --output arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/review-sample.json

(cd web && npm run build:review)
cp -R \
  arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/blind-review/replays \
  arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/blind-review/replays.json \
  web/dist-review/
(cd web && npm run review -- --no-build)
```

The default matrix is driven by the manifest. A cohort sprint uses one frozen
seed and both participant assignments: four bots, six unordered pairs, and 12
matches. This is sufficient for the current deterministic baseline, where
additional seeds produced byte-distinct replays but identical behavior for
every ordered pairing. Add pre-registered seeds when a ruleset or bot actually
consumes randomness, or when a causal arm explicitly needs them. The historical
first baseline remains a 36-match, three-seed run. The driver invokes the local
`frontline-labs` runner in WASM mode and requires `nilbots verify` to accept
each complete replay v3. `--runner-command` and `--verify-command` exist for
packaged-CLI or CI paths without changing the archived plan.

Numeric causal arms use a distinct, content-descriptive `rulesetId` and exact
candidate fingerprints in their cohort manifest. The driver keeps playlist,
map, format, and contract-profile identity fixed, but accepts that registered
experimental ruleset identity; `--runner-command` must resolve the same
candidate contract. Never reuse `frontline-labs-1` for changed numeric values.

Before opening results, combine the four frozen `DX.md` files into
`DX-SYNTHESIS.md`, grouped by severity and noting reproductions. This is a
developer-experience checkpoint only: do not edit strategies. Then lock the
outcome-blind replay sample, watch it at normal speed, and only afterward open
`results.json` and `dynamics.json`.

The review sampler copies replay bytes unchanged and emits a small hosted
package: `replays.json` plus neutral files under `replays/`. Anonymous names
are presentation aliases in the picker index only; the immutable replay
provenance remains intact. It does not copy the standalone CLI viewer. Overlay
the package onto `web/dist-review` as above to use the hosted review page, its
top replay picker, and the default 3D view.

Generated compiler caches (`bin/`, `obj/`, Docker layers, package caches) are
not evidence and stay out of this archive. Canonical sources, final WASM,
manifests, results, selected review manifest, and replays are evidence; if a
run is too large for ordinary Git, put its immutable replay directory in the
project's durable artifact store and commit an explicit content-addressed
manifest rather than silently discarding it.
