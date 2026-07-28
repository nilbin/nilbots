# Frontline Labs mechanics calibration v1

Framework-owned deterministic probes for the immutable `frontline-labs-1`
contract. These bots are calibration fixtures, not balance contenders: they
wait for the contract to expose the target mechanic, exercise it once (or, for
turret fire, repeatedly), and otherwise wait.

This kit is independent of `baseline-v1-2026-07-28`; no baseline source or
artifact was modified.

## Probes

### `split-probe`

`SplitProbe` discovers the contract's
`SplitReplicationTransition`, waits until its own observation contains a
Ready allied slot and the transition action is available, then submits the
negotiated action ID/code. Replication descendants wait.

This proves:

- an initial generation-0 Prime can successfully Split with one additional
  Ready slot because the contract reuses the source slot for one descendant;
- the source life retires and two fresh `replica-mobile` lives start on the
  next tick;
- action codes, transition IDs, descendant count/form, placements, and slot
  use come from the injected contract/legality state rather than copied Labs
  constants.

### `fabricate-anchor-fire-probe`

`FabricateAnchorFireProbe` keeps the initial Prime on its authored source
region, discovers the bounded fabrication transition, selects the first Ready
target allowed by the current `UnitTargetConstraint`, and Fabricates. A fresh
fabricated life identifies itself from `StartLife.Origin`, derives its Anchor
transition from the fabrication output form, pathfinds across the injected map
to the nearest tile satisfying the transition's required/forbidden tile tags,
transforms, then derives the turret attack and absolute heading from the target
form/action catalog and current `ProjectileHeadingConstraint`.

This proves:

- successful Prime Fabrication from the home/source region;
- next-tick creation of a fresh `child-mobile`;
- child navigation off the protected/transition-forbidden pad to a legal tile;
- successful same-life Anchor completion with preserved actor identity;
- successful `shoot-direction` decisions by the resulting turret.

Neither probe reads Frontline objective state. The mechanics layer depends on
generic topology, lifecycle state, action legality, transition catalogs, map
geometry/tags, and life origin only.

## Final controlled WASM evidence

Both final artifacts were cold-built with CLI `0.9.3`, SDK/Guest `0.10.2`,
NativeAOT-LLVM `10.0.0-rc.1.26306.1`, and the generic actor runtime. The
working tree was based on commit
`3286d0d29fd15afd44a8c10427cce37c09d9e6a7`; no commit was created for this
kit.

The exact contract fingerprints in both runs are:

- rules:
  `ab63d409b682ad32fdb816c13cc3271413c2d0f6b1937e4933b6e455ff5d2593`
- map:
  `e9e75c1366111c857c3af9b32828185ea7b937d7f176bf4e8843f4b550ed2d91`
- match:
  `cf10fe4929d8cd11cace95e62b07d9732fbd1549dc2e9fe096f78605028ca837`

| Evidence | Assignment | Exact proof | Canonical replay hash |
| --- | --- | --- | --- |
| `evidence/final-wasm-sdk-0.10.2-seed-104729/` | Split team 0; lifecycle team 1 | Both unit-1 slots are Ready before tick 120. Split and Fabricate both succeed at 120. At 121, two replicas spawn at `(2,6)`/`(2,8)` and the child at `(21,7)`. The child moves through ticks 121-127 to legal tile `(18,3)`, anchors at 128, and successfully fires heading 0 at 129. Zero runtime faults; canonical v3 verification passes. | `42251748c9c7673ede90f440addfb81a806fc5d9092cd13b99a86620ef9b5afd` |
| `evidence/final-wasm-sdk-0.10.2-seed-104729-swapped/` | Lifecycle team 0; Split team 1 | Both actions again succeed at 120. Replicas spawn at `(20,8)`/`(20,6)` and the child at `(1,7)` on tick 121. The child reaches legal tile `(4,3)` at 127, anchors at 128, and successfully fires heading 0 at 129. Zero runtime faults; canonical v3 verification passes. | `6a59d91e6131afe8ed88c2cafa307cda8b28c2bc50fd6b4e5513d9e6076d70fd` |

Both matches intentionally end as zero-territory max-tick draws. Match outcome
is irrelevant to this kit; the accepted action and lifecycle/form/attack
chronology is the calibration evidence.

Reproduce from the repository root:

```bash
dotnet build src/BotArena.Cli/BotArena.Cli.csproj

dotnet run --project src/BotArena.Cli --no-build -- build \
  arena-bots/frontline-labs/mechanics-calibration-v1-2026-07-28/split-probe \
  --no-cache
dotnet run --project src/BotArena.Cli --no-build -- build \
  arena-bots/frontline-labs/mechanics-calibration-v1-2026-07-28/fabricate-anchor-fire-probe \
  --no-cache

dotnet run --project src/BotArena.Cli --no-build -- \
  experiment frontline-labs \
  --bot arena-bots/frontline-labs/mechanics-calibration-v1-2026-07-28/split-probe/out/bot.wasm \
  --opponent arena-bots/frontline-labs/mechanics-calibration-v1-2026-07-28/fabricate-anchor-fire-probe/out/bot.wasm \
  --seed 104729 \
  --out arena-bots/frontline-labs/mechanics-calibration-v1-2026-07-28/evidence/reproduction
```

Add `--swap` and use a separate output directory for the mirrored proof.

## Final hashes

