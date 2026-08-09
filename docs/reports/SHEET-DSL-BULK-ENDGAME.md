# Consolidated sheet DSL — corpus endgame

Date: 2026-08-09  
Branch: `sheet-bulk`  
Continuation base: `e4ea620abb126c52b1dcdddd3264ae3fe0532243`  
Implementation checkpoint: `a147b8ac`  
Status: complete; Wellwright migrated, stale corpus removed, legacy linker
deleted, hosted v9 pinned; not merged

## Result

The tracked tactical-sheet corpus now has one current language and one explicit
historical exception:

- seven current sheets use the exact ten-key consolidated DSL, compile directly
  to ATL1, and run on the generic role-ladder executor;
- `home-siege-v2` remains byte-frozen archival evidence and is deliberately
  rejected by the current compiler; and
- the other fifteen playbooks were stale and are deleted rather than ported.

Wellwright was the last product/runtime blocker. The App template catalog, trial
opponent, exhibit harness, and hosted stock participant now all consume its
Warren-native direct sheet. No current sheet needs the retired desugarer or ATP1
link path.

## Deletion safety audit

The deletion audit happened before any source was removed.

- No immutable playlist definition pins any deleted sheet. Historical v7/v8
  and current v9 pin `wellwright-v1`; no other tactical sheet id or source hash
  is part of an immutable playlist definition.
- The App embedded only Hunter and Wellwright playbook/layout pairs. It embeds
  no deleted sheet. The catalog still exposes Hunter as the starter and
  Wellwright as the stock opponent.
- `home-siege-v3-double-kestrel` and `home-siege-v3-lib` were referenced only by
  the legacy compiler test fixture. Those references tested the desugar/ATP1
  path being deleted in this same change; neither was a product resource,
  playlist input, or independently load-bearing runtime asset. The sources and
  their legacy-only tests were therefore removed together.
- The final zero-reference layout,
  `counterflow-home-siege-v3-vs-recognizer.json`, was removed after the broader
  deletion commit. Historical layouts still named by exact evidence ledgers or
  reproducibility scripts were retained.

This audit found no pin that required stopping the goal.

## Final tracked sheet corpus

| tracked playbook | status | map/layout | ATL1 bytes | share of 65,536 | headroom | ATL1 SHA-256 |
|---|---|---|---:|---:|---:|---|
| `breakwater-v1` | current direct | Warren-06 / Wellwright native | 8,335 | 12.7% | 57,201 | `8c145445a903968efc62d471f40bc785e37b4ddb26e980fb93c7293375e22ffc` |
| `home-siege-v2` | byte-frozen archive; not compiler input | Counterflow | n/a | n/a | n/a | source hash below |
| `home-siege-v3` | current direct | Warren-06 native | 13,403 | 20.5% | 52,133 | `5a077318074ff9fa09ecf4a84e755f6ec856f4b5bb866fe03518c01cd5fea0c4` |
| `hunter-v1` | current direct | Warren-06 native | 14,548 | 22.2% | 50,988 | `fe3516d64231c5ebd28625fbf14649f9478f4903eee5a47b55c6752ba7d7cee2` |
| `predator-v2` | current direct | Warren-06 native | 6,500 | 9.9% | 59,036 | `d739699859c58357fc73a8cd2f88edb24afc2756916d3bbc034f212462394323` |
| `ripen-harvest-v1` | current direct | Warren-06 native | 5,670 | 8.7% | 59,866 | `c2f1e1707d7e9aad978d3b4002128a1dc75f27b4e0ec6ca325a254e0e3ad8bcf` |
| `trifold-balanced-v1` | current direct | Warren-06 / Wellwright native | 7,182 | 11.0% | 58,354 | `53cef753154fbb034b46047d3429fcf7880e0cb341b4b6dfd3e18d376c78644e` |
| `wellwright-v1` | current direct; hosted stock | Warren-06 native | 8,995 | 13.7% | 56,541 | `cbec5052a4ae21d3330de8deb6d656bda209cdb1c81f75a8c4af48f243de14c1` |

