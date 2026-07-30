# GateStone — DX findings and freeze record (wave 6, the coordination wave)

Wave-6 Frontline Labs entrant, class **bulwark**, revision 3 of the `gate-stone`
lineage (wave-5 revision 2 is its parent, wave-4 revision 1 its grandparent).
Written before seeing any other entrant's source, replays, standings, or any
aggregate balance report.

## Isolation statement

- **Read:** `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`
  (sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e`),
  `docs/FRONTLINE-LABS-RULES.md`
  (`06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8`),
  `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`
  (`2333bd3c9f412e4e9439779ef3d5f2ca6bc8abae6f00973daf54f7e4c892de50`). All three
  are the values my wave-5 freeze recorded, so **none of the three permitted docs
  moved between waves 5 and 6.** Plus `templates/botarena-generic-actor/`, type
  declarations under `src/BotArena.Sdk/` (`GenericActorContext.cs` and
  `GenericActorRulesContract.cs`, for the allied-observation and volley shapes),
  the CLI help of `sandbox/cli-publish/`, my own wave-4 and wave-5 directories
  (the only other bot sources this brief permits), and my own contracts, replays
  and qualification evidence.
- **Not read:** any other entrant's directory, source, replay, standing or
  aggregate report; any Engine or App implementation file; any `docs/` file
  outside the three permitted ones. The wave-6 directory did not exist when I
  arrived; I created it. I listed `arena-bots/frontline-labs/` once to find my own
  wave-5 path and never descended into a sibling entrant. `CLAUDE.md` was
  presented automatically by the harness as repository context; it is an agent
  guide, not an entrant's material.
- **Private scratch:** `sandbox/gate-stone-w6-scratch-9d2f41e7/` — uniquely named,
  created by me, never shared, never read from by anything else. No shared or
  guessably named scratch path was written or read, so there is **no accidental
  exposure to disclose**.
- **Sparring:** my own wave-5 predecessor and my own wave-4 predecessor, both
  **rebuilt from source** with `nilbots build … --no-cache` into that scratch
  directory (artifacts
  `5d965efedf486d6784998567821f9467a23a7fbb6270753fafcdb1579a8c67c7` and
  `61200b98198a13c58b7b2da68fe002613e9a70acd3c0a9f15812250e490674cc`). Every
  recorded number uses those rebuilds. The wave-5 rebuild's hash differs from the
  `bf975c47…` in its own freeze because the toolchain moved (CLI 0.9.21 → 0.9.22),
  which is exactly why the brief says rebuild.
- **Freeze-integrity warning honoured.** The coordinator relayed mid-run that
  `nilbots build` globs every `.cs` under the project directory, so an archived
  variant source inside the freeze tree makes the frozen tree fail to rebuild with
  duplicate-member errors. All eleven one-rule-off and half-rule ablation variants
  live in `sandbox/gate-stone-w6-scratch-9d2f41e7/abl-*/`, outside the freeze; this
  directory contains only the nine submitted files. The last action of this freeze
  was a fresh `--no-cache` build **run from this directory**, which reproduced
  `06b4ae21…` and the same cache key exactly.
- **Git:** nothing committed, nothing staged. The wave-6 tree is untracked.

## Freeze identity

| field | value |
| --- | --- |
| entrant / class | `gate-stone` / `bulwark` (declared in `botarena.json`) |
| lineage | wave-6 revision 3; parent = wave-5 revision 2, same name |
| role / target | `verdict-doctrine`, target T4 (suite 5), achieved T4 |
| doctrine | `capture-arithmetic-gate`, plus the wave-6 crew layer |
| author packet | `FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| the game | `deck` = `--movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open [--five-slots wane]` |
| resolved cells | `frontline-labs-1-bulwark-vs-fabricator-deck-facing-locked`; `…-bulwark-vs-bulwark-sail-open-facing-locked`; `…-bulwark-vs-striker-sail-open-facing-locked` (`deck` names itself only where a fabricator is in the cell) |
| toolchain | nilbots CLI **0.9.22**, SDK 0.10.6, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, Docker platform-matched builder |
| runtime | actor protocol 1.0, configuration 1.0, contract profile `generic-actor-match-2` |
| build | `nilbots build . --no-cache`, cache key `a513394d298faecbd37ef218df07cfe4e8ed376ad6aac0e44277b94642b7d1f0` (miss/compiled) |
| **bot.wasm sha256** | **`06b4ae21ae0393c220cd675933bfe5e2ff6efdeb37f45e7bba701178872a7d93`** |
| rebuild-from-frozen-tree | second `--no-cache` build invoked on this directory as the final step: **same hash, same cache key** |
| qualification | `evidence/t4/qualification.json`, sha256 `2b64b6bac3e6a74b44d79b9439e54099d3e0e2fb6402bd9ad5f77c32b26f63a7`, exit code **0**, tier **T4**, `balanceEvidenceEligible: true`, `passed: true`, prerequisite **T3 PASS**, all five T4 probes PASS, one attempt |
| source-tree digest | `55a70b245f1ae631cae1c647657227f1d40c60927b7ddbfcce982adc7438219c` (sha256 of the sorted per-file digest list below) |

Submitted sources (sha256):

| file | lines | sha256 |
| --- | --- | --- |
| `GateStone.cs` | 946 | `5cdb682b044f507f1d01ad37fa65c390c4d27de826d4e5ee52f0d98a95c31a52` |
| `StoneCrew.cs` | 461 | `3637971d1fd7ca5d37207937a027da5b6a5ff3427e4e2804fceb09bc6957b4b6` (new this wave) |
| `StoneGround.cs` | 1150 | `fce74dc3db2d269ad9b29075e793682546b9bf4af743af6bd0ca68a4789f71cb` |
| `StoneContract.cs` | 678 | `613f7016a94483a1123326de6a05ec6c7d797fd215ca8d45990df94d8bc68487` |
| `StoneAim.cs` | 504 | `549ba4016fe3cadbaa3200590d5b10d9ea49a2f9b70ace967ba3f4a0dbe35f38` (unchanged from wave 5) |
| `StoneMemory.cs` | 212 | `41df9ece7c828d594f8c89a9de74d616a0510a8677d0b19c0809758601b5d52c` (unchanged from wave 5) |
| `ArenaBasics.cs` | 1205 | `0f6cde2c1ba950ff69d6f41fec436ab762fddabf04306afebfe640dce40c8d74` (scaffold, unmodified — verified byte-identical to `templates/botarena-generic-actor/ArenaBasics.cs` by diff this wave, apart from the bot name in one doc-comment) |
| `GateStone.csproj` | — | `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573` |
| `botarena.json` | — | `d94f19d152b951c206aab17f23589b59c00c5ab13364b91f592bbe7fca38071d` |

Evidence layout: `evidence/t4/` (suite-5 report + 36 probe replays including the
hash-linked T3 and T2 prerequisites), `evidence/pairs/` (one WASM match per class
pair against the rebuilt wave-5 predecessor, all three `nilbots verify` OK).
Replays are stored gzipped — the uncompressed suite is still 213 MB, the gzipped
set is under 1 MB and the same bytes; `gunzip -k` before `verify`.

## Doctrine delta, in one paragraph

The ledger is unchanged and the capture arithmetic is untouched: one unit, **one
tick of objective weight**; a kill priced as the enemy's weight times the absence
its own lifecycle profile declares; the turret rented in the windows where weight
is worth nothing and handed back the tick the point starts paying again; relief
demanded according to the declared body curve; the shield a parry and mostly
declined. What wave 6 adds is the other side of that balance sheet — **what our
own bodies cost each other**. Wave 5 could price every enemy body and no sibling,
so a relief body that could not reach the point because the gate body stood in the
doorway was a cost the arithmetic simply could not see, and on the board it looked
like bodies walking into each other (59 refused steps in twelve games, eleven of
them into a sibling that physically could not move), queueing behind each other in
corridors (18 wasted ticks), and charging the team 180 ticks of avoidable detour.
The fix rests on one contract fact: **one submitted artifact controls every one of
a participant's lives, and team perception publishes each sibling's exact position,
facing, form and windup** — so a sibling's plan is *derivable*. Every body runs the
same planner over the same frozen union and reaches the same answer, and the crew
coordinates with no message, no shared memory and no leader. What ships is a
**transit ledger**: each body derives the routes of its higher-precedence
siblings, reserves the tiles those routes need this tick and next out of its own
graph, extends that reservation through a whole one-tile corridor when a sibling is
entering one, and steps off ground it is standing on that it owes; plus a **rally
lane** that derives the tile our next automatic arrival will take from the declared
`automaticReturnPlacement` policy, the objective chain and our own advance
delta — never from the team ID — and keeps it clear while the arrival is due. The
written precedence rule is the **muster order**: a body that can capture outranks
one that cannot, then the body nearer the active objective, then ascending actor
identity; it is computed from shared observable state alone, so every life derives
the same order. Because private memory is life-scoped and observations freeze
before any same-tick decision executes, each derived sibling plan is an
approximation and never a promise, so the rules degrade into ordinary traffic
rather than into deadlock: a yield is a step and never a refusal, and a reservation
is only ever applied where the route has an alternative — measured, the "wait
rather than route through a reservation" fallback fired **zero** times in twelve
games, because there was always a way round. **Everything else I built for this
wave lost its own measurement and is not in the artifact** — including the
doorway-yield rule, which is the single most obvious answer to the behaviour that
opened the wave.

## Measured coordination-rule attributions

**The measurement design, and why it is not seeds.** Seeds are **inert on this
arm**: seeds 104729 / 130363 / 155921 produce *byte-identical decision streams*
for these bots — I compared all 1343 decisions of a match element by element —
because neither consumes `context.Random` and nothing else in the deck contract is
stochastic. Three seeds is one game. Variance therefore comes from **cell × side ×
opponent**: 3 class pairs × 2 side assignments × 2 rebuilt predecessors (wave 5
and wave 4) = **12 games**, one seed. Every candidate and every ablation runs that
exact set. Outcomes are attributed per replay by matching
`header.provenance[].name` against `result.standings.winnerTeamId`, never by the
CLI's summary column (wave-5 friction 3, still live).

Two outcome columns are reported because they sometimes disagree and hiding that
would be dishonest: **aggregate** signed territorial progress over all 12 games,
and **same-side** progress over the 6 games where my bot holds the *bulwark*
chassis — the chassis this doctrine is written for, and the column wave 5 used.
Six congestion columns are computed from replay v3 for my team only; the last three
are recomputed geometrically from the replay rather than from anything the bot
reports, by replanning every tick's routes.

Baseline = the wave-5 source rebuilt and run on the identical 12 games.

| | aggregate | same-side | record | refused steps into a sibling | — of those, into a sibling that **could not move** | wasted ticks queued behind a sibling in a corridor | ticks standing on a corridor tile | **ticks blocking a sibling in a corridor** | **sibling detour charged, ticks** | ticks two bodies shared one enemy ray |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **wave-5 baseline** | +113 | 126 | 8-4-0 | 59 | 11 | 18 | 486 | 52 | 180 | 96 |
| **wave-6 shipped** | **+214** | **147** | **10-2-0** | **11** | **0** | **1** | **338** | **21** | **61** | **59** |
| shipped minus the **transit ledger** | +113 | 126 | 8-4-0 | 59 | 11 | 18 | 486 | 52 | 180 | 96 |
| shipped minus the **rally lane** | +169 | 113 | 10-2-0 | 12 | 0 | 1 | 411 | 23 | 65 | 68 |
| shipped minus the **corridor-run** clause | +214 | 147 | 10-2-0 | 11 | 0 | 1 | 338 | 21 | 61 | 59 |

**Every column improves against the baseline, and no column regresses.**

Three things in that table are worth reading twice.

- **Removing the transit ledger reproduces the wave-5 baseline exactly** — all ten
  columns, to the unit. That is the strongest correctness check I have on the
  refactor this wave required (`Price` and `Choose` and the router all had to stop
  reading `context.Self` and start taking a body, so that a sibling's plan could be
  computed with the same code): with the coordination rules off, the rewritten bot
  plays its predecessor's game move for move.
- **The rally lane and the transit ledger are not independent, and the pair is what
  wins.** The ledger alone takes the aggregate from +113 to +169 but the same-side
  column *down* from 126 to 113; adding the rally lane takes them to +214 and 147.
  The rally lane alone does nothing at all, because its reservation only becomes a
  decision through the ledger's yield. I report the pair as one rule with two
  clauses rather than pretending to two clean attributions.
- **The corridor-run clause is exactly neutral here, and it stays anyway.** It is
  the wave's required explicit choke-precedence rule — a sibling entering a
  one-tile corridor is treated as committed to all of it, because a corridor is
  the one place a meeting cannot be resolved by stepping aside — and it changes
  nothing on this map for a reason worth knowing: **no corridor on
  `frontline-labs-01` is longer than two tiles**, and the default reservation is
  already two. The runs are `(8,7)(9,7)`, `(13,7)(14,7)`, `(11,2)(11,3)`,
  `(11,11)(11,12)` plus the singletons `(5,7)` and `(17,7)`. Suite 5's map holdout
  runs `thin-fronts`, where that coincidence need not hold. A clause that is free
  here and load-bearing there is worth four lines.

### The four rules I built, measured, and did not ship

This is the most useful part of the wave, because three of the four are the
*obvious* implementation of a coordination bar and all four fail their own test.

**1. The doorway yield — "a body at rest steps out of a corridor a sibling
needs".** This is the direct answer to the behaviour that opened the wave, and I
implemented it twice: first as a blanket rule, then properly priced — the
sibling's route planned twice, once with the tile free and once with it walled, the
yield firing only when the saving beats the tick it costs, with exceptions for an
assigned station and for a corridor currently denying an enemy. Then I decomposed
it, and the decomposition is the finding:

| variant | aggregate | same-side | ticks blocking a sibling in a corridor | sibling detour, ticks | ticks standing on a corridor tile |
| --- | --- | --- | --- | --- | --- |
| yield ON, corridor-shoulder penalty ON | +188 | 153 | 33 | 79 | 317 |
| **yield OFF**, penalty ON | +188 | 153 | 33 | 79 | 317 |
| yield ON, **penalty OFF** | +214 | 147 | 21 | 61 | 338 |
| **both OFF (shipped)** | **+214** | **147** | **21** | **61** | 338 |

The priced yield fired 3–4 times in twelve games (4 with the penalty on, 3 with
it off) and changed **nothing** either way — in both pairs of rows above, every
outcome and every congestion column is identical with the yield enabled and
disabled. All
of the apparent effect of "the doorway rule" belonged to a different line: a −30
station penalty on shoulder tiles that happen to be corridors. And that line, on
its own, **cost 26 points of aggregate progress and made the owner-visible
silliness worse by its own direct measure** — ticks in which a body of mine stood
on a corridor tile that lengthened a sibling's route went from 21 to 33, and the
detour those ticks charged the team went from 61 to 79. It did buy a quieter
*occupancy* number (338 → 317 ticks standing on corridor tiles at all) and better
co-exposure (59 → 41), which is exactly the trap: the intuitive metric moved the
right way while the metric that matches the complaint moved the wrong way.

The explanation is the transferable part. **The transit ledger fixes corridors by
routing, not by yielding.** A reserved tile is simply not in the graph, so the
second body never walks into the first and never queues behind it. Adding a yield
makes the *first* body move too, and two bodies shuffling around one doorway
occupy more corridor between them than one body standing in it. The yield is the
intuitive fix; the reservation is the real one. The wave's brief asks for an
explicit choke precedence rule, and it has one — the muster order, applied to the
whole corridor by the corridor-run clause. It is a routing rule, not an etiquette
rule.

**2. "A sibling that cannot move is a wall, not a toll."** Eleven of the
baseline's 59 own-body collisions were literally a body walking into its own
anchored turret, twice in a row (`bulwark-vs-bulwark`, tick 336 into
`bulwark-prime`, tick 337 into the same body now `bulwark-prime-turret`). Making
an immobile sibling impassable rather than a soft cost is one line and looks free.
Measured: the transit ledger takes that count to **zero without it**, and adding it
on top moved twelve games by +4 — inside the noise — while constraining every
route. A routing constraint that removes no silliness only costs routes.

**3. The muster claim — "each body claims its assigned tile out of the pool".**
The joint station assignment is the textbook answer to two bodies converging on one
tile, and I built it in full: muster order, claims removed from the pool, later
bodies excluded. It cost **30 points of aggregate progress and removed no
congestion**, and splitting it apart says exactly why: the claim on *objective*
tiles is **precisely inert** (+0, every one of the twelve games byte-identical),
and the claim on *shoulder* tiles is the entire loss. Objective tiles are a
preference **list**, not a target, and presence **is** the score — so two bodies
wanting one point tile is not a problem worth solving, and it is the arithmetic
that sends both bodies to the point in the four-slot cell that wins that cell.
Shoulders are few, and pushing the second body to the next-best one is materially
worse ground. What survived is the muster **order**, which is the precedence rule
the transit ledger consumes — the ordering paid, the claiming did not.

**4. Enemy-relative spacing.** Bar (4) of the coordination brief. I implemented
all three shapes the ruleset publishes: an ordinary enemy ray covering two of my
bodies; a **volley fan**, whose `volley.projectileCount` neighbouring headings land
on the same tick; and a **deflection return**, where a bolt of mine arriving inside
a guarding form's quadrant comes back along the exact reverse heading from the
shell's tile, so a sibling behind me on my own firing ray stands on ground my own
gun makes dangerous. It cost 17 aggregate and 10 same-side, and — decisively — it
made **its own metric worse**: ticks with two of my bodies on one enemy firing ray
went from 41 without it to 45 with it. A penalty that moves a body off one shared
lane onto a tile sharing a different lane has bought nothing and paid a tick. Bar
(4) is met instead by the cruder geometric penalty the lineage already had
(`StoneGround.Clumping`, a cost for standing on a line with a sibling inside three
tiles), and the two shapes that penalty misses are the two that barely arise in
these cells: the only volley owner is the striker, and a shell in my own lane is a
shot `StoneAim` already declines unless the contact shatters it. The shipped bot's
co-exposure still improves 96 → 59 against the baseline, entirely as a side effect
of the transit ledger.

### One measurement that was a bug, and what finding it cost

The first rally-lane implementation counted **this body** among the tiles already
occupied, so it reserved the tile *after* the one the body was plugging — ground
nobody wanted — and never asked anybody to move. It measured as exactly inert
(identical progress, identical plugged-tile count), and I nearly wrote it up as
"the contract's placement policy is robust against own congestion here, so bar (3)
needs no bot rule". Fixing it — only a body that *cannot* vacate takes a tile out
of the running — moved the aggregate by +23 and **restored the
`bulwark-vs-fabricator` base-breach that the intermediate revision had lost**. The
lesson is narrow and worth stating: when a coordination rule measures as exactly
inert, the first hypothesis should be that it never fires, not that the situation
never arises — and the cheap check is to count its firings in the decision stream.
Over the shipped artifact's twelve games: 12 transit yields (6 of them rally-lane),
0 queue-behind-a-sibling waits.

### Where the effect actually comes from

Worth recording because it surprised me and it is the reason the doorway yield
failed: 12 explicit yield *actions* across 7222 decisions cannot be worth 101
points of progress, and they are not. The yield is the visible part; the work is
done by the **reservation**. A tile removed from the router's graph changes the
whole route, continuously, for as long as it is reserved. The rule that reads like
"occasionally step aside" is really "plan as if your siblings existed".

## Records vs the rebuilt wave-5 predecessor, on the deck game

12 games, mirrored: 3 class pairs × 2 side assignments × 2 rebuilt predecessors,
`--ignore-declared-classes` with an explicit `--classes` so a single-class author
can be measured across pairs, attributed per replay by provenance name against
`winnerTeamId`.

| opponent | record | aggregate progress |
| --- | --- | --- |
| rebuilt **wave-5** predecessor (6 games) | **4-2-0** | **+38** |
| rebuilt **wave-4** predecessor (6 games) | **6-0-0** | **+176** |
| **total (12 games)** | **10-2-0** | **+214** |
| *wave-5 baseline on the same 12* | *8-4-0* | *+113* |

Per game against the wave-5 predecessor, with the control that makes it readable.
The control is the baseline row of the same table: the wave-5 source against its
own rebuild scores **3-3-0 at exactly +0**, every game an exact mirror — which is
what two byte-identical policies must do, and which pins the side advantage in each
cell.

| cell | side my bot held | wave-5 self-control | GateStone w6 |
| --- | --- | --- | --- |
| `bulwark-vs-bulwark` | 0 (bulwark) | +1 W, max-ticks | **+4 W**, max-ticks |
| `bulwark-vs-bulwark` | 1 (bulwark) | −1 L, max-ticks | **+20 W**, max-ticks |
| `bulwark-vs-fabricator` | 0 (bulwark) | +30 W, breach t195 | **+30 W, breach t353** |
| `bulwark-vs-fabricator` | 1 (fabricator) | −30 L, breach against | −30 L, breach against |
| `bulwark-vs-striker` | 0 (bulwark) | +17 W, max-ticks | **+25 W**, max-ticks |
| `bulwark-vs-striker` | 1 (striker) | −17 L, max-ticks | **−11 L**, max-ticks |

The row that matters most is `bulwark-vs-bulwark` **side 1**. The control shows
team 0 wins that cell whenever the policies are identical; wave 6 wins it from
**team 1**, the disadvantaged side, by +20. Both remaining losses are my bot
playing the *other* chassis — a fabricator and a striker — and both were already
class facts rather than bot facts in wave 5: whichever bot holds the bulwark
chassis wins those cells. Wave 6 improves even those: the striker-chassis loss
narrows from −17 to −11. **On its own chassis, wave 6 wins all six games against
both predecessors**, and its one slower result is the fabricator breach arriving at
t353 rather than t195 — the same win, later, which is the honest price of a body
that routes around its sibling instead of through it.

Against the wave-4 predecessor the margin widened from 5-1-0/+113 to
**6-0-0/+176**, and the wins got sharper rather than merely wider: **four of the
six end in a base breach, against two for the baseline.**

Frozen-artifact WASM confirmation, one match per pair against the rebuilt wave-5
predecessor, seed 104729, all three `nilbots verify` OK, each reproducing its
in-process result exactly:

| pair | result | replay hash |
| --- | --- | --- |
| `bulwark-vs-bulwark` | win, max-ticks, +4 | `ec0a04274dbc3c0fa9385504fa3a95f2faf28890e0b8c916b8de576bbd5e2b36` |
| `bulwark-vs-fabricator` | win, base-breach t353, +30 | `47472e3aa847af06d35e16aa20ed28affca4b8d097fa5dfba80510e910fc0314` |
| `bulwark-vs-striker` | win, max-ticks, +25 | `a12e967754587a83ce01e719d3128344bc420838a19bb6618597632bd0ec022a` |

A full 12-game sweep of the **frozen** WASM artifact reproduced the whole
in-process table with no divergence at all: +214 aggregate, 147 same-side,
10-2-0, and every congestion column to the unit (11 refused steps into a sibling,
1 tick queued in a corridor, 338 corridor-standing ticks, 21 corridor-blocking
ticks, 61 detour ticks, 59 shared-ray ticks, 12 transit yields, 0 queue waits).

## Top 3 frictions

**1. Seeds are inert on this arm, nothing says so, and every surface that would
warn you agrees with you instead.** Neither of these bots consumes
`context.Random`, and nothing else in the deck contract is stochastic, so seeds
104729 / 130363 / 155921 produce *byte-identical decision streams* — I compared all
1343 decisions of a match element by element and they matched exactly. But the
replay **hashes differ**, because the seed is in the header, so you get three
directories, three distinct hashes, three summary lines, and one game. My first
candidate table was 3 seeds × 3 pairs and I read `[30, 30, 30]` as agreement across
seeds when it was one number printed three times; the wave-5 protocol I inherited
has the same shape, and its "3 seeds × both assignments = 6 games per pair" was
really 2 games per pair. For a wave whose entire method is A/B on fixed seeds this
is the most expensive trap in the harness, and it is cheap to close from either
end: have a multi-seed run report when two seeds produced identical actor decision
streams, or state in the rule card that the seed reaches bot behaviour **only**
through `context.Random`, so a bot that never calls it is seed-invariant. Until
then the honest design is to take variance from cell × side × opponent, which is
what this freeze does.

**2. `qualification.json` carries a `coordinationGradeAwarded` field, schema 6
ships it, and no available suite ever fills it — in the coordination wave.** The
report produced by this wave's mandatory gate has exactly one field about exactly
the thing the wave is about, and it comes back `null`. `--suite` offers
`frontline-qualification-1` … `-5`; suite 5 is the highest and the first
balance-eligible one; none populates it. So an author doing an own-congestion pass
gets **no signal at all** from qualification and must build the instrumentation
from scratch — I wrote about 250 lines of replay-v3 analysis to count refused steps
into a sibling, ticks queued behind one in a corridor, ticks standing on a corridor
a sibling needed, and the detour that charged, and had to replan every tick's
routes geometrically to get the last two. Every other author in this wave is
writing that code this week, differently, with incomparable definitions, which
means the wave's central question cannot be compared across entrants. Either
populate the field from a suite — even a crude "own-body refused steps per 1000
body-ticks" would make waves comparable — or remove it from the schema so it stops
promising a grade that does not exist. A smaller instance of the same thing:
`experiment frontline-labs` correctly stopped writing `viewer.html` in 0.9.22, but
`experiment frontline-labs qualify` still writes one per probe — 36 of them — so
the suite's footprint is still the 213 MB wave 5 complained about, and `--viewer`
is not accepted by `qualify` at all, so there is no way to ask for the new
behaviour on the one command that still has the old one.

**3. A sibling's plan is derivable but its private memory is not, and nothing marks
that boundary — so the one shared-state rule an author most wants to write is the
one that silently cannot be trusted.** The design that actually works here rests on
a fact the docs state in two places and never join up: "one submitted artifact
controls all of a participant's body lives" (rule card, Runtime section) plus
"every life receives current allied body state and the union of what declared
allied sensors see" (team perception) together mean a sibling's decision is
**recomputable** — run the same planner on the same frozen union and you get its
answer, with no message and no shared state. That is genuinely elegant and it is
the entire basis of this wave's fix. But the recomputation is *partial*, because
"private memory is life-scoped" means a sibling's refused tiles, its dodge history
and its form-dwell counter are invisible, and all three are inputs to its planner.
The result is a derivation that is right most of the time with no way to know
which times. Nothing warns you, and the observation is tantalisingly close to
closing it: it publishes an ally's `previousActionResolution` (so you can see its
last step was **blocked**) but not, in usable form, the tile it was blocked on, and
it publishes `PendingSameLifeTransition` for an ally — proving the engine is
willing to publish an ally's commitments when it has one — while publishing nothing
about the commitment an ally makes every single tick, its next step. One field —
the ally's last *submitted* direction, or a per-life "next tile" — would turn a
derivation into a fact and the difference between a coordination layer that yields
correctly and one that yields probably. Failing that, the packet should say plainly
that sibling plans are derivable-but-approximate, so that authors write rules which
degrade into traffic rather than rules that assume agreement. I found this by
building the pessimistic version first; an author who trusts the derivation will
find it as a deadlock.

