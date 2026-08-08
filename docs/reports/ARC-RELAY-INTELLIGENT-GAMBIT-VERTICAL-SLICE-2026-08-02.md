# Arc Relay intelligent-gambit vertical slice

Date: 2026-08-02

Branch: `codex/game-redesign`

Design contract:
[`docs/ARC-RELAY-INTELLIGENT-GAMBIT-FRAMEWORK.md`](../ARC-RELAY-INTELLIGENT-GAMBIT-FRAMEWORK.md)

## DECISION NEEDED

Watch the retained enabled broadcast and judge whether a bounded operation is
now understandable and strategically credible enough to justify a product
sheet/editor design pass. Recommended: keep the framework and iterate the two
cards in the preview playground before productising the provisional schema.

Rear Hook does not reach commitment in this sample: its setup times out once
and is repeatedly exposed or loses feasibility. That is disclosed evidence of
cost and counterplay, not a win claim. Lantern Sweep does reach a committed
alternate route, remains on it, then aborts when its carrier loses the Core.

## RESULT

The narrow vertical slice is implemented and deterministic:

- `stock-mind-v3` preserves the complete v2 baseline executor and adds one
  shared deterministic operation scheduler;
- the provisional `arc-relay-evaluation-sheet-v2` binary linker adds at most
  three small operation cards without changing v0/v1 bytes;
- Rear Hook and Lantern Sweep use atomic preparation, phase-scoped tasks,
  preparation-only substitution, at most two ordered commit branches,
  committed-minimum enforcement, physical recovery, cooldown, and edge re-arm;
- disjoint cards can coexist, overlapping preparation/recovery claims arbitrate
  by priority, and a committed claim cannot be silently preempted;
- operation facts are causal public/visible/remembered/inferred predicates with
  `true`, `false`, and `unknown`; fully observed `zone-clear` is the only clear
  result, while partial coverage remains unknown;
- Rear Hook binds the exact visible Core and carrier on commitment, updates
  only from later causal observations, and turns a missing target into an
  abort only after the authored two-tick freshness budget; an unrelated Core
  cannot satisfy its mission;
- every live body still has a useful baseline. The old rear-ambush sheet's
  permanent hold-fire staging was removed from units 4 and 5;
- replay debug traces record phase, fixed branch, transition reason, exact
  unit/task claims, and each evaluated fact's truth value. Compact role tags
  identify phase/task on the claimed bodies; ordinary action reasons explain
  movement, holding, firing, and extraction; and
- no Arc Relay rule, fog/rendering path, engine contract, frozen stock artifact,
  or canonical replay semantic changed.

This is not a fun, balance, or product-UX claim.

## EVIDENCE

### 1. Interpreter hostile cases

`IntelligentOperationMachineTests` implements all eight approved adversarial
cases:

| Case | Proved response |
| --- | --- |
| Rear Hook loses a Towline in preparation | abort to recovery; no undeclared substitute |
| Towline dies on the successful strike | success is processed before participant loss |
| carrier disappears under smoke | unknown preserves commitment only until the explicit target-expiry abort |
| false ambush signal | ordered clear route commits once; continuous evidence cannot re-arm the edge |
| Lantern dies before route choice | essential preparation loss recovers; no respawn joins |
| both Lantern branches are true | first authored branch wins once and never flips |
| replaceable reserve dies | feasible preparation substitute may join before the unchanged deadline; no commit substitute |
| emergency overlaps a rotation | preparing claim is preemptible/reselectable; committed claim remains locked |

Targeted CLI/interpreter test result: `12 passed, 0 failed` after the v2 sheet
fixture was added. The stock-mind project builds with `0` warnings and `0`
errors.

### 2. Build once, link sheets as data

| Item | SHA-256 / size |
| --- | --- |
| `stock-mind-v3/out/bot.wasm` | `50a032e96efc6502a4f2fb662eb095b37561e420d4644aa87e81977922dfc12b` / `4,457,315 B` |
| baseline sheet | `63a9b13697946cae5c3c0ccd1703f995204bfa2e21f03679e0cd2cd9e0d3b697` |
| operation-enabled sheet | `a8bd7bcafbf2f58488cb3ed31015a6ba57fb8285e9bad5a4b9d9741f5d7c8194` |

