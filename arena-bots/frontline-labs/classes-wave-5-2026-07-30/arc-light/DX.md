# arc-light — wave-5 DX and freeze record

Wave-5 Frontline Labs entrant. Class **striker**, doctrine
**interception-caster**. Revision of my own wave-4 lineage
(`arena-bots/frontline-labs/classes-wave-4-2026-07-30/arc-light/`), which was the
only other bot source read.

Wave game: **`deck`** — keel pendulum + the full skill kit + universal one-bend +
the tuned fabricator (`--five-slots wane`) + restored ±45° launch offsets + open
ground, all facing-locked.

```
--classes <pair> --movement facing-locked --pendulum keel --skills kit \
  --bend universal --aim offset --stance-ground open [--five-slots wane]
```

Assignment: fix the one broken leg — the fabricator matchup, measured −1.00 on
every wave-4 arm.

## Isolation statement

Material read while authoring, and nothing else:

- `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`
- `docs/FRONTLINE-LABS-RULES.md`
- `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` (read in full, including the aim,
  five-slot-variant and stance-ground sections)
- `templates/botarena-generic-actor/` (the scaffold; `ArenaBasics.cs` is retained
  unmodified in this project and used for its contract readers)
- `src/BotArena.Sdk/` public types and XML documentation (types only)
- my own wave-4 directory, its `DX.md`, and my own replays
- the CLI at `sandbox/cli-publish/` and its resolved contracts

No other entrant's source, standings, replays, DX notes, or aggregate report was
opened. No `docs/DECISIONS.md`, no `docs/BOT-QUALIFICATION-SUITE.md`, no other
`docs/DESIGN-*`/`FORENSICS` file, no Engine or App source. Private scratch was
`sandbox/arc-light-w5-scratch-4d81be97/`, a uniquely named directory used by
nothing else. **No accidental exposure to another author's material occurred.**
Nothing was committed to git.

One mid-wave correction arrived from the orchestrator: a stale line in
`EXPERIMENTAL-FRONTLINE-CLASSES.md` said a bulwark may mobilize back "once per
life", which is historical only. On this arm `irreversibleForLife` is false on
the mobilize routes and the cycle is unlimited. This project never assumed
otherwise — it reads `irreversibleForLife` from the route (`ArcStance.TryFortify`
prices relief differently for a reversible route than for a one-way one) and no
code path treats an enemy's anchor as a spent commitment.

Every sparring partner was built from permitted material only: my own rebuilt
wave-4 predecessor, and variants of my own source differing by one line or by
one manifest field. All were rebuilt from source (`build … --no-cache`) against
the current SDK; no frozen wave-4 artifact was played, as the brief requires.

| partner | what it is |
| --- | --- |
| `ArcW4` | wave-4 predecessor source, rebuilt, `"class": "striker"` |
| `ArcFab` | the same wave-4 source, `"class": "fabricator"` |
| `ArcBul` | the same wave-4 source, `"class": "bulwark"` |
| `Arc5Fab` / `Arc5Bul` | *this* revision's source on those two chassis |
| six ablations | this revision with exactly one behaviour removed (below) |

Because a WASM artifact carries no class manifest but a *project* does, every
match here was run from project specs, so declared classes bound each bot to its
class's canonical team side automatically and `--swap` swapped sides rather than
chassis. That removes the wave-4 accounting hazard where "both assignments"
silently meant "both chassis".

## Freeze identity

