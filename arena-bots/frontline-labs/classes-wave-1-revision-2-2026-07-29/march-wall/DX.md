# DX notes — march-wall, revision 2

Loss forensics were read from my own wave-1 factorial replays only, as the
assignment directs. No other entrant's directory, source, standing, aggregate
report or replay was opened, and no shared scratch path was used: all working
files live in `sandbox/march-wall-v2-scratch-8f31c2/`, a uniquely named private
directory. Nothing to disclose under the packet's exposure clause.

## Identity

| | |
| --- | --- |
| Entrant | `march-wall` |
| Population / wave | Frontline Labs classes, wave 1, revision 2 (`classes-wave-1-revision-2-2026-07-29`) |
| Authoring lineage | `march-wall-v1`, revision 2 |
| Doctrine | THE LANE IS THE WALL (advancing wall, second lineage) |
| Class | `bulwark` (declared in `botarena.json`, unchanged) |
| Role | `verdict-doctrine` |
| Target | cumulative T4 (retain) |
| Budget | one strategic revision; mechanical repairs and mechanics adaptation free |
| Predecessor | `arena-bots/frontline-labs/classes-wave-1-2026-07-29/march-wall`, untouched |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `3fb217bbf9ad1e181c103ebf19cd4b56ed1e8d38c54343fdc5cc7e6531b1aedf` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `4795ee5ac1f2afffd27d532eeee1a70242ca139a3dd64557015975d14ba427c3` |
| Toolchain doc | `docs/WASM-DEVELOPMENT.md`, sha256 `7f0bcafff85fb1fbcf9a9633237509888bfd4884f180af36afa77fce1173c5df` |
| Template helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `9cae31d594188f46403887578c2347307cfecd018b2a9d84e06fc82c09afb194` (byte-identical copy) |

## Frozen artifacts

| | |
| --- | --- |
| Submitted sources | `AnchorPlanner.cs`, `ArenaBasics.cs`, `ContractView.cs`, `FireControl.cs`, `Geometry.cs`, `Lane.cs`, `MarchWall.cs`, `Navigation.cs`, `Threat.cs` |
| Project metadata | `botarena.json`, `MarchWall.csproj` |
| Deterministic source-tree hash | `70fcb1b1af393a512956eb902585db9fcc68825ffdec358a25d3e0e787216405` (sha256 over the sorted sha256 list of `*.cs` + `botarena.json` + `*.csproj`, same recipe as v1) |
| Canonical WASM | `out/bot.wasm`, 3 319 378 bytes, built by `botarena build <project> --no-cache` |
| **`out/bot.wasm` sha256** | **`c0e24671a241ce77ff81df12550d304d93eacd17825ec1cbd87384d0b9ae51ac`** |
| Build cache key | `4a235b0339c12eb2c4e74bd5…` family; the builder's reported artifact hash equals the sha256 above |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/guest 0.10.4, WASI p1 core module, platform-matched Docker builder on macOS arm64 |
| Qualification report | `evidence/t4/qualification.json`, sha256 `959531ce6dde0982bdababb86877ae728e6badf89e1280466760148ab336dae7` |
| Verified probe replays | 36 replays under `evidence/t4/` across 17 probe variants (5 T4, 6 T3, 6 T2), both team sides |

## Qualification outcome

`experiment frontline-labs qualify --suite frontline-qualification-5`, profile
`frontline-duel-depth-union-t4-v1`, seed 104729, WASM runtime.

**Exit 0 — T4 retained.** Prerequisite T3 PASS (which itself re-ran and
hash-linked T2). All five T4 probes PASS: `suppression-choke`,
`entry-initiative`, `prediction-chamber`, `front-rotation`, `map-holdout`.
`balanceEvidenceEligible` is `false` in the report body, as in v1; the tier
award is what the floor is stated against.

Two intermediate artifacts were archived by re-qualification rather than kept
as directories — recorded here instead, because both failures were caused by
the strategic revision and both are worth the record:

1. artifact `c1140ec98882…` — **T1**. The lane doctrine preempted the march:
   `entry-initiative` and `map-holdout` both report
   `firstLifeObjectiveTick: null` with `damageDealt: 7, damageTaken: 7`. The bot
   stopped in the approach and traded instead of crossing. T3
   `cadence-parity/range-3-harmless` failed at the same time.