The first controlled build was a cache miss. An immediate second controlled
build was a cache hit with the same key
`0b50544a70c0cab6b4d1331c1a3747142c6473e3cde2e618f7e4421df9bde4d6`
and the same artifact hash. Changing either sheet requires no WASM rebuild.

### 3. Same-seed WASM comparison

Both cells use seed `86080201`, the `depth-counterflow` profile, the same v3
artifact on both teams, and these unchanged fingerprints:

- rules `f6d3ee9b1bb17d7bd8d0981941fd00a6a96f0e7ef834497d11924c06087174eb`;
- map `5ca7d1a1826791d736465d352c1558793846fc2e3df343d730f1df4c79f47e0c`;
- topology `53d0d7e0e0bc777a64083086a6fd6398e6c64ccd3805d149db74d027455eec53`;
  and
- match contract `a5c8780fe39471e5e67abf179ce165ed61b1bb725e5e2b6db4e11a2588c9eaff`.

| Cell | Team 0 sheet | Canonical replay hash | Repeat | End fact | Durable bytes |
| --- | --- | --- | --- | --- | ---: |
| baseline | baseline only | `512099fb1cb87d71cd1f7ec9b7cb63a8bca690ca198387a1f6f78b47d3512375` | byte-identical | team 1, reactor destroyed, tick 507 | `2,190 + 181,231 = 183,421` |
| enabled | Rear Hook + Lantern Sweep | `386b13c6a959f6c1b2155f646cc32758ef2dc8de33b0a92e78514e91ad313831` | byte-identical | team 1, reactor destroyed, tick 430 | `2,206 + 153,255 = 155,461` |

Both original and repeat canonical replays passed `nilbots verify`. Both teams
remained eligible; the enabled run had zero runtime faults. The outcome change
is reported only as provenance. Two cells cannot support a strength or fun
claim.

### 4. Concrete trace read

Selected team-0 trace events from the enabled canonical replay:

| Tick | Trace |
| ---: | --- |
| 9 | Rear Hook enters Prepare and atomically claims `4:north-hook,5:south-hook`; Well timing and observed approach pressure are both true. |
| 33 | Rear Hook reaches its unchanged preparation deadline without a valid carrier-strike branch; survivor 5 enters extraction. |
| 41 | Extraction deadline releases the survivor to baseline; no gambit intent survives. |
| 105 | Rear Hook prepares again after a real false-to-true edge. |
| 118 | A visible enemy enters the staging pocket; the authored compromise abort fires and both Towlines extract. |
| 384 | Lantern Sweep claims `7:carrier,6:lantern,3:screen` from current carrier and risk evidence. |
| 385 | A visible threat in the fork risk area selects ordered branch `alternate-return`; the Lantern releases and the carrier/screen minimum locks. |
| 394 | The carrier loses the Core, the authored abort fires, and the surviving screen takes recovery. |
| 395 | Recovery reaches its fallback and releases to baseline. The branch never flips. |

The longest per-tick debug trace in this run is `383 B`, below the `4,096 B`
mind debug cap.

### 5. Regression gates

- v0/v1 linker coverage remains green and v2 links deterministically;
- all eight hostile-case tests pass;
- the full solution regression run passed `1,879` tests with `83`
  environment-gated skips and no failures;
- the final six-cell Gate 3 golden sweep ran on WASM with three workers in
  `33.165 s` and reproduced `6/6` expected canonical hashes byte-identically;
- the enabled and baseline comparison replays each reproduced their new hash
  on a second independent execution; and
- compact records remain below `4 KiB`, broadcasts below `300 KiB`, and total
  durable payloads below `304 KiB`.

## NEXT

Owner watch/ruling only. If the behavior reads as intelligent, the next goal is
the actual player-facing sheet and preview-playground design, including
target-memory visualization and authoring warnings. If it does not,
revise the two cards and their trace presentation without expanding the
grammar. Do not add Birth Rotation, hosted APIs, or an editor merely because
the interpreter exists.
