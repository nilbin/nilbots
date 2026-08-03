# Arc Relay operation reliability and counterplay pass

## Result

The ten evaluation-grade team operations all produced, on held-out seeds and
authoritative WASM, at least one live mission success, one causally evidenced
opponent counter, one claimed-life casualty followed by a baseline respawn, a
bounded release, and complete survivor return to non-operation behavior.

That result needs one important qualifier. The discovery registration originally
accepted only counters after the commitment lock. It passed **8/10** operations:
`lantern-sweep` and `hardlight-gate` were repeatedly disrupted while assembling,
but never after committing. Calling those failures “uncountered” would discard
real anticipatory play; forcing the operations to commit before they were ready
would make their behavior worse merely to satisfy the metric. The amended,
tracked taxonomy therefore keeps two counter categories separate:

- **committed counter:** a causally contacted committed operation exits
  unsuccessfully and releases to baseline;
- **preparation denial:** the opponent directly destroys an already-claimed
  essential setup life, the operation explicitly aborts before locking, and the
  survivors release to baseline.

The amendment was recorded after discovery and before confirmation. Two new
campaign seeds then confirmed the combined taxonomy: **10/10 operations**, **40/40
eligible matches**. Under a committed-only interpretation the result remains
**8/10**, not 10/10.

No game rule, map tile, class value, replay semantic, renderer, fog/vision path,
hosted schema, or player-facing sheet/editor surface changed. No operation sheet
or stock-mind code needed an outcome-driven repair. This pass added evaluation
orchestration, causal evidence, a counter taxonomy, compact retained matches, and
an outcome-visible 3D review gallery.

This is not a fun claim, launch-balance claim, or claim that the evaluation JSON
is a good player-facing authoring format.

## Frozen surfaces

| Surface | Frozen value |
| --- | --- |
| Engine | `1.0.5` |
| Runtime | sandboxed WASM |
| Operation artifact | `stock-mind-v3`, SHA-256 `46f12690d4251e42584f5634b956844f7292498e485c99b231cb9ec1b8bc4aee` |
| Population artifact | `flow-intent-v1-2026-08-02`, SHA-256 `e945f8ad34ef350c5995a480d4793466a751aef1e5a32f29c045254583311f42` |
| Rules | `arc-relay-h0-01`, `f6d3ee9b1bb17d7bd8d0981941fd00a6a96f0e7ef834497d11924c06087174eb` |
| Map | `arc-relay-threefold-depth-counterflow-01`, `5ca7d1a1826791d736465d352c1558793846fc2e3df343d730f1df4c79f47e0c` |
| Loop profile | `depth-counterflow` |
| Eligibility bars | `balance/arc-relay-felt-degeneracy-bars-v3.json`, SHA-256 `7a32179220246997c37a40bcf9a5731f8ccc1c5f6cd50076439023274fe22ce4` |
| Counter taxonomy | `counter-taxonomy-v2.json`, SHA-256 `0b7fc8a9fb566c97bd54990538e1b8aa0c23e665b9b6b55791a8fbeda8dd08b1` |

Arc Relay topology and match-contract fingerprints depend on the ordered pair of
eight-class compositions. The first discovery launch exposed that the draft
harness had reused the old single-opponent proof fingerprint. The failed attempt
stopped before an outcome read. The generator now resolves every ordered pair
through `experiment arc-relay --print-contract`, without playing a match, and
freezes the resulting composition-sensitive fingerprints. Both manifest
generators reproduce byte-identically.

## Method

### Discovery

The tracked discovery manifest contains **240** cells:

- 10 operations;
- 8 authored counter candidates per operation;
- 3 deterministic campaign seeds (`32452843`, `49979687`, `86080201`);
- alternating operation side;
- all **32** members of the real counterflow sheet population represented;
- canonical replay verification and v3 felt-degeneracy scoring per cell.

The four-worker authoritative sweep verified **240/240** canonical replays in
`800.781 s`. All **240/240** were runtime- and cohort-eligible.

The final causal read inspected 1,213 operation activations. It found 418 success
activations, 208 committed-counter activations, 199 preparation denials, and 353
casualty recoveries. The original committed-only registration missed Lantern
Sweep and Hardlight Gate; the other eight passed without the amendment.

### Held-out confirmation

