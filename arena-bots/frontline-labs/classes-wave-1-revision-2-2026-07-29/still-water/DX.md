# DX notes — Still Water, revision 2

Written from this entrant's own authoring session, its own self-play, its own
qualification report, and its own factorial replays. No other entrant's source,
standings, or aggregate report was opened.

## Identity

| Field | Value |
| --- | --- |
| Entrant | `still-water` |
| Authoring lineage | `still-water-v1` |
| Revision | 2 (one budgeted improvement pass) |
| Class | `striker` (declared in `botarena.json`) |
| Role | `verdict-doctrine` |
| Doctrine | patient interceptor |
| Target tier | cumulative T4 (`frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`) |
| Budget | one strategic revision; mechanical/contract repairs free |
| Predecessor | `arena-bots/frontline-labs/classes-wave-1-2026-07-29/still-water` (frozen, untouched) |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `dcf1f4a25c43741af68c2ef77391858f9a7f88e2b23fec16cd871d4be9fd80c5` |
| Rule card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `3fb217bbf9ad1e181c103ebf19cd4b56ed1e8d38c54343fdc5cc7e6531b1aedf` |
| Starter helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `9cae31d594188f46403887578c2347307cfecd018b2a9d84e06fc82c09afb194` (vendored byte-identical) |

## Frozen artifacts

| Item | Value |
| --- | --- |
| `out/bot.wasm` sha256 | `b0fe1f36708df1c62a117dfe57f8506c6d59abea01770e2c78618d2a6712e289` |
| `out/bot.wasm` size | 3,230,495 bytes |
| `evidence/t4/qualification.json` sha256 | `6ae46552ff2518d0eb44680e6e1d1831b697f2fc29e14fdf22336ad1cca72262` |
| `evidence/t4/prerequisite-t3/qualification.json` sha256 | `e3893a7074702e57dbc307da728ce6b53c82888d91d55eb7623c34c3344276e2` |
| `evidence/t4/prerequisite-t3/prerequisite-t2/qualification.json` sha256 | `6d716bb891914fa397411d2534200f8b707f22c46a7ff66f1e2c5d49d3126b4d` |
| Deterministic source-tree hash | `4f9b55bc316b15cdac6c0d524fd829ed6e89c64686b66d1c1cd3cffacc0e07f3` (sha256 of the sorted per-file sha256 listing of all `.cs` + `.csproj` + `botarena.json`, excluding `bin/` and `obj/`) |
| Toolchain | controlled `botarena build --no-cache`, nilbots CLI 0.9.7, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK `0.10.4`, game rules 0.5, WASI p1 core module, macOS host via the platform-matched Docker builder |

Per-file source hashes at freeze:

```
513fc5d6f70403af07e97702a3c65ce8eb1f1a38b37ed8f2eeecc79e970da32f  ActionBook.cs
9cae31d594188f46403887578c2347307cfecd018b2a9d84e06fc82c09afb194  ArenaBasics.cs
117ebab2b09f03a4cd65d954a2213117be76c8298a74500daf9cb9ebe315042a  Doctrine.cs
df05fd11c3f1efa2dc032eebc9a11f65478aee5dc61e3447d2115935dedb12a6  Field.cs
e0c4c46870c238900ac0b15aac95a1dd511ccf4ee1419c8888eb3d6217218675  ForkPlanner.cs
be10b13da349a9212461d432a9dff0469c90abf5b990df7bb5ce763e8b16f502  Quarry.cs
fca6a078224a7407b5a955e8a71b11115471e5f61284ab1d5efaa40aea535112  StillWater.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  StillWater.csproj
c4d2afe4080bff8e0848b8fec79ac40401c72b502455386c608e74e7d56b6e81  ThreatField.cs
bc4e5dd51f3eb957982536925da6640a314b5e4631a870463ed38e1624f92d3c  botarena.json
```

`ActionBook.cs`, `Field.cs` and `botarena.json` are byte-identical to the v1
freeze; `ArenaBasics.cs` is byte-identical to the current template.

## Qualification outcome

`experiment frontline-labs qualify --suite frontline-qualification-5` exits
**0**. **Tier awarded: T4**, `profileComplete: true`,
`balanceEvidenceEligible: true`.

| Level | Component | v1 | v2 |
| --- | --- | --- | --- |
| T4 | suppression-choke | PASS | PASS |
| T4 | entry-initiative | PASS | PASS |
| T4 | prediction-chamber | PASS | PASS |
| T4 | front-rotation | PASS | PASS |
| T4 | map-holdout (thin-fronts) | PASS | PASS |
| T3 | wall-terminated-bend | PASS | PASS |
| T3 | strict-corner | **FAIL** | **PASS** |
| T3 | cadence-parity | PASS | PASS |
| T3 | cooldown-window | PASS | PASS |
| T3 | local-form-safety | PASS | PASS |
| T2 | contract-matrix | PASS | PASS |
| T2 | automatic-life-cycle | PASS | PASS |
| T2 | objective-path | PASS | PASS |
| T2 | direct-fire | PASS | PASS |
| T2 | straight-evade | **FAIL** | **PASS** |
| T2 | manual-fabrication | PASS | PASS |