All ATL1 measurements were regenerated at the implementation checkpoint with
`nilbots experiment arc-relay-playbook`; they include canonical playbook,
canonical layout, hashes, and envelope. The largest current package is Hunter
at 14,548 bytes, leaving 50,988 bytes of headroom.

## Wellwright migration

Wellwright is 554 source lines with the exact current root:

```text
schema, playbookId, auditStatus, composition, layout, memory,
roles, custodyPolicies, doctrines, conditions
```

Its source SHA-256 is
`2167bd34ea456e11cce271ecb822f82b03b55aa5b7341090803caed5172db017`.
Its native `wellwright-warren6-v1` layout is
`89141634c776ae7d98c7611577450fe7818d8dcaee8e0bb7adb9e343ce3699a5`
and targets `arc-relay-ambush-warren-06`; no coordinate projection occurs.

The translation has six roles, six named conditions, one custody policy, and
only authored `fetch` and `patrol` rungs. The old shared race allocation became
exactly three narrow cohorts:

| shared name | consumers | one job |
|---|---:|---|
| `north-race` | 3 | nearest three eligible bodies race the north Well rung |
| `centre-race` | 3 | nearest three eligible bodies race the centre Well rung |
| `south-race` | 3 | nearest three eligible bodies race the south Well rung |
| `haul` focus | 12 | maximum three attackers on one target |
| `aid` focus | 8 | maximum two attackers on one target |

The sole `farm-custody` policy has ten doctrine/fetch references. There is no
second carrier roster, fake group, operation, formation, escort call, muster
call, or hidden priority band. A rejected race candidate continues down its own
ladder. No grammar addition or sheet-id-specific executor branch was needed.

An in-process mechanism smoke completed at tick 402. The matching tracked-WASM
run completed at the same tick with the same winner and exact gameplay digest:

```text
4015ac280c54f10b9221ff2f0c805d816aea3ffe766acb6040a23902f6d21bb8
```

Replay container hashes differ because runtime provenance is part of the
canonical record; the gameplay identity does not.

## Removed playbooks and layouts

The fifteen deleted playbooks are:

```text
draft-mineline-v0
home-siege-v3-double-kestrel
home-siege-v3-four-down-double-relay
home-siege-v3-four-down-double-relay-vs-breakwater
home-siege-v3-lib
home-siege-v3-mortar-line
home-siege-v3-mortar-line-meta
home-siege-v3-south-mirror
home-siege-v3-south-mirror-vs-breakwater
home-siege-v3-vs-recognizer
hook-control-v1
hunter-fully-committed
predator-v1
sentinel-zone-v1
tempo-race-v1
```

Eighteen dedicated or unreferenced layouts and three obsolete migration/
pre-freeze scripts were removed with them. Counterflow layouts that remain are
not current authoring assets: they are still named by byte-frozen evidence,
historical examples, or their recorded reproduction scripts.

## The byte-frozen exception

`home-siege-v2.json` is unchanged from `e4ea620a`:

```text
bytes    49,566
sha256   1812b16d03ff9a70c3f9e66e6a4f0b0a9293b9543925281e9df2657ed25e5fdb
```

The current test suite pins both values and proves that compiling it fails on
the retired `perspective` field. Its remaining dependants are archival:

- the exact-byte compiler test;
- the `home-siege-v2` example/explain artifact; and
- candidate, reference-freeze, final-cohort, and development-screen evidence
  ledgers.

No App template, hosted admission path, current mind, or immutable playlist
executes it. The freeze still preserves historical evidence, but it has no
product/runtime dependency; removing that archive is a separate owner choice.

## Legacy compiler deletion

Exact C# line counts around the shared compiler are:

