# DX notes — ledger-fly revision 6 (Frontline classes, the deck game, coordination wave)

## Isolation statement

Written from this project's own sources, its own frozen wave-5 predecessor, its
own qualification report, and matches this entrant played against **its own
rebuilt wave-5 source and three sparring variants built from that same source,
and nothing else**. No other entrant's directory, source, standings, replays,
qualification report or aggregate balance report was opened; no scratch directory
other than my own was read or written. Permitted material actually consulted:
`docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`,
`docs/FRONTLINE-LABS-RULES.md`, `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` (all
three read in full), `templates/botarena-generic-actor/` (the scaffold carried
byte-identical), the public SDK types under `src/BotArena.Sdk/`, my own frozen
wave-5 directory (read only, left byte-untouched), and `sandbox/cli-publish/`.
All four of those document hashes are **unchanged from the values my wave-5 notes
recorded**, so nothing in my permitted material moved under me this wave. Private
scratch was `sandbox/ledger-fly-w6-scratch-7b3e91a4/` — a uniquely named
directory, not a shared or guessable one.

Two disclosures, both in the spirit of the packet's exposure rule.

1. **The cohort directory is still a shared parent of my output directory.** The
   ordinary act of creating my own freeze target puts it beside other entrants'
   directories. I opened none of them: no source file, replay, qualification
   report, standings table or aggregate report belonging to another entrant was
   read, and every match reported below was played against my own rebuilt
   predecessor or my own variants. Third wave running for this one; still cheap to
   fix by making per-entrant directories siblings of the cohort root rather than
   children of it.
2. **A freeze-integrity warning reached me from the orchestrator mid-run**, saying
   that `nilbots build` globs every `.cs` under the project directory, so
   archiving an ablation source anywhere inside a freeze tree makes that tree fail
   to rebuild with duplicate-member errors — and that this had actually happened
   in *another author's completed run*. That is the only thing I know about
   another entrant's work, it is a mechanical build fact rather than a strategic
   or competitive one, and I did not read their material. I acted on it: every
   ablation and sparring variant lives in my scratch directory and nothing but
   the submitted set is inside the freeze, and I added a rebuild-from-the-frozen-
   tree step as the last action before writing this file. It reproduced the
   shipped hash twice with the same cache key.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `ledger-fly` |
