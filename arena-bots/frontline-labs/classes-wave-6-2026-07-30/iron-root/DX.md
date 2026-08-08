# DX report — iron-root, wave 6 (revision 6, CLEAR LANE)

## Isolation statement

Written from this revision's own forensics, its own qualification report, and
private sparring against this lineage's own rebuilt wave-5 predecessor and
against eight variants of this revision's own source. **No other entrant's
source, directory contents, replays, standings, or aggregate balance report was
opened.** This revision's private scratch was
`sandbox/iron-root-w6-scratch-7b2e9d43`, a uniquely named directory used for
nothing else.

Permitted material used, and nothing else: the author packet, the Frontline Labs
v1 rule card, the experimental classes addendum, the
`templates/botarena-generic-actor/` scaffold, `src/BotArena.Sdk/` types, this
lineage's own wave-5 directory, and `sandbox/cli-publish/` (nilbots 0.9.22, SDK
0.10.6). The three briefed documents were hash-verified before use and all three
are **byte-identical to the versions revision 5 recorded** — the mechanical
confirmation that the game is unchanged:

```text
d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e  FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md
06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8  FRONTLINE-LABS-RULES.md
2333bd3c9f412e4e9439779ef3d5f2ca6bc8abae6f00973daf54f7e4c892de50  EXPERIMENTAL-FRONTLINE-CLASSES.md
```

The frozen wave-5 tree was left untouched and still reproduces its recorded
identity, `e2d868e794c9e090450758e9a8fce44ec7ce4c9c308544ca33255b4a4c11d7e7`.

### One incidental exposure to disclose, as the packet requires

Diagnosing why my own inner loop had slowed to a crawl, I ran `ps` filtered to
running match processes. The box is shared and several other entrants' sessions
were sweeping concurrently, so that listing printed **their full command lines**:
scratch directory names, some variant directory names that read as ablation
labels, their class pairs, their runtimes and their seed sets. About six
concurrent sessions and seventeen simultaneous matches were visible.

**No file inside any other entrant's directory was opened, listed, read, copied
or executed, and nothing in this revision was changed as a result.** The
protection here is chronological and checkable rather than a promise: the entire
coordination layer — `Traffic.cs`, all six rules, and every decision point in
`IronRoot.cs` — was written, compiled and first measured **before** that command
was run. The artifact that existed at that moment is `400c835e870f…`. Exactly two
behavioural changes were made afterwards, and both are documented below as
corrections forced by my own ablation numbers, with the losing measurement quoted
(the yield redesign, −10.1 points a cell; the forced-move claim). Every
subsequent process check in this session was narrowed to my own scratch path.

I also record it as a friction: on a shared box the natural diagnostic for "why
is my inner loop slow" is the one command that leaks every neighbour's experiment
design, and nothing warns you.

Nothing was committed to git.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `iron-root` |
| Population / wave | Frontline Labs classes, **wave 6** (`classes-wave-6-2026-07-30`) |
| Authoring lineage | `iron-root-v1` |
| Class | `bulwark` (declared in `botarena.json`) |
| Doctrine | FORTRESS ROTATOR — revision 6 codename **CLEAR LANE** |
| Role | `verdict-doctrine` |
| Target tier | cumulative T4 (retain) |
| The game | `--classes <pair> --movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open [--five-slots wane]` |
| Resolved identity, own mirror | `frontline-labs-1-bulwark-vs-bulwark-sail-open-facing-locked` |
| Predecessor | wave-5 directory, left untouched |
| Source-tree hash | `8bdb3403c07a8ad0637a9784cd8d43442c57377e8d3a4b5d7ea3fdf18375283a` |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/Guest **0.10.6**, actor protocol 1.0, WASI p1 core module, platform-matched Docker builder (macOS arm64 host) |
| Build cache key | `0e74858869fc6e331abfb793e37486d6a0bbc89734d627c1e1a912c6108b3e9d` |
| **`out/bot.wasm` sha256** | **`6a62b5c35d27914c0729035af39958d8038b79f39998bc7c9b2c83e6b8a684d3`** |
| `evidence/t4/qualification.json` sha256 | `1ec3ea713ab6d02a58512b0c6e4d279f2dedfe04da55765f4a8f58f2ea1d688b` |
| Cumulative T3 prerequisite report sha256 | `7bc4203c812be8b371fc9c628953c8e27356f2c7fd456977716c3469ecf3ba41` (hash-linked inside the T4 report) |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| **Qualification outcome** | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, **exit 0**, **T4 awarded**, `balanceEvidenceEligible: true`, `profileComplete: true`, **first attempt** |

All five T4 components passed (`suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`) with the cumulative T3 and
T2 prerequisites rerun and hash-linked automatically. The suite runs the
classless, skill-less, aim-less duel-depth union profile, and this artifact passes
it unchanged on the first attempt — the coordination layer is gated on declared
facts (a corridor derived from the map, a sibling that exists, a declared arrival
region) rather than on an arm, so it is inert-or-correct on a contract that carries
none of them.

