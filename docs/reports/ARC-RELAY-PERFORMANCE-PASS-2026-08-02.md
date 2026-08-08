DECISION NEEDED: none. Use persistent in-process execution for broad balance
screening, then spend the full WASM/canonical cost only on retained evidence and
gallery cells.

# Arc Relay performance pass — 2026-08-02

## RESULT

The reported 12–26 CPU-second figure was the cost of an isolated **audit cell**,
not the cost of keeping one match alive. It combined cold process and WASM
startup, simulation, canonical replay construction, replay verification,
compression, broadcast reduction, and scorecard evaluation.

The optimized warm sandboxed simulation now uses **1.74 CPU-seconds** for the
frozen 351-tick match. Those ticks represent about 70.2 seconds at the current
200 ms cadence, so simulation consumes about **2.5% of one core while the game
is in progress**. The new persistent in-process screening path runs a real
120-cell population workload at **0.655 CPU-seconds and 0.627 wall-seconds per
match**. This meets the sub-second target for broad screening without weakening
the evidence standard: a screen receipt explicitly says that no canonical
replay was produced and cannot be admitted as audit evidence.

The complete warm WASM audit path is **2.26 CPU-seconds** for the representative
cell after simulation, canonical serialization, compression, and verification.
That still exceeds one second, so it remains the finalist/evidence path rather
than the exploratory inner loop.

No game rule, result, match contract, participant artifact, sheet, or canonical
replay content changed in this pass.

## EVIDENCE

### 1. Frozen performance cell

| Field | Frozen value |
| --- | --- |
| Cell | `breach-column--smoke-convoy--s32452843--a1` |
| Rules/map profile | `arc-relay-h0-01` / `home-gates-wide` |
| Participants | frozen `balance-audit-v2-2026-08-01` mind artifact on both teams |
| End | reactor destroyed at tick 351 |
| Canonical replay SHA-256 | `77489e60207a5b3987c9f67b83914aaff64c770dbd5dcd052ee0ecd684fab807` |

### 2. Before/after and runtime tiers

All CPU numbers are user plus system CPU on the same Apple Silicon development
host. Cold measurements use one CLI process. Warm measurements reuse the
process and loaded runtime. They are engineering measurements, not a production
capacity guarantee.

| Path | Runtime and output | CPU per cell | Wall per cell | Use |
| --- | --- | ---: | ---: | --- |
| Previous isolated full cell | cold WASM + canonical gzip | 8.62 s | 7.55 s | old CLI portion of audit |
| Optimized isolated full cell | cold WASM + canonical gzip | 4.43 s | 4.22 s | direct before/after comparison |
| Optimized warm simulation | WASM, match only | 1.74 s | 1.58 s | hosted-sandbox cost proxy |
| Optimized warm full cell | WASM + canonical gzip/verify | 2.26 s | 2.06 s | retained audit evidence |
| Persistent bulk screen | in-process, receipt only | **0.655 s mean** | **0.627 s mean** | candidate filtering only |

The real 120-cell bulk run completed in 75.184 seconds. Individual wall times
were 367 ms minimum, 599 ms median, 750 ms p90, and 2.510 seconds maximum; the
maximum was the cold first cell. Excluding the first three warm-up cells, the
mean was 594 ms. `/usr/bin/time` measured 78.55 aggregate CPU-seconds, or
654.6 ms per cell, and about 259 MiB maximum resident memory.

Every one of the 120 screen receipts matched the winner, completion reason, and
end tick in its corresponding full-WASM population evidence. Two sheets had
been repaired after the original 120-cell sweep; those two were compared with
their registered repair evidence rather than the superseded sheet revisions.
Result parity is a screening sanity check, not a replacement for canonical
verification.

### 3. Determinism invariant

The tracked `balance/arc-relay-gate3-golden-v1.json` suite was rerun through
`scripts/arc-relay-sweep.py` with three workers after all engine, wire-codec,
and replay changes:

| Gate | Result |
| --- | ---: |
| Planned golden cells | 6 |
| Canonical replays regenerated and verified | 6/6 |
| Expected SHA-256 matched byte-identically | **6/6** |
| Unexpected retained replay files after pruning | 0 |
| Harness wall time | 33.459 s |

The six expected canonical hashes remain:

- `1661522b6eb3af8f05834f74c6665c69618ca142c5bba4dee26c7b190edd2f0e`
- `b0433312f8f2188435b086bce139eabb9d5618411d12cde53b40584a4a9eafbb`
- `37d7f726b992b606745246a493e93f93d6a0608608f993fde2421645c4dfa27c`
- `28acb15cadb60ecdf2fd0988e794af0c44d69893e5d1374a580031b39d399966`
- `1680c38dea521c2b2951f63bc025c0b28f61836d39625b5b644368ab27987605`
- `cda2fdb628ef71e5a523cd196031c490d2dc4b2d696b8e4f0376af2be50e2b20`

The compressed replay changed from 3,472,176 to 3,662,488 bytes (+5.48%) when
compression moved from smallest-size to the materially cheaper optimal level.
The canonical uncompressed bytes and replay hash did not change. This archive
is regenerable audit output, not the size-budgeted broadcast slice.

### 4. What changed

- Match preparation now caches immutable team projections, per-life visibility
  geometry, smoke occlusion, spawn reservations, legalities, signature targets,
  modes, and signature-life projections within their valid tick/life scopes.
- Observation framing and actor-wire arrays allocate their exact final buffers
  instead of repeatedly growing and copying streams.
- Canonical replay construction serializes the payload once, hashes those bytes,
  and assembles the envelope once. Replay documents retain UTF-8 bytes and only
  materialize a string when a caller explicitly requests it.
- Gzip verification now streams and compares content with pooled buffers.
- `arc-relay-screen-batch` keeps the stock algorithm and engine warm across N
  cells. Each sheet remains deterministic input data with its own recorded hash;
  no per-sheet algorithm build is required.
- Optional `BOTARENA_PERF_DIAGNOSTICS=1` phase counters expose preparation,
  stepping, runtime, projection, replay, and gzip costs without changing normal
  output or canonical content.

The remaining warm WASM bottleneck is guest execution and observation transfer:
about 1.20 of the final cell's 1.58 wall-seconds was inside the runtime. That is
why the sandboxed path did not reach the in-process screen's sub-second cost.

### 5. Capacity interpretation

At 1.74 CPU-seconds over a 70.2-second match, the arithmetic ceiling is roughly
40 continuously active matches per fully saturated core. A preliminary planning
range of **25–30 matches per core** leaves headroom for service scheduling,
network work, spikes, and longer or more demanding player-authored minds. It is
not a deployment commitment; production load tests must include the actual host,
concurrency, admission limits, and adversarial-but-legal bots.

Canonical replay, compression, broadcast, and scorecard work should happen
after the authoritative match and off the latency-sensitive tick loop. Balance
sweeps should screen broadly in-process, then audit only selected cells in WASM.

## NEXT

Use the new screen-first/finalist-audit workflow for the next sheet loop. Before
setting production capacity, benchmark concurrent warm WASM sessions on the
intended server class and define CPU/allocation admission budgets for submitted
minds. Further optimization should target the WASM observation boundary; it
must rerun all six golden hashes before it can land.
