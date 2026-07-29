# DX notes — march-wall

Written before seeing any opponent, standing, or aggregate result. Only my own
qualification report and my own self-play replays informed it.

## Identity

| | |
| --- | --- |
| Entrant | `march-wall` |
| Population / wave | Frontline Labs classes, wave 1 (`classes-wave-1-2026-07-29`) |
| Authoring lineage | `march-wall-v1` |
| Doctrine | ADVANCING WALL |
| Class | `bulwark` (declared in `botarena.json`) |
| Role | `verdict-doctrine` |
| Target | cumulative T4 |
| Budget | one authoring pass; mechanical contract repairs free; no open-ended strategic iteration |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `79ad08b6c4cc7c9494c9cd87bafbe5f2b9ca25ec97a1d380cd1f7cc46501df6a` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `42e12c66f3adc8628dfb505f9f403d8fd2ec3a150da140ebfd9e644bb6789a9a` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `676cb185b37ea82758b19ba110d4e1366cb0037d465e8777b2959c188dde77a4` |

## Frozen artifacts

| | |
| --- | --- |
| Submitted sources | `AnchorPlanner.cs`, `ContractView.cs`, `FireControl.cs`, `Geometry.cs`, `MarchWall.cs`, `Navigation.cs`, `Threat.cs` |
| Project metadata | `botarena.json`, `MarchWall.csproj` |
| Deterministic source-tree hash | `8f717b9fea1e01bc63a6a2d743a27f847370915f10c7702388aa213f99817dd7` (sha256 over the sorted sha256 list of `*.cs` + `botarena.json` + `*.csproj`) |
| Canonical WASM | `out/bot.wasm`, 3 271 341 bytes |
| **`out/bot.wasm` sha256** | **`cb7b182e707c886a0d3fc2492d8113d51b36e477d5285d8f9758f2750abb6eb9`** |
| Build cache key | `0b6ebfd256ffa94e0d12a7c0…` family; final build reported artifact hash identical to the sha256 above |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/guest 0.10.4, WASI p1 core module, platform-matched Docker builder on macOS arm64 |
| Qualification report | `evidence/t4/qualification.json`, sha256 `9caf6419f400399155182bdb4feffabe1d0149ecdfca8df7d803a7a3307237e0` |
| Verified probe replays | 21 replays under `evidence/t4/` (5 T4 probes, 5 T3 probes, 6 T2 probes incl. one determinism repeat) |

## Qualification outcome

`frontline-labs qualify --suite frontline-qualification-5`, profile
`frontline-duel-depth-union-t4-v1`, seed 104729, WASM runtime.

**Exit 0 — T4 awarded.** Prerequisite T3 PASS (which itself re-ran and
hash-linked T2). All five T4 probes PASS: `suppression-choke`,
`entry-initiative`, `prediction-chamber`, `front-rotation`, `map-holdout`.
`balanceEvidenceEligible` in the report body is `false`; the tier award is what
the packet's floor is stated against.

Two earlier revisions were archived by re-qualification rather than kept as
separate directories — noted here instead:

1. artifact `e2e6329fc805…` — T4 probes all PASS, T3 FAIL (`wall-terminated-bend`,
   `strict-corner`, `cooldown-window`) → **T2**;
2. artifact `e789a5460ed6…` — T4 and T3 all PASS, T2 `straight-evade` FAIL → **T1**;
3. artifact `cb7b182e707c…` — **T4** (frozen).

## Timings (Apple Silicon, warm)

- managed edit/compile loop: well under a second.
- in-process 500-tick self-play match: ~2 s including the diagnostic build.
- `build --no-cache` through the Docker builder: **11–13 s** each, three times.
- one full `qualify --suite frontline-qualification-5` run (22 WASM matches
  across three cumulative tiers): **~10 s wall, ~88 s CPU** at ~866 % — the
  suite parallelises well.
- total authoring pass, including three qualification cycles: comfortably under
  an hour.

## Repairs made (all mechanical contract handling)

Every fix below came from reading my own probe replays and self-play replays.
None of them changed the doctrine.