| File | SHA-256 |
| --- | --- |
| `split-probe/SplitProbe.cs` | `e672a630fa4c8f5581c5b7002fe1e0bb7adae792f97ef4be79aafc24a4760f0e` |
| `split-probe/bot.wasm` | `5a9bee10052eda0423b1ddb4abb4bad63ec8f2b4044c295d23031c9efa6db57e` |
| `fabricate-anchor-fire-probe/FabricateAnchorFireProbe.cs` | `bef25b76a6e961bea6f13064725b9d0d63c53caac9fdbb6c402953498e155001` |
| `fabricate-anchor-fire-probe/bot.wasm` | `2413e1fed46df9b44ffcf2c99782973d8a695df74ec432c155fe63f85898db28` |
| final normal `replay.json` bytes | `b262bc73bdedfdcd0fdefe466687122d0aa650041d0a437a25c7770334416113` |
| final swapped `replay.json` bytes | `f0ce8bb7fd7d33f19586f39dff23a82e906f2d9cb2ac113842a116db8ff590ce` |

The controlled build cache keys were
`b65fdb4e622568a9bb2249d76b2909b635a3dab831788296cc8c9820365f0b53`
for Split and
`1dc2fd7e66667353aa95ceae62067a1fccd61882f495f2493636dcf41177e3c6`
for Fabricate/Anchor/Fire.

`SHA256SUMS` is the content-addressed manifest for the complete local evidence
archive, including historical revisions and generated viewers that are too
large to track in ordinary Git. The two final replay JSON files, final probe
WASM files, sources, and manifests are tracked directly.

## Preserved diagnostic history

The first calibration attempt caught the exact same-tick Anchor decoder defect
that prompted SDK/Guest `0.10.1`. History is retained instead of overwritten:

- `evidence/normal/` and `evidence/diagnostic-in-process/` are the original
  SDK `0.10.0`-era failures at tick 129 (canonical replay hashes
  `61b4822b...` and `dd2bdf34...`).
- `evidence/wasm-seed-104729/` repeats the failure with the simplified turret
  heading extraction. Its CLI diagnostic identifies
  `ArgumentOutOfRangeException(dueTick)` for both allied observers at tick 129;
  replay hash `d1e04533...`.
- The failing replay itself shows valid completed Anchor events with
  `startedTick == dueTick == 128`. SDK `0.10.0` rejected that chronology while
  decoding tick-129 visible events; SDK `0.10.1` accepts it while retaining the
  stricter future-due rule for pending transition state.
- `evidence/current-wasm-seed-104729{,-swapped}/` are successful interim runs
  made after rebuilding the corrected local SDK source but before the public
  SDK version was bumped from `0.10.0` to `0.10.1`.
- `evidence/final-wasm-sdk-0.10.1-seed-104729{,-swapped}/` and the
  `revision-03-interim-sdk-0.10.1` /
  `revision-04-interim-sdk-0.10.1` WASMs preserve the next complete
  calibration pair. They were superseded by SDK `0.10.2`, whose observation
  contract correctly redacts parent and operation lineage for hidden enemy
  transition spawns.
- Every overwritten WASM revision is retained under each probe's
  `revisions/`. Source snapshots use `.cs.txt` so the controlled builder does
  not compile archived classes as additional entry sources.

Archived artifact SHA-256 values:

- Split stale SDK: `4dad0bea...`; Split corrected-source/interim-0.10.0:
  `bb9a7f26...`.
- Lifecycle original-source/stale SDK: `11a3ebd0...`; simplified-source/stale
  SDK: `83936738...`; corrected-source/interim-0.10.0: `c0f8ef88...`.

## Limits

- These fixtures validate accepted mechanics and chronology, not tactical
  usefulness, balance, targeting quality, projectile hits, objective play, or
  match termination pacing.
- The turret deliberately selects the first currently allowed absolute
  heading; it proves directional fire, not aim at an enemy.
- The child pathfinder uses the complete static map and declared transition
  tile tags. It excludes currently observed occupied tiles, but it is not a
  general hidden-enemy navigation policy.
- The probes select the first compatible transition in ordinal contract order.
  A future contract with several semantically distinct Split/Fabricate/Anchor
  transitions would merit one probe per transition.
- Open-tile pathfinding reads the current generation-3 map's `#` wall symbol.
  The contract does not currently expose a typed tile-collision query.

## DX findings

1. **Fixed by SDK/Guest 0.10.1:** completed one-tick form-transition events
   legitimately have `startedTick == dueTick`. A stale SDK treated equality as
   invalid while decoding visible events, causing every allied observer to
   fault immediately after Anchor.
2. **Fixed by SDK/Guest 0.10.2:** hidden enemy transition spawns must redact
   both parent identity and operation lineage before constructing the public
   observation payload. The 0.10.1 artifacts/replays remain archived as the
   immediately preceding contract generation.
3. **Stale `--no-build` binaries are hazardous during contract work:** source
   already contained the equality fix while the previously built CLI/SDK still
   rejected it. The old CLI initially surfaced only `tick-execution-failed`;
   rebuilding exposed the useful `ArgumentOutOfRangeException(dueTick)`.
   Always check `nilbots --version` and rebuild the CLI before freezing
   controlled artifacts.
4. **Revision archival inside a bot project is awkward:** both normal MSBuild
   and the controlled builder discover nested `.cs` files. A preserved source
   snapshot caused duplicate-type compilation until it was renamed
   `.cs.txt`.
5. **Transition placement is exact but low-level:** authors must manually join
   `SameLifeTransition.Placement` tag requirements to `Map.TileTags` and write
   their own path search. A read-only SDK helper for “does this tile satisfy
   this transition?” would reduce error-prone boilerplate without changing
   mechanics.