### Smaller notes

- **The 2 MB source cap is a real change and it shows.** Wave 5 recorded deleting
  documentation to fit 256 KB. This freeze is 213 KB of source carrying full
  reasoning, including the four rejected rules argued out **in the code**, at the
  call sites where the next wave will otherwise re-derive them. Nothing was
  trimmed to fit.
- **`--no-cache` WASM builds went from about two minutes to about nine seconds on
  this host**, which changed my method rather than just my patience: I iterated
  in-process (1.1 s build, 45 s for a 12-game sweep) and could afford to confirm in
  WASM (39 s for the same 12 games) far more often than the wave-5 notes suggest is
  affordable. Eleven ablation variants were only affordable because of it.
- **Replay writes and `nilbots verify` behaved exactly as advertised.** No
  truncated replay, no silent partial write, and all three pair replays verified
  first time. The 0.9.22 note about failing loudly on a full disk never got to
  prove itself, which is the correct outcome.
- **The choke set is a map read, not a constant.** One-tile corridors are computed
  at `StartLife` from `contract.Map.TileRows`: an open tile whose open **cardinal**
  neighbours are a single opposite pair, plus dead ends. Cardinals only,
  deliberately — bodies move one cardinal step per tick, so a diagonal gap is not a
  way past a body even where a bolt fits through it. Hard-coding the ten resulting
  coordinates would have worked on this map and failed on `thin-fronts`, which
  suite 5's holdout runs.
