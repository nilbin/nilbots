# Arc Relay ten-operation live proof

Date: 2026-08-02

Branch: `codex/game-redesign`

## DECISION NEEDED

Accept or reject these ten cards as proof that the bounded team-operation
grammar is mechanically viable. Recommended: accept the grammar proof and
keep all ten as evaluation examples. Do not treat this single-seed existence
proof as evidence that the cards are balanced, reliable across populations,
fun, or ready for a player-facing editor.

## RESULT

All ten distinct operations completed a qualifying activation in an
authoritative WASM match on the fixed, predeclared seed `86080201`:

- causal preparation evidence and an atomic actor claim;
- one committed branch that never flipped;
- `mission-success` in the live operation state machine;
- any card-specific action in that same activation;
- bounded physical recovery; and
- ordinary non-operation role tags from surviving participants after release.

The corpus covers exact-Core rear ambush, reactive route cutoff, information
probe and route choice, public-timing rotation, escort counterpunch, smoke
breach, hardlight return gate, a real in-flight Relay handoff, spatial feint,
and emergency Switchback extraction. The detailed new-player rules are in
[`README.md`](../../arena-bots/arc-relay/intelligent-operation-proof-v1-2026-08-02/README.md).

The implementation also fixes three general coordination defects found by the
live runs:

- exact-carrier operations pursue the causally bound carrier and its remembered
  position rather than switching to an unrelated visible carrier;
- one operation task may anchor to another operation task, allowing a receiver
  to follow its assigned carrier instead of a coarse theater; and
- an operation can remember a participant's issued signature action, so
  success can require both the action and its strategic result. Arc Toss's
  one-tick in-flight possession gap is no longer mistaken for a lost Core.

No fog, rendering, Arc Relay rule, map, canonical replay semantic, frozen
artifact, player-facing sheet/editor schema, backend, or web surface changed.
This is not a fun or balance claim.

## EVIDENCE

### 1. Ten successful live operations

All rows used `stock-mind-v3` versus the same artifact with the baseline-only
sheet, `depth-counterflow`, WASM, and seed `86080201`. Every canonical replay
passed `nilbots verify`; both proof runtime and team eligibility are enforced
by the harness.

| Operation | Prepare | Commit | Required live act | Success | Baseline release | Canonical replay hash |
| --- | ---: | ---: | --- | ---: | ---: | --- |
| Rear Hook | 71 | 72 | `tractor-hook` 80 | 86 | 98 | `3a342168d014b857ba337e9d3f982a2c1466d31c32dfb62b59b58df5a7c79612` |
| Lantern Sweep | 134 | 135 | — | 136 | 137 | `faaca66086ed805d30893c3c4c3aa9d1a0f65610f726d584973f199f1c549321` |
| Fork Shadow | 251 | 253 | `tractor-hook` 254/255/266/268 | 276 | 288 | `7b60b97e3694c4dd58f0a7df72a6395c005f7170550c5bb5eee2869fdb790bd9` |
| Birth Rotation | 205 | 209 | — | 216 | 218 | `5f5bcd43f23f51ecdf175a3f746de4a93e6bb19e1216cd9273efdd1c67bce6d2` |
| Escort Counterpunch | 26 | 28 | — | 29 | 35 | `35a54684272358119d2a12df6a13b37e5d261e6fae47bad958db68658cf33879` |
| Smoke Breach | 55 | 62 | `smoke-canister` 55/73 | 79 | 83 | `94bc4285a1ea0d055c0a7387be5ed81e08f087beb7135de7bf1764387dac67fe` |
| Hardlight Gate | 163 | 169 | `hardlight-block` 167 | 170 | 171 | `cbcd76f2dc057f1fa1ed0618bd8feb91160e8a04f2d79da16832110f8df50dd4` |
| Relay Catch | 432 | 433 | `arc-toss` 434 | 436 | 441 | `21c1c0238bf63a4330a42ecb5555deb73c664c23323335bfe0c34ee9c891875b` |
| Decoy Switch | 56 | 62 | — | 67 | 70 | `4b650dab14327561c9f450cc3c569359259b8380059ece37514f95e6a676488b` |
| Emergency Exchange | 79 | 80 | `exchange` 88 | 89 | 93 | `77d1ca98bacc914e643120391b79cb0bfd6dd3df6579a460bd27ab204ff3c119` |

The compact machine-readable receipt is
[`live-proof-summary.json`](../../arena-bots/arc-relay/intelligent-operation-proof-v1-2026-08-02/evidence/live-proof-summary.json).
Canonical replays are regenerated rather than committed.

### 2. Automated proof harness

`scripts/arc-relay-operation-proof.py prove` runs the catalog concurrently,
uses `scripts/arc-relay-match.py` for authoritative WASM records and bounded
broadcasts, verifies every canonical replay, inspects the complete transition
and command trace, and fails the run unless all cards qualify. Inputs are
hashed per cell; `--resume` only reuses a cell when artifact, operation sheet,
baseline sheet, seed, runtime/map/team, catalog, proof harness, match harness,
and built CLI still match.

The final fresh run reported `10/10`, zero resumed cells, artifact
`46f12690d4251e42584f5634b956844f7292498e485c99b231cb9ec1b8bc4aee`,
and ten verified canonical replays. An immediate `--resume` audit reused all
ten matching cells and re-ran replay verification and trace acceptance.

### 3. Hostile recovery coverage

`IntelligentOperationMachineTests` now has ten adversarial cases. The original
eight cover essential loss, same-tick success/loss ordering, causal unknown,
edge re-arm, scout loss, ordered branch locking, preparation-only replacement,
and priority/preemption. Two new cases prove that an optional committed strike
group can continue after one partner dies and that an unreachable recovery
route still reaches deadline, clears all claims, and releases survivors to
baseline.

Targeted interpreter/linker result: `10 passed, 0 failed`.

### 4. Determinism and regression

- Historical Gate 3 golden sweep: `6/6` expected canonical hashes matched
  byte-identically on WASM in `33.176 s` with three workers; all six replays
  verified and zero canonical replay files remained after pruning.
- Full solution: `1,881 passed`, `83` environment-gated skips, `0 failed`.
- `stock-mind-v3` build: `0` warnings, `0` errors.
- Controlled build: cold cache miss followed by a cache hit on key
  `31f5da0fa1dda794ddfb7414147b24033cc30d3402626024b6493fc092bb2a5d`;
  both produced the same artifact hash.

### 5. Size and delivery

| Item | Measurement |
| --- | ---: |
| v3 WASM | `4,472,912 B` |
| WASM delta from the prior two-card slice | `+15,597 B` (`+0.35%`) |
| baseline plus ten generated JSON sheets | `221,419 B` |
| largest operation sheet | `24,240 B` |
| ten match records, total / max | `26,414 B` / `2,659 B` |
| ten broadcasts, total / max | `1,326,726 B` / `158,012 B` |

Every record remains below `4 KiB`, every broadcast below `300 KiB`, and every
durable match below `304 KiB`. A new sheet variant still changes data only;
the frozen algorithm artifact does not rebuild.

## NEXT

Owner review of the ten concrete rules. If accepted, the next separate goal is
either a multi-seed/population reliability and counterplay audit or the
player-facing sheet/preview UX pass. The latter must translate these
evaluation cards into human authoring concepts rather than exposing this
provisional JSON. No further implementation is implied by this report.