`evidence/t4/` keeps the report, the hash-linked prerequisite reports and all
**36 verified probe replays** (one verified above with `nilbots verify`). The
self-contained `viewer.html` beside each replay was **deleted**: at 5.3 MB each
they were 192 MB of the 213 MB the suite wrote, they are regenerable from the
replay, and a full shared volume was a documented hazard for this population last
wave. The replays and every hash are untouched.

### Freeze integrity

The frozen tree was rebuilt `--no-cache` **from the freeze location as the last
step**, and reproduces the shipped artifact exactly
(`6a62b5c35d27…`, cache key `0e74858869fc…`). No variant, ablation or scratch
`.cs` file exists anywhere inside the freeze tree — `nilbots build` globs every
`.cs` under the project directory, so an archived variant source would make the
frozen tree fail to rebuild with duplicate-member errors, silently, because nobody
rebuilds a freeze. Every variant in this revision lived in private scratch and was
deleted after its numbers were extracted.

### Per-file source hashes

Recipe (unchanged across the lineage):

```bash
ls *.cs botarena.json IronRoot.csproj | LC_ALL=C sort | xargs shasum -a 256 | shasum -a 256
```

```text
6dfb7d6d55ed0e92e15e5cf47a6dd59add7eeef451ac02eac66d8024f1fb5987  ArenaBasics.cs
dfb1470dd8ad0a288f094d7127ed102b306aab0c5dc9624c4c581ccc67ba40d3  ArenaGeometry.cs
930facf28f6597836739db206d20e4705e76c40225568fd5a1229675ff1a74c6  ContractLens.cs
1e402fb87f26ae3f144ceb068fd95ff75cb8020c081b36d49e70f447689ec213  FortressPlan.cs
c4bfa78d7c249ff0cd66544cef12d95abd92226fcb2a52a9764a46c35b9dac7b  Gunnery.cs
1d631e428f4884472c240d4afd2875a8e92b37494afc11744a465efbfc226e67  IronRoot.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  IronRoot.csproj
4e69aef2560b076e1c114d65385b553b7052a28fcc51c7371887fe882d39032a  Kinematics.cs
548942fdaa738fc1ed8438bdcc20c0d3e5145de507d4de2f002ee0604eed0328  Traffic.cs
b983bb8cd98ad1702d15f07c119c124bae512e0fca1147a2e80e0fa02ce2339c  botarena.json
```

`Traffic.cs` is new. `ArenaGeometry.cs`, `Kinematics.cs`, `Gunnery.cs`,
`FortressPlan.cs`, `ContractLens.cs`, `ArenaBasics.cs`, `IronRoot.csproj` and
`botarena.json` are byte-identical to wave 5 — **the entire revision is one new
file plus edits to `IronRoot.cs`**, which is what an IQ pass on coordination
should look like when the doctrine underneath was already right.

## Doctrine delta in one paragraph

**CLEAR LANE.** Nothing about the doctrine moved: territory is still the only
currency, placement is still asked of the ROUTE, the ±45° envelope is still
exploited offensively and respected defensively, the turret is still a rental
whose entry bar is unchanged and whose exit is cheap, and shell discipline is
still *against poke raise the arc, against numbers root the gun*. What moved is
that the doctrine learned its own two strongest forms are **walls, and a wall does
not know whose side it is on** — a rooted turret and a raised shell cannot step
aside, so on this map they cork the 1-tile mouths of the central objective, which
are also the best tiles this chassis can shoot the surface from. Revision 5 did
that in **every cell of every seed**, for 21 to 97 ticks a match, while its own
reinforcements walked a three-tile detour around it, and it never knew: nothing
was illegal, nothing was refused, and the coverage ranking that chose the tile was
right about everything except who else needed it. It could not know, because it
owned none of the primitives — no notion of a corridor, no notion of what a route
costs, and no notion of where a sibling was going. What it had instead was the
exact opposite: a **reactive** blacklist that learned a tile was unusable by
walking into it and losing the tick, so it discovered a sibling existed only after
the sibling had already cost it a move. So revision 6 is one new file of
primitives — corridor cells and runs derived from the resolved map, route cost and
wall cost as a subtraction of two breadth-first searches, and every allied body's
post and forced next step derived from the frozen observation by the same function
every life runs — and six rules that consult them. The measured result is that the
corridor plug is **gone** (corridor-occupancy ticks 382 → 32 over ten cells, a 92%
reduction; choke-wall ticks 46 → 1) and the layer **wins** rather than paying for
it — **+5.8** points of territory per cell in-process against a predecessor that
the same harness proves it is decision-for-decision identical to with all six
switches off, and **+4.8 at 10–6–0** over sixteen WASM cells on a disjoint seed
set. The yield rule paid for its own lesson twice, and both corrections are worth
more than the rule was: its first version was airtight about collisions and lost
**ten points a cell** by buying four hundred waits, because *a certain tick spent
avoiding a coin flip is worse than the coin flip*, and its second correction —
claim a sibling's next tile only when it is that sibling's **only** move — is what
made it cheap enough to keep. And the honest headline
is uncomfortable and worth stating first: **the rule I was assigned is not the rule
that fixed the bug I was assigned.** The choke refusal changes one cell in ten and
costs 2.4 points; what actually empties the corridors is the right-of-way claim,
which was written for a different bar entirely.