2. artifact `faa808a16cbe…` — **T2**. All five T4 probes PASS once the march
   outranks the duel; T3 `range-3-harmless` still failed
   (`never-entered-the-shot-declared-remaining-range`, `took-no-damage`).
3. artifact `c0e24671a241…` — **T4** (frozen).

## Loss forensics (own wave-1 replays, 54 matches)

Measured with a private script over
`/tmp/nilbots-balance/…/matches/*march-wall*/attempt-01/replay.json`.

**Result shape.** bulwark-vs-bulwark 9-9-0 (mean territorial −0.7);
bulwark-vs-fabricator 3-12-3 (−8.2); bulwark-vs-striker 3-15-0 (−17.5). The
mirror was fine. Both cross-class arms were not, and for two different reasons.

**1. The chassis has four rays and v1 never stood on one.** Counting every tick
where a mobile body of mine had a visible enemy and a gun off cooldown:

| arm | gun-ready with target | on a cardinal ray | needed one rotation | no ray at all | of those, waited |
| --- | ---: | ---: | ---: | ---: | ---: |
| vs bulwark | 318/match | 44 (14 %) | 14 | 260 (82 %) | 151 |
| vs fabricator | 320 | 34 (11 %) | 25 | 261 (82 %) | 176 |
| vs striker | 167 | 31 (18 %) | 20 | 117 (70 %) | 46 |

When a ray existed v1 nearly always fired (it declined 0.3–4.8 times a match).
The failure was never fire control; it was geometry. `shoot-straight` takes no
payload and fires along facing, so a mobile bulwark covers exactly four tiles
per distance band, and a striker with a private 45° bend covers most of the
room. The opponent does not have to out-play that, only to stand off-axis.

The single clearest trace, from `bulwark-vs-striker-current`
(`010--march-wall-vs-vector-edge`): my Prime holds `(10,8)`, the striker
oscillates between `(12,7)` and `(12,8)`. On the `(12,8)` ticks it is due east
and I fire; on the `(12,7)` ticks I have no ray and submit `wait`. It fires
every second tick regardless. Five health, fifteen ticks, dead, two shots
returned. Stepping one tile north to `(10,7)` — still an objective tile — would
have put `(12,7)` on my axis. v1 never considered it, because its movement
planner only ever asked "is this nearer the objective".

**2. Denial-only turrets lost the presence war.** Objective-weight-zero body
ticks as a share of my own body ticks: 47 % (mirror), 43 % (fabricator), 29 %
(striker). Average objective-weighted bodies alive per tick — me versus the
opponent — 1.01 vs 1.70, 1.10 vs 2.03, 1.00 vs 1.68. v1's child rule was
"anchor if any other weighted ally exists", so both companions always became
turrets and the Prime carried the entire scoring burden alone; it was absent
(no weighted body at all) for 13 %, 9 % and 24 % of the three arms. Against a
fabricator the combat ledger was level — 22.7 damage dealt versus 22.3 taken —
and I still lost twelve of eighteen, purely on capture presence.

**3. The approach diagnosis is more specific than "out-ranged".** Their bolts
travel 8 and mine 6, but in the striker arm 82 % of the damage I took arrived
with an enemy within two tiles of the impact, not at range (89 % and 95 % in
the mirror and fabricator arms). v1 was not sniped crossing open ground; it was killed
at conversational distance by a gun that could shoot diagonally while its own
could not. The genuine range problem is narrower and it is *self-inflicted*:
v1's route planner soft-denied every tile inside a two-advance bolt sweep, so
against a two-tick cadence firing down a corridor it could never hold a
corridor, which is to say it could never hold a lane. Its caution and its
firing geometry were in direct opposition and caution always won.

**4. Nothing I shipped consumed `context.Random`.** Every metric above is
byte-identical across seeds 104729 / 130363 / 155921 within a pairing. v1 broke
direction ties on an absolute compass, which on a mirror-symmetric map is a
shared systematic side bias rather than noise. Adopting the template's
`OrderedDirections` is what finally makes the seed axis do anything.

## The strategic revision (one, as budgeted)

