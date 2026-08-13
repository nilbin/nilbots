# Arc Relay Tapline-3 promotion report

Date: 2026-08-13

## Outcome

The owner-approved Tapline-3 movement line is packaged as immutable hosted
entrant playlist v21. Local, exhibit, CLI, sheet-editor, scrimmage, and hosted
ranked defaults now resolve the same rules profile and coordinated movement
path. Playlist v20 and `tapline-1-stock-v1` remain immutable and executable.

This is a promotion and determinism report, not a balance verdict.

## Promoted identity

| Field | v21 value |
| --- | --- |
| Loop profile | `tapline-3` |
| Rules | `arc-relay-tapline-03` |
| Map | `arc-relay-tapline-01` |
| Sheet / linked data | Orders v2 / ATL2 |
| Stock sheet | `wellwright-tapline-v2` |
| Frozen artifact | `arena-bots/arc-relay/tapline-3-stock-v1/bot.wasm` |
| Artifact SHA-256 | `8feed1cd9a43e9a6ad8266d8ef2e6760c39386a080a974684d634082bb5cf969` |
| Build-input SHA-256 | `cd4615c6712c87d9830b459bafc1835c9b9004563501969df308c056b66dc8eb` |
| Artifact bytes | 5,304,224 |
| CLI/package | 0.9.60 |

The artifact was produced through the controlled platform-matched Docker path
with `nilbots build --no-cache`. The tracked current artifact and append-only
v21 stock copy are byte-identical and carry the same receipt.

## Runtime proof

Registered cell: Tapline-3 seed 9001, Wellwright west versus Hunter east.

| Runtime | End | Gameplay-state digest |
| --- | --- | --- |
| in-process | reactor destruction, tick 466 | `9e8455726089e56081ccfe892735c03fc414ece6aad7c0b551cc50b44103af05` |
| WASM | reactor destruction, tick 466 | `9e8455726089e56081ccfe892735c03fc414ece6aad7c0b551cc50b44103af05` |

The promotion gate caught a real sandbox-only blocker: the first EPIBT WASM
candidate exhausted fuel at tick 26. Candidate sets were regenerated inside
recursive priority inheritance, and carrier scoring repeated a complete map
flood for each candidate state. Tick-stable candidate generation is now cached,
and carrier scoring uses one reverse distance field per carrier/tick. The
in-process digest before and after this optimization is exactly the value above,
so the fix changes cost without changing the accepted gameplay.

## Rating decision

v21 starts a fresh rating book at 1200. It does not copy v20 Elo because the
rules profile, planner, stock sheet source, and registered gameplay digest move.
The seeder closes exactly one open v20 ladder and creates fresh rows for opted-in
entrants. Historical v20 matches, ratings, definition fingerprint, stock bytes,
and provenance remain readable.

## Owner exhibit

The accepted six-cell phone exhibit was served at:

`https://particularly-medicare-solutions-benjamin.trycloudflare.com/`

All six exhibit cells completed between ticks 355 and 469. The exhibit is a
visual review artifact only; it is not statistical balance evidence.

## Validation

The final checkpoint records the complete command results after packaging:

- controlled WASM build and exact in-process/WASM state-digest equality;
- focused compiler, engine, movement, playlist, artifact-receipt, and hosted
  identity tests;
- full solution build and repository test harness;
- PostgreSQL-gated App integration tests;
- generated API-client drift check;
- web test suite and production build;
- `git diff --check` and a clean final worktree.

No release or deployment workflow was run. `publish-cli` must create
`cli-v0.9.60` before deployment.