Zero runtime faults, zero rejected actions, `contractValid` and
`probeControllerValid` true in every probe run and every self-play match, in all
three movement arms.

`evidence/t4-pre-revision/` is the same suite run against the *unmodified* v1
source, kept as the before-picture rather than overwritten. `evidence/t4/` is
the freeze. `evidence/wasm-parity/` is a WASM mirror at seed 104729 with a
`botarena verify` pass on its canonical replay-v3 hash
(`8fdc828c6b4d732ea2da552b78b0774dd0c905ebc35558dec22f36463a000624`).

## Did the new per-case diagnostics shorten debugging?

**Yes, decisively, and this is the single biggest DX improvement between the two
waves.** v1's DX notes complained that "probes report rich metrics but no
criterion", and that four of seven qualification cycles were spent guessing
which number the analyzer cared about. This revision cost **two** qualification
cycles total, and neither was a guess:

- `straight-evade` returned `failedCriteria: ["took-no-damage"]` with
  `threatenedTurnCount: 1, successfulThreatMoveCount: 1`. That combination is
  only readable as "you dodged once and were hit anyway", which pointed
  straight at the ordering of my escape rule rather than at its existence. v1
  had exactly the same numbers and I read them as "the evade logic never
  fires", which sent two whole cycles down the wrong road.
- `strict-corner` returned `failedCriteria: ["fired-no-curved-shot"]` with an
  `expectation` sentence naming *the intercept that only the lax preview
  allows*. My v1 notes concluded the probe wanted a legal intercept from an
  objective tile and that no such intercept existed — the exact opposite of
  what it wanted. The criterion name alone corrected a wrong belief that had
  survived a whole freeze.
- `resolvedScenario` removed the other guessing game. Having the bot's and
  controller's start tiles, facings, attack profile and the active objective
  region printed next to the failure meant I could reconstruct the geometry on
  paper before opening a replay. For `strict-corner` — bot at (10,7) facing
  north, controller at (9,3) — the bend arithmetic and the blocking corner
  at (10,3) fell out in under a minute.

One request: `failedCriteria` names the failures but there is still no list of
the criteria that *passed*. When a fix flips one probe and silently breaks a
neighbouring criterion in the same probe (which happened here — see below) a
full pass/fail criterion list would show the trade immediately instead of after
a rebuild.

## What actually broke, and why the fixes are what they are

### `straight-evade` — the horizon, not the reflex

The bot spawns at (8,7) in a two-tile-high corridor whose only lateral exits
are three tiles west. A single straight bolt comes down the corridor. v1's
threat model asked one question — *does a bolt cross this tile during the
coming resolution?* — and at the tick where the answer first turned "yes" the
bot was already at (9,7), from which every legal continuation is also "yes".
Worse, the doctrine's own choke-commitment rule (correct, and load-bearing for
three T4 components) then preferred stepping *forward* into the bolt over
stepping back, because backward increased distance-to-goal.

The fix is not a stronger reflex, it is a longer question. Bolts are stamped
per tile with the tick offset they arrive, plus the offset they come to rest,
and a candidate tile is accepted only if a three-tick BFS over that timed map
finds any surviving continuation. At tick 1 the bot now holds; at tick 2 it
steps west; at tick 3 it leaves the corridor entirely; the bolt passes, and it
walks back and holds the objective for the rest of the probe. Damage taken: 0.
The choke-commitment rule is untouched — it just cannot pick a dead end any more.

### `strict-corner` — a bend that never bent

This one was a scoring bug disguised as a geometry bug, and I had it backwards
in v1. `ForkPlanner.CanCover` was already corner-exact (it goes through
`ShotPaths.Preview`, which applies strict corners). What was not corner-exact
was the *plan enumeration*: the program `bendAfter 3, direction -1` from (10,7)
facing north sweeps (10,6),(10,5),(10,4) and then dies on the corner at (10,3)
— exactly the tiles the straight shot sweeps. Both plans scored identically on
the forecast, and `PlanShot` gave curved plans a flat +0.5 "a curve hides its
intent" bonus, so the bot fired a curve that was, physically, a straight shot
with a payload. The probe counts that as a curved attack. Fix: trajectories
whose turn does not happen inside the tiles they actually reach are no longer
enumerated, and the bonus is conditional on the aimed tile lying past the bend.

