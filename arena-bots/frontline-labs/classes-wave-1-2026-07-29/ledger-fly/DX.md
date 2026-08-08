# DX notes — ledger-fly (Frontline classes population, wave 1)

Written before seeing any other entrant, any standing, or any aggregate
balance result. The only outputs consulted are this project's own
qualification report and replays, plus one control run of the untouched
generated scaffold.

## Assignment and freeze identity

| Field | Value |
| --- | --- |
| Entrant | `ledger-fly` |
| Class | `fabricator` (declared in `botarena.json`) |
| Authoring lineage | `ledger-fly-v1` |
| Role | verdict-doctrine |
| Doctrine | attrition banker |
| Target | cumulative T4 |
| Budget | one authoring pass; mechanical repairs free |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `79ad08b6c4cc7c9494c9cd87bafbe5f2b9ca25ec97a1d380cd1f7cc46501df6a` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `42e12c66f3adc8628dfb505f9f403d8fd2ec3a150da140ebfd9e644bb6789a9a` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `676cb185b37ea82758b19ba110d4e1366cb0037d465e8777b2959c188dde77a4` |
| Source-tree hash | `4ee8e2290d5961d8c13ec9a3f8fd65859363c7f0e2c8372d0fb03d866d40e522` |
| Toolchain | nilbots CLI 0.9.7, SDK 0.10.4, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, WASI p1 core module, platform-matched Docker builder (Apple Silicon) |
| Build cache key | `dee7bfd819d606119ca7764981ffdf212db3f471ae1e2852ffa6f0ca647cebd9` |
| **`out/bot.wasm` sha256** | **`337c8b37efb378a852bf527cf6f09594fac4f5d94d3a322f188d9d1ec19e75c6`** (3,266,524 bytes) |
| Qualification | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, exit **0**, tier **T4**, `balanceEvidenceEligible: true` |
| `evidence/t4/qualification.json` sha256 | `131222abdc6574c6f8b1c15a6a3cfe45d31245c4fc1a65d26bcdb4afb7f40bee` |
| T3 prerequisite report sha256 | `597ac4f36aeac9e5da6066bc78ed41bd1fa6d7fbad8a8e50c0a4741db2aefb68` |
| T2 prerequisite report sha256 | `3bf7073b2d72c5835f63032a96d244fb1167501581bcb24c6cb591a19af9b491` |
| Verified probe replays | 36 under `evidence/t4/` |

Source-tree hash is sha256 over the sorted submitted files
(`FabricationRoute.cs`, `Field.cs`, `Gunnery.cs`, `Ledger.cs`, `LedgerFly.cs`,
`LedgerFly.csproj`, `MatchLens.cs`, `botarena.json`), each contributed as
name, NUL, big-endian 8-byte length, bytes.

Every probe passed on the first canonical build: T2 `contract-matrix`,
`automatic-life-cycle`, `objective-path`, `direct-fire`, `straight-evade`,
`manual-fabrication`; T3 `wall-terminated-bend`, `strict-corner`,
`cadence-parity`, `cooldown-window`, `local-form-safety`; T4
`suppression-choke`, `entry-initiative`, `prediction-chamber`,
`front-rotation`, `map-holdout`.

## Time

| Step | Wall time |
| --- | --- |
| `dotnet build` of the editing project | 0.5 s |
| in-process class mirror, 500 ticks, seed 7 | ~1 s |
| cold `nilbots build . --no-cache` (NativeAOT-LLVM via Docker) | ~2.5–3 min |
| repeat `nilbots build .` (content cache hit) | 0.04 s |
| full cumulative suite-5 qualification (T2+T3+T4, both assignments, WASM) | 10.7 s wall / 90 s CPU |

The inner loop is genuinely fast. The only slow step is the one canonical
build, and it is paid once.

## Repairs

**Zero mechanical repairs after building.** The first canonical WASM artifact
qualified T4 on the first attempt.

Two mechanical faults *were* fixed before building, because I first qualified
the untouched generated scaffold as a control. The scaffold reaches T2 and
fails T3 on `wall-terminated-bend` and `cooldown-window`. Both failures come
from the sample helper rather than from strategy, and both are worth reporting
as scaffold bugs:

1. **`ArenaBasics.Occupied` treats our own bolts as obstacles.** It unions
   every entry of `VisibleProjectiles` into the blocked set, but
   `CollisionRules.AlliedMovementDestinationOverride` is
   `pass-through-does-not-block-or-consume`. In the one-tile corridor on row 7
   this pins the body: it fires east, then paths *west* because its own bolt
   looks like a wall, then paths back east, forever. That is precisely the
   `cooldown-window` failure — the scaffold never closes on the objective
   during the enemy's cooldown because it is busy dodging itself. The sample
   code teaches a pattern that the contract contradicts two fields down.
2. **`ArenaBasics.Wait` throws.** It raises `InvalidOperationException` when no
   `wait` legality is available. The packet forbids deliberate faults, and the
   scaffold ships one on a reachable path. LedgerFly replaces it with a
   fallback that walks the legality mask and always yields a legal action.

The scaffold also never fires a curved shot, which is why
`wall-terminated-bend` fails: with `minInitialAimSteps == maxInitialAimSteps ==
0` on the union contract, `TryDirectShot`'s aim-offset branch can never
produce a non-straight solution, so an enemy two east and one north of a body
facing east is simply never engaged.

## Strategy passes

One. The decision ladder was written once, from (a) the resolved class
contract dumped out of a throwaway mirror replay and (b) the control run's
probe metric names. In-process mirror runs on seed 7 were used only for sanity
(no faults, fabrication fires, bodies do not deadlock), not for tuning. No
constant in the bot was fitted against a probe.

One doctrine/qualification tension is worth recording for the cohort. The
brief says the prime should "contest the objective with children, never with
the prime until the endgame is decided", and that is literally unqualifiable:
several probes hand the bot only its economy anchor and gate on objective
entry and consecutive capture ticks. I resolved it by making the hold-back
conditional on the front actually being manned — the bank steps up when no
companion is within two tiles of the active objective. That is the rule the
doctrine wanted anyway (a bank with nothing lent out has no reason to sit on
its hands), but the resolution was forced by the suite, not chosen freely.
Anyone reading LedgerFly's replays as evidence about "primes that never
contest" should know that qualifier.

## Hardcoding temptations

Every one of these was tempting and every one would have been wrong:

- **Map geometry.** The rules card prints the 23×15 map, both spawn tiles, and
  all five objective regions as literal coordinates. Pasting them is a
  five-minute shortcut. It breaks on the `map-holdout` probe (thin-fronts is a
  different map) and it breaks on the class arm, which keeps the same tile
  layout under a *different* map ID and fingerprint. `Map.Regions`,
  `Map.SpawnAnchors`, and `Map.TileTags` are the only safe sources.
- **Unlock ticks.** 120/260 in the hosted card, 60/180 on the class arm, 0 in
  the `manual-fabrication` probe. Three different answers for the same
  sentence in three permitted documents. `LifecycleAssignment.UnlockTick` plus
  the `UnitSlotState` machine are the only durable read.
- **"The prime is unit 0."** Almost true and completely unusable: the doctrine
  needs "which slot is the economy anchor", including for the *enemy* team, and
  probes deal participant IDs 7 and 19. The durable predicate is
  `AssignedRespawnSpawnId != null`, or a lifecycle profile with a non-null
  `AutomaticReturnFormId`. That test also identifies the opposing bank, which
  is what makes "shoot their economy first" expressible.
- **Numeric action codes.** `shoot` is 4 on the union contract; the class arm
  has no `shoot` at all and its `shoot-straight` is 105; `fabricate` is 100 in
  both. Codes are stable *per contract*, not across arms. Only the per-tick
  legality pairing is safe.
- **Shot-program bounds.** The rules card describes "offset by one 45-degree
  sector and bend left or right after 1–4 tiles, every 1–3 tiles, for 1–3
  bends". The union contract actually pins aim offsets to 0, `bendEveryTiles`
  to 1, and `bendCount` to 1, allowing only `bendAfterTiles` 1–4; the class arm
  disables shot programs entirely. Coding the prose would have produced
  rejected payloads on one arm and unreachable code on the other.
- **Damage, health, range, cooldown.** 3 HP and range 8 in the card; the
  fabricator prime has 2 HP and range 7 and its child has 3. Read
  `Forms` / `AttackProfiles`, including for the enemy's visible `FormId` — the
  addendum is explicit that stat-based counters generalise to classes that do
  not exist yet, and that turned out to be the cheapest correct choice anyway.