**The wall is the set of lanes our guns close, not the tiles we stand on**,
with fortification rationed by presence. Concretely: `Lane.cs` answers "what
could that gun reach from a pose nobody is holding yet" by replaying the
declared aim/bend/travel rules through `ShotPaths.Preview`, and the mobile
ladder uses it to seek a shared lane while a ticks-to-kill ledger favours us,
to leave the tiles a gun covers from most of its facings when it does not, and
to force an idle contested standoff open. `FortifyPermitted` now requires the
team to keep at least as many capturing bodies as the enemy has shown, and
never more guns than scorers. `TryMobilize` stands a segment back up when the
team has no scorer left. Full statement in `README.md`.

Two things I tried inside the same revision and **measured as losses**, so they
are not in the frozen source:

- *Pre-empting the evade with a rotation onto the lane.* Reads well; a gun on a
  three-tick cadence spends the rotation, eats the bolt it did not step off,
  and is facing the wrong way again by the time it can fire. Measured on the
  intermediate build, cross-class territorial over the six sparring matches
  went from −69 to −109 with the rotation alone.
- *Absorbing the bolt while holding a lane* (the "durability is for this"
  instinct). Same direction, worse: −183 with both rules in. It converts the
  cadence deficit into a health deficit at no gain.

Both are recorded rather than silently dropped, because "the durable class
should stand and trade" is exactly the plausible idea this doctrine invites and
the measurement says otherwise.

## Iteration evidence

In-process, three seeds, versus my frozen v1 on the bulwark mirror and versus
two private sparring builds — my own v1 source with `"class"` re-declared as
`striker` and `fabricator`, so the chassis differs and the doctrine is one I
already understand. Territorial progress, my side, summed over the three
cross-class seeds per arm:

| arm | v1 | revision 2 |
| --- | ---: | ---: |
| mirror vs frozen v1 (preserve-facing) | 0 / 0 / 0 (all draws) | +2 / 0 / +30 |
| vs sparring striker | −30 / −30 / −30 (breached at 433) | −1 / 0 / −12, no breach |
| vs sparring fabricator | −30 / −30 / −30 (breached at 369) | **+30** (breach at 184) / −2 / −2 |
| `--movement move-sets-facing`, cross-class | — | −15 / −20 / −20 striker; **+30 / +8 / +13** fabricator |
| `--movement facing-locked`, all nine | — | **9–0**, every one by base breach |

The facing-locked sweep is the mechanics adaptation paying for itself, and the
sparring builds show why: v1's route search only ever starts from directions
the movement mask currently allows, so under `facing-locked` it can move in
exactly one direction until something else turns it. Its whole match becomes
`wait`/`move` with 20 shots and no anchors. Revision 2 turns into the step.

Caveat I would not want a reader to miss: the sparring opponents run *my own*
doctrine on another chassis. They are a fixed reference for measuring change,
not a sample of the population.

## Timings (Apple Silicon, warm)

- managed edit/compile loop: ~0.5 s.
- in-process 500-tick match through the CLI: 1–3 s including the diagnostic
  builds of both bots.
- `build --no-cache` through the Docker builder: 9–10 s, three times.
- one full `qualify --suite frontline-qualification-5` (17 probe variants from
  both team sides across three cumulative tiers): ~12 s wall, ~92 s CPU at
  ~790 %.
- parsing 54 factorial replays for forensics: ~4 minutes (24 MB of JSON each).

## Repairs and reconciliation against the current template

`ArenaBasics.cs` was not present in v1 — v1 replaced it with its own modules.
It is now synced byte-identical and used, and the reconciliation was the useful
part:

1. **`OrderedDirections` everywhere a direction tie is broken.** `Navigation`'s
   breadth-first search, `Evade`'s scoring loop, `HoldTheLine`'s rotation
   scan and `AnchorPlanner`'s equal-score site tie-break all took an absolute
   N/E/S/W order. The anchor planner's was the worst of them: it preferred the
   lower row, then the lower column, which on a mirror map systematically
   favours one team's flank. It now projects candidates onto the mirror-fair
   order instead.
2. **`Occupied` and the allied-bolt policy.** v1 hardcoded "our own bolts pass
   through" in a comment. It now reads the joint answer of
   `projectilesBlockMovement` and `alliedProjectileContact`, matching the fixed
   helper, so a future collision arm cannot silently make the bot walk into its
   own covering fire.
3. **`Wait`.** The fallback now delegates to the helper's never-throw ladder and
   keeps its own argument synthesis only for a catalog that declares no wait at
   all.
4. **`TryDirectShot` / straight-only chassis.** The helper's fix — a
   payload-free attack when `ShotProgram.Enabled` is false — is the same
   condition `FireControl` already reached through `payloadAllowed`; verified
   equivalent rather than duplicated.