## The coordination rules, with per-rule measured attribution

Every rule is a switch in `Traffic.cs`. Each row is the same artifact with exactly
one switch flipped, rebuilt through the controlled toolchain and swept over the
**same ten cells** (5 seeds × both sides, in-process, against the rebuilt wave-5
predecessor on the deck game). Raw cells in
`evidence/forensics/coordination-cells.txt`.

**The baseline row is a proof, not an estimate.** With all six switches off the
artifact is **decision-for-decision identical to the rebuilt wave-5
predecessor** — 706 of 706 team-0 decisions on a checked cell, including every
debug string — so its margin is exactly `+0.0` and every number below is a
difference rather than a comparison. The rebuilt predecessor is in turn
decision-identical to the **frozen** wave-5 artifact (718 of 718 on another
checked cell), so the chain reaches the archived doctrine.

| configuration | W–L–D | margin / cell | corridor-occupancy ticks | choke-wall ticks | sibling lost ticks | one-fan ticks |
| --- | --- | --- | --- | --- | --- | --- |
| **all six OFF** (≡ wave 5) | 4–4–2 | **+0.0** | **382** | **46** | 12 | 103 |
| **all six ON** (shipped) | **7–3–0** | **+5.8** | **32** | **1** | 12 | 203 |
| − C1a choke refusal | 7–3–0 | +8.2 | 38 | 2 | 12 | 205 |
| − C1b choke gate | 7–3–0 | +5.8 | 32 | 1 | 12 | 203 |
| − C2 right of way | 7–2–1 | +8.4 | 215 | 0 | 28 | 110 |
| − C3 choke precedence | 7–3–0 | +5.8 | 32 | 1 | 12 | 203 |
| − C4 rally clearance | 7–3–0 | +5.8 | 32 | 1 | 12 | 203 |
| − C5 spacing | 6–4–0 | +3.3 | 63 | 0 | 10 | 211 |

Per-rule verdicts, and three of them are negative results:

- **C2 — right of way. The rule that actually did the job, and it was not written
  for it.** Removing it triples corridor occupancy (32 → **215**) and doubles
  sibling collisions (12 → **28**). It costs 2.6 points of territory a cell and
  **does not cost a win** — 7–3–0 becomes 7–2–1, so removing it upgrades one loss
  to a draw and changes nothing else. **KEPT**: it is the single largest measured
  reduction in owner-visible silliness in this revision, at no cost in wins.
- **C5 — spacing. The only rule that straightforwardly wins.** Worth **+2.5** a
  cell and it *halves* corridor occupancy as a side effect (32 vs 63), because the
  posts it declines are the ones an enemy muzzle already sweeps, and on this map
  those are the row-7 corridor mouths. **KEPT.**
- **C1a — choke refusal. My assigned rule, and it is measured redundant.** It
  changes **one cell in ten** (seed 2, side 0: +6 with it, +30 without; the other
  nine cells are identical to the point). It costs 2.4 a cell, leaves the record
  untouched at 7–3–0, and adds no corridor reduction that C2 has not already made
  (32 vs 38). **KEPT, and the case against it stated plainly**: an author
  optimising the number would cut it. Two reasons it stays. First, it is the only
  rule that *guarantees* the invariant instead of producing it as a side effect —
  C2 empties the corridors by changing where mobile bodies walk, which is an
  accident of this map's geometry, whereas C1a asks the question directly and will
  still be right on a map I have not seen. Second, and this is the part C2 cannot
  do at all: C1a is also the **exit** rule. C2 keeps a body from walking into a
  lane; only C1a makes a body that is *already frozen* in one leave. The
  predecessor's 97-tick plugs are frozen bodies that never left, and the stance's
  own declared budget cannot evict them because that budget is spent by the
  **enemy's** decision to fire — a shell nobody shoots at never returns.
- **C1b — choke gate. Provably inert here.** Every counter and every cell score is
  byte-identical to the shipped base. It fires only when the doctrine wants to root
  on a corridor tile whose coverage is below the top tier *and* the wall costs my
  own traffic nothing, and in ten cells that never happened: reaching the
  opposition's mouth means standing past the objective, which this doctrine only
  does after an advance. Implemented, priced, contract-correct, **unexercised** —
  and labelled that way rather than implied to work.
- **C3 — choke precedence. Provably inert here**, byte-identical counters. It
  fires only when a sibling's *unique* next step enters a corridor run another
  sibling has claimed. Kept because the brief requires chokes to have an explicit
  precedence rule and because it is the only correct rule where the geometry
  forbids routing around — but it earned nothing on this pairing and I will not
  claim it did.