### The regression this caused, caught in one cycle

The first fix pass also added a clause pulling the bot onto a point it already
claimed once the capture window was closing. `cadence-parity/range-3-harmless`
immediately failed with `made-no-evasive-move-while-the-threat-was-apparent-only`.
Cause: qualification scenarios set `captureThreshold: 1000`, so
"remaining ticks ≤ ticks needed to capture" is trivially true in an eight-tick
probe, and the clause fired on tick 0 of every probe. The clause was dropped —
the forensic value was in the *opposing*-claim half anyway. Worth writing down
for the next author: **probe scenarios deliberately use degenerate capture
thresholds, so any doctrine rule of the form "remaining < f(threshold)" will be
permanently on inside qualification.** `resolvedScenario.captureThreshold` is
now printed, which is how I found this in one read.

## Loss forensics (own replays only)

v1 went 48–6–0 across the wave-1 class-pair factorial. All six defeats are in
two clusters, both seed-invariant, both as team 1, both decided at the tick cap:

| Cluster | Arm | Margin | Shape |
| --- | --- | --- | --- |
| 3 matches | `fabricator-vs-striker`, thin-fronts | **−2** | position 3 held alone from t475; opposing claim of 14 eroded to zero by t487; own capture reached 13/15 at the cap |
| 3 matches | `striker-vs-striker`, outer-shoulder | **−1** | dead-level centre; opponent's residual claim decaying 1-per-2 at the cap, two ticks short of neutral |

Neither loss is a positional failure. In both, Still Water was doing the right
thing and started roughly three to seven ticks too late. In the thin-fronts
cluster its two bodies sat on (15,6) and (15,4) — both objective tiles — for the
last twenty-five ticks, and *still* ran out of clock, because the opposing claim
had to be burned to zero before its own could start. The trigger that gated
entry was `progress >= threshold/5`, i.e. it ignored any claim of 1 or 2; the
final margins were exactly 1 and 2.

Hence the ledger: neutralisation cost is computed from the contract
(`DecayAmount`, `DecayIntervalTicks`, `GainPhaseAtTick`), the walk is the live
cost-field distance, and once `remaining` no longer covers both, any adverse
point is contested. In the thin-fronts geometry that moves entry from t475 to
roughly t468 — enough to complete the capture and turn a −2 loss into a
territorial advance.

A second, unrelated finding fell out of the same replays: **every one of the
six losses, and every mirror match, was byte-identical across all three seeds.**
v1 consumed `context.Random` nowhere, so its direction tie-breaks were absolute
compass order, and in the striker mirror team 0 won +30 on every single seed.
Adopting `ArenaBasics.OrderedDirections` (advance first, retreat last,
perpendiculars ordered by the per-life random stream) converts that systematic
side bias into seed noise: the same mirror now returns +15 / +5 / −5 across
three seeds, and the base-contract mirror returns a clean 0–0 draw. That is a
correctness fix for the *cohort's* accounting as much as for this bot.

## Repairs and changes in this revision

Strategic (the one budgeted pass):

1. Timed bolt projection plus a three-tick, coupling-aware escape horizon
   (`ThreatField`), and a hard "not survivable" term in the option scorer.
2. Realised-bend filtering in trajectory enumeration and a bend bonus
   conditional on the aimed tile lying past the turn (`ForkPlanner`,
   `StillWater.PlanShot`).
3. The endgame ledger: contract-derived neutralisation clock closing the
   contest dead band (`Doctrine.TicksToNeutralise`, `StillWater.ChoosePosture`).
4. Facing-coupling adaptation across all three arms (below).
5. `ArenaBasics.OrderedDirections` for movement and rotation tie-breaks;
   a contract-axis-derived order for the enemy-approach forecast.

Mechanical (free):

- Vendored the current template `ArenaBasics.cs` verbatim. Nothing to reconcile:
  v1 had deleted the starter helpers rather than vendoring them, so this is an
  adoption rather than a merge. Only `OrderedDirections`/`AdvanceDirection` are
  live; the tactical helpers stay unused because `ForkPlanner`/`ThreatField`
  supersede them, and `ClassOf` is deliberately not called — the class addendum
  says to condition on stats and routes, and `Doctrine` already does.

## Movement-arm adaptation

Read from `Rules.MovementProfiles[form.MovementProfileId].FacingCoupling`,
never assumed, defaulting to `PreserveFacing` when the field is absent.

- **`preserve-facing`** — unchanged. Backpedalling to restore the band is free,
  which is the doctrine's cheapest move and stays cheap.