Before confirmation, two causally interacting real-population opponents were
frozen per operation and the taxonomy above was hashed into the manifest.
Confirmation used seeds `67867967` and `982451653`, neither present in discovery
or the original ten-operation proof. The 40-cell, four-worker WASM sweep completed
in `138.126 s`; **40/40** canonical replays verified and **40/40** passed every
eligibility bar.

| Operation | Success cells | Committed-counter cells | Preparation-denial cells | Casualty-recovery cells | Retained counter |
| --- | ---: | ---: | ---: | ---: | --- |
| Rear Hook | 4/4 | 4/4 | 4/4 | 4/4 | committed |
| Lantern Sweep | 4/4 | 0/4 | 4/4 | 4/4 | preparation denial |
| Fork Shadow | 4/4 | 2/4 | 2/4 | 3/4 | committed |
| Birth Rotation | 4/4 | 4/4 | 3/4 | 4/4 | committed |
| Escort Counterpunch | 4/4 | 1/4 | 3/4 | 3/4 | committed |
| Smoke Breach | 4/4 | 4/4 | 2/4 | 4/4 | committed |
| Hardlight Gate | 2/4 | 0/4 | 4/4 | 4/4 | preparation denial |
| Relay Catch | 2/4 | 4/4 | 0/4 | 4/4 | committed |
| Decoy Switch | 4/4 | 2/4 | 3/4 | 4/4 | committed |
| Emergency Exchange | 4/4 | 4/4 | 3/4 | 4/4 | committed |

The counts are match counts with at least one qualifying activation, not match
wins. An operation success and an opponent counter may occur in different
activation windows of the same match; the retained gallery deliberately selects
one such all-three match per operation.

## What the evidence requires

A success needs a real commitment, `mission-success`, all operation-specific
required signature actions, a release no later than the card's own recovery
deadline, non-operation commands from every released survivor, and match
eligibility.

A counter needs one of the two frozen causal categories above. A doctrine name,
match loss, non-activation, false trigger, uncontacted timeout, or damage alone is
never counter evidence.

Casualty recovery binds exact `(team, unit, life)` identities: a hostile
destruction must hit a claimed life, survivors must release to baseline, and the
destroyed stable unit must later issue a non-operation command under a new life.

Across confirmation there were 217 activations, 208 completed bounded releases,
and **zero stranded activations**. Median recovery was 2 ticks; the maximum was the
frozen 12-tick deadline. Two recoveries and seven still-active operations were
preempted by match termination. They are reported separately rather than called
stranding: the authoritative match ended before another operation tick existed.
Every retained proof uses an actual bounded release and has no such preemption.

## Felt-degeneracy and eligibility read

Every discovery and confirmation cell passed both teams through the existing v3
eligibility bars. Confirmation maxima were:

| Detector | Confirmation maximum | Bar | Trips |
| --- | ---: | ---: | ---: |
| Handoff ping-pong | 0 reversals | 3 | 0 |
| Sustained passivity | 6 quiet ticks in a 75-tick window | 60 | 0 |
| Formation freeze | 24 high-wait ticks in a 75-tick window | 60 | 0 |
| Same-life stuck carrier | 25 ticks | 30 | 0 |
| Home-carrier non-progress | 29 ticks | 30 | 0 |

The last value is close to the existing bar. It did not trip and was not moved to
improve this result. The retained scorecards expose every detector, both team
reads, and the exact frozen thresholds. “Convoy stall” is covered by the stuck
carrier and home-carrier non-progress detectors; repeated passing is separately
covered by handoff ping-pong.

## Outcome-visible 3D review

The generated hosted gallery lives at:

`sandbox/arc-relay-operation-counterplay-3d-review-v1/`

Unlike earlier blind galleries, its index intentionally states what is being
watched. Every card names the operation side and color, real-population opponent,
final winner and termination, success and release ticks, counter category and
release ticks, and casualty-to-baseline life/ticks. It contains one match per
operation, and every selected match contains success, counter, and casualty proof.

The production-browser smoke opened all ten pages. All ten mounted WebGL rather
than Canvas2D fallback, showed the score bug, advanced playback, and produced zero
page errors, console errors, or failed requests. The slowest cold-ready measurement
was `9,348 ms`; it includes the full production 3D asset load. The screenshot is
captured at Hardlight Gate's preparation-denial tick 284, not at an empty opening.