## Documentation gaps

- **The qualification contract is an undocumented third thing.** The rules card
  describes hosted v1, the addendum describes the class arm, and the suites run
  a *duel-depth union* profile that is neither. It has `transform` and
  `shoot-direction` but sometimes no `fabricate` and never `split`; children are
  sometimes automatic and sometimes explicit; unlock ticks are 0. No permitted
  document describes it beyond one clause per suite. I learned its shape by
  reading `header.contract` out of my own control run's replays. One paragraph
  saying "the union profile turns fabrication and automatic activation on in
  different variants; write to the legality mask" would save every author a
  cycle.
- **Probes report metrics but never state their gate.** `cooldown-window`
  reports `objectiveDistanceAtControllerAttack`,
  `minimumObjectiveDistanceDuringCooldown`, `damageDealtDuringCooldown`, and
  `maxConsecutiveCaptureTicks`, and says only "passed: false". The intent is
  guessable from the names, which is the same as saying the report is a puzzle.
  A one-line `failureReason` per case would be pure profit and would not leak
  any holdout.
- **`ShotPaths.Preview` is undiscoverable.** The SDK ships an exact local
  replay of the engine's bend rule — the single most useful authoring
  affordance in the whole API — and neither the template README nor the rules
  card mentions it. I found it by reading `ProgrammedShots.cs` while looking
  for the `ProjectileHeading` enum. (LedgerFly ends up with its own walker
  because it also needs actor stops, but the existence of a canonical preview
  is the thing worth advertising.)
- **`--print-candidate-contract` does not print the candidate contract.** It
  prints eight fingerprints and a ruleset ID. To actually read a resolved class
  contract you must run a throwaway match and dig `header.contract` out of the
  replay JSON. The flag name promises the thing the author needs and delivers
  the thing the archivist needs.
- **No worked example of reading a fabrication route.** Placement is fully
  data-declared — source region role, output region role, required and
  forbidden tile tags, ordered facing-relative candidate offsets, and a
  `positionSelection` policy of `first-eligible-declared-offset` — and getting
  it right is what makes "place the replacement where the exchange happened"
  possible at all. There is no example anywhere of turning those five fields
  into a predicted tile. `FabricationRoute.cs` in this project is offered as
  one.

## Confusing terminology

- **Slot / life / form / "Prime".** `ActorIdentity` is `(teamId, unitId,
  lifeId)`; the docs say "the Prime" for something that is really a stable unit
  slot with an automatic-return lifecycle whose current life happens to be in a
  particular form. Three concepts, one word, and the word never appears in the
  contract.
- **`fabrication-source` vs `fabrication-output` vs `spawn-protected`.** On the
  union arm both roles resolve to the home pad and both *require* the
  `spawn-protected` tag. On the class arm both roles resolve to a region named
  `fabrication-source-anywhere` and the output *forbids* `spawn-protected`.
  Identical field names, opposite meanings, and the region ID reads like a
  policy statement when it is just a name. The tags and the role bindings are
  the truth; the region names are decoration.
- **`Available` vs `AllowedByForm`.** The distinction is clear once read, but
  the load-bearing sentence — availability "deliberately cannot promise the
  result of simultaneous physical resolution" — is buried mid-paragraph in the
  Actions section. It deserves to be a callout, because every author's first
  instinct is to treat `Available` as "this will work".
- **`SameLifeTransition` / `transform` / "Anchor".** Contract kind, action ID,
  and player-facing name for one mechanic. Grep for "anchor" in the SDK and you
  find nothing.
- **Suite number versus tier label.** Suite 3 awards T2, suite 4 awards T3,
  suite 5 awards T4. The permanent off-by-one made me reason about the wrong
  prerequisite twice. The report's own nesting (`prerequisite-t3/` containing
  `frontline-qualification-4`) is correct and, read quickly, looks like a bug.
- **"Objective weight" versus "capture".** Weight 0 means a turret cannot
  contest, which reads as a stat but is a hard rule. Relevant to my doctrine
  only as a reason never to Anchor, but it took a second reading to be sure the
  turret does not merely capture *slowly*.

## What I would want next

A `--explain` mode on `qualify` that prints, per failed case, the analyzer's
gate expression. Nothing else in this toolchain cost me real time.