- **C4 — rally clearance. Provably inert here, and I can say exactly why**, which
  is better than reporting a zero. The rule protects the tile my next automatic
  arrival will take. Under a forward rally that tile is in the objective
  **one position behind** the active one (`activePositionIndex −
  advance.ObjectiveIndexDelta`), and every post this doctrine ranks is on the
  active objective or its one-tile ring. On this map those two sets are disjoint —
  position 1 sits at x 6–7 while position 2's ring spans x 9–13 — so the tile it
  reserves is never a tile it wanted. It would bind on a chain tight enough for one
  position's ring to reach the next.

### Two rules shipped wrong and corrected by measurement

Both corrections are worth more than the rules were.

1. **The yield rule waited, and waiting lost ten points a cell.** The first
   version claimed every tile a senior sibling's shortest first step could use,
   and when no route avoided them, it waited. That is airtight about collisions —
   it removed 28 sibling lost ticks and 190 corridor ticks — and it measured
   **7–2–1 at +8.4 without it against 4–5–1 at −2.0 with it**: a **10.1-point**
   loss per cell, bought with roughly 440 extra waits over ten cells, 44 a match.
   The arithmetic I had missed is that *the two costs are not the same size*. A
   collision costs two bodies one tick; a wait costs one body one tick — so
   yielding only pays when it actually prevents a collision, and out in the open it
   usually does not. A senior with **two** equal first steps can avoid me by
   itself, so claiming both makes me pay a certain tick to dodge a coin flip. The
   rule is now asymmetric and the asymmetry is the map's own: **route around a
   claim wherever routing around exists; wait only where the geometry forbids it,
   which is exactly inside a 1-tile corridor.** Combined with the second
   correction, the layer went from −2.0 to +5.8 on the same cells.
2. **Claim a forced move, not every move.** Three bodies converging on a six-tile
   objective claim six of the tiles worth standing on, so the route search that was
   meant to find a way *round* kept finding a longer way *in*, on every tick of
   every approach. Claiming only a sibling's **unique** next step is what made the
   rule cheap — and it still catches the case the replays actually caught: two of
   my bodies at (4,8) and (4,10) both had (4,9) as their one shortest first step
   and both lost the tick to each other, at tick 466 and again at 470.

### What the coordination layer does NOT fix, measured

- **`one-fan ticks` got worse: 103 → 203.** Two of my bodies stand inside one
  enemy muzzle's launch envelope twice as often as the predecessor's did, and the
  cause is the fix. A 1-tile corridor cell has walls on two sides, so six of the
  eight headings cannot reach it: **the tiles this revision refuses to freeze in
  are the best cover on the board**, and bodies that leave them stand in the open
  where more muzzles bear. C5 pushes back on it (211 without, 203 with) and does
  not undo it. This is a real cost of the assignment, it is paid for by the +5.8,
  and it is the first thing I would attack next.
- **Sibling lost ticks are 12 either way.** Against the predecessor's own 12, the
  shipped layer removes exactly none — C2 removes 16, and C1a's corridor eviction
  adds 16 back by putting bodies into the open together. The layer's win on this
  metric is zero, and the earlier claim I could have made from the C2 ablation
  alone (28 → 12) would have been an artefact of comparing against my own
  regression rather than against the predecessor.

## Measured record versus the rebuilt wave-5 predecessor

Opponent: this lineage's own wave-5 source rebuilt from the frozen tree with
`--no-cache`. **WASM runtime (the frozen-cohort standard), the deck game on a
bulwark mirror, both sides, eight seeds — 16 cells.**

| pairing | cells | W–L–D | margin / cell | how it ended |
| --- | --- | --- | --- | --- |
| **bulwark mirror vs rebuilt wave 5, both sides** | **16** | **10–6–0** | **+4.8** | max-ticks in all sixteen |
| — as team 0 only | 8 | 8–0–0 | +23.5 | max-ticks |
| — as team 1 only | 8 | 2–6–0 | -13.9 | max-ticks |

Cited cell: `evidence/forensics/cited-lane-cell/` (seed 6, team 0, `+37`, replay
hash `710407e873dc…`, `nilbots verify` OK) — corridor occupancy 8 ticks, longest
plug 6, zero sibling lost ticks, zero choke-wall ticks.

**Read the sides, not the total.** The record is 8–0–0 from team 0 and
2–6–0 from team 1: the coordination layer is worth a great deal on one
side of a mirror-symmetric map and costs on the other. That asymmetry is not new —
the predecessor has one too, in the opposite direction (its own mirror is won by
team 1, and the all-switches-off control loses as team 0 and wins as team 1 on the
same seeds) — but this revision has made it larger, and I did not chase it. The
most likely cause is inherited rather than mine: the station ranking's final
tie-break is ascending X, which is not mirror-symmetric, so "the lowest-X of the
equal posts" is a tile behind the objective for team 0 and past it for team 1. My
corridor rules changed which posts are equal, and therefore how often that
tie-break decides anything. Fixing it means replacing an absolute coordinate
tie-break with one derived from the team's own advance direction, and that is a
change to the ranking rather than to the coordination layer, so it belongs to the
next revision rather than to this one.