- [Labelled gallery index](../../art/reviews/arc-relay-operation-counterplay/index.png)
- [WebGL counter moment](../../art/reviews/arc-relay-operation-counterplay/first-operation-webgl.png)
- [Browser smoke record](../../art/reviews/arc-relay-operation-counterplay/smoke.json)

Serve locally with:

```sh
python3 scripts/serve-gallery.py 8947 \
  --directory sandbox/arc-relay-operation-counterplay-3d-review-v1
```

## Durable evidence and budgets

The retained package stores ten compact match records, broadcasts, scorecards,
exact causal activation excerpts, manifests, and analyzer reads under
`arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/`.

| Artifact | Total | Maximum | Budget |
| --- | ---: | ---: | ---: |
| Retained match records | 23,475 B | 2,375 B | 4 KiB / match |
| Retained broadcasts | 1,364,354 B | 164,315 B | 300 KiB / match |
| Gallery broadcast payload | 1,364,354 B | — | 8 MiB / gallery |

The retained tree regenerates byte-identically. A retained Hardlight Gate record
also regenerated its canonical replay hash
`29247eefbbf548c8815fbc038b2b1db9950f2cabd27896721ca9547237cee1c4`
through the normal WASM match-record path.

## Determinism and regression results

| Check | Result |
| --- | --- |
| Frozen Gate 3 canonical set | 6/6 regenerated and verified on WASM; 6/6 expected hashes byte-identical; 0 scratch replays retained |
| Full .NET suite | 1,881 passed, 0 failed, 83 environment-gated skips |
| Engine suite including DocDrift | 1,357/1,357 passed |
| Web suite | 381/381 passed |
| Production web + CLI viewer build | passed |
| Merged 3D fleet deterministic check | 16/16 GLBs; 5,464,772 B; 90,266 triangles |
| Counterplay trace unit tests | 5/5 passed |
| Manifest/retention integrity tests | 4/4 passed |
| Production gallery browser smoke | 10/10 labelled WebGL matches passed |

The six historical replay hashes remain byte-identical. This work does not change
canonical replay content or any contract-owned fingerprint.

## Reproduction

```sh
dotnet build BotArena.sln --configuration Debug --nologo
python3 scripts/generate-arc-relay-operation-counterplay.py
python3 scripts/arc-relay-sweep.py \
  --manifest arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/discovery-manifest.json \
  --output /tmp/nilbots-operation-counterplay-discovery --jobs 4 --keep-canonical
python3 scripts/generate-arc-relay-operation-confirmation.py
python3 scripts/arc-relay-sweep.py \
  --manifest arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/confirmation-manifest.json \
  --output /tmp/nilbots-operation-counterplay-confirmation --jobs 4 --keep-canonical
python3 scripts/arc-relay-operation-counterplay.py \
  --catalog arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/catalog.json \
  --manifest arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/confirmation-manifest.json \
  --sweep-output /tmp/nilbots-operation-counterplay-confirmation \
  --output arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/confirmation-read.json
python3 scripts/retain-arc-relay-operation-evidence.py \
  --read arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/confirmation-read.json \
  --manifest arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/confirmation-manifest.json \
  --output arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/retained
```

## Candid limits and next decision

- Lantern Sweep and Hardlight Gate have confirmed anticipatory counterplay but no
  observed committed counter in 28 discovery-plus-confirmation cells each. If the
  owner requires every operation to remain counterable after its lock, this pass
  does **not** meet that stronger requirement; those two cards need another design
  round.
- The population is the real 32-sheet evaluation cohort, but it is still first-party
  authored. This is not evidence against arbitrary future custom minds.
- Three discovery and two held-out confirmation seeds are meaningful deterministic
  coverage, not exhaustive state-space proof.
- The gallery labels the evidence outside the arena and gives exact seek ticks. The
  renderer does not visualize internal operation state; adding that would be a
  separate presentation/debug feature and was outside this no-renderer-change pass.
- No operation sheet or engine behavior changed, so all positive and negative reads
  describe the currently approved evaluation implementations rather than repaired
  variants.

Owner decision: accept preparation denial as real counterplay and review the ten
labelled matches, or require a further committed-counter design round specifically
for Lantern Sweep and Hardlight Gate. No player-facing operation UX should be built
from this evaluation schema without its separate design pass.
