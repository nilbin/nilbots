# Arc Relay stock recovery pass — 2026-08-03

## Outcome

The three quarantined operation examples are repaired without changing Arc
Relay rules, map geometry, balance constants, engine execution, canonical
replay contracts, or renderer code. The replacement stock artifact passes all
ten registered operations in two deterministic WASM campaigns: **20/20
operation cells passed, and both teams passed the v4 felt-degeneracy bars in
every complete match**.

This is evidence that the registered operations execute and recover cleanly.
It is not a claim that the game is balanced or fun.

## Root causes and fixes

| Quarantined proof | What actually happened | General fix |
| --- | --- | --- |
| Emergency Exchange | After eight stuck ticks, the stock carrier voluntarily used `drop-core` before its deterministic no-progress route recovery. Automatic pickup gave the same Core back to the same body on the next tick, so it could drop again indefinitely. The v4 bar found 56- and 71-cycle episodes on one Core and an additional 18-cycle episode. | Removed voluntary drop as an automatic stuck-carrier recovery. The stock mind still tries its authored Relay toss; after 12 no-progress ticks it commits to a deterministic shortest legal return step. `drop-core` remains a legal game action for intentional authored or custom-mind use. |
| Rear Hook | The Blue operation itself completed, but the named Balanced opponent ran the obsolete `stock-mind-forward-v1` artifact. Its carrier oscillated between two home-side tiles for roughly 230 ticks, so the complete match was ineligible. | Retained the distinct Balanced sheet, but both sides now run the same current stock artifact. Historical stock artifacts remain compatibility evidence, not eligible tactical opponents. |
| Relay Catch | The Blue operation itself completed, but the obsolete opponent artifact left several own carriers waiting on one tile for 34–121 ticks. | Retained the distinct Hook Burst sheet and moved its execution to the current stock artifact. The complete match now passes stationary-carrier, home-progress, passivity, handoff, and pickup/drop bars on both teams. |

The operation state machine already supplies the higher-level failure semantics
the game needs: preparation does not commit without the declared participants;
pre-commit participant loss aborts; committed optional roles can degrade only
where the operation declares that policy; Core loss or ownership change aborts
carrier-bound plans except for the Relay Catch in-flight grace; branch and
timing windows remain locked; and bounded recovery releases surviving bodies to
their baseline jobs. The defect was the lower-level carrier fallback overriding
that machinery with a locally legal but strategically useless drop loop.

## Hosted product revision

The repaired artifact does not mutate immutable entrant playlist v4. Hosted Arc
Relay advances to playlist **v5**:

- v5 uses stock artifact
  `fdd61b1f4c24895926d3bdde7e8b70c0c6eb957d107dda99b04719614d499368`;
- v4 retains historical artifact identity
  `195114c7bd12758dc5b55060381c48782fe4e370a26a7c79883d6eb921490a64`;
- v2, v3, and v4 definitions remain registered for historical execution and
  replay resolution;
- rating and ranked-match count carry from the latest open ladder into v5;
- the v4-to-v5 stock-only transition does not revise sheets, move entrant
  identities, clear custom-mind preflight, or opt custom minds out; and
- older map/rules transitions still perform their existing sheet migration and
  custom-mind preflight reset.

The new WASM is 4,508,839 bytes, 499 bytes smaller than the prior artifact. Its
primary source SHA-256 is
`637822b12b9b0643410ddaa42919d009f0a4727768e210b792ae3179e5a8b23b`.
The seeder pins both source and artifact hashes in the first-party build
receipt. A current-artifact WASM match and the trusted in-process stock lane
produce the same canonical replay hash from identical inputs.

## Two-campaign operation proof

The tracked receipt is
`arena-bots/arc-relay/forward-combat-operation-proof-v1-2026-08-03/evidence/live-proof-summary.json`.
Campaign one uses each operation's registered seed (eight cells on `86080201`,
Rear Hook and Relay Catch on `67867967`). Campaign two overrides every cell to
the independent seed `86080202`. The population includes Baseline, Balanced,
and Hook Burst opponent sheets. Every cell uses the current artifact on both
sides and the authoritative WASM runtime.