**Cross-class probe (`bulwark-vs-fabricator` + `wane`, WASM, 3 seeds × both
sides): no attribution available, and the reason is worth recording.** In that
pairing the bulwark side wins all six cells *whoever drives it*, so aggregated over
sides the cell is 3–3 by construction and measures the class matchup rather than
the doctrine. The one asymmetry inside it is suggestive at best: the candidate on
the bulwark side base-breached in **3 of 3** (ticks 326, 328, 326) while the
predecessor on the bulwark side breached in **1 of 3** (+21 and +4 at max-ticks
otherwise) — but the two sets face different fabricator drivers, so that is not a
controlled comparison. Revision 5 reported this same probe as a clean `3–0–0` win;
it is not one.

**Seeds are informative on this pairing, and revision 5's report said they were
not.** Its DX states "Neither artifact consults `context.Random`, so within a side
every seed produces identical decisions", and builds its whole methodology on it
("there are two informative cells here, one per side"). That is **false**, and the
correction matters because it changes how many cells anyone has: five seeds
produce five distinct replay hashes and per-cell scores from −3 to +15 on the same
fixed pairing. The culprit is friction #2 below — the scaffold's own
`OrderedDirections` helper draws a bool from the per-life random stream to break
lateral ties, and neither revision looked there.

## Skill, cycle and coordination counts

Per match, candidate side, summed over the ten in-process attribution cells.