| Class | `fabricator` (declared in `botarena.json`) |
| Authoring lineage | `ledger-fly-v1` |
| Revision | 6 (wave-6 cohort, the coordination wave) |
| Role | verdict-doctrine |
| Doctrine | attrition banker |
| Target | cumulative T4 (retained) |
| Budget | a coordination pass over multi-body play; the doctrine is not reopened |
| Predecessor | `arena-bots/frontline-labs/classes-wave-5-2026-07-30/ledger-fly` (untouched) |
| Game | `--classes fabricator-vs-fabricator --movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open --five-slots wane` |
| Resolved ruleset | `frontline-labs-1-fabricator-vs-fabricator-crew-facing-locked`, rules fingerprint `b28fb9d001d615b303efa11f1d676f42bcb3a76415966962ff1d698e1f0760fa`, topology `two-team-one-controller-four-slots-v1` |
| Author packet | sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| Class addendum | sha256 `2333bd3c9f412e4e9439779ef3d5f2ca6bc8abae6f00973daf54f7e4c892de50` |
| Template helper | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` (carried **byte-identical**; verified by diff) |
| Source-tree sha256 | `63a6f2fec86b54985833095c8f69f6297a1cde7f6ae1e837940b5f0ba0b72801` |
| Toolchain | nilbots CLI **0.9.22**, SDK 0.10.6, game rules 0.5, runtime protocol 0.1 / actor 1.0, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, WASI p1 core module, platform-matched Docker builder (Apple Silicon) |
| Build cache key | `6a3b2bb6f2e4b0cdeef7d753ffafb943d81cfd5ef9c1d5ebf60b028a9f8ed1de` |
| **`out/bot.wasm` sha256** | **`49f452a1e53b6e3297e6bae8a8c2bb3f35dd4cafb0a775a2a1d0ea1c7b29c752`** (3,518,776 bytes) |
| Qualification | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, WASM, exit **0**, tier **T4**, `balanceEvidenceEligible: true` |
| `evidence/t4/qualification.json` sha256 | `1e5a58b5506a65c278f34d968d5cb984f2d17eb4be9145bee0d7adf414efe6b0` |
| T3 prerequisite report sha256 | `0d0cd1451db18db3faca2cccf750484a89655a087ef6145ccca63f05f77f3c7e` |
| T2 prerequisite report sha256 | `e619fdc76ed4903b5a3cb93a4b03f7e32660188b6ac45f5495de341569937038` |
| Verified probe replays | 36 under `evidence/t4/`, one spot-verified with `nilbots verify` (OK) |
| Sparring baseline | wave-5 source rebuilt `--no-cache` under CLI 0.9.22, artifact `0bb65081965dea0c060a2ee1648d513d35dab7f6348d4fe5978316a9cf31fc5f` |
| Rebuild proof | two `--no-cache` builds **from the frozen tree**, same cache key, same artifact hash |

Per-file sha256 of the submitted set is in `SHA256SUMS`, with the source-tree hash
construction (name, NUL, big-endian length, bytes, sorted) carried unchanged from
revisions 2–5. `Bearings.cs`, `Stances.cs` and `LedgerFly.cs` changed; `Convoy.cs`
and `Coordination.cs` are new; `ArenaBasics.cs`, `FabricationRoute.cs`,
`Field.cs`, `Gunnery.cs`, `Kinematics.cs`, `Ledger.cs`, `MatchLens.cs`,
`Ratchet.cs`, `LedgerFly.csproj` and `botarena.json` are **byte-identical to wave
5**. Every suite-5 probe passed on the first canonical build of this revision.

**One identity note carried forward.** The brief names this game `deck`; on a
fabricator mirror it resolves to `…-fabricator-vs-fabricator-crew-facing-locked`,
because `--stance-ground open` touches nothing that exists in that cell and is
inert-omitted. My own cross-class probes below resolve `…-fabricator-vs-striker-
deck-facing-locked` and `…-bulwark-vs-fabricator-deck-facing-locked`, so the
documented behaviour is exactly right — it is just startling the first time your
primary cell answers with a different token than your brief.

**The wave-5 artifact hash does not reproduce under 0.9.22, and that is expected.**
The frozen wave-5 source rebuilt byte-for-byte gives cache key
`9effe77f…`/artifact `0bb65081…` where wave 5 recorded `63359242…`/`12165ad4…`.
Nothing in the source moved (I verified the csproj's relative-path depth is not an
input by building the same bytes at two tree depths and getting one cache key).
The staged SDK/Guest bytes are part of the key by design, so a CLI bump
invalidates it. Worth knowing before anyone tries to re-verify a frozen hash with
a newer CLI.

## Doctrine in one paragraph — the delta

The doctrine is not reopened and not one line of it changed: the bank is still the
slot the contract returns automatically, children are still the currency, the unit
of account is still the convertible objective-tick, and every trade still settles
against the two clocks the contract declares — one capture is `threshold / gain`
ticks of sole presence, one body is its own slot's rebuild clock, and where the
second exceeds the first a trade the earlier revisions booked as profitable is a
loss. What wave 6 adds is the one debit those books never carried: **the tick this
team loses to itself.** A body whose step is refused because a *sibling* wanted
the same tile pays a full tick, and the ledger recorded nothing — so it kept
paying. Measured on the rebuilt wave-5 artifact playing itself over 24 matches,
**470 of its 503 refused steps were caused by one of its own bodies**, and
essentially all of them were the same failure: two or three bodies stepping onto
one tile of the contested region, the same pair re-colliding every third tick for
as long as the exchange lasted, because the shortest route to the nearest tile of
the region is the same route for all of them. The congestion line prices that
debit the same way everything else in this bot is priced — in ticks, against the
declared clocks — and it collects it in two places: a **berth**, so one tile of
the region belongs to one body, and a **schedule**, so a dearer sibling's route
reserves the tiles it needs for the ticks it needs them. Right of way is priced
rather than arbitrary: fewer ticks of route remaining first (asking the body that
is nearly there to detour is the dearer yield), then the larger declared
replacement cost (a slow-refilling slot is worth more ticks, and the bank carries
every pipeline clock it feeds on top of its own return), then unit and life id so
the order is total and two of our bodies can never both believe they have right of
way. And because there is no channel to publish an intention through — private
memory is life-scoped and a life never sees an ally's action — the plan is built
from the frozen team observation and the contract *alone*, with no remembered tile
and no `context.Random`, which is what makes every one of our bodies compute the
identical plan and obey the part of it that binds itself.

## The coordination mechanism, stated once

This is the part I would most want handed to me at the start of the wave, so it
goes first.

There is no shared state between our bodies. Each life is a fresh runtime with
empty private memory, observations are frozen before any same-tick decision
executes, and a life never sees an ally's current action. Those are the rules and
they are right. The constructive consequence is not stated anywhere and it is the
whole mechanism: **every life of a team receives the same frozen observation** —
the same allied bodies with their positions, facings, forms, health and cooldowns,
the same enemy union, the same slot states, the same mode. So a plan computed from
observation and contract alone is *common knowledge*: each body derives the whole
team's plan, obeys its own part, and knows the others are deriving the same one. No
channel is needed and none exists.

It has one sharp edge, and I walked into it in my first draft. **Any private-memory
or random term inside such a plan silently breaks the agreement.** My first version
handed `Convoy` the ledger's *remembered* exchange anchor as its focus; that is
life-scoped, so a body born on tick 300 and one born on tick 40 would have planned
two different plans and handed the collisions straight back. `Convoy.cs` therefore
takes the region centroid (or the declared arrival region when no region is
active), and the file says why in a comment I would not delete.

## The four lines, and what each one is answerable for

All four are single static readings in `Coordination.cs` so that a leave-one-out
build is a one-line diff. All four are true in the frozen artifact.

| line | rule | bar it meets |
| --- | --- | --- |
| **congestion** | one berth of the contested region per body, assigned in precedence order; a dearer sibling's route reserves each tile against the tick it needs it — refused this tick and next, priced after | (1) and the placement half of (3) |
| **corridor** | a one-tile corridor cannot be shared: the body with right of way owns the corridor tiles its route crosses. Corridors are derived from the delivered map (an open tile whose open cardinal neighbours are one opposed pair, or a dead end), never from coordinates | (2) |
| **traffic** | no body of ours stands on, and no fabrication lands on, the tile our own next automatic arrival needs — the rear-most free tile of the own-side region along our advance direction, not the whole region | (3) |
| **spacing** | two of our bodies are not left where one declared fan spread, or one deflection returned down our own firing lane, covers both — at strict tie-break weight | (4) |

A yield never immobilises a body. `Walk` drops the yields as its last resort
before it would return nothing, and a dodge *prices* them instead of obeying them,
because the ledger has never rated a tile above a body.

## Instruments, and which ones to believe

Three, and the differences between them are the most useful thing I learned.

- **Record and margin versus the rebuilt predecessor, paired across sides, 24
  seeds** (48 matches). Required by the brief. The primary reading is the **paired
  mean territorial margin**, because that is what the rules card says decides a
  tick-capped match, and because the null validates it at exactly **+0.00** while
  W/L points on the same null give exactly half — both are unbiased, and the
  margin has far more resolution on a cell where most losses are by one point.
- **The opponent stable** — the same build against the rebuilt predecessor *and*
  three sparring variants built from that same source (`spar-hug`: bank standoff 1;
  `spar-deep`: standoff 5; `spar-straight`: aim-only diagonals removed, which was
  wave 5's largest lever). Read as a difference-in-differences: the build's paired
  margin minus the predecessor's own paired margin against the same opponent. This
  is where the honest per-rule attribution lives, for the reason below.
- **Head-to-head candidate versus candidate-minus-one-line.** This *looks* like
  the cleanest causal A/B, and its null is exactly right (the pair that turned out
  to be behaviourally identical scored exactly 24W 24L / +0.00). **It is a trap on
  near-twin pairings and I nearly published from it.** Two builds of one policy
  usually produce a single deterministic story: on the pre-repair source,
  `cand vs no-congestion` returned **0W 48L over 48 matches with one distinct
  outcome per side**, and `cand vs no-corridor` the same. That is not 48
  measurements, it is one coin reported 48 times, and it flatly contradicted the
  other two instruments — which is how I found out that the first corridor and
  traffic rules were over-broad, so it did earn its keep as a *smoke alarm*. After
  the repairs the same instrument returns the exact null on three of the four pairs
  and 17W 31L on the fourth. I report all of it, labelled, because the disagreement
  between instruments is itself the finding of this wave.

**Seeds barely matter in this cell and opponents do.** Across the stable, 64
matches produced 16–27 distinct stories; 48 matches of one pairing produced as few
as 2. Genuine variation came from changing the opponent, not the seed. If I ran
this wave again I would spend the whole compute budget on opponents.

## Records versus the rebuilt wave-5 predecessor

Deck game, both sides, 24 seeds per side, 48 matches, in-process, records are the
candidate's (side *a* = candidate as team 0).

| | side a | side b | record | paired margin |
| --- | --- | --- | --- | --- |
| **revision 6** | 24W 0L | 24W 0L | **48W 0L 0D** | **+20.00** |
| the null: predecessor mirrored against itself | 8W 15L 1D | 15W 8L 1D | 23W 23L 2D | +0.00 |
| WASM confirmation (6 seeds × 2 sides) | 6W 0L | 6W 0L | **12W 0L 0D** | +19.50 |

The WASM subset agrees with in-process **seed for seed** — identical winners,
identical margins, identical end ticks on all 12 — and this wave the **replay
hashes are identical too**, which is a change from my wave-5 note that they differ
because runtime provenance sits inside the hashed header. I re-checked it as a
single direct pair outside the sweep to be sure: same hash, both runtimes.

**Read the null first, as always.** Wave 5's honest claim against its own
predecessor was one net game in twenty-four. This is 48–0 against a 23–23–2 null,
which is a much bigger number than that lineage is used to, and I want to be
precise about what it is and is not. It is a real, side-paired, sign-consistent
result against the exact artifact the brief names. It is **also** a result against
one opponent, on a pairing that collapses to two distinct stories, and the moment
three more opponents are in the room it shrinks to +4.88 mean margin and 51 points
of 64 against the predecessor's 41 — better, but not 48–0 better. Both sentences
are true and the second one is the one I would bet on.

## Per-rule attribution

Opponent stable: 4 opponents × 8 seeds × both sides = 64 matches per build. Margin
columns are the build's paired mean margin against that opponent; the delta is
against the predecessor's own margin against the same opponent, so the predecessor
row is 0 by construction and a behaviourally neutral change scores 0 everywhere.
`selfblk/kdec` is self-inflicted refused steps per 1000 accepted decisions —
the owner-visible silliness, measured.

| build | vs pred | vs spar-hug | vs spar-deep | vs spar-straight | mean delta | pts/64 | selfblk/kdec | choke stalls |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| wave-5 predecessor | +0.00 | +0.00 | +0.00 | +0.00 | +0.00 | 41.0 | **5.86** | 10 |
| **revision 6 (all four)** | **+19.75** | −14.56 | +14.31 | +0.00 | **+4.88** | 51.0 | **0.60** | **1** |
| leave out congestion | +2.44 | −5.00 | +22.94 | +0.00 | +5.09 | 48.0 | 2.37 | 3 |
| leave out corridor | +12.69 | −5.31 | +10.06 | +0.00 | +4.36 | 52.0 | 1.33 | 1 |
| leave out traffic | +19.75 | −14.56 | +14.31 | +0.00 | +4.88 | 51.0 | 0.60 | 1 |
| leave out spacing | +19.75 | −14.56 | +14.31 | +0.00 | +4.88 | 51.0 | 0.60 | 1 |

And the same four, head-to-head against the full build (48 matches each, the
instrument I distrust — distinct stories in brackets):

| pair | record | margin | distinct stories (a + b) |
| --- | --- | --- | --- |
| revision 6 vs leave-out-congestion | 17W 31L | −5.88 | 7 + 2 |
| revision 6 vs leave-out-corridor | 24W 24L | +0.00 | 1 + 1 |
| revision 6 vs leave-out-traffic | 24W 24L | +0.00 | 1 + 1 |
| revision 6 vs leave-out-spacing | 24W 24L | +0.00 | 1 + 1 |
| **revision 6 vs itself (the h2h null)** | **24W 24L** | **+0.00** | 1 + 1 |

Three of those four are the exact null, which is the tell: in a near-twin pairing
the corridor rule never *binds differently* from its absence, so leave-out-corridor
plays the same match as the full build there and returns the null too. Only the
congestion pair separates, and it separates against me: **the build without the
congestion line beats the full build 31–17 head-to-head.** An earlier pass of this
same instrument, on the version of the source before the two repairs below,
returned 0–48 against the full build on *one* distinct story per side; the numbers
moved a lot for a change I can show is behaviourally inert elsewhere, which is the
clearest possible evidence about how much this instrument is worth.

What I conclude, line by line, and what I decline to claim.

- **Congestion earns, and it is the load-bearing line.** Against the artifact the
  brief names it is worth **+17.31 margin** (+19.75 against +2.44) and it is the
  difference between 48–0 and a much thinner result. Across the stable its margin
  contribution is a wash (+4.88 against +5.09 — inside the noise of a metric whose
  whole predecessor-to-candidate delta is +4.88) and it costs 3 points of 64. What
  it does reliably, against every opponent, is cut self-obstruction to a quarter
  (2.37 → 0.60 per 1000 decisions) and choke stalls from 3 to 1. **Shipped**: it
  wins decisively on the required instrument and removes the silliness everywhere
  without losing on the primary metric.
- **Corridor earns on margin and pays 1 point for it.** +7.06 margin against the
  predecessor (+19.75 against +12.69), +0.52 across the stable, 52 → 51 points,
  and it halves the remaining self-obstruction (1.33 → 0.60). **Shipped**, and it
  took two repairs to get there — see below.
- **Traffic is inert in every cell I can reach.** Byte-identical results to the
  full build against all four opponents: same margins, same 0.60 self-obstruction,
  same choke count. It meets bar (3), it costs nothing, and **I am not claiming
  it.** The reason it is inert is worth stating rather than hiding: the tile it
  protects is on the *own-side* region, one position behind the active one, and
  our bodies are hardly ever standing there when the bank's 18-tick clock lands.
  The half of bar (3) that *is* measured — do not fabricate into your own traffic
  — rides on the congestion claims, because the placement set handed to the
  fabrication route includes them.
- **Spacing is inert at the weight it ships at, and measurably worse at any weight
  that changes anything.** In a fabricator mirror no class in the cell declares a
  fan or a guard, so I exercised it in my own cross-class probes (below). At 4
  points it changed real behaviour and the change was bad: against a fan-capable
  chassis it **more than doubled the ticks to a breach, 163 → 366**, because bodies
  spread to dodge a cast nobody had entered yet. At the tie-break weight the bar
  actually asks for ("when an equal-value adjacent pose exists") it returns to
  byte-identical. **Shipped at tie-break weight to meet the bar, claimed at
  nothing.** This is the second lineage in a row where I ship an unfalsified
  reading and say so; a reviewer entitled to be tired of that should read this one
  as an honest negative result rather than a hedge — I found the weight at which it
  mattered and it was worse.
- **The self-mirror is where this pass is weakest, and it deserves its own
  paragraph** (below).
- **`spar-hug` is where the coordination layer loses, and it loses badly**
  (−14.56, with both live rules contributing). That variant is my own bank with
  standoff 1: it puts more mass forward, sooner. Against concentrated mass, a plan
  whose berths deliberately spread my bodies around the region gives up the local
  firefight. I have no fix that measures, and it is the most interesting hole in
  this revision.

## Three rules whose first version measured worse

Each of these was built, measured, and repaired rather than shipped or deleted.
They are the whole reason the numbers above are what they are.

1. **Corridor, version 1: hold the corridor and all four of its neighbours.** This
   map's middle row is a chain of **six** one-tile corridors and it is the main
   artery from either prime spawn to the centre objective, so holding every
   neighbour of every corridor tile on a four-tick route refused most of that lane
   to my own bodies for a third of the match. Head-to-head it lost **0–48**.
2. **Corridor, version 2: hold the corridor plus the tile it exits onto.** Better,
   but the exit mouth *is* the objective approach on this map, and it still cost 6
   of 64 points on the stable (45 against 51) for no margin. Version 3 — the
   corridor tiles and nothing adjacent — is what ships, and it keeps the entire
   silliness reduction (0.60/kdec, 1 choke stall) that version 2 bought.
3. **Traffic, version 1: reserve the whole own-side region.** The helper answers
   *which region* an arrival lands in; the declared policy is narrower than that —
   the rear-most free tile of the region along your own advance direction — and the
   difference cost real games, because the bank returns on a short clock and so a
   whole region went off-limits to every body of ours for three ticks out of every
   eighteen. Narrowed to the rear-most free tiles, the rule became free (and
   inert). **This was my bug, not the rule's:** I reserved what the helper returned
   instead of what the policy picks.

## The self-mirror, which is the weakest result here

Revision 6 against itself, 24 seeds, both sides: 24W 24L, margin +0.00 — a clean
null, as it must be. What it exposes is that the coordination layer's product is
much smaller against a copy of itself than against anything else.

| 48 matches, self-mirror | wave-5 predecessor | revision 6 |
| --- | --- | --- |
| self-inflicted refused steps per 1000 decisions | 12.39 | **5.89** (−52 %) |
| refused steps at a one-tile corridor | 4 | **72** |
| refused steps with neither own nor enemy body on the tile | 33 | **192** |

Against the four opponents in the stable the same build runs at 0.60 per thousand
with **one** corridor stall in 64 matches. Against itself it is ten times that.

I think the corridor and pad numbers are a consequence rather than a failure, and
the reasoning is testable by whoever comes next. Wave 5's bodies piled up on the
contested region and stopped there — 470 of its refused steps are two bodies on one
region tile, which is a stall that never becomes travel. Revision 6 resolves the
pile-up, so its bodies actually *move*, and on this map moving means the middle row,
which is a chain of six one-tile corridors and the only artery between the spawns
and the centre. More travel through a corridor-shaped map is more corridor contact,
and pushing deeper is more contact with the opposing protected pad (which own ground
units may not enter, hence the 192). Against every opponent that is not a copy of
this policy, the same build shows *fewer* corridor stalls than the predecessor, which
is what makes me believe the mirror figure is a mirror artifact and not the rule
misfiring. But it is 18× worse on a metric this wave is explicitly about, I did not
fix it, and the honest summary of the wave is: **this pass removes ~90 % of the
self-obstruction against any opponent that plays differently, and about half of it
against itself, while moving the remainder from the objective region into the
corridors.**

## The owner's complaint, in numbers

The assignment came from watching games — "bots making silly decisions, e.g.
blocking an ally's path in a choke". This is that, counted. Self-inflicted refused
steps are attributed offline from replay v3 by cross-referencing every own
movement intent in the same tick against the refused destination.

| | wave-5 predecessor | revision 6 |
| --- | --- | --- |
| refused steps per 1000 decisions (all causes) | 13.3 | **2.3** |
| of those, caused by one of our OWN bodies | 12.4 (93 %) | **0.8 (36 %)** |
| same-destination collisions with a sibling | 494 in 48 matches | **32** |
| swap deadlocks / static self-blocks | 0 / 0 | 0 / 0 |
| refused steps inside a one-tile corridor | 4 | **0** |
| longest observed repeat of one collision | **6 consecutive attempts**, same pair, same tile, every third tick | none |

Two details worth having in writing. **Every single self-inflicted block in this
lineage is a same-destination collision** — never a swap, never walking into a
standing sibling — because wave 5 already treated allied bodies as obstacles for
*this* tick. What it could not see is a tile that is empty now and that a sibling
is also about to step onto, which is precisely the case the general bar names
("this tick or next"). And the residual `otherblock` count — 56 refused steps with
no own or enemy body involved — is a *different* bug I am not fixing this wave:
those are my bodies walking at the opposing protected pad, which own ground units
may not enter. It is cosmetic (the step is simply refused) and it is not
self-obstruction, but it is silly and it is now measured.

## Cross-class probes (diagnostics, not records)

Declared classes bind each side to its class's canonical team, so there is no
paired-side design available here; these are the fabricator's own signed margin
over 6 seeds. Both opponents are my own rebuilt wave-5 source playing whatever
class the arm assigns it — no other entrant's artifact was involved.

| probe | resolved ruleset | result | what it exercised |
| --- | --- | --- | --- |
| revision 6 vs my source as `striker` | `fabricator-vs-striker-deck-facing-locked` | **6W 0L**, +30, breach @163 | 68 stance transitions; the fan spread is live, so the spacing line is live |
| the same with spacing at weight 4 | same | 6W 0L, +30, breach **@366** | the tempo cost that made me reprice it |
| revision 6 vs my source as `bulwark` | `bulwark-vs-fabricator-deck-facing-locked` | 6W 0L, +7 @499 | **110 deflections against me**, 329 transitions; the return term never fired |

The return half of the spacing line is unfalsified even where shells are up and
being hit 110 times, and the reason is structural: it needs two of my bodies on
one ray *into* a raised arc, and `Gunnery` has refused arcs since wave 4, so the
configuration is one this bot already avoids for a different reason.

## Time

| Step | Wall time |
| --- | --- |
| `dotnet build` of the editing project | 0.6 s |
| in-process match, 500 ticks (warm) | under 1 s |
| 48-match sweep, 24 seeds, both sides, in-process (8-way parallel) | 33 s |
| 64-match opponent-stable pass per build | ~45 s |
| cold `nilbots build --no-cache` (warm Docker builder) | 10.5–11.5 s |
| full cumulative suite-5 qualification (T2+T3+T4, WASM) | 6.0–7.0 s |

The inner loop is excellent and the parallel-friendly CLI is most of why. **My
wave-5 friction #3 is fixed and it mattered**: experiment runs no longer write
`viewer.html`, so a sweep costs ~500 KB per match instead of ~15 MB, I never came
close to filling the disk this wave, and I could afford 700-odd matches instead of
rationing them. The 2 MB source cap is also gone as a constraint — this revision
ships more documentation than wave 5, not less. Qualification evidence is still
213 MB for 36 probes because `qualify` writes a viewer per probe replay; that is
the same as wave 5 and it is the one place the old cost survives. A `--no-viewer`
on `qualify` would take a freeze from 213 MB to about 20 MB.

## Documentation gaps and hardcoding temptations

**1. The coordination mechanism is derivable and undocumented.** See the section
above. The rules card is explicit and correct about what is *not* shared; the
constructive half — the frozen team observation is identical across a team's lives,
so an observation-only plan is common knowledge and needs no channel — is the
entire technique for a multi-body class and every author has to derive it. Two
sentences in the rules card's runtime/memory section would hand it to everyone
equally, and the second sentence should be the warning: a private-memory or
`context.Random` term inside such a plan breaks the agreement silently.

**2. A blocked step does not say what blocked it.** `MovementBlocked` carries the
actor, the tile retained, the tile attempted and the facing — but not the blocker.
So a body cannot tell "an enemy stood there" from "my own sibling wanted the same
tile", which is the difference between a trade and a self-inflicted stall, and it
cannot price the second even though the second is the one it controls. I had to
reconstruct the attribution offline from replay v3 by cross-referencing every own
movement intent in the same tick. A `blockerTeamId`, or a blocked-reason enum
distinguishing *occupied* / *same-destination* / *swap* / *followed-a-vacated-tile*
(the rules card already names those four cases), would turn self-obstruction from
something an author discovers with a replay analyser into something the bot can
bill at runtime. This is the single biggest gap for this wave's assignment.

**3. One match has two authoritative summaries and nothing says which is the
measurement.** `standings.teams[].outcome` and the `territorial-progress` score
are both delivered, and on my ablations they rank in *opposite orders* — with the
second corridor version in, dropping the rule entirely scored 7 points better
(52 against 45) while scoring marginally worse on margin. The rules
card says the tick-cap ranking is signed territorial progress, which makes the
margin the more fundamental quantity, but the CLI line and the standings both lead
with W/L. A sentence in the measurement guidance ("for a small change on a
low-variance cell, read the margin; W/L is the coarse view of it") would have saved
me a wrong turn. Adjacent and worth the same sentence: **effective** sample size.
My 48-match head-to-heads contained one distinct story per side and looked like
authoritative 0–48 results.

**4. `ExpectedArrivalTiles` answers a wider question than the placement policy
does.** The helper returns the arrival *region*; `--pendulum forward-rally`
declares the rear-most free tile of that region along your own advance direction.
Both are documented correctly in their own places, and using the helper's answer
as if it were the policy's answer cost me real games (repair 3 above). Either the
helper should narrow to the policy, or its doc-comment should say "region, not
tile" in the sentence that names the rally.

**5. Hardcoding temptations resisted.** New this revision: **corridors are derived
from the delivered map**, never from the coordinates of this map's middle row
(which I could see, and which would have been six literals); the arrival tile is
derived from the declared advance direction and the region rather than from the
rear objective's coordinates; the fan's spread width comes from
`volley.projectileCount`; a body's tick value comes from its own slot's lifecycle
profile and the bank's from the sum of the pipelines it feeds; the facing-locked
route cost comes from the declared movement coupling (which is what makes the
projection's tick numbers exact rather than a guess — every action costs one tick
under that profile, so plain breadth-first order over (tile, facing) states *is*
the schedule). Carried from wave 5: the rebuild clocks, the four-slot roster, the
unlock schedule, the capture threshold and gain, the aim bounds and aim-only
sentinel, the bend depth, objective weight, and `irreversibleForLife`. `Standoff`
remains the only tuned constant in the bot; the coordination layer adds a horizon
and a search depth expressed in ticks, and the one-point tie-break weight whose
whole job is to be the smallest number in its file.

## Top remaining frictions, ranked

1. **A blocked step does not name its blocker, so the tick a team loses to itself
   is the one cost a bot cannot see.** Everything this wave asked for is priced
   from the *outside* — I could only quantify the problem by writing a replay
   analyser, and a bot in the arena still cannot tell a sibling's block from an
   enemy's. One field on `MovementBlocked` (`blockerTeamId`, better a
   four-way reason matching the rules card's own list) makes multi-body
   coordination measurable from inside the bot, which is where it has to be
   measurable for anyone to price it.
2. **No neutral opponent per class — and this wave showed exactly what that costs.**
   Against the one opponent the brief names, my coordination pass reads 48–0 and
   +17.31 for its main line. Against a stable of four opponents I had to build
   myself out of my own source, the same line is a wash on margin and slightly
   negative on points, and its real product is a 90 % cut in self-obstruction. Both
   readings are honest; only the second is trustworthy, and only the second
   required me to invent the opponents. A system-owned non-strategic calibration
   opponent per class remains the single biggest measurement gap for an isolated
   author, and for a *coordination* brief it is worse than usual, because a mirror
   against your own lineage is the one matchup in which everybody makes the same
   mistake at the same time.
3. **Seeds are not the variance knob in this cell and nothing says so.** 24 seeds
   bought me 2 distinct stories on my primary pairing; 4 opponents bought 27. I
   spent most of my compute before noticing, and the guidance I was given talks
   about seeds. "Vary the opponent, not the seed, on a low-variance cell" belongs
   in the measurement doc beside the existing sample-size advice — with the
   corollary that a paired-side sweep can report a confident 0–48 that contains one
   observation.

Runner-up, carried from wave 5 and still true: the published CLI binary is
`sandbox/cli-publish/botarena` while the brief, the help text and every doc say
`nilbots` (it self-reports `nilbots 0.9.22`, which does match this brief).
And the provided scaffold still recovers class by parsing a form-ID prefix
(`ArenaBasics.ClassOf`) while the brief forbids it and the contract publishes the
typed field on four surfaces.