1. **Reserved own-team spawn anchors are not walkable, and nothing says so.**
   A companion that spawned at `team-0-child-2` walked one tile north and
   permanently trapped itself: the tile north was the *other* companion's
   assigned spawn anchor, the tile east was the Prime spawn, the tile west was
   a wall. It then alternated two blocked moves for 239 consecutive ticks. The
   rule card mentions the Prime spawn being "reserved against own child
   movement"; the class arm reserves *every* assigned spawn anchor, and the
   legality mask still reports `move` as `Available` with all four directions
   allowed, because availability deliberately cannot predict the joint step.
   Fixed by deriving the reserved set from `lifecycleAssignments[].assigned
   RespawnSpawnId` + `initialDeployment.spawns` joined to `map.spawnAnchors`,
   and excluding it from both pathfinding passes. Added a short-lived memory of
   any tile a `Blocked` outcome names, so no future surprise can produce a
   two-cycle either.
2. **Own projectiles were treated as movement blockers.** The collision block
   declares `alliedMovementDestinationOverride: pass-through-does-not-block-or-
   consume`, so a friendly bolt does not block us — but `projectilesBlockMovement`
   is `true` and it is easy to read the general flag and stop there. The result
   was a Prime that fired east down a one-wide corridor and then walked seven
   tiles around its own covering fire to reach a tile two steps away, which is
   exactly what the `cooldown-window` probe measures and fails.
3. **Speculative curved fire.** Both curved-shot probes (`wall-terminated-bend`,
   `strict-corner`) count curved attacks and their hits. My suppression logic
   was firing bends at tiles an enemy *might* step onto; those never connect
   against a stationary probe controller and drag the ratio to zero. Now a bend
   is only ever spent on a body I can actually reach; predictive fire is
   straight-only and additionally gated on the bolt not arriving before the
   target could.
4. **Projectile threat modelled as a ray, not as a sweep.** The first version
   asked "is this tile on the bolt's line within N ticks", which cannot tell the
   difference between a tile the bolt crosses *this* tick and one it reaches in
   three. In a one-wide corridor that is the whole game: by the time the naive
   test fires, both escape tiles are already inside the same sweep. Replaced
   with an explicit per-projectile walk over the tiles it will traverse within
   *k* advances, derived from `TicksUntilAdvance`, `TilesPerAdvance` and
   `RemainingTiles`; the one-advance sweep decides "must move now" and the
   two-advance sweep keeps ordinary movement out of a corridor it cannot leave.
   This is what turned `straight-evade` from fail to pass.

## Documentation gaps

- **Reserved placement is under-documented and cost the most time.** The rule
  card names only the Prime spawn. The class arm's assigned companion spawn
  anchors behave the same way and nothing in the rules card, the addendum, or
  the SDK doc comments says so. `SpawnAnchor` has no "reserved" flag either, so
  a bot has to infer the rule from lifecycle assignments. One sentence in the
  rules card, or a tile tag for it, would have removed a whole debugging cycle.
- **`Available` really does mean "individually legal"**, and the SDK says so
  clearly — but every failure mode above surfaces as `Available: true` followed
  by `Blocked`, so in practice the *only* signal a bot gets about permanent map
  restrictions is a repeated blocked outcome. A worked example in the template
  README of "what to do when a move keeps blocking" would be worth more than
  another paragraph of theory.
- **Allied vs hostile projectile interaction is spread across three fields.**
  `projectilesBlockMovement`, `alliedProjectileContact` and
  `alliedMovementDestinationOverride` jointly decide whether your own bolt is an
  obstacle. Reading only the first is a very natural mistake with an expensive,
  silent, positional cost.
- **The qualification profile's shot envelope differs from the published rules
  card in a load-bearing way.** `FRONTLINE-LABS-RULES.md` says a program "may
  offset initial aim by one 45-degree sector"; the T2–T4 profile ships
  `minInitialAimSteps: 0, maxInitialAimSteps: 0`, so facing *is* the aim and
  rotation becomes a real tactical action rather than a vision tweak. Reading
  it from the contract is correct and the packet says to; but a bot author who
  trusts the prose here writes a fire-control module that silently never fires.