- **The rally tile is derivable, and the derivation is worth writing out.**
  `lifecycle.automaticReturnPlacement` reads
  `own-side-chain-adjacent-objective-tile-in-team-advance-order-then-assigned-spawn`,
  which resolves to: the objective region at `activeIndex − myAdvanceDelta`,
  ordered rear-most-first along my own advance direction, first free tile, else the
  assigned spawn. Confirmed against 14 observed arrivals in one replay — team 0 with
  active objective 2 arrives at `(6,5)`, the rear-most tile of region 1; with active
  objective 1 it arrives at `(3,8)`, the rear-most of region 0. The advance
  direction itself is derived from the objective chain and the signed index delta,
  **never from the team ID**, which is what makes one rule work from both sides.
- **`Price` had to take a body rather than read `context.Self`, and getting that
  wrong is silent.** A joint assignment computed from one body's price is a
  different assignment in every life, because `WantSurplus`, `Throttled` and
  `WeightTick` all depend on whether *this* body stands on the objective and what
  *its* weight is. The team-level halves — the hold, the presence counts, the relief
  clock — are identical in every life, because every life receives the same frozen
  union. Split them wrongly and the code still runs, each body still plans
  coherently for itself, and the crew quietly disagrees.
- **`--five-slots` is still rejected rather than inert-omitted** (wave-5 friction 2,
  unchanged), and it broke a loop in this wave's evidence script too. Separate and
  mine, but worth recording for anyone scripting this on a Mac: `zsh` does not
  word-split unquoted parameters, so the `extra="--five-slots wane"` idiom that
  works in `bash` hands the CLI a single argument and produces
  `Unknown option(s): --five-slots wane`. The CLI's error message was clear enough
  to diagnose in one read.
