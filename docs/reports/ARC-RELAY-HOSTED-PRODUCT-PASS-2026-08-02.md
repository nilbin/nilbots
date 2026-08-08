DECISION NEEDED: none. The first hosted Arc Relay product slice is ready for
integration. Its steady full-horizon worker gate is under one second on the
measurement host; production capacity still requires a concurrent load test on
the intended server class.

# Arc Relay hosted product pass — 2026-08-02

## RESULT

Arc Relay now has a real hosted path from an account-owned, player-facing
eight-slot commander sheet to a completed, watchable match. The server stores
revisioned sheets in PostgreSQL, enforces class unlocks and composition limits,
snapshots both sheet revisions into the match, executes one immutable first-party
stock mind per side, and stores a causal compact broadcast for the existing
viewer. Editing a saved sheet cannot alter an old match.

The worst-case 600-tick horizon cell measures **938.011 ms** at steady state
through the production job handler, including EF reads and writes, simulation,
broadcast projection, gzip object storage, and result persistence. The first
cold execution remains 3.214 seconds while tiered compilation warms; the second
is 1.228 seconds. This is a steady-host gate, not a claim that a newly started
worker serves its first match in under one second.

The authoritative audit path did not change. All six registered replay-v3 WASM
goldens reproduced byte-identically. A separate parity test produced the same
full canonical replay hash with the ordinary SDK projection and the optimized
trusted projection, and compact recording produced the same terminal result.
No rule, balance value, doctrine, class capability, map fingerprint, or frozen
stock source changed. No fun claim is made.

## EVIDENCE

### 1. Hosted execution and storage

The product lane compiles the exact frozen stock sources once into the worker.
Every player sheet is validated, canonicalized, SHA-256 hashed, and linked as
deterministic `ARS1` evaluation data. Submitted player code continues to use the
sandboxed WASM runtime; only the registered first-party artifact may select the
trusted in-process lane.

| Identity | Frozen value |
| --- | --- |
| Stock source SHA-256 | `c8182e133a202733ef7c6b43367097eb118d2295a91dcdbf592e6fe13ff48f79` |
| Stock WASM SHA-256 | `c574c09a832d0a28cd1be8fd645a02685ad9c24a02543bce5c9819d5e1fd65f9` |
| Player sheet schema | `arc-relay-player-sheet-v1` |
| Hosted playlist | `arc-relay` version 1, public unranked |
| Rules / map | `arc-relay-h0-01` / `arc-relay-threefold-home-gates-wide-01` |
| Runtime | `trusted-stock-in-process-v1` |
| Viewer cadence | 1.25 presentation ticks/second |

The integration benchmark uses a real HTTP admission, a fresh migrated
PostgreSQL database, the production EF entities, the hosted job handler, and the
local production object-store adapter. The timer starts when the queued match is
handed to the job handler; HTTP registration, sheet authoring, and match
admission are verified but not included in the worker duration.

| Full 600-tick horizon execution | Worker time | Replay hash |
| --- | ---: | --- |
| Cold process / tier-0 JIT | 3,213.942 ms | identical |
| First warm repeat | 1,228.374 ms | identical |
| Steady repeat | **938.011 ms** | identical |

The integration test fails when the steady sample is at or above one second.
It also requires all three executions to produce the same compact replay hash.

| Replay/storage measure | Before hosted compaction | Final |
| --- | ---: | ---: |
| Replay representation | canonical replay v3 | spectator broadcast v2 / DB format 4 |
| Uncompressed bytes, 600 ticks | 118,428,761 B | 6,281,142 B |
| Stored bytes | 118,428,761 B | **209,226 B gzip** |
| 300 KiB match budget | fail | **pass** |
| Replay projection time | about 778 ms | about 66 ms cold |

The compact document owns its own canonical payload hash. Completed responses
are served as the stored gzip bytes; live responses are inflated server-side,
causally truncated to the presentation clock, and returned with both terminal
result and replay hash set to null. The integration test verifies the partial
and complete HTTP forms. Canonical replay v3 remains the audit artifact and is
not relabelled as the compact broadcast.

### 2. Player-facing eight-slot sheet

This is a new product schema, not the provisional evaluation JSON used by the
Gate 3 audit. A sheet contains exactly eight body slots drawn from whatever the
account has unlocked. There is no stable-of-five layer. The current composition
limit is two copies of one class, and the server enforces it independently of
the browser.

The authoring surface provides:

- class, theater, role, partner, outbound path, and return path per slot;
- named theater zones and rally lines drawn on the authoritative map;
- carrier, escort, and interception policy controls;
- zero to three ordered, edge-triggered gambits with bounded duration and
  cooldown;
- saved revisions, optimistic-concurrency conflict handling, and save-as-copy;
- a two-saved-sheet scrimmage launcher.

The product gambit vocabulary is delivered by the backend catalog and is
restricted to the final bounded Gate 3 grammar the frozen mind actually
executes: enemy Pulse, own Pulse, and double enemy possession. This removed a
pre-smoke schema defect where the browser exposed trigger names not implemented
by the frozen algorithm.