| compiler surface | at `e4ea620a` | final | change |
|---|---:|---:|---:|
| `ArcRelayTacticalPlaybookCompiler.cs` | 6,087 | 720 | -5,367 |
| consolidated validator partials | 1,000 across four files | 980 in one file | -20 |
| **combined compiler surface** | **7,087** | **1,700** | **-5,387 (-76.0%)** |

The removed code includes `ExpandAuthoring`, generated orders/tasks/groups/
formations/engagements, the off-path expanded-source validation oracle, ATP1
encoding, legacy source dispatch, and CLI paths that described or copied the
expanded package. `TacticalPlaybookPackage.Load` now accepts ATL1 only. The CLI
keeps the filename `playbook.atp` solely for command compatibility; its envelope
magic and content are ATL1.

`docs/SHEET-AUTHORING.md` was rewritten as the complete current contract: exact
ten-key root, six verbs, role-local latching, self facts, custody/fetch,
follower-owned follow, narrow cohort/focus primitives, Warren-native layout,
and hosted editor integration. It teaches none of the retired source planes.

## What remains in the in-memory adapter

Deleting serialized legacy IR did not rename the mature action machinery. The
direct runtime still projects each selected authored rung into existing
in-memory `TacticalPlaybookPackage.Order` and `Engagement` records so movement,
fight allocation, custody, repair, signatures, route progress, and combat
replay fixes stay shared. It also currently synthesizes:

- `FetchRung` / `ModeRung` lookup records;
- role-keyed `Group`, `Membership`, and one-state `StateMachine` scaffolding
  for role allocation and snapshot code;
- empty `Formations`, plus per-rung posture carried on `Order`;
- one steady `Coordination` / `Phase` containing the floor order ids; and
- existing `SupportPolicy`, `CustodyPolicy`, `ConditionGroup`, movement,
  fallback, and focus-related records.

`TacticalTaskCandidate` is also still the body snapshot type accepted by the
direct selector. The old task/transition/escort/formation record families and
some corresponding primitive tests therefore remain in memory/code even
though no current linked package can supply the old source structures.

The follow-up is to introduce direct action-policy DTOs, move role/snapshot
consumers onto them, then delete dead `TacticalTaskMachine`, task,
reintegration, transition, formation, escort, and synthetic phase/group
records. That cleanup must preserve the mature action behavior and reason
strings. It is explicitly not part of this pass and must not change the DSL.

## Hosted cutover

Wellwright's source hash and the direct mind bytes differ from immutable v8, so
v8 was not amended. Entrant playlist v9 was minted instead.

| hosted item | value |
|---|---|
| current playlist | immutable entrant playlist v9 |
| rules/map | unchanged `ambush-warren-11` / Warren-06 |
| build input | `b126edf8c768a7cca8aec88bcd851ab77f458522a88ed710e29b7399e35b390a` |
| v9 artifact | `48d778f42e104e30611216c9e72620981e9994857a0052188ba08a866064de3f` |
| artifact bytes | 8,428,228 |
| frozen directory | `ambush-warren-11-stock-v3` |
| linked format | ATL1 |

The current tactical source artifact and frozen v3 artifact are byte-identical.
Historical v7 artifact
`42288a911821f7893f34fb096cd8c17464f24da7a170761898f10e8b0497763b`
and v8 artifact
`7cf7e2f56fad1d80bec2250b0c309191b9b24ac2141bf36374116431c19adec3`
remain untouched and addressable through their immutable definitions.

v9 changes representation/compiler identity, not rules, map, or match facts.
The seeder therefore closes v8 to new pairing and carries an already-open
public Season 1's ratings and ranked-match counts into v9 idempotently. A real
disposable-PostgreSQL integration test proves that continuity; historical v8
rows remain readable.

CLI/package version moved to 0.9.38 under the compatibility rule. No release or
deploy workflow ran. The local tag inventory ends at `cli-v0.6.1`; before any
deploy carrying this line, `publish-cli` still owes the recorded compatibility
tags through `cli-v0.9.38` (including 0.9.34 and 0.9.35).

