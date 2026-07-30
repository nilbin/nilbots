# DX — VectorEdge revision 5 (wave 5, class striker, doctrine pressure-duelist)

**Lineage** vector-edge-v1 · **Revision** 5 · **Role** verdict-doctrine ·
**Target** T4 (`frontline-qualification-5`) · **Budget** one strategic revision,
mechanical and contract repairs free.

## Isolation statement

I read only the permitted material: the author packet
(`docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aa…`), the
rule card (`docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e…`), the classes
addendum (`docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `2333bd3c…`, read in
full including the aim, five-slot-variant and stance-ground sections), the
`templates/botarena-generic-actor/` scaffold, `src/BotArena.Sdk/` types and their
XML documentation, my own frozen wave-4 directory and its replays, and the
sandbox CLI at `sandbox/cli-publish/` (`nilbots 0.9.21`).

I did not open any other entrant's source, replays, standings, or aggregate
balance reports, nor Engine/App implementation, nor any cohort directory other
than my own lineage's. `arena-bots/frontline-labs/classes-wave-5-2026-07-30/`
contains three sibling entrant directories; I listed the directory (to confirm
my own output path exists) and opened nothing inside any of them. My wave-4
predecessor was **copied out** into private scratch and rebuilt there, so that
building it could not touch the frozen directory.

Private scratch: `sandbox/vector-edge-w5-scratch-2d9f0a7c/` — a uniquely named
directory, not a shared or guessable path. Nothing was written outside that
directory and my own output directory. **No accidental exposure to another
entrant's material occurred.**

Everything I sparred against is my own source: the rebuilt wave-4 predecessor,
eleven intermediate and ablation builds of my own revision-5 source (each
differing from the shipped one by a single named construct, listed with its
artifact hash in the ladder below), and the same predecessor artifact resolved
onto a **bulwark** and a **fabricator** chassis by an explicit `--classes` pair.
Those chassis fixtures are my own striker doctrine wearing another class's stats —
a fixed opponent for an A/B, not a bulwark or fabricator doctrine, and not a
population revision. Their records are reported as what they are.

One more fixture is worth naming because it produced nothing and that was the
finding: a copy of the predecessor with a single evidence-weight constant nudged
in its fifth decimal, built to test whether this mirror punishes *any*
perturbation. It drew all 40 matches with byte-identical decision counts, which
told me the perturbation never reached a decision — and put me onto the
measurement hazard that is friction 2 below.

I also received one mid-wave coordinator correction (the addendum's class-identity
paragraph still said the bulwark mobilizes "once per life"; on this arm the
turret is an unlimited cycle). It changed nothing I had written, because
reversibility is read from `irreversibleForLife` rather than remembered — but it
did add the opponent-model consequence noted under *Contract reads* below.

## Freeze identity

| Field | Value |
| --- | --- |
| Output directory | `arena-bots/frontline-labs/classes-wave-5-2026-07-30/vector-edge/` |
| Class (declared in `botarena.json`) | `striker` |
| `bot.wasm` sha256 | `9912013a033fead7cda342362e3137e18fab71d22727270a05c5675e35115415` |
| Build | `sandbox/cli-publish/nilbots build <project> --no-cache` |
| Build-cache key | `2f0911e9ead78b3804e276816bded8951534ec8c71c0d67561360afe58b56817` |
| CLI / SDK / rules | nilbots 0.9.21 · SDK+Guest 0.10.6 · game rules 0.5 |
| Compiler | NativeAOT-LLVM 10.0.0-rc.1.26306.1 (platform-matched Docker builder) |
| Qualification suite | `frontline-qualification-5` (`frontline-duel-depth-union-t4-v1`) |
| Qualification exit code | **0** |
| Tier awarded | **T4** · `balanceEvidenceEligible: true` · `profileComplete: true` |
| Probes | prerequisite T3, suppression-choke, entry-initiative, prediction-chamber, front-rotation, map-holdout — all PASS |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| `qualification.json` sha256 | `27a5329b1b9d3a067fa98cf043d2adb9c12677ed088b38e339d22d5332078ef1` |
| Evidence | `evidence/t4/qualification.json` + every probe replay and viewer |
| Per-file source hashes | `sha256s.txt` |
| Crew game measured on | `frontline-labs-1-striker-vs-striker-sail-open-facing-locked`, rules fp `218b6f06…` |
| Sparring baseline | wave-4 rebuilt from source, `a7bd232c1cb92e6099c21c34b75e62b95e01a678c4ab2bbb378719206ca2dfdd` |
| Git | nothing committed |

The frozen wave-4 artifact (`16ab20f1…`) faults on the current crew contracts, as
the brief warned. Its rebuild from the same source under nilbots 0.9.21 is
`a7bd232c…`, and every record below is against that.

## Doctrine, in one paragraph

Ground is the only score, so every tick either takes a tile, holds a tile, or
removes the body standing on it, and every weapon is priced against the tick it
displaces. Revision 5 re-derives the pose-space around the one new contract
fact — the mobile gun launches at −1/0/+1 sectors off facing — and the derivation
turns on an asymmetry nobody has to be told: a CARDINAL bearing is launchable
from exactly one facing, a DIAGONAL bearing from two, because a diagonal is the
shared boundary of two apertures, so under `facing-locked`, where a rotation is
not a flourish but the way a body travels, a contact on a diagonal is the one
pose where turning onto a route does not cost the shot; `Arms` reads that
aperture from `shotProgram.minInitialAimSteps`/`maxInitialAimSteps` and every
firing-seat, lane and tie-break question is asked through it instead of through
an assumption about cardinals, which is also why an aim-only diagonal is
enumerated beside the straight bolt rather than beside the bends — it is one
decision with one committed heading, so the diagonally adjacent kill and the
bolt that covers the tile a target steps ONTO both exist now where neither was a
legal program before. The same offsets cut the other way and the dodge stops
being binary: one enemy facing lays three rays, so "out of the lane" mostly
stops existing near a contact and the router takes the tile under the fewest
rays rather than the tile under none. What the offsets do NOT do is pay for the
fan any more: the volley's declared spread is the facing lane and both 45°
neighbours, which is now exactly what the mobile gun can launch one at a time at
less than half the cadence without giving up the step, so the cast's whole
remaining product is simultaneity across lanes, worth nothing while the lanes
point at one body that can only take one damage — hence a re-derived, re-measured
decline, tightened by a gate that also refuses to feed a raised arc, because
every deflected ray returns along the exactly reversed heading to a tile a
stance cannot leave. And because the offsets make firing seats roughly twice as
common, the striker can finally hold the seat its stat line was always pointing
at: a body whose *declared sight range* is shorter than this one's is blind in a
band this one is not, at every bearing and from every facing, and free damage in
that band is the only way a 3-health duelist beats a 5-health one — so the
doctrine stops advancing there, which is a STOP read off the tile already
occupied rather than a chase, and it is inert wherever the envelopes match.
Everything else — slot counts, unlock ticks, rebuild clocks, stance routes and
budgets, fan width, guard arcs, route placement legality, transition
reversibility, capture policy, decay clock, arrival placement, the live advance
hold, per-projectile cadence and damage — comes from `StartLife.Contract`, the
frozen observation, or the per-tick legality mask.

## Measured records — revision 5 vs the rebuilt wave-4 predecessor

Crew game, spelled every time as
`--movement facing-locked --pendulum keel --skills kit --bend universal
--aim offset --stance-ground open` (plus `--five-slots wane` where a fabricator
is in the pair), WASM runtime, 20 seeds
(42, 104729, 7, 1337, 20260730, 99, 3, 5, 11, 23, 8191, 65537, 2026, 314159, 17,
71, 977, 4099, 60013, 101). `prog` is mean signed territorial progress for the
candidate; `brW/brL` counts wins and losses decided by base breach.

| pairing | resolved identity | side | n | W-L-D | brW/brL | prog | K/D | faults |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| striker-vs-striker | `…-striker-vs-striker-sail-open-facing-locked` | a | 20 | 0-0-20 | 0/0 | +0.00 | — | 0 |
| | | b | 20 | 0-0-20 | 0/0 | +0.00 | — | 0 |
| | | **both** | **40** | **0-0-40** | 0/0 | **+0.00** | 1.00 | 0 |
| bulwark-vs-striker | `…-bulwark-vs-striker-sail-open-facing-locked` | b | 20 | **20-0-0** | **20/0** | **+30.00** | 0.71 | 0 |
| fabricator-vs-striker | `…-fabricator-vs-striker-sail-open-wane-facing-locked` | b | 20 | 0-20-0 | 0/17 | −29.40 | 1.10 | 0 |

Predecessor controls, same fixtures, same seeds, same flags:

| pairing | candidate | W-L-D | brW/brL | prog | K/D |
| --- | --- | --- | --- | --- | --- |
| striker mirror | wave-4 vs wave-4 | 0-0-40 | 0/0 | +0.00 | 1.00 |
| bulwark-vs-striker | wave-4 as the striker | **0-20-0** | **0/20** | **−30.00** | 0.67 |
| fabricator-vs-striker | wave-4 as the striker | 1-17-2 | 0/11 | −21.35 | 1.14 |

Read those two tables together, because the headline is one row.

**The provisional lab signal replicated, and then inverted in one cell.** My own
wave-4 doctrine, put on a striker against a bulwark chassis, loses every match by
base breach at the floor (−30). That is the signal the brief describes, measured
on my own lineage. Revision 5 turns that exact cell into 20-0-0 by breach at
+30, and the ablation below shows which construct does it. Against the
fabricator chassis it goes the other way by about eight points of progress, and
in the mirror it is behaviourally **identical** to the predecessor — same 3680
shots, same 480 diagonals, same 2960 straight bolts, tick for tick.

**Only three of the six pairings can carry a candidate of my class**, and one
side is unavailable in the cross-class ones: a canonical pair binds each class to
its own team side, so there is no assignment that keeps my bot a striker on the
other side of `bulwark-vs-striker`. Side b there means "the striker side", not
"the swapped side".

**Twenty seeds are not twenty observations, and this is the single most important
caveat in this document.** In the mirror and in both cross-class pairings, all 20
seeds produced *byte-identical* decision counts and identical outcomes — 1080
kills over 40 matches, 80 over 20, first claim at exactly tick 51.0 — so each
pairing is effectively **one** game per side, not twenty. The map is
mirror-symmetric, the opening is forced, and the per-life random stream is only
consumed by tie-breaks that this lineage's route search almost never reaches. The
only configurations whose mirror showed real seed variance are rows 4 and 5 of the
ladder below — both discarded, and both variable precisely because they spend
ticks on choices the random order gets to break. So a 20-0-0 and a 0-20-0 in the same
cell differ by one duel, and I would not present either as twenty independent
wins. It is why I measured duel metrics (kills, damage, first-claim tick) beside
the outcome, and why the ablation below is reported as a ladder rather than as
significance.

## Ablations — every construct measured, three of five thrown away

Each variant is my own revision-5 source with exactly one construct changed,
built through the same controlled toolchain, and run over the same three
pairings and 20 seeds. `mirror`/`bulw`/`fabr` are mean territorial progress.

| # | variant, relative to the shipped build | artifact | mirror | bulw | fabr |
| --- | --- | --- | --- | --- | --- |
| 0 | the wave-4 predecessor itself | `a7bd232c…` | +0.00 (0-0-40) | −30.00 (0-20-0) | −21.35 (1-17-2) |
| 1 | **shipped** — aperture reads, aperture tie-breaks, sight-band standoff | `9912013a…` | **+0.00 (0-0-40)** | **+30.00 (20-0-0)** | −29.40 (0-20-0) |
| 2 | **+** bearing-derived DESTINATIONS (armed goal filter, twice-armed reseat); no standoff yet | `d93705ec…` | −25.40 (2-38-0) | — | — |
| 3 | **+** action-count ROUTING **+** facing bought in the step tie-break | `585fc455…` | −30.00 (0-40-0) | +30.00 (20-0-0) | −30.00 (0-20-0) |
| 4 | **+** action-count ROUTING alone | `a5a065cb…` | −16.02 (5-35-0) | +30.00 (20-0-0) | −30.00 (0-20-0) |
| 5 | **+** facing bought in the step tie-break alone | `b06d0064…` | −9.35 (16-24-0) | −30.00 (0-20-0) | −27.35 (0-20-0) |
| 6 | **−** the two aperture tie-break terms | `60eabac9…` | +0.00 (0-0-40) | −18.25 (3-17-0) | −28.05 (0-20-0) |
| 7 | **−** the out-of-reach standoff clause (measured inert on every pairing) | `42d8dc9b…` | +0.00 (0-0-40) | +30.00 (20-0-0) | −29.40 (0-20-0) |

Row 2 predates the standoff, so it has only the mirror sweep: it was abandoned
there. Rows 3–5 are the same three constructs in the three combinations that
separate them. Five things in that ladder are worth stating plainly.

**A pose may be preferred, never chased (row 2).** My first attempt made the
aperture pick destinations: filter the objective's free tiles to the ones the
most facings arm, and reseat within the objective toward a twice-armed tile. It
lost 38 of 40 mirror matches. The reason generalizes past this bot and past this
wave: under `facing-locked` a change of destination costs a ROTATION, and a
destination derived from a contact's bearing changes every time the contact
steps — so the body pays a turn per enemy step and arrives nowhere. Bearing
preferences belong in tie-breaks, where they are free.

**Correct arithmetic, wrong objective (rows 3–4).** Revision 4 searched pose
space for the *enemy's* reachable set and was right to; its own routing stayed a
tile-count search, which prices four steps and three turns exactly as it prices
four steps. I fixed that — a backward uniform-cost search over (tile, facing)
whose answer is the first ACTION — and it worked exactly as designed: first claim
moved from tick 80 to 51 against the bulwark chassis and to tick 12 in the
mirror. It then lost every mirror match by breach. Arriving first is what loses
here: companions unlock at 120 and 260, arrivals rally to the owning side of the
ACTIVE objective, so a body that moves the front before it has a companion is a
lone body standing deep in ground the opponent respawns beside. I deleted the
search rather than ship a faster route to a worse position, and I would want it
back the moment a doctrine exists that can hold what it takes. Row 4 isolates it
from the tie-break change: on its own it costs the mirror less (−16.02) and still
costs it.

**Row 6 is the attribution for the one cell that inverted, and neither half does
it alone.** Row 6 is the shipped build with the two aperture tie-break terms
removed — credit for a bearing more than one facing arms, and a charge per launch
ray beyond the first over a tile — leaving the sight-band standoff in place. The
standoff by itself moves the bulwark cell from the predecessor's −30.00 (0-20-0)
to −18.25 (3-17-0): better, not an inversion. The two tie-break terms on top of
it take it to +30.00 (20-0-0), and they change the mirror by nothing at all. They
are the smallest constructs in this revision and jointly the load-bearing ones —
which is itself the wave's lesson, since they are the two places the aperture is
consulted where consulting it is *free*.

**Row 5 is the trap I nearly shipped.** Scoring the step tie-break at the pose
the action actually produces is a real correctness fix — under `facing-locked` an
"equally short step" is often the rotation that unlocks the step, and revision 4
charged lanes and arcs to a tile the body is not standing on. Adding the *aim*
forecast to that pose looks like the same fix and is not: it makes the router
buy facings, which reintroduces the row-2 failure through a side door. The
corrected tile stayed; the forecast term went.

**The out-of-reach standoff clause never fires here (row 7).** A body whose gun
cannot travel this far cannot answer, which is sound doctrine and would matter
against a shorter-ranged chassis — but my own sight range is six and the shortest
gun on this arm reaches six, so the band where the clause could apply is a band I
cannot see into. It is kept because it is a correct contract read that
generalizes, and reported as measured-inert rather than as a feature.

**Where the fabricator regression comes from, honestly.** It is the same
aperture tie-break terms: row 6 recovers about a point of it while destroying the
bulwark cell, so the trade is real and I took the side the brief points at. The
fabricator chassis fields four bodies to my three under `wane`, and against
several bodies the "fewest rays over this tile" preference has many more rays to
count and picks differently. I did not find the fix inside this budget and I am
not going to claim I know which way it would go with variance in that cell.

## Re-pricing the volley under the new geometry

Revision 4 declined every cast and granted the fan one thing the gun could not
buy: BEARINGS. That premise is now false, and it is the cleanest thing the aim
arm changed.

`striker-volley` declares `volley.projectileCount: 3` with spread
`symmetric-adjacent-heading-fan-…` — the facing lane and both 45° neighbours.
`striker-bolt` declares `minInitialAimSteps: -1, maxInitialAimSteps: 1`. Those
are the **same three headings**. So the fan no longer sells a bearing the mobile
gun cannot open; it sells the three of them *simultaneously*, for
`cooldownTicks: 5` against the bolt's `2`, from a form whose action mask offers
no `move`, behind a 2-tick wait-only entry and a 1-tick forced exit, with
`cooldownContinuity: preserve-remaining-ticks` on both legs.

Simultaneity across lanes is only worth buying when the lanes hold different
BODIES, because coverage is capped at one damage per body however many rays sweep
it. So the shipped gate adds two contract-derived refusals to revision 4's three:

- **fewer than two bodies under the rays** — a fan whose whole value sits on one
  target is a bolt with a four-tick tax, and the gun can have that bearing this
  tick;
- **any ray a raised arc would eat** — the deflection relaunches from the
  shield's tile along the exactly reversed heading, which is the line back to the
  tile a stance cannot step off. The addendum is right that a fan is the natural
  shell-breaker: three rays into a face is the whole three-deflection break in
  one cast. It is simply not the caster who profits, and a 3-health body that
  breaks a shell by standing still in the return path has traded a shield for a
  life.

**Casts cast across the entire wave: 0.** Not for want of a route: the route is
read, priced, and reachable, and the predecessor cast twice against the
fabricator fixture where this build casts nothing. That is the new gate, and it
is the same verdict re-derived rather than the old verdict re-used.

## Skill and diagonal usage — shipped build, per sweep

| pairing | shots | diagonal launches | aim-only diagonals | diagonal+bend | cardinal+bend | straight | casts | shells | slots fielded |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| striker mirror (40) | 3680 | 480 | 0 | 480 | 240 | 2960 | 0 | n/a | 3 own / 3 opposing |
| bulwark-vs-striker (20) | 592 | 172 | 60 | 112 | 60 | 360 | 0 | 0 raised, 0 fed | 3 own / 3 opposing |
| fabricator-vs-striker (20) | 1429 | 609 | 187 | 422 | 136 | 684 | 0 | n/a | 3 own / **4** opposing |

- **Diagonal launches are 13% of fire in the mirror and 43% against the
  fabricator chassis**, and the aim-only diagonal — the shot that did not exist
  before this arm — is 0 in the mirror and 12–13% of fire in the cross-class
  cells. The mirror figure is not a bug: two identical duelists on a symmetric
  map hold cardinal bearings on each other, and the aim-only diagonal only wins
  the bucket where a bearing is genuinely off-axis.
- **Shells raised: 0. Shells fed: 0. Enemy turrets anchored: 1 per bulwark
  match.** My class has no guard route, so the count that matters is the second
  one, and it is zero: no `projectile-deflected` event appears in any match this
  wave, so no bolt of mine ever died on an arc. The bulwark fixture's own
  fortification is a turret rather than a shell (my emplacement search wants
  strictly more health, which the shell does not have), and it used it — one
  `transform`, 96 ticks in `bulwark-prime-turret`, 7 absolute-heading turret
  shots, under the open arm's placement rules. So the guard geometry is exercised
  only as a negative, and the turret cycle and open-ground anchor are exercised
  positively, both with zero faults. Cited replay:
  `sandbox/vector-edge-w5-scratch-2d9f0a7c/bulw-probe/`.
- **Slots fielded** is read from `topology.unitSlots` per team, never assumed: 3
  against a non-fabricator, and the asymmetric 4-3 under `--five-slots wane`,
  whose fourth slot unlocks at 300 with a 22-tick ordinary rebuild. Zero faults
  reading it.
- **Zero runtime faults across every match in this wave**: 40 mirror + 20 + 20
  cross-class per configuration × 7 configurations (560 matches), plus the whole
  qualification suite.

## Contract reads and repairs made this pass

- **The aperture is a contract read, not a constant.** `Arms` resolves the legal
  initial-aim offsets per form and answers two questions with them: which
  headings a facing may launch along, and how many of this body's four facings
  would leave it armed against a visible body from a tile. Revision 4's
  `HasLane` hand-walked the four cardinals; it is gone. Where the bounds are
  zero, every answer is revision 4's answer, which is what keeps the
  qualification profile — which carries **no** aim offsets — playing the measured
  doctrine.
- **Route placement, not map tags.** Revision 4 refused to transform on the union
  of every `transition-placement-forbidden` tile on the map. Under
  `--stance-ground open` a route's own `placement.forbiddenTileTags` is **empty**
  while the map keeps the tags it always carried, so the old test refuses legal
  emplacements on exactly the objective tiles the ground arm opened. Placement is
  now asked of the route (`Doctrine.PlacementAllows`).
- **A fortification is a posture, not a sentence.** `irreversibleForLife` is read
  rather than remembered, and the opponent-model consequence is priced: a
  zero-weight body that declares a route back into a form with objective weight
  is discounted 0.15 instead of 0.40, because it is temporarily absent from the
  capture count rather than spent, and the health it is carrying is what it
  brings back onto the point. On a contract where the exit does not exist the old
  number applies. (The coordinator's mid-wave correction landed exactly here.)
- **A turret on an objective is not a contest.** Objective weight zero is already
  excluded from the presence and capture reads, so an opponent that fortifies the
  point it is holding hands over sole presence. The open game makes that
  reachable; the code needed nothing.
- **Lane pressure replaced lane membership.** `Field.LanePressure` counts the
  distinct launchable enemy rays over a tile instead of answering yes/no, and
  every ordering that used the predicate now uses the count. On a contract
  without offsets a covered tile counts exactly one, so the ordering is
  unchanged.
- **Deflections resolved per ray, once.** The guard scan used to run inside the
  per-enemy loop, which billed the same dead bolt once per visible body. It now
  runs once per ray before anything is priced — which it had to, because the
  count is load-bearing in the new cast gate.
- **The diagonal tie-break is mirror-fair.** Turning toward a target exactly on a
  diagonal used to prefer the horizontal axis. Both bracketing facings arm that
  bearing and both keep it in the sight quadrant, so a fixed axis there is a
  systematic side preference on a mirror-symmetric map that both teams share and
  which therefore does not cancel. It is broken on this life's own stream now,
  advance-first, like every other tie in this doctrine.
- **The scaffold helper was trimmed, and this is a disclosure.** `ArenaBasics.cs`
  is no longer byte-identical to `templates/botarena-generic-actor/ArenaBasics.cs`
  (`567e9faf…`): I deleted the four composite decision helpers and ten unreachable
  private helpers this doctrine never calls. Nothing retained was edited. The
  reason is friction #1 below.

## Frictions — top 3

1. **The 256 KB source cap counts the scaffold, and the scaffold is a fifth of
   it.** `nilbots build` refuses with `error: Sources too large (max 256 KB)`,
   and the generated `ArenaBasics.cs` is 49.7 KB of which this bot calls seven
   members. My wave-4 freeze was 238 KB — 24 KB of headroom for a whole
   revision — so adding one 8 KB file and documenting it put me over, twice, and
   both times the fix was to delete parts of a *generated* file I am told to keep
   synced with the template. That is a bad incentive: the cap is spent on code
   nobody wrote and prose is the first thing an author reaches for when trimming.
   Three fixes, any one of which would do: exclude the unmodified scaffold helper
   from the cap, split it into per-concern files so unused ones can simply be
   deleted, or make the error say what the current total is and which files
   dominate it — it currently names neither, so the first thing I did was guess.

2. **Twenty seeds are one observation, and nothing says so.** Every sweep I ran
   on this arm returned byte-identical decision counts across all 20 seeds:
   3680 shots in the mirror, 80 kills against the bulwark chassis, first claim at
   tick 51.0 with zero variance. The map is symmetric, the opening is forced, and
   the per-life stream is only consumed by tie-breaks a competent route search
   rarely reaches — so a 20-seed sweep buys one game and a false sense of n. I
   only caught it because six quantities in a row were exactly divisible by 20.
   This is a measurement hazard for every author on this arm and for the lab
   reading their reports: `--seeds` looks like replication and here it is not. The
   runner could say so — report a variance or a distinct-outcome count beside the
   record, or refuse to summarize a sweep whose replay hashes are all identical
   modulo the artifact — and the balance packet could ask for a seed-variance line
   in every measured table.

3. **`--five-slots wane` is legal on a cell that cannot carry it, and silently
   inert.** The brief tells me to pass `--five-slots wane` only when a fabricator
   is in the pair, which is careful advice I had to follow by hand for three
   pairings. On `bulwark-vs-striker` the flag is accepted and inert-omitted from
   the identity; on `striker-vs-striker` likewise. That is friendly, and it is
   also how a sweep silently measures the wrong cell: an author who sets one flag
   list for a matrix and forgets which arm resolves the fabricator gets a
   `wane`-labelled log line for a ruleset with no fabricator in it. One printed
   line — `five-slots: wane (inert on this class pair)`, beside the bend-envelope
   line that already prints exactly that kind of note — would close it.

### Smaller notes

- **Documentation gap, and the one that cost me most:** the addendum's aim
  section says the offsets restore "the ±1-sector initial launch offset" and that
  a bolt "may launch at 45° off facing (aim-only, zero bends)". What it never
  says, and what turned out to be the whole revision, is that the offsets make
  the volley's declared spread *redundant with the mobile gun* — the fan's three
  lanes and the gun's three aim options are the same set. The skills table and
  the aim section are two screens apart and neither points at the other. One
  sentence in the aim section ("the offsets give the mobile gun the volley's own
  three lanes, one at a time") would have saved a full measurement cycle, and it
  is exactly the kind of consequence the addendum otherwise states well.
- **`aimOnlyProgram` is the right shape and the docs undersell it.** The contract
  hands you the exact inert curvature to send with an aim-only shot, which is the
  difference between a rejected payload and a legal one. Its SDK summary is
  "Required inert curvature for aim-only attacks" — accurate, and it does not say
  that `invalidPayloadResult: rejected` means guessing costs you the tick. Worth
  a sentence.
- **Confusing terminology, still and newly:** "range" is three unrelated numbers,
  as the rule card warns — and this revision's whole standoff read is the
  observation that two of them (a form's declared *sight* range and its
  projectile's *travel* budget) differ between two chassis in opposite
  directions, which is easy to miss precisely because one word covers both.
  Newly: `aim` names two
  different things one screen apart — `--aim offset` the arm, and
  `minInitialAimSteps` the per-profile bound — while `omnidirectionalAim` on the
  turret means "ignores facing entirely", which is a third sense.
- **Hardcoding temptations resisted:** ±1 aim offsets (read Min/MaxInitialAimSteps
  per profile); the fan's three lanes (derived from `volley.projectileCount` plus
  the spread policy); four slots and the 300/22 schedule under `wane` (read
  `topology.unitSlots` and the lifecycle assignments per slot); the turret's
  once-per-life anchor (read `irreversibleForLife`); the empty stance-placement
  tags under the open arm (read the route's own `placement`); stance windups 2
  and 1, the volley cooldown 5, the break at 3 (read per route and per profile);
  one damage per bolt (read `damagePerHit`, per projectile).
- **Timings:** cold `--no-cache` WASM build ≈ 9 s (platform-matched Docker
  builder on macOS); warm cache hit ≈ 0.06 s; full `frontline-qualification-5`
  including the hash-linked T3 prerequisite ≈ 5.5 s; one 500-tick WASM match
  ≈ 2.8 s, so a three-pairing 80-match sweep is about 4 minutes and writes
  ~2 GB of replay and self-contained viewer. Deciding what to measure was again
  the bottleneck — and this wave, discovering that the measurement was nearly
  blind (friction 2) cost more than any build.
- **Strategy passes:** one, spent as a ladder rather than as a guess. Five
  constructs were built and measured; two shipped, three were deleted with their
  numbers recorded above. The one idea underneath all five is the lineage's
  usual one — price a decision against the tick it displaces, using contract
  legality rather than a prior — and the two that survived are the two where the
  contract had genuinely changed what a tick buys.