| item | value |
| --- | --- |
| artifact | `out/bot.wasm` |
| bot.wasm SHA-256 | `da7c4907846d0ddf6d453d1a28c37434a803a99ca93f7efe0b3dd931c26f5553` |
| build | `nilbots build <project> --no-cache`, cold cache (key `025c47f4257affcb…`) |
| toolchain | nilbots **0.9.21** (the brief says 0.9.20), SDK 0.10.6, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, Docker builder (macOS arm64 host) |
| entry | `ArcLight` (`botarena.json` `entryType`), declared `"class": "striker"` |
| qualification | `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, seed 104729, runtime wasm |
| qualification result | **T4**, `passed: true`, `balanceEvidenceEligible: true`, **exit code 0** |
| probes | `prerequisite T3`, `suppression-choke`, `entry-initiative`, `prediction-chamber`, `front-rotation`, `map-holdout` — all PASS, first run, no repairs needed |
| qualification report | `evidence/t4/qualification.json`, SHA-256 `60623fa00239da0ba3450fadf3a70e9dab3899c26becb24bb0a2708226a9496f` |
| T3 prerequisite | `frontline-qualification-4` (`frontline-duel-depth-union-t3-v1`) rerun and hash-linked, passed, report SHA-256 `b987fbb4c68531a9c4a53c29d92174b34a4a64b7c8115832f53eed13aac19ca0` |
| source-tree hash | `67c94b64aa1d0d4f52b24d234057305c2b9d6d856862b97302e6051a4c38491a` (SHA-256 over the name+digest lines below, name-sorted) |

Per-file SHA-256:

| file | sha256 |
| --- | --- |
| `ArcBoard.cs` | `f6e665c994634a7d281358b8297da0f3c24ca74a2ea82b937a7a2b9978afca94` |
| `ArcFacts.cs` | `ef7758f29cf4f7e84195dd36e06932aa7d12da96f0dceb251736c6a87a64586e` |
| `ArcGun.cs` | `b0b68b9fc15d7bf6eb9565ca83f084f1ac7a1fd06a64bc9d9c04c0f8a715e224` |
| `ArcKeel.cs` | `d30eb3ee5f835b0ea2385db39eb14f061989e0992528b16cda7c734331fa1f99` |
| `ArcLight.cs` | `2116e8e1bc824f70dbfdc4a4c17b57176a4ff3c4fd8e9ca3129bafbefbdc7f19` |
| `ArcLight.csproj` | `d2288bda995372814943941c1fba7becbfe016f883691052f3f6f5c6d9e17ef6` |
| `ArcMemory.cs` | `8f8338426c6619ccab641ce7f36296fff252c2763c2229dc1c93e876216c5924` |
| `ArcMove.cs` | `0d86e1f8c5fa6dfb48a796529f081e2f57d4bc687aef07a6fdcdab22407b8b44` |
| `ArcStance.cs` | `27ce50a00a397d3b5542838696d78a520f0271f031cb806ac528b89161f9cabf` |
| `ArcThreat.cs` | `bbb244d4c9d0c6ca0f12bc276d856656a18f21ccac3fafabcbbae19a4aa87cb5` |
| `ArenaBasics.cs` | `a198af0a28ace85ed9034a9a93d8e106f21a907681547ac7a65e9e21871ce773` (scaffold, unmodified) |
| `botarena.json` | `ada09877c60994dc6d799ae0b5d0864e10b1d18a07b29fdc6827c8a64805ba98` |

Resolved arm identities this one artifact played:

| pair | flags beyond the common set | rulesetId | rules fingerprint |
| --- | --- | --- | --- |
| `fabricator-vs-striker` | `--five-slots wane` | `frontline-labs-1-fabricator-vs-striker-deck-facing-locked` | `0922aa9320ffaa8e…` |
| `striker-vs-striker` | — | `frontline-labs-1-striker-vs-striker-sail-open-facing-locked` | `218b6f0611bb8ea6…` |
| `bulwark-vs-striker` | — | `frontline-labs-1-bulwark-vs-striker-sail-open-facing-locked` | `dbe453aa44da3228…` |

`sail-open` spells itself out where no fabricator is in the cell, exactly as the
composite-token rules say it should.

`--five-slots wane` is inert-omitted where no fabricator is present, exactly as
documented, so one flag set worked on every pair.

## Doctrine, in one paragraph

arc-light is a striker that treats aiming as an interception problem and every
commitment as a purchase it has to justify from the contract's own numbers. It
reads, rather than assumes: the facing coupling (a facing-locked body may only
step where it faces, so an enemy's reachable set is a **ray**, and every shot is
aimed at the ray the target is confined to); the gun's declared **initial-aim
envelope**, which is the one place a doctrine can learn that a diagonal launch
exists — when it does, one facing owns three headings, a step stays armed 45° off
the line it walks, and the near-diagonal blind spot that used to be the fan's
whole justification is closed by the gun instead; the declared bend envelope,
where a *curved* arc is spent only on a hard interception; the **route's own
placement**, not the map's tag, so on an open-ground arm the fan rises on the
objective it is denying instead of refusing 112 legal tiles; the fabrication and
replication catalogs, which name the enemy forms that can create another body and
therefore the one target whose death stops a swarm's rebuilds; the capture policy,
where surplus weight scales gain, so being outnumbered on the point is a reason to
stand on it (each of our bodies cancels exactly one point of their per-tick gain)
and only an enemy standing *alone* erodes a claim, so stepping off an empty
objective is free; the live territory hold, read from `holdOwnerTeamId` /
`holdEndsAtTick` and never inferred, so a capture that would be spent inside an
enemy hold is banked instead; and the stance routes, where the cast is priced in
**bodies** against the contract's tempo arithmetic — the stance costs entry windup
plus the cast plus the return's windup, the ordinary gun would fire one aimed bolt
per cadence in that window, so the fan must connect with `ceil(cycle/cadence)`
distinct bodies or it is a tempo loss, and a *loaded enemy gun* bearing on the
tile is refused as firmly as a bolt in flight because a windup can do nothing but
wait. Ownership is the only test applied to an incoming bolt, so a bolt returned
by an aegis shell is hostile without a special case; enemy guard deflections are
accumulated across the life rather than counted per tick, because one frozen
observation can never reach a threshold of three; and bodies spread by rank among
the team's active lives so independent lives with empty private memory enter from
different bearings instead of stacking into one lane that feeds a fan.

## Measured records vs the rebuilt wave-4 predecessor

All matches WASM runtime, **16 seeds**, on the wave game. Records are
arc-light-w5's; `prog` is mean signed territorial progress (±30 is a base
breach). Zero runtime faults and zero disqualifications across every match
measured for this freeze (112 matches verified programmatically, plus the
ablation sweeps).

### The broken leg: `fabricator-vs-striker` (`deck`)

Both sides are the *same doctrine on different chassis*, which makes this the
cleanest causal A/B available: in the "before" row the identical artifact plays
both teams.

| striker driver | record | prog | mean end tick | breaches against | net-weight ticks (own / opp) |
| --- | --- | --- | --- | --- | --- |
| **wave-4 predecessor** | **3-13-0** | **−19.31** | 381 | 9 | 129.0 / 149.4 |
| **wave-5 arc-light** | **15-1-0** | **+23.38** | 312 | 0 (11 for) | 131.9 / 98.9 |

The leg is fixed: −19.31 → +23.38, and the swarm never breaches this revision's
base on any of the 16 seeds.

### The striker mirror (`sail-open`), both assignments

| assignment | record | prog |
| --- | --- | --- |
| arc-light-w5 as team 0 | **15-1-0** | +27.31 |
| arc-light-w5 as team 1 (`--swap`) | **16-0-0** | +30.00 |
| **combined** | **31-1-0** | +28.66 |

### The other cross-class leg: `bulwark-vs-striker` (`sail-open`)

| striker driver | record | prog | mean end tick |
| --- | --- | --- | --- |
| wave-4 predecessor | 0-16-0 | −30.00 | 143 |
| wave-5 arc-light | **3-13-0** | **−8.38** | 449 |

Reported as measured and **not** claimed as fixed: this leg is still negative.
It improved from "base-breached in 143 ticks, sixteen times out of sixteen" to
"3 wins and 13 losses that mostly reach the tick cap". The brief scoped this wave
to the fabricator leg; on this evidence the bulwark chassis under `sail-open` is
the next thing to answer, and my own doctrine driving one (below) says the same.

### Chassis-swap robustness (the artifact on chassis it was not written for)

| pairing | record | prog |
| --- | --- | --- |
| this doctrine on a **fabricator** vs the w4 striker | **16-0-0** | +28.50 |
| this doctrine on a **bulwark** vs the w4 striker | **16-0-0** | +30.00 |

### Causal ablations — one behaviour removed, same 16 seeds, same swarm

Each row is this revision with exactly one line or one method changed, so the
delta is attributable. Ordered by cost.

| build | record | prog | casts/match |
| --- | --- | --- | --- |
| **arc-light-w5 (frozen)** | **15-1-0** | **+23.38** | 0.6 |
| route commitment removed | 15-1-0 | +22.50 | 0.6 |
| supply-priority targeting removed | 8-8-0 | −0.75 | 0.0 |
| enemy-gun **bearing** gate removed | 5-11-0 | −6.81 | **10.8** |
| cast **commitment** refused, bearing discipline kept | 9-7-0 | +4.12 | 0.0 |
| declared launch offsets ignored when pricing own gun | 2-14-0 | −16.25 | 0.4 |
| stance route hidden entirely (no cast, no bearing) | 1-15-0 | −19.75 | 0.0 |

Five things this table says that I would not have believed without it:

1. **Reading the launch offsets is the largest single lever in the wave**
   (15-1 → 2-14). Nothing about firing changed in that ablation — only how the
   doctrine *prices its own gun*. Wave 4 measured its gun as the straight lane
   alone, which was true while the aim envelope was 0..0 and became a silent lie
   the tick the offsets came back: the gun under-reported two thirds of its
   reach, so the fan kept winning a comparison it should now lose.
2. **The bearing gate is what makes the cast rare and correct.** Remove it and
   the bot casts eighteen times as often (0.6 → 10.8 per match) and loses. The
   gate is not conservatism; it is the difference between a cast and a donation.
3. **The cast is worth six wins at 0.6 uses per match** (15-1 → 9-7 when the
   commitment alone is refused). Ten casts across sixteen matches, each one
   decisive enough to move the record — which is what a correctly priced skill
   should look like, and the opposite of the wave-4 shape.
4. **The bearing discipline is worth as much again as the cast** (9-7 → 1-15 when
   the route disappears and the fan-aiming rotations go with it). A facing chosen
   because it would rake three bodies is also a facing whose ordinary gun is
   pointed at the fight; the fan pays even on ticks it is never spent.
5. **Route commitment is measured neutral** (15-1 either way). It was kept
   because it halves the share of actor-ticks spent turning, but it is reported
   as neutral rather than as a win, because that is what it measured.

One more pricing question was asked and answered against the brief's own
suggestion. Ordering the cast **ahead** of an available aimed bolt whenever the
fan is forecast to beat break-even — the most aggressive honest reading of
"cast INTO multi-body bearings" — measured **13-3-0 / +16.19** against
**15-1-0 / +23.38** for the same build with the cast kept *below* the shot. One
bolt now beats three bolts in four ticks, because the four ticks are immobile and
the swarm is still walking. The cast stayed below the shot.

## Skill and diagonal usage counts

Exact totals over the 16-match `deck` `fabricator-vs-striker` set, arc-light's own
bodies (`evidence/self-play/counts-final.txt`):

| quantity | count |
| --- | --- |
| volleys cast (stance entries) | **10** (9 prime, 1 child) |
| fans actually fired | **10** — every cast spent its budget |
| bolts launched by those fans | **30** (3 per cast, contiguous IDs) |
| stance exits by engine `automatic-threshold-return` | **10** |
| stance exits by early `mobilize` | **0** |
| bodies lost inside a stance | **0** (wave-4 predecessor on this arm: 2.4/match) |
| wait ticks carrying a priced cast decline | **1,819** — forecast 1 body `pay1` 219, forecast 0 `pay0` 121, `pay2` 14, `pay3` 5, `pay4` 2, bolt-in-windup `windup-bolt` 33, and 1,425 with no enemy visible at all (a striker's stance route always exists, so `no-route` here means an empty enemy list) |
| fan-bearing rotations bought outside the stance | **100** |
| attacks | **631** |
| diagonal launches (initial aim offset ≠ 0) | **243** (39% of all attacks) |
| — of which aim-only diagonals, zero bends | **66** |
| — of which diagonal + one bend | **177** |
| straight + one bend | **33** |
| kite steps / kite turns | **24 / 18** |
| slots fielded | mean **1.36** bodies, max **3** of 3 (opponent fielded 4 of 4) |
| shells raised | **0** — a striker has no route to a guarding form |

Shells are not raised by a striker, so the deflection paths were exercised on the
one chassis that has them, using this same artifact (`Arc5Bul`, 16 matches):
**528 shells raised, 176 deflections returned by its own guard, 0 bodies lost.**
That is the first measured evidence this lineage has for the guard code, which
wave-4's DX had to report as compiled-but-unmeasured. It also exposed a real bug
described in friction 3.

## Top 3 frictions

**1. An open-ground arm moves transition legality from the MAP to the ROUTE, and
nothing on either side says so — so correct contract-driven code silently keeps
enforcing a rule that no longer exists.** The map still publishes
`transition-placement-forbidden` over 112 of 233 open tiles, including all 22
objective tiles and the whole central corridor. What changed under
`--stance-ground open` is that the volley entry route's
`placement.forbiddenTileTags` is now **empty**. Wave 4 derived "where may I cast"
by intersecting the map tag — which was right then, is documented behaviour for
*anchor* routes now, and is wrong for stance routes on this arm. The failure is
invisible in the worst possible way: my bot was reading the contract, just the
wrong end of it, so it refused 112 tiles the legality mask was offering it. There
is no cross-reference in either direction — the tag does not say which routes
still forbid it, and the route does not say which tiles its tag list resolves to.
This was the single highest-value fix of my wave and I found it by dumping both
structures and diffing them against my own wave-4 assumptions. Note this is the
exact **inverse** of my wave-4 friction #1 (a legality entry saying
`Available: false` with no reason attached): there the mask refused and would not
say why; here the mask permits and the bot never asks. Both are the same missing
affordance — **a legality entry should name the constraint that decided it**.

**2. `--five-slots wane` is documented as a fabricator lever and quietly changes
the STRIKER's lifecycle *mechanism*, not just its numbers.** On
`fabricator-vs-striker` + `deck`, my own slots resolve to
`dormant-automatic-activation-at-tick` at 120/260 with a 30-tick automatic
respawn, and `striker-prime` does not carry `fabricate` in `allowedActionIds` at
all — while the fabricator gets four explicit slots at 60/180/300 with 22/30-tick
rebuilds and a prime that must spend a combat action to use them. The two halves
of the topology differ in *kind*: one side buys bodies with tempo and the other
is given them. The classes doc's five-slot variant table and the CLI help both
talk only about the fabricator's slot count and clock; neither mentions that the
non-fabricator side's companions become automatic in this cell. The standing
advice — "read the actual unlock ticks and rebuild delays from your slots'
lifecycle assignments" — is advice about **numbers**, and it is repeated three
times, which is exactly what made me skim it: the thing that actually moved was a
mechanism, and no sentence anywhere points at that. A bot that budgets ticks for
supply on the striker side is budgeting for an action its forms do not have.

**3. `fabrication-source-anywhere` makes `participantRegionAssignments` useless
for the one thing it is the natural source for, and the only place that fact is
published is a region's NAME.** In this arm both participants' `fabrication-source`
*and* `fabrication-output` roles bind to a single region covering all 233 open
tiles. Two consequences, one tactical and one a silent bug. Tactically, this is
the most important fact in the whole matchup — the fabricator prime is a mobile
spawner that materialises children beside itself on the objective — and the typed
contract still says `sourceRegionRoleId: "fabrication-source"` with an empty
`requiredSourceTileTags`, so nothing in it reads as "unconstrained"; you learn it
by noticing that a region is called *anywhere* and counting its tiles. As a bug:
wave 4 derived "which protected pad is the enemy's" from the participant's own
region assignments, and with both participants assigned to the same all-map
region every pad looks like mine — so the router happily planned routes through
the enemy pad, where ground entry is refused. A blocked move is
indistinguishable from traffic; there is no event and no diagnostic. The durable
derivation, which this revision now uses, is
`lifecycleAssignments[].assignedRespawnSpawnId` → `map.spawnAnchors` → whichever
all-spawn-protected region contains that anchor.

**Honourable mention, because it is a repeat.** My wave-4 friction #2 still
stands verbatim: the published CLI is `sandbox/cli-publish/botarena` on disk
while every document and brief invokes `nilbots`, and its banner prints
`nilbots 0.9.21` (the brief says 0.9.20). Wave-4 friction #3 also still stands —
`ArenaBasics.ClassOf` still recovers a class by splitting form IDs on `-`, the
precise thing the classes doc forbids now that `ClassId` is published on self,
allies, enemies, participants and teams, and its own doc-comment still promises a
replacement that has already shipped. The scaffold still offers no reader for
`AttackProfile.Volley`, `Form.ProjectileGuard`, `FormTransition.AutomaticReturn`,
`FormTransition.Placement`, or `ShotProgramDefinition.MinInitialAimSteps` — the
five facts this wave is *about*. Every entrant writes those five independently,
and friction 1 above is what happens when one of them is written from the map
instead of the route.

## Repairs found from my own probe feedback and replays

Qualification passed on the first attempt with no repairs, so all six of these
came from my own `deck` replays rather than from a probe failure:

1. **Own gun under-priced.** `LaneValue` built its lane set from the volley
   spread only, so a striker measured its gun as one straight lane. Now the lane
   set is the union of the volley spread and the declared initial-aim offsets.
   Cost when absent: 15-1 → 2-14.
2. **Cast legality read from the map tag instead of the route's placement**
   (friction 1). `ArcFacts.PlacementAllows(route, tile)` now resolves the route's
   own forbidden and required tag kinds against the map's tag sets.
3. **Cast priced in lane crossings instead of bodies.** `ArcGun.FanForecast`
   counts distinct bodies a fan would hard-hit; `ArcFacts.RequiredFanHits`
   derives the break-even count from the stance cycle and the gun's cadence.
4. **Windups entered against loaded guns.** `ArcThreat.Bearing` prices enemy guns
   that bear on a tile, over each enemy's own declared envelope and every facing a
   single rotation could reach. Deaths inside a stance: 2.4/match → 0.
5. **Enemy pad walkable** (friction 3), fixed via spawn anchors.
6. **Shield gated on an arriving bolt, so it never rose.** Bolts advance two tiles
   per tick, so by the time one is visible and inbound it is one tick out and a
   windup-1 shield cannot complete in front of it — measured at exactly **zero**
   shells raised in 16 matches. A guard is raised against a **bearing** inside the
   quadrant it would cover, which is also what the design says ("the protected
   quadrant is chosen before the shield rises"). After the fix: 528 raises, 176
   deflections. Also in this repair: deflection counts are now accumulated in
   life-scoped memory rather than counted from one frozen observation, which could
   never reach a threshold of three.

Verified afterwards that the guard work is behaviourally inert on a striker: the
16-seed `deck` `fabricator-vs-striker` set is identical before and after it
(15-1-0, +23.38, same 16 end ticks), differing only in artifact provenance.

## Build and qualification timings

Cold `--no-cache` WASM build: ~15 s (Docker builder, macOS arm64 host). Full
`frontline-qualification-5` including the hash-linked T3 and T2 prerequisites:
~6.5 s. One 500-tick WASM match: ~1.3 s. A 16-seed sweep: ~25 s.

**Operational note worth passing on:** each match writes ~15 MB of replay plus a
self-contained viewer, so a 16-seed sweep is ~250 MB and I filled the disk
mid-wave. Numbers were extracted to text summaries and the bulk replays deleted
after each sweep; this freeze keeps the qualification evidence, the text
summaries, and one cited replay (`evidence/self-play/`, viewers stripped).

## Strategy passes

One authoring pass, then four measured pricing iterations (fan forecast
strictness, supply credit, cast ordering versus the aimed shot, bearing budget),
then a six-way single-lever ablation sweep on identical seeds. Nothing in the
frozen build was tuned by eye; every number in this file was measured against my
own predecessor or my own ablations.