| Review operation | Prepare | Commit | Success | Release | Match result | Whole-match v4 bars |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| Rear Hook | 186 | 203 | 219 | 231 | Orange, reactor at 376 | both teams pass |
| Lantern Sweep | 53 | 59 | 60 | 61 | Orange, reactor at 402 | both teams pass |
| Fork Shadow | 93 | 103 | 124 | 136 | Blue, reactor at 465 | both teams pass |
| Birth Rotation | 373 | 379 | 386 | 388 | Orange, reactor at 412 | both teams pass |
| Escort Counterpunch | 53 | 59 | 60 | 61 | Blue, reactor at 356 | both teams pass |
| Smoke Breach | 255 | 264 | 273 | 277 | Orange, reactor at 355 | both teams pass |
| Hardlight Gate | 57 | 69 | 70 | 71 | Orange, reactor at 376 | both teams pass |
| Relay Catch | 76 | 77 | 80 | 84 | Orange, reactor at 380 | both teams pass |
| Decoy Switch | 9 | 15 | 25 | 29 | Orange, reactor at 402 | both teams pass |
| Emergency Exchange | 101 | 102 | 113 | 114 | Blue, reactor at 436 | both teams pass |

The ten retained review broadcasts total 1,214,036 gzip bytes; the largest is
138,097 bytes. That is below the 300 KiB per-match and 8 MiB whole-gallery
budgets. The retention script refuses a campaign unless every operation passes,
the artifact hash matches, the registered bar schema is v4, and both teams in
every complete match are cohort-eligible.

## Determinism and validation

| Check | Result |
| --- | --- |
| Gate 3 golden set | 6/6 WASM canonical replays verified; all six expected hashes byte-identical; zero canonical replay files retained |
| Current WASM vs trusted stock runtime | identical canonical replay hash |
| Operation campaigns | 20/20 passed; 20/20 complete matches eligible for both teams |
| Operation proof tests | 4/4 passed |
| Scorecard detector tests | 13/13 passed |
| Full .NET solution | 1,892 passed, 0 failed, 84 environment-gated skips |
| PostgreSQL cutover tests | 2/2 passed against PostgreSQL 17, including v4 mind preflight/rating continuity |
| Web suite | 390/390 passed |
| Production web and four scoped CLI viewers | built successfully |
| Gallery eligibility build | 10/10 broadcasts admitted under v4 bars |
| Production browser smoke | 10/10 labelled matches loaded, rendered on WebGL, advanced playback, exposed score and causal tactics trace, and produced no page/console/request failures |

The historical Gate 3 golden set intentionally contains old doctrine behavior
that does not qualify under today's cohort bars; it is used here only as the
frozen determinism invariant. Its six hashes did not move.

## Owner review gallery

The rebuilt gallery is
`sandbox/arc-relay-stock-recovery-review-v1`. It is outcome-visible. Each card
states the operation side, opponent, final result, causal trigger, intended
tactic, legal counterplay, fallback/recovery behavior, and authoritative ticks
to watch. The skill pins this corpus for future broad Arc Relay renderer,
awareness, and tactics reviews.

## Candid limits

- Two campaigns and three opponent sheet styles protect against the exact
  replay overfitting that caused the quarantine, but they are not an exhaustive
  state-space search and contain no hostile custom-mind population.
- Intentional `drop-core` remains part of the public rules. A custom mind can
  still use it badly; hosted felt-degeneracy suspension is the product guard,
  not a rules prohibition.
- This pass proves clean operation execution and recovery. Owner viewing is
  still required to judge whether the plays read naturally and are enjoyable.
- Concurrent renderer/model work was left untouched and is not included in the
  stock-mind commit.

## Reproduction

The exact proof, second-seed, retention, and gallery commands are recorded in
`arena-bots/arc-relay/forward-combat-operation-proof-v1-2026-08-03/README.md`.
The retained evidence is hash-pinned and the gallery builder independently
re-runs the current scorecard before admitting a broadcast.