- Probe reports name a `capabilityComponent` and pass/fail per probe, but the
  numeric fields have no published pass thresholds. Diagnosing `curvedAttack
  Count: 3, curvedProjectileHitCount: 1 → FAIL` meant inferring the criterion
  from the metric names. That inference was correct three times out of three,
  so the metric naming is good; stating the predicate would still be cheaper.

## Hardcoding temptations resisted

- The obvious one: the five objective regions and both home pads are printed as
  literal coordinates in the rules card, and the class arm's map is *nearly* but
  not exactly the same. Everything goes through `map.regions` / `map.tileTags`.
- "A turret is the form called `turret`" — false in the class arm, where it is
  `bulwark-child-turret` and `bulwark-prime-turret`. The bot instead calls a
  form fortified when its declared action mask contains no movement action,
  which also correctly classifies a form neither arm has invented yet.
- "Children unlock at 120 and 260" and "the Prime returns after 18" — both are
  in the rules card, both are read from lifecycle state instead.
- "Anchor comes from `transform`, mobilize from `mobilize`" — resolved as
  whichever same-life transitions run out of and back into this form.
- `maxTravelTiles`, cooldown, launch distance, bend bounds, strict corners:
  the T3 probes explicitly test declared range and cadence, so a hardcoded 8/2
  would have been caught, but it was tempting while the numbers agreed.

## Confusing terminology

- **"Life" versus "unit" versus "slot" versus "actor".** The model is coherent
  once you have it, but four words for adjacent concepts and a three-part
  `ActorIdentity` where only two parts survive destruction takes a careful read.
  `ObservedUnitSlot.State` being a seven-case closed union with two different
  "pending" families (`AvailabilityPending` vs `LifecyclePending`) is where I
  had to slow down most.
- **"Fabrication" names two different things.** `FabricationTransition` is the
  catalog rule; `FabricationPending` is a slot state; `fabricate` is an action;
  and the class addendum uses "fabrication" as a class identity. In the
  automatic-activation arm the *action* is gone but companions still appear.
- **"Objective weight"** reads like a scoring multiplier; it is really a
  presence/contest flag, and weight zero is the entire reason a turret is a
  denial tool rather than a capture tool. That is the single most
  doctrine-relevant number in the contract and its name undersells it.
- The `frontline-qualification-N` suite numbers and the `TN` tiers are offset by
  one (suite 3 → T2, suite 4 → T3, suite 5 → T4). Correctly and repeatedly
  documented, still momentarily jarring every time.

## Strategy passes

One, as budgeted. The doctrine ladder written in the first pass is the one that
shipped; the three qualification cycles changed only the four mechanical
behaviours listed above.

## Observations on the doctrine's own behaviour

Recorded from my own replays only, as evidence about the doctrine rather than
about any opponent:

- Against a passive sparring dummy I wrote for the purpose, march-wall breaches
  at tick 67 in the class arm and tick 81 on the base contract — the front
  rotation and the capture loop work end to end.
- The **bulwark class mirror is a genuine stalemate**: 0–0 territorial progress
  at the tick cap across seeds, with `claimingTeamId` never leaving null. Two
  five-health, three-cooldown, omnidirectional Primes sit on the centre
  objective and neither can be dislodged, while the two walls anchor at
  opposite lips of the same choke and shoot each other to simultaneous death on
  a ~20-tick cycle (six paired turret deaths in one 500-tick match). That is a
  property of the class pairing more than of the bot — the base-contract mirror
  of the same source is decisive and moves the front — but it is the sharpest
  signal I can offer the cohort from inside my own evidence: bulwark-vs-bulwark
  may need a tiebreaker or an asymmetry that this doctrine cannot supply.
- The wall's cost is legible: turrets cannot capture, so a match where both
  companions anchor puts the entire scoring burden on one mobile body. The
  guard that stops the last weighted body from anchoring is doing real work.