The launch catalog contains all sixteen approved classes. Eight are starter
unlocks; the other eight are admitted through generic entitlement grants using
`arc-relay-class:<classId>`. Unlocks add options, never stat tiers or
score-to-power. The server rechecks current entitlements both when saving and
when admitting a match.

### 3. PostgreSQL, API, and web wiring

Migration `20260802094417_ArcRelayPlayerSheets` adds:

- account-owned `ArcRelaySheets` with revision, canonical JSON, content hash,
  timestamps, indexes, and a positive-revision check;
- immutable sheet id/revision/name/hash/JSON/data snapshots on each match
  participant;
- fractional presentation cadence storage so 1.25 ticks/second is exact.

`Revision` is an EF concurrency token. A stale update returns HTTP 409 rather
than overwriting another tab's edit. Match admission takes the existing
per-account unranked admission lock, applies the existing daily and network rate
limits, resolves only sheets owned by that account, recompiles their canonical
JSON against current unlocks, and verifies each stored content hash. The worker
performs the same snapshot/hash/data verification before execution.

The generated OpenAPI contract and the web and CLI clients were regenerated.
The web page is routed at `/relay`, uses the shared query/mutation layer, and is
present only in the desktop navigation. There was deliberately no mobile
feature work; the mobile generated schema changed only because generated API
contracts stay in lockstep.

The hosted viewer accepts broadcast v2 and expands it at the existing replay-v3
normalization boundary. Causal prefixes remain partial; terminal results remain
withheld until the shared broadcast clock completes.

### 4. Determinism proof

`scripts/arc-relay-sweep.py` reran the tracked six-cell manifest with three
WASM workers after the engine and replay work. It verified 6/6 exact canonical
hashes in 33.047 seconds:

- `1661522b6eb3af8f05834f74c6665c69618ca142c5bba4dee26c7b190edd2f0e`
- `b0433312f8f2188435b086bce139eabb9d5618411d12cde53b40584a4a9eafbb`
- `37d7f726b992b606745246a493e93f93d6a0608608f993fde2421645c4dfa27c`
- `28acb15cadb60ecdf2fd0988e794af0c44d69893e5d1374a580031b39d399966`
- `1680c38dea521c2b2951f63bc025c0b28f61836d39625b5b644368ab27987605`
- `cda2fdb628ef71e5a523cd196031c490d2dc4b2d696b8e4f0376af2be50e2b20`

In addition, the product parity test runs the same full match through the
ordinary SDK projection and the allocation-reduced trusted projection and
requires the complete replay-v3 hashes to match. It then runs the compact,
chronology-free recorder and requires its terminal result to be recursively
equivalent. This is the proof boundary for the trusted specialization; it is
not inferred from matching win/loss alone.

### 5. Production browser smoke

`scripts/smoke-arc-relay-product.mjs` was run against a Release production web
build served by a real `BotArena.App` `all` role over a database migrated from
empty. The script:

1. registered a real account;
2. opened `/relay` and loaded the server catalog;
3. saved the starter sheet and a second revision-independent copy through the
   browser UI;
4. launched their scrimmage through the UI;
5. waited for the background worker to persist `Completed`;
6. fetched and validated a live causal broadcast-v2 prefix; and
7. reloaded the match page and found the real arena with no page or console
   errors.

The final smoke match id was
`6dcf875e-ed5f-4c60-a9cb-f20e7a6f56ec`. Its first observed prefix contained one
visible tick; database integration separately verified stored format 4, the
209,226-byte gzip object, and the eventual complete result/hash response.

### 6. Build and asset budget

The before build is detached commit `3e42c300`; the final build is this pass.
No new runtime image, sound, or model asset was added.

| Production artifact | Before | Final | Delta |
| --- | ---: | ---: | ---: |
| Main hosted JS | 1,258,212 B | 1,278,418 B | +20,206 B (+1.61%) |
| Hosted CSS | 58,622 B | 60,190 B | +1,568 B (+2.67%) |
| Entire `web/dist` | 43,316 KiB | 43,336 KiB | +20 KiB (+0.05%) |
| Four CLI viewers | 23,740 KiB | 23,824 KiB | +84 KiB (+0.35%) |

### 7. Verification ledger

| Check | Result |
| --- | --- |
| Release solution build | pass, 0 warnings |
| Full .NET suite | 1,861 pass; 78 infrastructure-gated skips; 0 failures |
| PostgreSQL hosted integration | pass, including strict 938.011 ms steady gate |
| Trusted/full/compact parity | pass |
| DocDrift | 24/24 pass |
| Web tests | 354/354 pass |
| Production web + four CLI viewer build | pass |
| Browser production smoke | pass, no browser errors |
| Canonical WASM golden set | 6/6 byte-identical |
| `git diff --check` | pass |

## NEXT

Deploy and load-test the web and match-worker roles together on the intended
server class before setting worker concurrency or capacity. The already queued
design task after this product slice is to author twenty genuinely distinct
player sheets, reject near-duplicates structurally, and then run a fresh
balance/degeneracy loop. That work must not reinterpret this integration pass
as balance evidence.