- **`move-sets-facing`** — every candidate tile is now evaluated with the facing
  the step *leaves you in*, so the coverage term prices a retreat at the gun it
  throws away. No special case was needed for the standoff band: honest coverage
  scoring makes retreating expensive exactly where it should be. The withdrawal
  clause narrows to a real breach of the band (`nearest < StandBand`).
- **`facing-locked`** — rotation is promoted from a gun-aiming action to the
  steering wheel: a turn is scored for the lane it opens (goal-cost improvement)
  and, when the current tile is doomed, for the escape it makes legal. The
  withdrawal clause is removed entirely, because turn-then-walk is two ticks a
  one-hit-from-death body does not have; holding the line and firing is better.
  The escape-horizon search models the same constraint, spending a whole tick on
  each direction change, so a corridor is correctly scored as more lethal here.

Mirror self-play, all three arms, five seeds each, in-process: 15/15 complete at
the tick cap with zero faults and no degenerate freeze. Preserve-facing mirrors
draw on every seed; the two coupled arms split sides across seeds.

## Timings (macOS, Docker builder, CLI 0.9.7)

| Step | Time |
| --- | --- |
| `dotnet build` (editing loop) | 0.5 s |
| `botarena build --no-cache` (cold) | 8.9 s |
| In-process 500-tick match | ~2.5 s including the in-process build |
| `qualify --suite frontline-qualification-5` (full cumulative chain, WASM, warm build cache) | 11.3 s wall, 89 s CPU |

## New frictions found this revision

1. **`--movement` silently refuses to compose with `--duel-map`.** The error is
   `Use one Frontline Labs experiment option at a time.` That is a real
   coverage hole for a coupling-aware doctrine: retreat cost is exactly the
   thing thin-fronts is designed to raise, so "does my facing-coupling
   adaptation hold on the map arm that punishes retreat?" is currently
   unanswerable. It also reads as a generic parser error rather than a
   deliberate arm-composition rule, so the first two attempts looked like a
   quoting mistake on my side.
2. **The declared class silently disappears when you point at an artifact.**
   `--bot <project>` resolves the `"class": "striker"` manifest and runs the
   class arm; `--bot <project>/out/bot.wasm` — the exact command in the rule
   card's "default final authoring check" — resolves the *base* contract, with
   different forms, different companion lifecycle, and a materially different
   match. I compared two mirror runs for several minutes before realising they
   were not the same experiment. A one-line note on the resolved run
   (`class arm: none (artifact spec carries no manifest)`) would remove it.
3. **`captureThreshold: 1000` in probe scenarios is a live trap for
   clock-aware doctrines.** Any rule shaped "commit when remaining ticks no
   longer cover a capture" is unconditionally true inside every probe. It cost
   me one cycle and a probe regression. The value is now visible in
   `resolvedScenario`, which is what let me diagnose it — but a sentence in the
   suite documentation saying scenarios use degenerate capture arithmetic on
   purpose would have prevented it.
4. Still open from v1 and still true: `ShotPaths.Preview` bend indexing has no
   worked example; composed impact timing (`LaunchTiles`, `TilesPerAdvance`,
   `TicksPerAdvance`, `AdvancesOnLaunchTick`) has to be reconstructed from a
   replay; and a one-bend striker's permanent blind spot on exact diagonals is
   nowhere in the class addendum. The `MovementFacingCoupling` XML docs, by
   contrast, are excellent — "a blocked move changes neither position nor
   facing" and "the Direction constraint offers exactly the current facing" are
   precisely the two sentences an implementer needs, and I needed no experiment
   to write the adaptation.

## Hardcoding temptations resisted (unchanged from v1, plus)

- Facing coupling is read per form from the movement profile, never inferred
  from the CLI flag name or assumed to be uniform across forms.
- Decay amount and interval come from `FrontlineCapture`; the ledger takes the
  slower of the declared decay clock and the sole-presence erosion rate rather
  than assuming which control policy is active.
- The escape horizon's step model is derived from the coupling, not branched on
  a known arm list.
- Direction tie-breaks derive from the contract's own front axis; the only
  absolute compass values left in the source are the four `Direction` enum
  members themselves.

## Isolation

Work was confined to this output directory plus the private scratch directory
`.../scratchpad/sw-rev2`. Forensics used only the factorial replay paths
containing `still-water`. No other entrant's directory, source, replay,
standings table, or balance report was opened in this revision.

**Carried forward from v1, still disclosed:** during the first authoring pass a
shared scratchpad directory name (`mirror1`) collided with another agent's run
and aggregate statistics from one `fabricator-vs-fabricator` replay that was not
mine were read before I noticed. No source, standings, doctrine, or striker
material was seen, and nothing from it influenced either revision. Repeating the
disclosure here so the record stays with the lineage rather than only with the
frozen v1 directory.
