# DX — VectorEdge revision 6 (wave 6, class striker, doctrine pressure-duelist)

**Lineage** vector-edge-v1 · **Revision** 6 · **Role** verdict-doctrine ·
**Target** T4 (`frontline-qualification-5`) · **Budget** one coordination pass on
multi-body play; mechanical and contract repairs free.

## Isolation statement

I read only the permitted material: the author packet
(`docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aa…`), the
rule card (`docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e…`), the classes
addendum (`docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `2333bd3c…`), the
`templates/botarena-generic-actor/` scaffold (`ArenaBasics.cs`, sha256
`567e9faf…`), `src/BotArena.Sdk/` types and their XML documentation, my own frozen
wave-5 directory and its replays, and the sandbox CLI at `sandbox/cli-publish/`
(`nilbots 0.9.22`).

I did not open any other entrant's source, replays, standings, or aggregate
balance reports, nor Engine/App implementation, nor any cohort directory other
than my own lineage's. `arena-bots/frontline-labs/classes-wave-6-2026-07-30/`
contains sibling entrant directories; I listed that directory twice, to confirm my
own output path existed, and opened nothing inside any of them. My wave-5
predecessor was **copied out** into private scratch and rebuilt there, so that
building it could not touch the frozen directory.

Private scratch: `sandbox/vector-edge-w6-scratch-8b41fe7d/` — uniquely named, not
a shared or guessable path. Nothing was written outside that directory and my own
output directory. Every variant and ablation source lives in scratch and none of
them is inside the frozen tree, because `nilbots build` globs every `.cs` under
the project directory and an archived variant would make the freeze fail to
rebuild with duplicate-member errors. The last thing I did was rebuild
`--no-cache` **from the frozen tree** and confirm it reproduces the shipped
artifact hash exactly.

**One incidental exposure, disclosed as the packet requires.** Mid-wave my sweeps
slowed to roughly a quarter of their earlier throughput and I ran `ps` to find out
whether one of my own matches had hung. It had not — the host was running several
other entrants' sweeps concurrently — and the process table showed me their
scratch-directory names, their bot paths, and the flags they were passing. I
opened none of it: no source, no replay, no report and no standing was read, and
nothing in that output influenced a decision here, because a flag list is not a
doctrine. It is still more than I was entitled to see, so it is on the record. The
lab-level lesson is worth more than my apology: **a shared host leaks other
entrants' experiment designs through the process table**, and `ps` is the first
thing any author reaches for when a sweep stalls. Isolated authorship on one
machine needs either separate accounts or an explicit "do not inspect the process
table" rule, because as it stands the leak is the natural consequence of ordinary
debugging.

Everything I sparred against is my own source: the rebuilt wave-5 predecessor, and
twenty-five variant builds of my own revision-6 source — eight whole-build stages
of the ladder and seventeen single-constant ablations across two bases — each
listed below with its artifact hash. The cross-class fixtures are that same predecessor artifact
resolved onto a **bulwark** and a **fabricator** chassis by an explicit
`--classes` pair — my own striker doctrine wearing another class's stats, a fixed
opponent for an A/B rather than a bulwark or fabricator doctrine, and reported as
what it is.

## Freeze identity

| Field | Value |
| --- | --- |
| Output directory | `arena-bots/frontline-labs/classes-wave-6-2026-07-30/vector-edge/` |
| Class (declared in `botarena.json`) | `striker` |
| Coordination rules shipped | 7 (of 8 built; pair spacing measured and deleted) |
| `bot.wasm` sha256 | `3ca784538f34d157de83de2003feb6c471f360627ec0e4902ed8016cfa9075e6` |
| Build | `sandbox/cli-publish/botarena build <project> --no-cache`, run from the frozen tree |
| Build-cache key (frozen tree) | `f9ed29ad4176b679ec2c6fa6b0a0b93502b46fb8864f51e6feb45ec2cada6637` |
| CLI / SDK / rules | nilbots 0.9.22 · SDK+Guest 0.10.6 · game rules 0.5 · runtime protocol 0.1 |
| Compiler | NativeAOT-LLVM 10.0.0-rc.1.26306.1 (platform-matched Docker builder) |
| Qualification suite | `frontline-qualification-5` (`frontline-duel-depth-union-t4-v1`) |
| Qualification exit code | **0** |
| Tier awarded | **T4** · `balanceEvidenceEligible: true` · `profileComplete: true` |
| Probes | prerequisite T3, suppression-choke, entry-initiative, prediction-chamber, front-rotation, map-holdout — all PASS |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| `qualification.json` sha256 | `01a8aaab13647ae6d752be36c618b20af82065b123337e7e1cb8a8964733565e` |
| Evidence | `evidence/t4/qualification.json` plus every probe replay and viewer |
| Per-file source hashes | `sha256s.txt` |
| Deck cells measured on | `…-striker-vs-striker-sail-open-facing-locked` (rules fp `218b6f06…`), `…-bulwark-vs-striker-sail-open-facing-locked` (`dbe453aa…`), `…-fabricator-vs-striker-deck-facing-locked` (`0922aa93…`, topology `…asymmetric-slots-4-3-v1`) |
| Sparring baseline | wave-5, rebuilt from its own frozen source under 0.9.22: `e3e1a44826937e57efd9d1f66524202a5c82178f46f0d7eedacbc11ae04777c0` |
| Git | nothing committed |

## The platform moved under the wave-5 conclusion, and that has to come first

Wave 5's headline was that revision 5 turned the `bulwark-vs-striker` cell into
20-0-0 by breach at +30. **That does not replicate on CLI 0.9.22.** The *frozen*
wave-5 artifact — `9912013a…`, the exact bytes that measured it — now loses that
cell at −37 on the tick cap, with zero runtime faults on either side and no
contract-compatibility error. Same bytes, same nine flags, opposite result. The
cell's rules fingerprint is `dbe453aa…` today and wave 5 recorded no fingerprint
for it, so I cannot say what moved.

Everything below is therefore measured against my **rebuilt** predecessor
(`e3e1a448…`) on 0.9.22, which is the only honest baseline available, and none of
my absolute numbers are comparable to wave 5's. This is friction 1.

## What the assignment asked, and what was actually wrong

The owner's complaint was bots making silly decisions — "blocking an ally's path
in a choke" — and my brief named my own gap: bodies contesting the same firing
seats and blocking each other's rotations in corridors.

So the first thing I built was an instrument, and the instrument changed the
assignment. Over replay v3 I count, per team: moves blocked by a body of my own
team; two of my bodies submitting a move to one tile; stepping into a tile a
sibling vacates; rotation flips (turn to X, turn straight back next tick);
net-zero travel (back on a tile held 2–4 ticks ago having moved in between, which
is the shape a reversal takes under `facing-locked`, where each leg costs a
rotation); route steps refused because a sibling stands on them; one-tile-corridor
jams; a sibling parked on a ray my own facing arms; and two of my bodies on two
*different* rays of one enemy facing.

**Revision 5 blocked a sibling exactly zero times** — in every cell, on every
seed. Its `Contested` avoid-set already kept bodies off each other's tiles. What
it did instead was thrash:

| per 1000 two-body-ticks, revision 5 | mirror | bulwark cell | fabricator cell |
| --- | --- | --- | --- |
| moves blocked by own body | **0** | **0** | **0** |
| route steps a sibling was standing on | 152 | 79 | 90 |
| rotation flips with a sibling alive | 90 | 72 | 42 |
| net-zero travel with a sibling alive | 78 | 120 | 146 |

The mechanism is legible straight off a replay, at ticks 313–323 of the bulwark
cell. Unit 0 stands at (18,8) and wants (18,7); unit 2 is standing on (18,7); the
route search returns the *cheapest legal* first step, which is (18,9) — a step
**away**. Next tick the step back is cheapest again. Under `facing-locked` each
leg also buys a rotation, so the body spends four ticks arriving where it started,
does it twice, and only advances when its sibling dies. That is the owner-visible
silliness on this lineage, and it is not a blocking problem at all: it is a
routing rule that never asked whether a step goes anywhere.

## The eight rules, seven shipped, and the measured attribution of each

Seven live in `Traffic.cs` behind their own constants, so every ablation build
differs from the shipped one by a single symbol. The eighth — pair spacing — was
built, measured, and **deleted**; it is listed here with its numbers because a rule
that failed the wave's own test is a result, and because bar 4 asked for it.

There is no shared state and there cannot be: a life is a fresh instance with empty private memory and never
sees an ally's current action. What every life *does* share is the frozen
observation, so each rule is a function of that alone — and each is written so
that two bodies applying it reach **complementary** conclusions rather than the
same one.

| # | rule | what it says | measured effect on the shipped build |
| --- | --- | --- | --- |
| 1 | **Precedence** | Own bodies are totally ordered by *(walk distance to the active objective, then MORE health, then lower actor identity)*. The nearer body keeps its tile and its route; the further body yields. | **+0.50** in the bulwark cell, nothing elsewhere. Small because it mostly agrees with revision 5's identity order on this topology — the three slots rally forward to the same region, so distance rarely separates them. Kept because it is the written rule bars 1–2 require, because it is what makes rule 8's symmetry break, and because a strict TOTAL order is what makes rule 3 acyclic: the most senior body claims nothing from anyone, so somebody always moves. Distance first because the nearest body *is* the capturer; health second because the body that can survive a corridor should be the one in it; identity last only so the order is total. |
| 2 | **Yielding is a hold, not a detour** | A route step must strictly reduce the route. With no reducing step available the body keeps its tile and spends the tick on its gun. | **The revision.** Removing it costs all four mirror wins and 29.75 mirror progress, and 24.75 in the bulwark cell; it also doubles the jammed-step rate (70 → 152 per 1000 two-body-ticks) and the rotation thrash (53 → 90). It gains 1.50 in the fabricator cell, which is the only cell where any of this pass is worth less than nothing. |
| 3 | **Two-tick route claim** | Every senior sibling claims the tiles its shortest routes need this tick and next — the union over tied routes, because a sibling breaks its own ties on a private stream that is not derivable. A **preference**, never binding. | The wave's clearest trade: removing it *gains* 1.50 in the bulwark cell and takes own-traffic blocked ticks from **8 to 110**, because it is the set rule 8 tests membership in. Kept on the brief's second clause — measurably removes the owner-visible silliness — with the 1.50 stated rather than buried. |
| 3b | **…and the next tick** | The depth-2 half, and the only genuinely new half: revision 5 already avoided the tiles a higher-identity ally could reach *this* tick. | **Inert** — every cell identical, blocked ticks identical. Kept because bar 1 says "this tick or next" and it costs nothing; reported as inert rather than as a feature. |
| 4 | **Choke precedence** | A choke is a one-tile corridor — an open tile whose open cardinal neighbours lie on one axis — and connected chokes form a RUN. A run admits one own body at a time: the body inside keeps it, a body outside prefers not to enter a run a senior owns, and two bodies already inside one run resolve by precedence, with the junior **backing out along the run**. | **Inert on this map family, and the back-out never executes on it.** The map's one-tile runs are (5,7), (8,7)+(9,7), (13,7)+(14,7), (17,7) and four dead-ends; they are at most two tiles long and my bodies enter them single file, so two of mine were never simultaneously inside one — zero occurrences in every match of the wave, on the deck map and on the thin-fronts holdout, whose wall grid turns out to be identical. Kept because bar 2 asks for the rule and because it is derived from the wall grid rather than from coordinates, so it holds on a map with longer passages. Reported as measured-dead. |
| 5 | **Do not rally into own traffic** | Keep clear of the tile an imminent own arrival will take, and its only exit. Read from `LifecyclePending.ReservedPosition` where a reservation exists; derived as the rear-most free tile along this team's own advance direction where it does not. | **Inert as shipped.** Scoped to the whole own-side objective region — my first version — it cost **23.5 points of progress and two wins** in the bulwark cell, because that region is ground. Narrowed to the one tile the contract would actually pick, it changes no record and no metric. Kept as the written rule bar 3 asks for; the *wide* version is the measured result worth reporting. |
| 6 | **Pair spacing — BUILT, MEASURED, DELETED** | Among poses of EQUAL value, refuse the one an enemy facing's three rays cover together with a sibling on two *different* rays: one volley, two hits. | It works on its own metric and it loses the game. Shared-fan poses fall from 34 to 30 and sibling co-linearity from 461 to 439 per 1000 two-body-ticks — roughly a tenth — and the twenty-seed bulwark comparison is **13-7-0 at +8.85 with it and 13-7-0 at +10.80 without**, the same record for 1.95 less progress and, more tellingly, **0 breach wins with it against 13 without**: it converts decisive wins into tick-cap wins. That is the wave's test failed, so it is gone from the source rather than parked behind a false constant. As a filter on the DESTINATION set — my first version — it was far worse, costing **7.5 mirror points and 5.0 in the bulwark cell**, because a destination chosen for its bearing to a contact is exactly the construct revision 5 measured at 2-38-0 and deleted. |
| 7 | **Distinct rays** | Among equal-value firing seats, take a launch ray onto a contact that no senior sibling already holds — asked over every visible contact, not only the nearest. | **Completely inert: no record moves, and neither does the metric it targets.** This is my brief's named gap and the honest answer is that the rule as written almost never fires, for a legible reason. It requires *both* bodies to be armed on the *same* ray onto the *same* contact, with a clear line inside the travel budget — a conjunction that a facing quadrant and three-tile spacing make rare. The looser phenomenon my instrument counts (a sibling anywhere on a ray this facing arms, 318–452 per 1000 two-body-ticks) stays exactly where it was — and it is *mechanically free*, because allied projectiles pass through allies, so a sibling on my launch line costs this team nothing at all. The half of co-linearity that does cost something is shared exposure, and that was rule 6's job; rule 6 lost and is gone. Rule 7 is kept as a correct read that would matter on a contract where allied bolts stop on allies, and reported as inert here. |
| 8 | **Race memory** | After a step is blocked by this team's own traffic, the junior body leaves that tile alone for two ticks while the senior one keeps trying. | **Own-traffic blocked ticks 110 → 8, with every cell's W-L-D and progress unchanged to the decimal.** The only rule in the pass that meets the brief's test cleanly, and the only one that acts on a *fact* — `PreviousActionResolution` publishes the collision — rather than on a forecast of a sibling's intent. |

## The ladder: seven whole-build stages, three of them thrown away

Each row is the same source with one construct changed, built through the same
controlled toolchain and run on the same three cells and the same four seeds
(42, 104729, 7, 1337) against the rebuilt predecessor. Progress is mean signed
territorial progress for the candidate.

| # | build | mirror | bulwark cell | fabricator cell |
| --- | --- | --- | --- | --- |
| 0 | the wave-5 predecessor itself, `e3e1a448…` (self-mirror for row 0) | 0-0-4, +0.00 | 0-4-0, −31.75 (3 breach) | 0-4-0, −30.00 (3 breach) |
| 1 | all eight rules, arrival claim on the whole own-side region, spacing filtering DESTINATIONS · `bce2df49…` | 4-0-0, +22.25 | 0-4-0, −20.50 | 0-4-0, −22.75 |
| 2 | **row 1 with every sibling claim BINDING** · `146d0323…` | **0-4-0, −30.00 (4 breach)** | 0-4-0, −18.50 | 0-4-0, −25.50 (3 breach) |
| 3 | **row 1 with only CORRIDOR claims binding** · `fcc52177…` | 2-2-0, +3.25 | 0-4-0, −22.00 | 0-4-0, −30.00 (3 breach) |
| 4 | row 1, arrival claim narrowed to ONE tile, spacing narrowed to tie-breaks · `1b64e995…` | 4-0-0, +29.75 | **2-2-0, +3.00** | 0-4-0, −23.25 |
| 5 | **row 4 with only FORCED sibling steps binding** · `a8c7997f…` | **0-4-0, −5.75** | 2-2-0, −0.50 | 0-4-0, −23.25 |
| 6 | row 4 plus race memory · `c2cd8049…` | 4-0-0, +29.75 | 2-2-0, +3.00 | 0-4-0, −23.25 |
| 7 | **shipped** — row 6 with pair spacing deleted · `3ca78453…` | **4-0-0, +29.75** | **2-2-0, +4.50** | 0-4-0, −23.25 |

Five things in that ladder are the wave, and four of them are things I got wrong
first.

**A route step must reduce the route, and that single rule is the revision.**
Removing it returns the mirror to the predecessor's draw — 4-0-0 at +29.75 becomes
0-0-4 at +0.00 — and costs 26.25 in the bulwark cell, while doubling the jammed-step
rate and the rotation thrash. It is also the whole of the owner-visible fix, because yielding
by walking backwards is what four wasted ticks look like on a replay. The general
form is worth more than my numbers: under `facing-locked`, a search that returns
"the cheapest legal first step" will return a step *away* from the goal the moment
the one step toward it is occupied, and will then return the step back — so any
route search on this arm needs a monotonicity guard, whether or not it has
siblings.

**Yielding absolutely is not coordination; it is not playing (rows 2 and 3).**
Making every sibling claim binding — hold the tile rather than take a sibling's
route even when it is the only route — is the textbook answer to bar 1, and it
worked exactly as designed on the instrument: jammed route steps down 85%,
rotation thrash down 61%, corridor jams to zero. It lost the mirror 0-4-0 at the
breach floor. Restricting the binding to CHOKES only, which is the principled
middle because a corridor genuinely has no second lane, lost too — and produced
*more* corridor jams than the preference did, because on this map the chokes ARE
the routes to the objective, so a body that yields a passage has conceded the
front. Both rows are in the file's own comments so the next author does not spend
the same two builds.

**Courtesy has to be scoped to what the contract actually reserves (row 4a).** My
first arrival rule kept clear of the whole own-side chain-adjacent objective
region, on the reasoning that the forward rally lands somewhere in it. That region
is *ground*: the rule told a body to vacate four objective tiles so a companion
could land on one of them, and it cost 23.5 points of progress and two wins in the
bulwark cell. The contract is more specific than I was — a reserved operation
publishes `LifecyclePending.ReservedPosition`, and where no reservation exists the
placement is the rear-most free tile measured along this team's own advance
direction. Claiming that one tile plus its only exit changes **no record and no
metric** — it is inert as shipped, and the result worth reporting is the cost of the
wide version rather than the value of the narrow one. I keep the narrow rule because
bar 3 asks for it and it is free; I would not have found the 23.5 without building
the wide one.

**Spacing was wrong twice and then still not right enough to ship (rows 4b and
7).** My first pair-spacing rule filtered the DESTINATION set to poses no single
enemy facing shares with a sibling. That is a destination chosen for its bearing to
a contact — the exact construct revision 5 built, measured at 2-38-0, and deleted
with a note saying it thrashes because under `facing-locked` a change of destination
costs a rotation and a bearing-derived destination moves every time the contact
steps. I reintroduced it through a side door and it cost 7.5 mirror points and 5.0
in the bulwark cell. Wave 5's own lesson relearned at full price: **a pose may be
preferred, never chased.** Narrowed to the tie-break and the seat score, where a
preference is free, it stopped being harmful — and it still did not earn its place.
Twenty seeds on the cell where it registers: the same 13-7-0 record, 1.95 less
progress, and 0 breach wins against 13. So the rule that satisfies bar 4 is not in
the shipped build, and the reason is a measurement rather than an opinion. What
spacing costs, on this map, is the tempo that closes a match: a body that declines
the shared ray takes a tick longer to reach the ground, and the enemy base stops
falling.

**The rule the wave was actually looking for is about a fact, not an intent (rows
5 and 6).** Row 4 wins, and it has one visible defect: on two of four bulwark
seeds two of my bodies approach one tile from opposite sides, both take it, both
block, and repeat on a three-tick cycle — turn toward contact, turn onto route,
blocked move — for 55 ticks, to the tick cap. Refusing a sibling's *forced* step
(the case where that sibling has exactly one way forward, so the tile is derivable
as a certainty rather than a guess) fixes it completely and loses the mirror
0-4-0, because hesitating in open ground is the row-2 failure in miniature. What
costs nothing at all is remembering a collision that has **already happened**:
`PreviousActionResolution` publishes the block and the direction, revision 5 fed
it into that tick's occupancy, and one tick of memory is exactly one too few —
hold, forget, collide again. Two ticks of life-scoped memory, applied only by the
body that is not senior for that tile so the symmetry actually breaks, turns 110
own-traffic blocked ticks into 8 with every cell's record unchanged to the
decimal. That is the brief's own test — measurably removes the silliness without
losing — and it is the only one of my eight rules that passes it cleanly.

## Per-rule ablations

Same three cells, same four seeds. Each row is the eight-rule build with exactly
one constant flipped, so the baseline for the *ablations* is row 6 of the ladder;
the shipped build is row 6 with rule 6 removed, and it is the second line of the
table. Deleting the rule reproduced the flipped-constant variant exactly — same
records, same metrics, every counter identical — which is the check that the
deletion changed nothing but the source.

| rule turned off | artifact | mirror | bulwark cell | fabricator cell | own-traffic blocked ticks |
| --- | --- | --- | --- | --- | --- |
| *(none — the eight-rule build)* | `c2cd8049…` | 4-0-0, +29.75 | 2-2-0, +3.00 | 0-4-0, −23.25 | 8 |
| **6 — permanently, as shipped** | **`3ca78453…`** | **4-0-0, +29.75** | **2-2-0, +4.50** | 0-4-0, −23.25 | **8** |
| 2 · yielding is a hold, not a detour | `263ea033…` | 0-0-4, +0.00 | 0-4-0, −21.75 | 1-3-0, −21.75 | 2 |
| 1 · precedence by distance, then health, then identity | `e1a3bd05…` | 4-0-0, +29.75 | 2-2-0, +2.50 | 0-4-0, −23.25 | 4 |
| 3 · the sibling route claim (both depths) | `5ab0d75e…` | 4-0-0, +29.75 | 2-2-0, +4.50 | 0-4-0, −23.25 | 110 |
| 3b · the claim's NEXT-tick depth alone | `5eca3de4…` | 4-0-0, +29.75 | 2-2-0, +3.00 | 0-4-0, −23.25 | 8 |
| 4 · corridor-run precedence and back-out | `a8a942d3…` | 4-0-0, +29.75 | 2-2-0, +3.00 | 0-4-0, −23.25 | 8 |
| 5 · keep an imminent arrival's tile and exit clear | `f69edd42…` | 4-0-0, +29.75 | 2-2-0, +3.00 | 0-4-0, −23.25 | 8 |
| 6 · pair spacing in the tie-break and the seat score | `a15136f6…` | 4-0-0, +29.75 | 2-2-0, +4.50 | 0-4-0, −23.25 | 8 |
| 7 · distinct launch rays among equal seats | `de998147…` | 4-0-0, +29.75 | 2-2-0, +3.00 | 0-4-0, −23.25 | 8 |
| 8 · two-tick memory of a step lost to own traffic | `9f0bb2fe…` | 4-0-0, +29.75 | 2-2-0, +3.00 | 0-4-0, −23.25 | 110 |

The `own-traffic blocked ticks` column is the count of moves refused because a
body of this team occupied or claimed the destination, summed over all twelve
matches. It is the closest thing this wave has to a direct measurement of the
owner's complaint, and it is worth reading beside the records: rules 3 and 8 are
one mechanism in two halves — 3 answers "does a sibling want this tile", 8 acts on
the answer *after* a collision proves it — and together they cost 1.50 progress in
one of three cells and remove 93% of the blocked ticks. Every other rule either
moves nothing or, in rule 2's case, is the whole revision.

## Measured records — revision 6 vs the rebuilt wave-5 predecessor

Deck game, spelled every time as `--classes <pair> --movement facing-locked
--pendulum keel --skills kit --bend universal --aim offset --stance-ground open`
plus `--five-slots wane` where a fabricator is in the pair. WASM runtime, 20 seeds
(42, 104729, 7, 1337, 20260730, 99, 3, 5, 11, 23, 8191, 65537, 2026, 314159, 17,
71, 977, 4099, 60013, 101). `prog` is mean signed territorial progress for the
candidate; `brW/brL` counts wins and losses decided by base breach; `distinct`
counts distinct (progress, outcome) pairs across the 20 seeds.

**Candidate — revision 6 as the striker, against the rebuilt wave-5 predecessor.**

| cell | side | n | W-L-D | brW/brL | prog | K/D | distinct | faults |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| striker mirror | a (team 0) | 20 | **20-0-0** | 0/0 | **+28.70** | 1.05 | 3 | 0 |
| striker mirror | b (team 1) | 20 | **19-1-0** | **19/0** | **+27.95** | 1.11 | 2 | 0 |
| | **both sides** | **40** | **39-1-0** | 19/0 | **+28.33** | 1.07 | — | 0 |
| bulwark-vs-striker | b (the striker side) | 20 | **13-7-0** | **13/1** | **+10.80** | 0.61 | 4 | 0 |
| fabricator-vs-striker | b (the striker side) | 20 | 2-18-0 | 0/9 | −23.45 | 1.06 | 4 | 0 |

**Predecessor controls — the same fixtures, the same seeds, the same flags.**

| cell | candidate | W-L-D | brW/brL | prog | K/D | distinct |
| --- | --- | --- | --- | --- | --- | --- |
| striker mirror | wave 5 vs wave 5 | 0-0-20 | 0/0 | +0.00 | 1.00 | 1 |
| bulwark-vs-striker | wave 5 as the striker | 1-19-0 | 0/10 | −27.70 | 0.48 | 6 |
| fabricator-vs-striker | wave 5 as the striker | 0-20-0 | 0/15 | −28.55 | 1.08 | 5 |

Read together, the delta per cell is:

| cell | wins gained | progress gained | breach outcomes |
| --- | --- | --- | --- |
| striker mirror | 39 of 40 matches taken from a bot that draws its own mirror | +28.33 | 19 breach wins against 0 |
| bulwark-vs-striker | **+12** | **+38.50** | breach losses 10 → 1, and 13 breach wins where there were 0 |
| fabricator-vs-striker | +2 | +5.10 | breach losses 15 → 9 |

**Revision 6 against itself**, same seeds: 0-0-20, +0.00, `distinct` 1 — a
symmetric map with a forced opening still draws its own mirror tick for tick, so
the coordination rules add no side bias and no entropy of their own.

Zero runtime faults in every match of the wave — the 200-match record sweeps, the
twenty-five variant screens behind them, and the whole qualification suite.

### And the silliness itself, over the same twenty seeds

Rates are per 1000 two-body-ticks, so they are not inflated by revision 6 keeping
more bodies alive for longer (which it does: 12 288 body-ticks against the
predecessor's 9 000-odd in the bulwark cell).

| | mirror w5 → w6 | bulwark cell w5 → w6 | fabricator cell w5 → w6 |
| --- | --- | --- | --- |
| moves blocked by own body | 0 → 0 | 0 → 0 | 0 → 0 |
| own-traffic blocked ticks (absolute) | 0 → 0 | 20 → 20 | 0 → 0 |
| route steps a sibling stood on | 152 → **72** | 81 → **105** | 88 → **67** |
| rotation flips with a sibling alive | 90 → **52** | 72 → **50** | 42 → **31** |
| net-zero travel with a sibling alive | 78 → **75** | 144 → **106** | 154 → **81** |
| corridor pairs (two own bodies in one run) | 0 → 0 | 0 → 0 | 0 → 0 |

Four of the six rows improve, one is flat, and one — jammed route steps in the
bulwark cell — gets 30% worse while that cell's record goes from 1-19 to 13-7. That
is not a contradiction and it is worth stating plainly: a body that *holds* its tile
behind a sibling registers as a jammed step every tick it holds, where revision 5
registered one jam and then wandered off. The metric counts the symptom; the rule
fixed the disease and made the symptom more visible. The counters that cannot be
gamed that way — rotation flips and net-zero travel, both of which are literally
wasted ticks — fall in every cell.

Two rows are zero on both sides, and both are honest zeros rather than wins:
revision 5 never blocked a sibling either, and neither bot ever put two bodies in
one of this map's two-tile corridors.

**Twenty seeds are several observations now, and it is not my doing.** Wave 5's
single most important caveat was that all 20 seeds produced byte-identical decision
counts, so a sweep bought one game and a false sense of *n*. On 0.9.22 the
*predecessor itself* now spreads: 6 distinct outcomes in the bulwark cell and 5 in
the fabricator cell, where wave 5 measured 1 and 1. My own build spreads similarly
(2 to 4). So the variance is the platform's, not the rules' — and the two
self-mirrors confirm it from the other side, both landing 0-0-20 with `distinct` 1,
which is what a symmetric map with a forced opening should do. The caveat is
therefore weakened rather than removed: four to six distinct outcomes over twenty
seeds is a real spread and still not twenty independent observations, and every
per-rule ablation below is a four-seed screen, which is why I report it as a ladder
rather than as significance.

**The fabricator cell is still the weak one and I will not dress it up.** It
improves — 2-18-0 at −23.45 against the predecessor's 0-20-0 — and it is the only
cell where revision 6 does not take the majority of matches. The fabricator
chassis fields four bodies to my three under `wane`, and the coordination rules
that help me at parity help it more at four; nothing in this pass addresses being
outnumbered. Wave 5 said the same thing about the same cell for a different
reason, and the honest note is that two waves have now failed to move it.

## Contract reads and repairs made this pass

- **A choke is geometry, not a coordinate.** `Doctrine` derives one-tile corridors
  from the wall grid — an open tile whose open cardinal neighbours lie on a single
  axis — and groups connected ones into runs by flood fill. Nothing names a map or
  a tile, so the same code answers on the thin-fronts holdout (whose wall grid is
  in fact identical, which is itself worth knowing before designing a corridor
  rule around it).
- **An arrival tile is published where it is reserved.** `LifecyclePending`
  carries `ReservedPosition`, so a queued fabrication or replication needs no
  derivation at all; only an automatic return or an unlocking slot does, and there
  the placement policy string plus `ArenaBasics.AdvanceDirection` gives the
  rear-most free tile without naming an arm.
- **Precedence reads health off the observation.** `ObservedAllyState.Health` is
  published for every allied body, so "the body that can survive the corridor
  should be the one in it" is a contract read rather than a guess.
- **A block is published, and its shelf life is one tick.**
  `PreviousActionResolution` names the outcome and the direction. It is the only
  evidence a life ever gets that its own team blocked it, it survives exactly one
  tick, and private memory is the only place to keep it — which is fine, because
  memory is life-scoped and a body that respawns should not inherit a grudge about
  a tile.
- **Rule 7 reuses the aperture rather than re-deriving it.** "Which ray does this
  seat hold onto that contact" is `Arms.Aperture` traced through `Ballistics`, so
  the diagonal and strict-corner cases are the contract's, and on an arm without
  initial-aim offsets the question collapses to the single cardinal ray revision 4
  asked about. The deleted spacing rule asked the mirror-image question — which
  rays one enemy facing lays — through the same two calls, which is why building it
  cost almost nothing and deleting it cost nothing at all.
- **The scaffold helper stays as wave 5 left it.** `ArenaBasics.cs` is still the
  trimmed copy (`6cb7e9b9…`, not the template's `567e9faf…`) — wave 5 deleted
  fourteen unreachable helpers to fit the old 256 KB cap. With the cap now 2 MB I
  could restore it, and deliberately did not: re-adding dead code to a frozen
  lineage would change a file for reasons that have nothing to do with this
  revision. The whole source tree is 280 KB against a 2 MB cap, so wave 5's
  friction 1 is closed and I have nothing to add to it.

## Frictions — top 3

1. **A frozen artifact plus a rules fingerprint is not enough to reproduce a
   result across a CLI bump, and nothing tells you when it has stopped being
   enough.** My wave-5 freeze recorded the artifact hash, the build-cache key, the
   CLI version, the SDK version, the rules version and the mirror's rules
   fingerprint — everything the packet asks for — and its headline still does not
   replicate on 0.9.22: the same bytes on the same flags now lose the cell they
   won, with zero faults and no warning. The freeze checklist is silently
   incomplete in two ways. First, it asks for the artifact's identity but not for
   the *cell's* identity on every cell measured, so wave-5 me recorded a
   fingerprint for the mirror and none for the two cross-class cells that carried
   the conclusion — I could not even diff what changed. Second, and this is the
   part only the tool can fix: `experiment frontline-labs` prints a rules
   fingerprint but never the CLI version beside it, and `qualify` records the
   artifact and the contract fingerprint but not the CLI that ran them. Two fixes,
   either of which would have saved me an hour of doubting my own rebuild: print
   the CLI version in the run header next to the fingerprints, and have the packet
   require a fingerprint per measured cell rather than one per freeze. A third,
   larger one: when the CLI is asked to run a cell whose registered ruleset it
   mints with a fingerprint different from one recorded in a replay it is being
   compared against, it should say so out loud.

2. **`qualification.json` has a `coordinationGradeAwarded` field, in the wave that
   is about coordination, and no permitted document mentions it.** Schema 6 emits
   `"coordinationGradeAwarded": null` beside `tierAwarded` and
   `balanceEvidenceEligible`. It appears nowhere in the packet, the rule card or
   the classes addendum. An author doing a coordination pass finds that field and
   has to guess three things: whether suite 5 grades coordination at all (it does
   not), whether a future suite will and against what, and whether `null` means
   not-applicable, not-implemented, or failed. Worse, the guess matters — I spent
   real time deciding whether to shape rules toward a grade I could not read the
   definition of. Related and larger: **nothing in the qualification path
   exercises the thing this wave was about.** All six probes are single-body or
   opposed-body capability checks; none of them puts two of my own bodies in one
   corridor, so a bot can take T4 with a coordination layer that has never been
   executed. If the lab wants coordination, the cheapest instrument is a probe with
   two own bodies and one 1-tile passage, and it would have caught my row-2 and
   row-5 regressions in one run each.

3. **`qualify` writes 192 MB of self-contained viewers into the freeze, and the
   flag that would stop it does not exist.** This wave's tooling note is that
   experiment runs no longer write `viewer.html` by default — a genuinely good
   change that cut my sweep footprint about fourfold. `qualify` did not get it: my
   `evidence/t4/` is 212 MB, of which 192 MB is 36 viewers, and there is no
   `--no-viewer` on the qualify command. Every author's freeze therefore carries
   ~200 MB of generated HTML that duplicates the replay JSON beside it, and the
   packet's own instruction — "every verified probe replay" — asks for the replays,
   not the viewers. Either give `qualify` the same default as `experiment` with an
   opt-in flag, or say in the packet which of the two the archive is expected to
   keep. Smaller and in the same family: the runtime message says "pass `--viewer`
   or `--open`", and `--viewer` is not listed in `experiment frontline-labs
   --help`, which lists only `--open`.

### Smaller notes

- **The binary is `botarena`; every document, help string and brief calls it
  `nilbots`.** `sandbox/cli-publish/nilbots` does not exist — the file is
  `sandbox/cli-publish/botarena`, and it introduces itself as `nilbots 0.9.22`.
  Two waves of author packets, the rule card's authoring check, and my own brief
  all give commands that fail as written. A symlink would fix it in one line.
- **The registered composite token is printed at me but cannot be passed.** The
  deck game is nine flags, one of them conditional on the class pair, and the
  ruleset ID that comes back is `…-deck-facing-locked` or
  `…-sail-open-facing-locked`. The CLI clearly owns the flags-to-token mapping,
  because it prints it. A `--level deck` alias that expands to the registered
  combination would delete an entire class of silent measurement error; as it
  stands every author retypes nine flags into every script and the addendum
  documents the mapping in prose. Wave-5 me reported the other half of this — that
  `--five-slots wane` is accepted and silently inert on a cell with no fabricator;
  that is unchanged.
- **`nilbots build` globs every `.cs` under the project, which makes an archived
  variant a silent freeze-breaker.** I did not hit this — a mid-wave coordinator
  note warned me — and I am recording it because the failure mode is invisible: a
  frozen tree with `evidence/ablation/Foo.switched.cs` inside it compiles fine
  today from the cache and fails to rebuild forever after with duplicate-member
  errors. The fix on the author's side is to keep variants in scratch and to prove
  the freeze rebuilds; the fix on the tool's side is either an explicit source
  list in `botarena.json` or a build-time warning when the glob picks up a file
  under a directory the manifest never mentions.
- **The build-cache key is path-sensitive and the artifact is not.** The same
  sources under two directories produced cache keys `6fce12a1…` and `ec1d3442…`
  and the identical artifact `c2cd8049…`. That is the right behaviour for the
  artifact and a guaranteed cache miss for every freeze rebuild, which is worth
  one line of documentation so the miss does not read as a reproducibility
  failure.
- **Documentation gap that cost me a build.** The addendum explains
  `forward-rally` placement precisely — "the rear-most free tile of that region
  measured along your own advance direction" — and I still implemented it as "the
  region", because the sentence describes where an arrival *lands* and never says
  the corollary an author needs: that the tile is chosen from the FREE ones, so
  standing on it does not block the arrival, it *displaces the arrival forward*.
  One clause ("so occupying the rear tiles pushes your own companion deeper into
  contact") turns a placement rule into a decision.
- **Confusing terminology, newly.** "Choke" is the brief's word, `suppression-choke`
  is a qualification probe about firing down a corridor rather than about a
  corridor's capacity, and `Openness` in my own wave-4 code counted open
  neighbours. Three senses of one idea, one of them mine. And `contested` in
  revision 5's code meant "a tile another body will probably take", while
  `contest-majority` in the pendulum means "surplus weight scales capture" — I
  renamed my own to `EnemyClaims`/`Yielded` this pass for exactly that reason.
- **Hardcoding temptations resisted:** the map's corridors (derived from the wall
  grid, and the derivation found four dead-ends I would not have listed); the
  arrival tile (`LifecyclePending.ReservedPosition`, else the advance direction);
  the fan's three headings (`Arms.Aperture`); a sibling's tie-break order (NOT
  derivable — its random stream is private, so rule 3 claims the union over tied
  routes rather than pretending to predict one); allied health, cooldown and
  pending transitions (all published on `ObservedAllyState`); slot unlock and
  rebuild schedules (lifecycle assignments).
- **Timings.** Cold `--no-cache` WASM build ≈ 9 s idle; one 500-tick WASM match
  ≈ 2.8 s idle and up to 15 s under a load average of 24, which is what this host
  ran for most of the wave; full `frontline-qualification-5` including the
  hash-linked T3 prerequisite ≈ 8.5 s; a three-cell four-seed screen ≈ 40 s idle.
  The 20-seed record sweep is 140 matches. Deciding what to measure was again the
  bottleneck, and this wave the instrument was most of the work: the first version
  of my coordination counter said the problem did not exist, because it only
  counted blocks.
- **Strategy passes:** one, spent as a ladder. Eight rules were built and measured;
  five ship with a measured effect, two ship measured-inert because a bar asks for
  them and they cost nothing, and three whole-build variants were deleted with
  their numbers above. The idea underneath all of it is the lineage's usual one —
  price a decision against the tick it displaces — applied for the first time to a
  tick displaced by a body on the same team.