- **Hardcoding temptations resisted this wave:** the choke coordinates; the rally
  region and its ordering; the advance direction (derived from the chain and the
  signed delta, not from `teamId`); the four-tile size of an objective region; "the
  turret is the immobile form" (asked of the form catalog's allowed-action list);
  "a windup is one tick" (read from the route); the volley's fan width (read from
  `volley.projectileCount`, before that rule was cut); and the reservation depth,
  which is 2 because a reservation covers this tick and next, not because 2
  measured best.
- **Times** (this host): in-process build 1.1 s; `--no-cache` WASM build ≈ 9 s;
  suite-5 qualification in WASM **6.2 s** for 36 probe replays across three
  hash-linked tiers; one 500-tick WASM match ≈ 3.2 s; a full 12-game A/B sweep 45 s
  in-process and 39 s in WASM. The crew layer runs up to five Dijkstra searches per
  tick over 1380 (tile, facing) states and never came near a fuel, memory or
  wall-clock limit in WASM.

## Repairs and strategy passes (the improvement budget, itemised)

Every row is the same 12 games (3 pairs × 2 sides × 2 rebuilt predecessors, one
seed), reported as aggregate signed territorial progress and the same-side bulwark
subtotal. Losers were reverted and their reasoning kept at the call site.

| # | change | aggregate | same-side | kept |
| --- | --- | --- | --- | --- |
| 0 | inherited wave-5 policy, unchanged, on the deck game | +113 | 126 | baseline |
| 1 | refactor so a sibling's plan is computable with the same code (`Body`, roster, per-body `Price`, route prefixes), all rules off | +113 | 126 | yes — verified behaviour-identical to row 0 in all ten columns |
| 2 | all six candidate rules at once | +114 | 93 | no — flat, and it lost the fabricator breach |
| 3 | drop "an immovable sibling is a wall": redundant with the transit ledger | +118 | 97 | yes — dropped |
| 4 | drop the muster tile **claim**; keep the muster **order** | +144 | 123 | yes — dropped |
| 5 | fix the rally-lane occupancy bug (self is vacatable) | +171 | 143 | yes — restored the fabricator breach |
| 6 | drop enemy-relative spacing: loses, and worsens its own metric | +188 | 153 | yes — dropped |
| 7 | price the doorway detour rather than yielding blindly | +188 | 153 | superseded by row 8 |
| 8 | decompose the doorway rule; drop **both** halves (the yield is inert, the corridor-shoulder penalty costs 26 and worsens the direct measure) | **+214** | **147** | yes — dropped |
| 9 | add the corridor-run reservation as the explicit choke precedence rule | +214 | 147 | yes — exactly neutral here, correct on longer corridors |

Honest gap against the packet's archive rule: the intermediate revisions and the
eleven ablation variants were built as copies in my scratch directory rather than
frozen as separate source trees, so what survives of them is the tables above plus
their complete replay sets under
`sandbox/gate-stone-w6-scratch-9d2f41e7/runs/`. Only the final revision is frozen
as source, which is what this brief's single freeze location asks for — and per the
coordinator's mid-wave warning, keeping those variants out of this directory is a
freeze-integrity requirement rather than tidiness.

Qualification history: one attempt, exit **0**, **T4**,
`balanceEvidenceEligible: true`, prerequisite T3 PASS, all five T4 probes PASS.
The suite runs the classless duel-depth union profile and the revision needed no
repair to pass it: the crew layer is contract-driven throughout — it asks the form
catalog which forms can move, the route catalog what a windup costs, the lifecycle
what the placement policy is, the mode binding which way we advance, and the map
where the corridors are — so it degrades correctly on a profile with no classes, no
shell and no turret.