| | all six OFF (≡ wave 5) | all six ON (shipped) |
| --- | --- | --- |
| corridor-occupancy ticks (immobile own body on a 1-tile cell) | **382** | **32** |
| longest single corridor plug, any cell | **97 ticks** | **5 ticks** |
| cells with a plug ≥ 20 ticks | **10 of 10** | **0 of 10** |
| choke-wall ticks (plug that lengthened a sibling's route ≥ 2) | 46 | 1 |
| sibling lost ticks (same-destination, swap, follow, ally standing) | 12 | 12 |
| ticks two bodies shared one enemy launch envelope | 103 | 203 |
| ticks two bodies shared one deflection-return lane | **0** | **0** |
| turret ticks | 344 | 652 |
| shell ticks | 1086 | 859 |
| waits | 1558 | 2231 |
| deaths | 166 | 140 |

Three of those need saying out loud rather than hiding in a table.

- **The plug was universal and it is gone.** Ten cells out of ten had an immobile
  body corking a corridor mouth for at least twenty ticks; none do. The two tiles
  are `(8,7)`/`(9,7)` and `(13,7)`/`(14,7)` — the west and east mouths of the
  central objective — and blocking either costs a body approaching from that side
  three extra steps, which is the number `WallCost` computes and refuses on.
- **Deflection-return stacking measures zero on both sides.** C5's second half —
  never feed an arc with a sibling behind me on the return lane — is implemented,
  priced from the declared reversed-heading policy, and **the predecessor never did
  it either**. Honest zero: the rule prevents a bug this doctrine did not have. It
  is kept because the mechanism is real and one body's position is all that
  separates them, but it earned nothing.
- **Turret ticks nearly doubled and shell ticks fell.** Not a coordination rule:
  it is C1a and C5 declining shells on corridor mouths, after which the same
  envelopment count that used to raise an arc roots the gun instead. The doctrine's
  own "against numbers root the gun" clause absorbed the refusal, which is what it
  was written to do.

## Timings (Apple Silicon, warm Docker builder) — and why they are unreliable

| Step | Time |
| --- | --- |
| `dotnet build` of the editing project | ~1 s |
| `nilbots build --no-cache` (cold, Docker) | 16 s |
| `qualify --suite frontline-qualification-5` (WASM) | ~11 s |
| one 500-tick deck match (in-process) | ~3 s |
| 10-cell attribution sweep | ~35 s uncontended, up to 6 min observed |
| one single-rule ablation: variant build + 10-cell sweep | ~50 s uncontended |

**Every number above except the two builds is contaminated and I would rather say
so than publish it.** The box was running about six other authors' sweeps
concurrently — seventeen simultaneous matches at one point — so wall-clock varied
by a factor of six for the identical command. The fix that mattered was
structural: build the variants serially (one Docker builder) and then run all
seven sweeps **in parallel**, which turned a 40-minute serial batch into one
roughly the length of its slowest member. Anyone reporting inner-loop timings on a
shared box should say what else was on it.

## Repairs and strategy passes

One coordination pass; everything else is a decision point wired to it. Each is
driven by a measurement or a contract read.

1. **New — `Traffic.cs`, the primitives.** Corridor cells and corridor runs
   derived from `map.TileRows`; route length and wall cost as a subtraction of two
   breadth-first searches; order-free first-step derivation; the rear-most-free
   arrival tile from the declared return placement and the team's own advance
   delta. 250 lines, no strategy, no coordinate, no arm name.
2. **Strategy — CLEAR LANE.** Six rules at six decision points: the anchor and
   shell entries (C1a refusal, C1b gate), the shell and turret exits (C1a again —
   the half that evicts a body which has *become* a wall), the step and the dodge
   (C2, C3), the station ranking (C4 exclusion, C5 tie-break), and the target
   builder (C5's return-lane clause).
3. **Repair — the yield rule waited, and waiting lost 10.1 points a cell.**
   Redesigned to route around a claim wherever routing around exists, and to wait
   only inside a 1-tile corridor where the geometry forbids it. Measured.
4. **Repair — claim a forced move, not every move.** Claiming the union of every
   shortest first step blocked six of the tiles worth standing on. Measured.
5. **Repair — split the assigned rule in two.** Refusal and gate have opposite
   signs on the board, and bundled they were unpriceable: split, the refusal prices
   at −2.4 and the gate at exactly zero.
6. **Deletion — `ScreenRank`.** It answered "what is MY rank", which is enough to
   hand out distinct posts and not nearly enough to yield to anybody. `RankOf` is
   the same arithmetic asked about an arbitrary body, which is what makes a
   precedence order derivable rather than declared. Same numbers for self.

## Top 3 frictions

### 1. `context.Random` is per-life, the one shared helper the scaffold ships consumes it, and that quietly makes multi-body coordination underivable

This is the deepest friction in the wave, because it is not about a doc — it is
about what "coordination" can even mean here, and the platform's answer is
correct but undiscoverable.

The rules are unambiguous and good: every life gets a fresh instance with empty
private memory, a life never sees an ally's current action, observations are
frozen before any decision executes, and there is no shared state. So the **only**
way two of my bodies can agree about who yields is for both to *derive* the same
answer from the same frozen bytes. Fine — that is a real design and I built to it.

Then the starter scaffold hands you `ArenaBasics.OrderedDirections(contract,
context)` as the recommended mirror-fair tie-break, its doc comment carefully
explains why an absolute direction preference is a measured side bias on a
symmetric map, and its body is:

```csharp
if (laterals.Length == 2 && context.Random.NextBool())
    (laterals[0], laterals[1]) = (laterals[1], laterals[0]);
```

`context.Random` is a **per-life** deterministic stream. So the helper that exists
to make two bodies fair makes two bodies **disagree**: allied lives draw from
different streams, get different lateral orders, and take different routes between
two equal-length options. Any coordination that derives a sibling's route through
that helper is a guess wearing a derivation's clothes — which is why every claim in
`Traffic.cs` is order-**free** (the union of every shortest first step, or nothing)
and why the shipped rule narrows to a *forced* move: a forced move is the only step
a sibling's private randomness cannot change.

It cost my predecessor more than a rule. Revision 5's DX asserts that neither
artifact consults `context.Random` and therefore that a fixed map plus a fixed
opponent yields one game per side however many seeds you spend — and it reports its
headline as "**6–0–0**" while telling the reader to weigh sides rather than seeds.
The assertion is false, and the helper is why: five seeds give five distinct replay
hashes and scores spanning eighteen points on this pairing. Revision 5 under-counted
its own evidence and mis-stated its own determinism, and it did so because
randomness entered through a helper nobody would think to audit.

Two fixes, either of which closes it. **One sentence** in that doc comment — *this
draws the per-life stream; two allied lives will disagree, so do not derive a
sibling's choice through it.* Or, much better, a **team-scoped** deterministic
stream (`context.TeamRandom`, derived from seed + teamId + tick) so that a shared
tie-break is genuinely shared. The second one would make a whole class of
coordination rule expressible that currently is not: with a team stream, two bodies
can agree on *which* of two equal routes each takes, instead of both having to
avoid both.

### 2. The wave is about coordination; the platform has no coordination instrument, and the report has a null field where one should be

`qualification.json` gained a field this wave: **`coordinationGradeAwarded`**. On
suite 5 it is `null`. Suites 1–5 are all that `--suite` accepts, none of them
awards it, and neither the author packet nor the classes addendum mentions it. An
author told to do "an IQ pass on multi-body coordination" who then reads a null
coordination grade in their own passing report has to guess between three very
different meanings: a component they failed, a suite they were not assigned, or a
field that is not implemented yet. I concluded the third from the suite list, but I
am guessing.

The larger version is the real friction: **there is no coordination metric, so
every author this wave had to build one.** Mine is ~300 lines over replay v3, and
it works — which is a genuine compliment to that format. Replay v3 snapshots, per
tick per actor, the submitted decision *including its debug string*, the accepted
and validated forms, and the resolution `outcome`, plus full `activeLives`
positions and forms. That is enough to reconstruct every coordination failure the
brief names, and it is why this wave was measurable at all.

What it is missing is one field. There **is** a `movement-blocked` event, and it
helpfully carries `from`, `attemptedTo` and `facing` — but it does not say **what
blocked the move**. So "an ally blocked me" has to be recovered by re-deriving the
declared collision rules (same-destination all block, swaps block, following a
vacated actor blocks) against `activeLives` and against every *other* actor's
submitted intent in the same tick. That is exactly the reconstruction the engine
already did authoritatively, thrown away and redone approximately. A `blockedBy`
naming the actor, or a `blockingKind` naming which of the three collision rules
fired, would turn a 300-line analyser into a grep — and would let the platform
report the metric the whole wave was graded on.

Concretely, from my own replays: the single clearest instance of the bug the owner
described is two of my own bodies at (4,10) and (4,8) both submitting a move to
(4,9) on tick 466 and again on tick 470. Two `movement-blocked` events fire. Nothing
in either event says the other body's name, and nothing says they blocked each
other rather than a wall.

### 3. The 2 MB cap is a real gift; the freeze tree's own build reproducibility is the new binding constraint

The 256 KB squeeze being gone changed this revision's character. Wave 5 deleted a
file it had proved unreachable and pruned 24 KB of scaffold *for budget*, and said
so. This revision added a 250-line documented primitives file and heavily
documented every decision point, finished at **324 KB**, and deleted nothing. That
is the cap doing exactly what it should. Thank you.

What replaced it are three reproducibility hazards, none documented author-facing,
and all three cost me real time:

- **The editing project only builds at one directory depth.** The generated
  project's SDK reference is `../../../../src/BotArena.Sdk/BotArena.Sdk.csproj` — a
  relative path that resolves at the freeze location and nowhere else. The packet
  *requires* a uniquely named private scratch directory, which is at a different
  depth, so `dotnet build` — the one-second syntax check — fails with thirty
  `CS0246: type or namespace 'BotArena' could not be found` errors that read
  exactly like a broken toolchain. The fix is to nest the scratch project one level
  deeper so the `../../../../` happens to land; that is a coincidence, not a
  solution. `nilbots build` is unaffected because it generates its own workspace
  against prebuilt DLLs — which is the hint that the ProjectReference could be
  emitted as an absolute path, or as a `$(BotArenaSdkPath)` property with a default.
- **A frozen artifact is not reproducible across a CLI republish, and nothing warns
  you.** My predecessor's frozen `bot.wasm` is `9f5a7ae3…`. Rebuilt under 0.9.22
  from the byte-identical frozen source tree — the tree hash `e2d868e7…` reproduces
  exactly — it is `2e7f7015…`, because the build cache key covers the SHA of the
  staged Sdk/Guest DLLs and the republish restamped them. The repo's own footgun
  list says this; no author-facing document does. For a wave-over-wave lineage the
  consequence is sharp: **the previous wave's headline artifact hash can never be
  re-verified, so "records versus the rebuilt predecessor" is the only honest
  comparison available.** The saving grace, which I checked rather than assumed, is
  that the drift is bit-level and not behavioural: frozen and rebuilt wave-5 are
  decision-for-decision identical, 718 of 718.
- **A replay hash changes when only the artifact's identity changes.** My
  all-rules-off control produced *different replay hashes* from the predecessor
  while being decision-for-decision identical, because provenance is inside the
  hash. For thirty seconds that read as "my control build is broken", which would
  have invalidated every attribution in this report. Establishing otherwise took a
  decision-level diff. One sentence — *the replay hash covers artifact provenance,
  so two artifacts that play identically hash differently* — would save that.

And the coordinator's mid-wave warning deserves repeating because it is the same
family: `nilbots build` globs **every** `.cs` under the project directory, so
archiving a variant source anywhere inside the freeze tree makes the frozen tree
fail to rebuild with duplicate-member errors — silently, because nobody rebuilds a
freeze. Every variant in this revision lived in private scratch and was deleted,
and the frozen tree was rebuilt `--no-cache` as the last step to prove it
reproduces the shipped hash.

## Documentation gaps

Beyond the three frictions:

- **Nothing anywhere says which tiles of the map are 1-tile corridors, and the
  addendum uses the word "choke" for something else.** `--duel-map` describes
  `outer-shoulder-bypass` as adding a flank "without opening the last-moment
  central choke", and the qualification suite has a component literally named
  `suppression-choke`. Neither is about a 1-tile corridor. The map that matters
  publishes its geometry as tile rows, so deriving the corridors is four lines and
  the right thing to do — but an author reading "choke" in three documents will
  reasonably think the concept is already named for them. Worth one clause: *a
  choke in the suite name is a pressure situation, not a corridor; derive corridors
  from the tile rows.*
- **The collision rules are stated in one dense sentence and every clause of it is
  a coordination bug.** "Same-destination moves all block, swaps block, following a
  vacated actor blocks, and projectiles block movement." That is four distinct
  ways two allied bodies can waste a tick, and the crucial word is **all** —
  same-destination has no winner, so a doctrine cannot rely on precedence being
  arbitrated for it. The rule card lists this under Actions among the physics; it
  deserves to be under a heading that says *these are the ways your own bodies cost
  each other tempo*, because that is what it is for a multi-body policy.
- **`automaticReturnPlacement` says arrivals land on "the rear-most free tile" and
  never says what "free" is measured against.** It is the one lever a doctrine has
  over where its own reinforcement appears — occupancy is the input, and the body
  doing the occupying is usually yours. Whether the tile is judged free before or
  after movement decides whether stepping off on the arrival tick is enough. I
  implemented the conservative reading (vacate early, from the slot's own declared
  due tick) and the rule measured inert for an unrelated reason, so I never
  established which it is.
- **`RelativePositionOffset(Forward, Right)` is facing-relative and the
  `CandidateReference` policy that says so is a frozen string.** Not load-bearing
  for a bulwark — this arm's `fabricationTransitions` is empty for it, which is
  itself worth stating in the class table: **the brief's "do not fabricate into
  your own traffic" bar is structurally inert for two of the three classes**, and
  an author has to discover that by finding an empty array rather than by reading
  "Companions: automatic".

## Hardcoding temptations

All resisted; the ones this revision created:

- **"The corridors are (8,7), (9,7), (13,7), (14,7)."** They are, on this map, and
  I know it because I derived them. `Traffic` computes them from `map.TileRows` at
  `StartLife` — both-neighbours-walled on one axis — and the source contains no
  coordinate. A map with none of them makes C1 and C3 provably inert rather than
  wrong.
- **"A choke is an articulation point."** It is not, and checking cost nothing:
  this map's only true articulation points are the two dead-end pockets at
  `(11,1)`/`(11,2)` and `(11,12)`/`(11,13)`, which no doctrine ever wants. The tiles
  that matter are corridors that are *expensive* to go round, not impossible — so
  the rule prices the detour instead of testing connectivity, and a severed route
  is priced separately and enormously rather than being the trigger.
- **"Blocking a lane costs three steps."** It costs whatever two breadth-first
  searches differ by. The refusal threshold is 2, which is a chosen number and the
  only one in the layer; the detour it actually meets on this map is 3.
- **"The senior takes the tile I think it will take."** It takes one of possibly
  several equal steps, chosen with per-life randomness. So the claim is the union,
  and the shipped rule narrows to the case where the union has exactly one member.
- **"Precedence is unit ID"** — or spawn order, or distance. It is the screen rank
  every life already derived to hand out distinct posts, generalised from "mine" to
  "anyone's". Anything else would need a second ordering that all lives agree on,
  and there is only one: canonical actor identity, which is the documented tie-break.
- **"The arrival tile is the spawn anchor"**, or "is the objective". It is the
  rear-most free tile of the region the declared return placement names, ordered by
  the team's own advance delta read from the mode binding — so a contract that
  rallies home resolves the same reader to the spawn.
- **"One muzzle covers one heading."** It covers `LaunchWidth` headings, read from
  the enemy's own `shotProgram` bounds and its `volley` count, so the spacing rule
  widens by itself on an arm with a fan in it and narrows to a single lane on a
  straight-only arm.

## Confusing terminology

Carried forward and still true: "Anchor"/"Mobilize" are prose words with no
contract representation; `irreversibleForLife` reads backwards; "Available" versus
"will succeed"; "facing-locked" restricts movement, not rotation; "hold" is three
different things; "deflect" sounds defensive and is an attack; "free" and "open"
are two placement arms and one English word; "unlimited" is about the count and
says nothing about the price; "placement" is used for three unrelated things.

New this revision:

- **"Choke" is a corridor, a pressure situation, and a map feature, in three
  documents.** See the documentation gaps.
- **"Blocked" is used for a legality refusal, a physical collision, and a
  doctrine's own blacklist.** The `movement-blocked` event is the second; the
  legality mask is the first; `OccupiedTiles` is the third. They need different
  responses — the first means never, the second means not this tick, the third
  means I decided — and revision 5 conflated the second and third, which is
  precisely how a reactive blacklist ends up treating a walking sibling as a wall.
- **"Objective weight zero" reads as "contributes nothing" and it means "scores
  nothing".** A turret still blocks movement, still absorbs bolts, and still denies
  a tile. That gap is the whole content of the gate rule: a body that scores nothing
  can still be the most valuable thing on a corridor, and the word "weight" invites
  you to think it is weightless.

## What I could not evaluate

- **Whether the gate is ever right.** C1b is contract-correct, priced, and fired
  zero times in ten cells, because reaching the opposition's mouth means standing
  past the objective. The one measurement I have is that removing it changes
  nothing at all.
- **Choke precedence as a claim.** Same shape: provably inert on this pairing, kept
  because the brief requires an explicit rule and because it is the only correct
  rule where routing around is impossible. Zero informative cells.
- **The deflection-return lane.** Zero on both sides — the predecessor never
  stacked two bodies on a return lane either, so the rule prevents a bug this
  lineage did not have.
- **Whether the layer helps against numbers.** Every cell in this report is a
  bulwark mirror against my own lineage. Coordination should matter *more* with
  more bodies, which is the fabricator cell, and I did not get a clean ablation
  there — the cross-class probe is a single configuration, reported as such below,
  not an attribution.
- **Whether 2 is the right refusal threshold.** It is the only tuned number in the
  layer and I measured it once, at 2. The one cell where C1a binds is a cell where
  corking was worth 24 points, which is exactly the evidence that the threshold —
  or a comparison against what the wall costs *them* — is where the next
  measurement should go.
- **Anything about the `one-fan` regression.** I measured it (103 → 203), named its
  cause (corridor cells are the best cover on the board), and did not fix it.