5. **`ClassOf` / `Capabilities`.** Read, deliberately unused for branching. The
   addendum says to condition on stats and routes rather than on a class name
   and the doctrine does exactly that; a digest that returns the class prefix
   is a hardcoding temptation with a friendly face.

Genuine mechanical repairs found by the probes this pass:

6. **Voluntary movement must respect a bolt's *declared remaining travel*, not
   its next two advances.** `Threat.Hits(tile, 1)` answers "will something hit
   this tile soon", which is the right question for a dodge and the wrong one
   for a step forward. The T3 `range-3-harmless` probe is built precisely on
   the difference: the controller's shot expires two tiles short of where you
   stand, so it is harmless while you hold and lethal the instant you advance
   into it. Added `Threat.InDeclaredPath`, and gated every voluntary step on it.
7. **Cover is not traded for a lane.** The same probe also fails a bot that
   steps into range *before* the shot exists, which no projectile-based test can
   catch. The rule that fixes it is a doctrine statement rather than a patch: if
   that gun cannot reach the tile we are on, do not step onto one it can. An
   enemy parked just outside our range is parked just outside its own.
8. **Ground outranks the duel.** `entry-initiative` and `map-holdout` both say
   so in one sentence — cross under fire and hold, rather than stop and trade —
   and my first lane implementation had the two steps in the wrong order.

## Documentation gaps

- **The class card's stat table omits the turret's gun.** It gives bulwark
  "projectile range 6" and the turret row lists only "HP 7, omni fire; windup
  3/1". The resolved contract says `turret-bolt` travels **8** tiles on
  **cooldown 1** — same range as a striker, twice the cadence, with eight
  headings and seven health. That is the single most important number in my
  class and it is not in the player-facing table. Reading it from the contract
  is correct and I did; a reader deciding whether the class is worth playing
  from the addendum alone would badly misjudge it.
- **Nothing in the card says a straight-only chassis has four rays.** It says
  `shoot-straight` "takes no payload and fires along your facing", which is
  accurate and completely undersells the consequence: with facing cardinal, the
  gun covers 4 of the 8 headings, so half the tiles at any distance are
  unreachable forever. The card's own warning that "omnidirectional vision and
  turret fire are eight rays, not a filled radius" is exactly the right shape of
  sentence, and the mobile straight gun deserves the matching one.
- **`facingCoupling` needs a note about path search.** The SDK doc comment on
  `FacingLocked` is precise — the movement constraint offers exactly the
  current facing — but the failure mode it implies is severe and silent: any
  bot whose route search seeds itself from the currently-legal directions
  becomes immobile rather than slower. One sentence in the enum's remarks
  ("rotate to unlock a step") would save every author the same debugging.
- **The bulwark sees 4 and shoots 6.** Vision shorter than gun range is a real
  design statement and it is nowhere in prose; you find it by diffing
  `visionProfiles[].range` against `attackProfiles[].projectile.maxTravelTiles`.
  Hearing radius 8 with bearing octants and distance bands exists in the
  contract and is the obvious compensation, and nothing points an author at it.
- **Probe pass predicates are still unpublished** (carried over from v1).
  `failedCriteria` names are excellent — `never-entered-the-shot-declared-
  remaining-range` told me exactly what to fix — but the numeric thresholds
  still have to be inferred. Naming the predicate next to the metric would be
  cheaper than the inference, which was right three times out of three again.

## Confusing terminology

- **"Objective weight"** remains the most doctrine-relevant number in the
  contract with the least descriptive name; a second pass of forensics only
  sharpened that. It is not a multiplier, it is a franchise: weight zero means
  the body is not in the election.
- **"Range" is three different numbers** — sight range, projectile
  `maxTravelTiles`, and hearing radius — and for this class they are 4, 6 and 8.
  Prose that says "range 6" without naming which one is genuinely ambiguous.
- **`Available` versus resolvable** (carried over): every joint-resolution
  surprise still arrives as `Available: true` followed by `Blocked`.
- The `frontline-qualification-N` / `TN` off-by-one is still momentarily
  jarring every single time.

## Strategy passes

One, as budgeted, plus the two measured-and-rejected variants recorded above.
Everything else in this revision is mechanical repair, template reconciliation,
or movement-arm adaptation.