## Behavior exhibit

[Open the outcome-visible six-cell phone gallery](https://climbing-politicians-intermediate-typically.trycloudflare.com/).

The registered cells run Wellwright against Hunter on both assignments. They
are an exhibit, not a battery; eligibility was explicitly skipped by the fast
exhibit harness, and no outcome below is a balance claim.

| cell | recorded Wellwright result | final score from Wellwright perspective | gameplay digest |
|---|---|---|---|
| `w-9001` | win, t402 | pulses 3–1; integrity 2–0; charge 0–2 | `4015ac280c54f10b9221ff2f0c805d816aea3ffe766acb6040a23902f6d21bb8` |
| `w-9002` | win, t457 | pulses 3–2; integrity 1–0; charge 0–0 | `dc8d5258d7a96b5676dd224ecd7e24a736c369baffd40c6df2701c8262ae937e` |
| `e-9005` | win, t438 | pulses 3–2; integrity 1–0; charge 0–1 | `8138ca244f4a7ea709d79461a2540187dabb2abf5c8f2f6492fc37dcd05913b5` |
| `e-9009` | win, t462 | pulses 3–2; integrity 1–0; charge 0–1 | `175874620e3fa713c12f87b82efc99affa017695a3381d14d389cfa7e532ffd2` |
| `w-9012` | loss, t494 | pulses 2–3; integrity 0–1; charge 1–0 | `f49914ced96cd1748b30f9c93ee11b37bb82cb6a110162d1610b7af4d294d43a` |
| `e-9004` | win, t460 | pulses 3–2; integrity 1–0; charge 0–1 | `c716f1f1648933f9c1315b1f09435dc7784a10fd2aeb3aac3c968694e1279432` |

All six games reached reactor destruction. Trace reasons carry the direct
sheet hash/layout hash and authored role/rung positions; authorized Core pickup
and committed delivery reasons remain explicit. Wellwright's traffic read is
recorded for review, not judged:

| assignment group | blockades | flaps/body-ticks | reversals/body-ticks | chokes |
|---|---:|---:|---:|---:|
| west cells | 1.26% | 6.58% | 9.98% | 1 |
| east cells | 1.04% | 7.04% | 10.62% | 2 |

The six compressed broadcasts total 996,047 bytes (0.95 MiB); the largest is
186,749 bytes (182.4 KiB), below the 300 KiB per-match and 8 MiB gallery
budgets. A 390×844 touch-capable Chromium smoke opened the public gallery,
found six cards, loaded a WebGL match and score HUD, and advanced playback from
tick 0 to 5.19 without a page or request error. The server and quick tunnel run
in detached sessions on local port 8988.

## Verification

- `dotnet build BotArena.sln` — passed, 0 warnings / 0 errors.
- `dotnet test tests/BotArena.Cli.Tests` — 257 passed.
- `dotnet test tests/BotArena.Engine.Tests --no-build` — 1,436 passed.
- `dotnet test tests/BotArena.Determinism.Tests --no-build` — 17 passed.
- `dotnet test tests/BotArena.Runtime.Wasm.Tests --no-build` — 68 passed.
- full App suite against disposable PostgreSQL — 284 passed, 3 unrelated
  opt-in S3/container-toolchain tests skipped.
- `bash scripts/generate-api-clients.sh` — regenerated all three clients with
  zero Git diff.
- `npm test` in `web/` — 437 passed after materializing the repository's
  intentionally ignored, audited Meshy raw/review inputs into this worktree;
  no web file is changed or committed.
- `npm run build` in `web/` — passed production build.
- tracked WASM versus in-process `w-9001` — exact gameplay digest equality.
- frozen Home Siege v2 byte/hash assertion — passed.
- `git diff --check` — passed before this report.

No battery, balance run, release, deploy, merge, or `web/` edit was performed.
