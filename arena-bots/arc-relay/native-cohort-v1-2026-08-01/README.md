# Arc Relay native cohort v1

This is the frozen four-cell Gate 3 cohort. Four isolated authors received the
same public Arc Relay packet, the same one-pass budget, one seed-`314159`
mechanical self-play allowance, zero access to other cells, and no cohort
outcomes before freeze. Each cell is a coverage probe, not a claim that its
doctrine is optimal.

| Cell | Declared emphasis | Archive |
| --- | --- | --- |
| split-control | independent Well theaters and distributed recovery | `split-control/` |
| convoy | a protected Relay handoff chain with peripheral pickets | `convoy/` |
| interception | carrier cutlines, displacement, and loose-Core recovery | `interception/` |
| information/route-control | visible-hazard-aware routing and conditional lane control | `information-route-control/` |

Every archive contains its doctrine brief, source, provisional evaluation
sheet, frozen WASM, mechanical-smoke record, and honest DX/repair history.
Those sheets use `arc-relay-evaluation-sheet-v0`: an audit-only schema for
coverage and reproducibility. It is not the player-facing sheet format. The
human draw/edit UX and unlock-gated parts get a separate design pass after
Gate 3. An in-process preview playground is the intended future iteration
path; this audit deliberately does not optimize variant artifact build speed.

`cohort.json` is the coordinator-owned input. Its seed and review-order seed
are frozen before the native round-robin, and its four source bundles include
all active source, project, and toolchain configuration inputs.
