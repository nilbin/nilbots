# DX notes — march-wall, wave 6 (revision 6): the coordination pass

## Isolation statement

Everything in this revision was authored from the three permitted documents
(author packet, Labs rules card, class addendum — all three hash-verified before
reading, see the identity table), the `templates/botarena-generic-actor/`
scaffold, `src/BotArena.Sdk/` types, my own wave-5 directory and its replays, and
the sandbox CLI. No other entrant's source, standings, replays, aggregate report
or scratch directory was opened. **No directory under
`arena-bots/frontline-labs/classes-wave-6-2026-07-30/` other than my own was
listed or read, and no wave-5 directory other than my own was listed or read** —
the wave-5 brief named my predecessor's path explicitly and that is the only path
under it I touched. The frozen wave-5 predecessor was read and left byte-
untouched: all fourteen of its source hashes, its `out/bot.wasm`
(`d4e5e7899aff…`) and its `evidence/t4/qualification.json` (`56dc9f602151…`) were
re-verified after this pass against the table in its own `DX.md` and are
unchanged. All working files live in
`sandbox/march-wall-w6-scratch-7b2e9f41/`, a uniquely named private directory
created for this pass. Every match run in this pass had march-wall source on both
sides: the rebuilt wave-5 predecessor, this revision, or one of its own ablation
variants. Nothing was committed to git; `git status` shows only the untracked
wave-6 directory and nothing staged. Nothing to disclose under the packet's
exposure clause.

One coordinator message arrived mid-pass — a freeze-integrity warning that
`nilbots build` globs every `.cs` under the project directory, so an archived
variant source anywhere inside the freeze tree makes the frozen tree fail to
rebuild. Adopted: every ablation variant lived in `sandbox/…/abl-<Rule>/` and was
deleted after its sweep, the frozen tree contains exactly the thirteen submitted
`.cs` files, and the last action of this pass was an extra `--no-cache` build
**from the frozen tree** that reproduced the shipped hash. See
[Freeze verification](#freeze-verification).

## Identity

| | |
| --- | --- |
| Entrant | `march-wall` |
| Population / wave | Frontline Labs classes, wave 6 (`classes-wave-6-2026-07-30`) |
| Authoring lineage | `march-wall-v1`, revision 6 |
| Doctrine | A WALL THAT MARCHES IN ORDER IS A WALL THAT ARRIVES (advancing wall, sixth lineage) |
| Class | `bulwark` (declared in `botarena.json`, unchanged since v1) |
| Role | `verdict-doctrine` |
| Target | cumulative T4 (retain) |
| Budget | one IQ pass on multi-body coordination; mechanical/contract repairs free |
| Predecessor | wave-5 `march-wall`, untouched and re-verified |
| The arm (one, no matrix) | `--classes bulwark-vs-bulwark --movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open` |
| Resolved ruleset | `frontline-labs-1-bulwark-vs-bulwark-sail-open-facing-locked` (the `deck` game; `wane` is inert-omitted with no fabricator in the pair) |
| Rules / map / match fingerprints | `77f07162e1615a89b9901c2cc4fc903c0f9edd4f037a44e93054d46ddb74af05` / `61f477904dfaf048093d5fb164f5d580f8b41f5c884eb357446de9b8739d1a3d` / `6d239aa54f890cc33a0340e124ec348e7902abb9c5fcb0c4d363abf67cc1df6f` — **all three identical to wave 5. The game is unchanged and that was verified rather than assumed.** |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` (unchanged since wave 4) |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` (unchanged since wave 4) |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `2333bd3c9f412e4e9439779ef3d5f2ca6bc8abae6f00973daf54f7e4c892de50` (**moved again this wave**; wave 5 read `3cb2814b…`, wave 4 read `b91047df…`) |
| Template helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` (byte-identical copy, unchanged since wave 4) |
| CLI | `sandbox/cli-publish`, **nilbots 0.9.22** (SDK 0.10.6, game rules 0.5, runtime protocol 0.1), invoked as the `botarena` executable — see friction 3 |

## Freeze identity

| | |
| --- | --- |
| Submitted sources | `AnchorPlanner.cs`, `ArenaBasics.cs`, **`Column.cs`**, `ContractView.cs`, `Cycle.cs`, `FireControl.cs`, `Geometry.cs`, `Lane.cs`, `MarchWall.cs`, `Navigation.cs`, `Pendulum.cs`, `Stance.cs`, `Threat.cs` (7 017 lines) |
| Project metadata | `botarena.json`, `MarchWall.csproj` |
| **`out/bot.wasm` sha256** | **`fa364da95eef50bdbd7cc4d008ee20a296fbdde8b678bc16b82754081dc03d2b`** |
| Canonical WASM | `out/bot.wasm`, 3 543 767 bytes, built by `nilbots build <project> --no-cache` **from the frozen directory**; a second `--no-cache` build from the frozen directory reproduced the hash byte-for-byte |
| Deterministic source-tree hash | `cb758d0177e446ac946e35ab69b3fb56938ee6b0380f57a3ca88f20bf8e9ad50` (sha256 over the sorted sha256 list of `*.cs` + `botarena.json` + `*.csproj`, same recipe as v1 through revision 5) |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/guest 0.10.6, WASI p1 core module, platform-matched Docker builder on macOS arm64 |
| Qualification report | `evidence/t4/qualification.json`, sha256 `b5834f02d945396bb766652eda1293515e759d0be4e285e404fc8b44d513a626` (produced by qualifying **from this frozen directory** — the wave-5 convention, for the reason in [that note](#the-projects-directory-name-is-still-an-input-to-the-report-hash)) |
| Verified probe replays | 36 replays under `evidence/t4/`, both team sides, three cumulative tiers; every one asserted `partial == false` |
| Sparring baseline | wave-5 source rebuilt unchanged with the current SDK → `e7504b8b6b21b7efcf7e291aea41c6063dd5e804ff63cefcd2b51adaa57f07eb` (the frozen wave-5 artifact `d4e5e789…` was never run, per the brief — and could not be reproduced under 0.9.22 anyway; see friction 3) |

### Per-file source hashes

**Four files changed, one is new, and ten are byte-identical to wave 5** — which is
the shape an IQ pass on coordination should have. The whole doctrine ladder,
firing geometry, threat model, cycle economics and stance layer are untouched.

| file | sha256 | vs wave 5 |
| --- | --- | --- |
| `AnchorPlanner.cs` | `a121a6b0959e085ce6daeb343430d7bf5e6e99ec5ee79b633790feefca5d4d50` | changed |
| `ArenaBasics.cs` | `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` | identical |
| `Column.cs` | `6682a546f763bceff29543e3ba33ffaf1283ee2568fc0f1ca490e64738a6af59` | **new** |
| `ContractView.cs` | `a13af131fdaa094d75cdcf86993034be861819a8620f1cb0dc60165c910fb0e9` | changed |
| `Cycle.cs` | `974d60836f93f52c598e4c88fd08b60ba583310f4412738f7df777010af01542` | identical |
| `FireControl.cs` | `5497b7c28069d26806cc5e6258e5da52d8f12ac017702a79c9314bf01fe7d87a` | identical |
| `Geometry.cs` | `6b5933c7582df5025cc9b5b3eaafcd58bc58e415007591f50a5dd7e6f25028ea` | identical |
| `Lane.cs` | `03d5f2c92ddc398e8c547d7e3e991a2cc4cd36d0f196d277bcd375dda543f8cd` | identical |
| `MarchWall.cs` | `46da236b3b0db11b4b2087890087aded76db31852dcd293a9a75df05b17fa41e` | changed |
| `Navigation.cs` | `f997762be0d27160ae51309c9354bb35a441b09251e39b7203e24bcefaf2c357` | changed |
| `Pendulum.cs` | `be9502f662baee0334e730d503e322ec301609ff1df2d61efedb33297d770868` | identical |
| `Stance.cs` | `05bd646affab0812ab0e71c4095e518606fdcfe6c54b62120b8223ea19ccad25` | identical |
| `Threat.cs` | `d6b7bcd90d193016b353b669983aaa19048719147ac6428f46f00f47b0158695` | identical |
| `botarena.json` | `43d359abe4262852ffdfb64249b255e3ece348bb59cbe297adb04e05bf552ecc` | identical |
| `MarchWall.csproj` | `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573` | identical |

## Qualification outcome

`experiment frontline-labs qualify --suite frontline-qualification-5`, profile
`frontline-duel-depth-union-t4-v1`, WASM runtime, artifact `fa364da95eef…`.

**Exit 0 — T4 awarded.** Prerequisite T3 PASS (which re-ran and hash-linked T2).
All five T4 probes PASS: `suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`.
`balanceEvidenceEligible` is **`true`** in the report body and the report's
`artifactHash` equals the frozen artifact hash above. Suite wall time 7.8 s, no
probe repair needed, first attempt, and reproduced on a second run.

The suite carries no pendulum, no skills, no bend envelope, no aim offsets, no
classes and — the relevant one this wave — **often only one body of ours on the
board**, so passing it is also the check that the whole march order is inert where
there is nobody to march with: `Column.Siblings` is empty, `Denied` is empty,
`InTheWay` is false, and every clause in `Column.Rules` gates itself off through
the declared collision policy rather than through a form or class name. See
[the coordination grade nobody awards](#the-report-has-a-coordination-grade-field-and-nothing-fills-it)
for what the suite does *not* check.

## Doctrine delta, in one paragraph

**The wall doctrine is unchanged and this is the pass that let it happen.**
Revision 5's four decisions still decide most ticks — mobilize to advance,
fortify anywhere with the presence ration unrelaxed, decline the shell to a swarm,
and keep the shield on the point — and not one of their predicates moved. What
moved is that the wall stopped being three bodies that each believed it was alone.
The resolved contract declares `actorsBlockActors`, `sameDestinationMovesBlockAll`,
`swapMovesBlocked` and — the field that turns a column into a queue of refused
decisions — **`followingVacatedActorAllowed: false`**, so when a leading segment
steps out of a corridor tile the follower's step into that tile is rejected
anyway. Revision 5 submitted that rejected step forty times per sixteen matches
and, worse, put a *turret* in a pinch on purpose without ever asking whether the
segment behind it still had a route: the gate rule tested only that the MAP still
connected our side to the point, which a six-tick detour satisfies. Revision 6
adds one file with no tactics in it — a march order, re-derived every tick from
the frozen shared observation because there is no channel to negotiate over, so
every life computes the same total precedence over the same bodies and agrees
about who yields without being told. Five clauses fall out of it, each measured
alone: choke precedence, route yield with one written exemption for the team's
sole scorer, gate discipline, rally traffic, and spacing. The wall now arrives,
and the doctrine that was already right about presence turns out to have been
right only because it could never breach: with the gate no longer sealing our own
advance the artifact breaches in **20 of 32 cells** where revision 5 breached in
**0**, and takes fewer objective ticks per tick while winning far more.

## Measured records against the rebuilt predecessor

Sparring baseline: wave-5 source rebuilt unchanged with the current SDK. **Two
independent eight-seed sets, both team sides, WASM runtime: 32 matches.** Set 1 is
wave 5's own seeds (104729, 130363, 155921, 202961, 224737, 262147, 293459,
350377); set 2 was added because one rule's verdict came down to a single cell and
guessing was not acceptable (15485863, 32452843, 49979687, 67867967, 86028121,
104395301, 122949829, 141650939).

| arm | seed set | record (W-L-D) | territorial | breaches |
| --- | --- | --- | ---: | ---: |
| **revision 6** | set 1 | **16-0-0** | **+388** | 11 |
| **revision 6** | set 2 | **13-1-2** | **+312** | 9 |
| **revision 6** | **both** | **29-1-2** | **+700** | **20** |
| the same source, coordination off | set 1 | 3-3-10 | +0 | 0 |
| the same source, coordination off | set 2 | 2-2-12 | +0 | 0 |

Per-match territorial, revision 6, set 1: `12 16 11 30 4 30 30 30 30 30 30 30 15
30 30 30`. Set 2: `30 30 30 30 11 30 30 30 −17 0 30 12 16 20 0 30`. The single
loss is one cell of set 2 at −17.

**Two floors make those numbers readable and both were measured rather than
assumed.**

1. **The control is the predecessor, exactly.** This exact source with all five
   clauses switched off is not "approximately wave 5" — it is byte-identical in
   behaviour. On a shared seed the two artifacts produce **1 642 actor turns with
   zero divergences** in accepted action, arguments and outcome. Its record
   against the rebuilt predecessor is therefore a self-mirror and reads exactly as
   one: **3-3-10 and +0** on set 1, **2-2-12 and +0** on set 2. Every number in
   the attribution table below is a difference from that control.
2. **The shipped artifact has no side bias.** Revision 6 against itself over the
   same thirty-two cells is **8-8-0 and +0**, on both seed sets independently.
   That matters more than usual here, because the precedence order is derived from
   the team's own advance bearing and a mistake in that derivation would hand one
   side the tie-break in every contested corridor. It also says something about
   the doctrine: revision 5's self-mirror was 3-3-10 with **ten draws of sixteen**,
   and revision 6's decides every single cell.

## Attribution: what each coordination rule is worth

Same thirty-two cells, same opponent, same arm. Each row is **this exact source
with one clause switched off and nothing else**, so the difference is that clause.
The switches live in `Column.Rules` as properties for exactly this reason — an
ablation is a one-line edit that cannot drift.

| variant | set 1 | set 2 | combined | worth (Δ wins, Δ losses, Δ terr) | self-blocks |
| --- | --- | --- | --- | --- | ---: |
| **shipped (C2–C6)** | **16-0-0 +388** | **13-1-2 +312** | **29-1-2 +700** | — | **0** |
| C2 choke precedence OFF | 14-2-0 +318 | 10-5-1 +177 | 24-7-1 +495 | **+5, −6, +205** | 4 |
| C3 route yield OFF | 9-6-1 +110 | 14-1-1 +282 | 23-7-2 +392 | **+6, −6, +308** | 0 |
| C4 gate discipline OFF | 10-1-5 +126 | 14-1-1 +185 | 24-2-6 +311 | **+5, −1, +389** | 0 |
| C5 rally traffic OFF | 15-0-1 +410 | 13-2-1 +332 | 28-2-2 +742 | **+1, −1, −42** | 0 |
| C6 spacing OFF | 10-6-0 +63 | 7-9-0 −10 | 17-15-0 +53 | **+12, −14, +647** | 0 |
| all five OFF (= revision 5) | 3-3-10 +0 | 2-2-12 +0 | 5-5-22 +0 | **+24, −4, +700** | 80 |

Four of the five rules are worth five or more wins and every one of them is
positive on both seed sets independently. Three rows deserve a paragraph.

**C6 spacing is the largest and I did not expect it to be.** It was written as the
weak rule — asked last, only for a body with nothing better to do, and only onto a
tile of *equal* standing value, because every positional preference this lineage
has invented before lost (revision 5's widest-facing posture cost six wins). It is
worth twelve wins, and the mechanism is this class's own rather than the striker's
fan it was drafted for: **poking a guarded arc relaunches our own bolt from the
shell's tile back down the lane it went out on.** In a bulwark mirror both sides
field shells, so the tile behind our shooter is a queue for our own fire, and the
rule that keeps two bodies out of one enemy's simultaneous coverage is mostly
keeping them out of each other's returns. Turning it off raises refused moves
from 153 to 227 across the same thirty-two cells — bodies stack on the objective
and on each other's lanes, and are chewed by returns nothing in the observation
announces, because a bolt that does not exist yet cannot be seen coming.

**C4 gate discipline is the one the owner actually watched, and its counter is
binary.** With the rule, this artifact breaches the enemy base in 20 of 32 cells.
Without it, in **0 of 32** — not fewer, none. The wave-5 gate placement was
correct about the enemy (a segment in a pinch turns a four-tile approach into a
fifteen-tile one) and never asked the same question about us; its self-seal test
walked the MAP from our deployment anchor to the objective, and a detour passes
that test while costing the segment behind us six ticks it did not have. Replacing
"can our side still reach the point?" with "does a sibling's actual route still
exist?" is the whole edit, and it converts a doctrine that wins on territorial
into one that breaches.

**C5 rally traffic is marginal, is shipped, and the honest reason is a second seed
set.** On set 1 alone it read +1 win and −22 territorial, which is one cell and
therefore nothing. Rather than pick a side, I ran a second independent eight-seed
set: it reads +0 wins, −1 loss and −20 territorial there. The direction on record
is the same on both sets and record is the primary ranking, so it ships — but it
is the one rule in this table I would not defend from a single sweep, and it
removes no owner-visible silliness at all (waits beside a sibling move 725 → 735).
It costs no presence, which is why a wash is enough to keep it.

### The clause that measured as nothing, and is not shipped as a clause

"An immobile sibling — an emplacement, a raised shield, or any body inside a
declared transition windup — is a WALL to the pathfinder, not a transient blocker
to be optimistically routed through." It is the most obviously correct rule in the
set and it is worth **exactly zero**: with choke precedence on, the ablation is
byte-identical to the shipped artifact (16-0-0, +388, every counter equal),
because denying every occupied tile already denies every pinned one. Measured in
isolation, with choke precedence also off, it is **two wins and 53 territorial
WORSE** than not having it — a longer detour around a turret loses to one blocked
tick. So it is not shipped as its own clause. It survives as the *floor* of choke
precedence, for the arm this brief does not run: where a ruleset declares
`followingVacatedActorAllowed: true` the blanket occupied-denial is wrong, a queue
simply flows, and the only bodies that still need excluding are the ones that will
not move at all.

## The coordination counters

Thirty-two cells, this artifact's side only, against the control (which is the
predecessor). These are the owner-visible numbers.

| counter | revision 6 | control (= revision 5) |
| --- | ---: | ---: |
| **moves refused because one of our own bodies was on the destination** | **0** | **80** |
| **steps aimed at a tile a sibling occupied** (refused or not) | **0** | **80** |
| **two of our bodies aiming at the same tile** (all block, by rule) | **0** | **6** |
| ticks spent waiting beside a sibling | 1 535 | 3 332 |
| — as a share of all our ticks | **7.0 %** | 12.6 % |
| refused moves, all causes | 153 | 150 |
| transforms started | 1 454 | 3 943 |
| mobilizes completed | 986 | 3 251 |
| turret ticks | **1 441** | 417 |
| turret shots fired | **472** | 51 |
| ticks standing in a one-tile corridor | 3 540 | 4 148 |
| objective body-ticks | 7 322 | 12 026 |
| — per tick of ours | **0.333** | 0.456 |
| breaches (early endings) | **20** | 0 |
| total ticks played | 22 003 | 26 387 |

Five of those rows are the report.

- **The three silliness counters are zero, not merely lower.** A step onto a
  sibling's tile is now structurally unreachable: the tile is denied on both
  passes of the route search, so the decision is never formed rather than being
  formed and rejected. Same-destination collisions go with it, because the
  precedence order allocates a corridor run to one body.
- **Refused moves barely moved (150 → 153) while self-inflicted ones went to
  zero.** That is the single most useful diagnostic in this table, and it says the
  wall did not become timid: it is pushing into contested ground exactly as hard,
  and the blocks it still eats are enemies, bolts and reserved pads.
- **Transforms and mobilizes both fell by about two thirds while turret ticks
  tripled and turret fire went up nine-fold.** Revision 5 was a flap machine and
  I could not see it: it anchored, discovered it was now an obstacle, mobilized,
  re-planned, anchored again — 3 943 transforms bought 417 turret ticks. Revision 6
  spends 1 454 transforms and gets 1 441 ticks of turret, because a site that
  respects a sibling's route stays valid. The wall finally gets to *be* a wall.
- **Objective presence per tick went DOWN, and revision 6 wins anyway.** Wave 5's
  headline finding was "a turret's objective weight is zero, and on this ruleset
  presence is not a tactic, it is the scoring channel." That was true — of a
  doctrine that could never breach. Once the wall arrives, three advances end the
  match, and the breach is worth more than any amount of presence. The finding was
  not wrong; it was **conditional on the defect this pass removed**, and no
  measurement inside wave 5 could have told the difference.
- **Corridor ticks went down (4 148 → 3 540) and gate value went up.** Fewer
  bodies stand in pinches, and the ones that do are there on purpose.

## The bug that nearly invalidated the whole attribution table

**`ArenaBasics.OrderedDirections` draws from `context.Random`, so counting how
many times you call it is a gameplay input.** The scaffold helper returns a
mirror-fair cardinal order by shuffling the two lateral directions off the
per-life deterministic stream — a good rule, documented in the helper, and the
reason this lineage has used it since v1. What is not documented is the
consequence: the function is **not pure**. Every call advances the stream, so
every later draw in the match shifts.

My first `Column.Read` consulted it once per tick to get a tie-break order for
route prediction. With every coordination clause switched off — a configuration
that touches no decision anywhere — the "control" artifact diverged from the
predecessor. One decision in 1 642, a rotation that went north instead of south at
tick 438, which then decided otherwise-drawn cells and moved the control's record
from the true mirror floor (3-3-10, +0) to 3-7-6 and −15. I nearly wrote that
number down as "the refactor costs fifteen territorial".

It is also a correctness bug in the layer itself, and the more interesting half.
**The stream is per LIFE.** A life asking that helper for the order gets its own
shuffle, not its sibling's — so predicting a sibling's route with it predicts
against the wrong tie-break on every tie. The entire march order rests on every
body deriving the same answer from the same observation, and a per-life coin flip
breaks exactly that.

The fix is one method and it made the layer strictly better: `Column.MarchOrder`
derives the order from the contract alone — our own advance bearing, then the
lateral 90° clockwise of it, then the other lateral, then the retreat. It is
life-independent, which is what makes siblings agree, and still mirror-fair,
because the two teams' forward bearings are opposites and therefore so are their
clockwise laterals. Nothing in `Column` touches `context.Random`, so the
coordination layer can look at the board without changing it. With that in, the
all-clauses-off control is byte-identical to the predecessor over 1 642 turns and
the attribution table means what it says.

Two lessons worth more than the bug. **A coordination layer must be a pure
observer**, or its own diagnostics perturb what they measure. And **an "inert"
configuration is a testable claim, not a comment** — the diff test that caught
this (dump both artifacts' accepted actions on one seed and compare turn by turn)
took ten minutes to write and is the only reason six numbers in this report are
attributable to anything.

## Repairs

- **`ContractView` now publishes the two collision facts the march order rests
  on** — `ActorsBlockActors` and `FollowingVacatedActorAllowed` — and every
  exclusion is gated on them. A ruleset where allied bodies do not block gets no
  march order at all, and one that allows the follow gets only the corridor
  allocation. The wave-5 code asserted "bodies block bodies" in three doc comments
  and read it from nowhere.
- **`Navigation.RouteTiles`** is a shortest-route *path* rather than a first step,
  using the same walk and tie-break order the step search uses, which is what
  makes it usable as a prediction of a sibling's route rather than only as a plan
  for our own.
- **`Navigation.CorridorRun`** walks the indivisible pinch a tile belongs to. A run
  is the unit a march order allocates, because two bodies inside one run heading
  the same way are a jam no tie-break fixes.
- **`AnchorPlanner` excludes two kinds of tile the map does not exclude**: one a
  sibling's route needs, and one whose corridor run a sibling still has to walk.
- **`TryMobilize` gained the door clause.** An already-anchored turret that has
  walled in its own advance picks itself up, at any health, because the
  alternative is a wall that cannot advance. This is the only place in the pass
  where a coordination rule spends health.

## Top three frictions

**1. The report schema has a `coordinationGradeAwarded` field and nothing on the
tier ladder ever fills it.** `qualification.json` from suite 5 carries
`"coordinationGradeAwarded": null` at the top level *and* inside the hash-linked
T3 prerequisite, so the field is not a suite-5 omission — it is unpopulated across
the cumulative ladder. That is a precise statement of this wave's whole problem:
**the entire owner-visible defect class is unmeasured by every mechanical gate an
entrant passes through.** My predecessor passed T4 first-attempt with zero probe
repairs while submitting eighty refused steps per thirty-two matches into its own
bodies, flapping 3 943 transforms to buy 417 turret ticks, and sealing its own
advance so thoroughly that it breached in zero cells of thirty-two. Nothing in the
suite noticed, because the T4 probes are single-body geometry tests —
`suppression-choke` names a choke and is about suppression versus concession, not
about two bodies wanting the same corridor tile. A probe that put two of a
participant's own bodies on one side of a pinch and asserted that both arrive
would have caught all of it, and the field to report it in already exists. Until
one does, "qualified" and "coordinates" are unrelated properties, and an owner
watching replays is the only instrument that measures the second.

**2. `ArenaBasics.OrderedDirections` consumes `context.Random`, and neither its
name, its signature nor its documentation says so.** It is spelled
`OrderedDirections(contract, context)` and returns a `Direction[]`; it reads as a
projection of the contract. Its doc comment explains *why* the laterals are
shuffled (a measured 40-of-40 side sweep) and never states the two consequences
that bite: the result is **per-life**, so it cannot be used to reason about an
ally; and the call is **not idempotent**, so how many times your code path happens
to reach it is a gameplay input. The cost of learning that the hard way is
recorded above — a control artifact that was supposed to be inert diverging by one
decision in 1 642 and shifting six attribution numbers. Two fixes, either
sufficient: hand the caller the draw explicitly (`OrderedDirections(contract,
context, out bool flipped)` or an overload taking a seed), or say in the doc
comment "this advances `context.Random`; call it at most once per tick and never
about another body." A helper that silently spends the deterministic stream is the
one kind of impurity a deterministic sandbox cannot make visible, because every
run reproduces perfectly and only the *comparison between two builds* is wrong.

**3. The CLI is `nilbots` everywhere in the documentation and `botarena` on disk,
and `--viewer` exists but is not in `--help`.** Every brief, the author packet,
the rules card and every `nilbots build`/`nilbots experiment` example name a
binary that does not exist at `sandbox/cli-publish/`; the executable there is
`botarena`, which prints `nilbots 0.9.22` when asked its version. Five waves in,
that is a fifteen-second papercut every time and a real one the first time. In the
same area: the brief for this wave says experiment runs no longer write
`viewer.html` by default and to "pass `--viewer` or `--open` if you need one", and
`--viewer` **works** while the command's own usage block lists only `--open` — so
the documented flag is discoverable from a brief and not from the tool. Two small
notes on the same surface: a symlink or a renamed executable closes the first, and
adding `[--viewer]` beside `[--open]` closes the second. Related, and worth a
sentence because it is not a friction so much as a trap: `qualify` still writes 36
self-contained viewers alongside its 36 probe replays — **192 MB of viewer against
20 MB of evidence**. The packet asks a freeze to preserve "every verified probe
replay" and says nothing about viewers, so this freeze keeps the replays and drops
the viewers, taking the archived tree from 213 MB to 24 MB. If that is wrong, the
packet should say so; if it is right, `qualify` should take the same `--viewer`
opt-in the experiment runs now take.

## Other documentation gaps

- **`followingVacatedActorAllowed` is the most consequential field in the
  collision block and the rules card states it as prose in a list of nine.** "Same-
  destination moves all block, swaps block, following a vacated actor blocks,
  and projectiles block movement" is complete and correct, and it buries the one
  clause with a structural consequence: **a column cannot advance in lockstep.**
  Every other clause in that sentence describes a collision you can see coming; this
  one describes a tile that is empty on your observation, empty in the post-state,
  and refuses you anyway. One added sentence — "so a body cannot follow an ally out
  of a corridor on the same tick; a queue advances at one tile per two ticks" —
  turns a rule authors rediscover by watching refused moves into a rule they design
  around.
- **Team perception publishes an ally's `previousActionResolution` and
  `pendingSameLifeTransition`, and no document mentions what they are for.** They
  are the entire substrate for coordination without a channel: the last accepted
  action is the only evidence of a sibling's *committed direction* that exists, and
  a published transition windup is the difference between "a body that is in my way"
  and "a body that will be in my way for three more ticks and may only Wait". The
  rules card's perception section lists what is shared; it does not say that
  `StartLife.Origin` and these two fields are how the packet's own instruction —
  "use observations and declared team perception for coordination" — is actually
  carried out.
- **"Allied actors also block movement" is stated once, in the collision
  paragraph, and its doctrinal consequence is stated nowhere.** It is the same fact
  that makes a segment in a pinch a physical gate (wave 5's strongest placement)
  and makes that segment a wall across our own advance (this wave's most expensive
  bug). One field, two doctrines, opposite signs — and an author who reads it as a
  pathfinding detail gets the first and not the second.
- **Wave 5's headline finding needs a footnote, and it can only be written from
  outside wave 5.** "Objective presence is not a tactic, it is the scoring channel"
  was measured over sixteen cells in which *neither side ever breached*, because
  both were walling themselves in. Revision 6 takes fewer objective ticks per tick
  and wins nearly everything, in twenty of thirty-two cells by breach. A population
  measurement taken while every entrant shares a defect measures the defect.

## Methodology

The wave-5 disk-full trap is now permanent policy here and it paid for itself
again: `counters.py` refuses a replay whose `partial` is not exactly `false`
before it reads a single counter, and it asserts it on qualification evidence too
(36 replays, 0 partial). Every sweep deletes its replay tree the moment its
summary JSON is written; the scratch directory ended this pass at 9.3 MB holding
27 summaries, three scripts, two source trees and a lint project.

Two additions this wave.

- **The mirror floor is an assertion, not a convention.** `counters.py` refuses to
  read a self-mirror without an explicit team hint, because reading team 0 from
  both cells of a seed fabricates a bias silently and the floor is the whole point
  of running it. Both self-mirrors in this report (`+0` for the predecessor, `+0`
  and 8-8-0 for the shipped artifact, on both seed sets) came out of that check.
- **Our side is identified by artifact hash, never by display name.** A mirror
  carries the same name on both sides, and the name is a folder rather than an
  identity — see below.

### The project's DIRECTORY NAME is still an input to the report hash

Unchanged from wave 5 and worth restating because it decided this freeze's
convention: qualifying the identical artifact from `sandbox/…/w6/` and from the
frozen directory produces two `qualification.json` files whose only semantic
difference is `artifactName`, and because that name lands in every probe replay's
`header.provenance.participants[].name`, all thirty-six replay hashes, the
hash-linked T3 report hash and the top-level report hash change. The frozen report
here is the one produced *in* the frozen directory, which is the only convention
that makes the recorded hash re-derivable. A `--name` flag, or omitting the display
name from the hashed provenance, would close it. The build cache key has the same
property and the artifact hash does not: the frozen tree and the scratch tree
compiled to different cache keys and the same `fa364da95eef…`.

### The wave-5 artifact cannot be reproduced under 0.9.22

Rebuilding the untouched wave-5 source with the current CLI gives
`e7504b8b6b21…` where the frozen artifact is `d4e5e7899aff…`. That is expected —
the build cache key covers the staged SDK/Guest DLL bytes, and 0.9.21 → 0.9.22
moved them — and it is the reason the brief's "rebuild anything you spar against
from source" is the only workable rule. It is also worth writing down for whoever
reads a frozen `bot.wasm` hash later and expects to reproduce it: **a frozen
artifact hash is reproducible only under the CLI that produced it**, and nothing
in the freeze records which that was except this table.

### Freeze verification

Per the coordinator's mid-pass warning, and as the last action of this pass: no
`.cs` file exists anywhere under the frozen tree except the thirteen submitted
sources at the project root (every ablation variant lived in
`sandbox/march-wall-w6-scratch-7b2e9f41/abl-<Rule>/` and was deleted after its
sweep), and an extra `nilbots build --no-cache` run **from the frozen directory**
reproduced `fa364da95eef50bdbd7cc4d008ee20a296fbdde8b678bc16b82754081dc03d2b`
byte-for-byte.

## Confusing terminology

- **"Choke" now means three unrelated things in this arm's vocabulary.** The T4
  probe `suppression-choke` (a scenario about suppression versus concession), a
  one-tile corridor on the map (the shape this pass allocates), and the verb for a
  jam of one's own bodies. The probe name is the one that could have been anything,
  and the collision matters: an author reading the suite manifest reasonably expects
  `suppression-choke` to be the coordination probe, and it is not.
- **"Order" collides with itself, badly, in exactly the file this pass added.**
  `Navigation.Order` is a *direction preference*; `Column`'s march **order** is a
  *precedence over bodies*; `MarchOrders` in `MarchWall.cs` is the *march step*.
  All three appear within twenty lines of each other and I renamed nothing, because
  two of the three names are wave-5's and a rename would have made the diff
  unreadable. Noted as a real cost of keeping the diff honest.
- **"Blocked" is an action outcome and a routing exclusion and a physical wall.**
  `GenericActorActionResolution.ActionOutcome.Blocked`, the `blocked` set a BFS
  treats as walls, and `_blockedUntilTick` (a memory of the first, used as the
  second) are three different things spelled the same way in one method.
- **"Objective weight"** remains the most doctrine-relevant number with the least
  descriptive name, and this is the wave where its *ranking* bit: weight is the
  scoring channel right up until a breach is reachable, and nothing names the
  threshold.
- The `frontline-qualification-N` / `TN` off-by-one is still jarring, six waves in.

## Timings (Apple Silicon, warm)

- managed lint compile of all thirteen sources: ~2–4 s (needs a separate
  `Compile Include` project — the submitted `.csproj`'s `ProjectReference` is
  relative to the freeze depth and does not resolve from a scratch copy).
- `build --no-cache` through the Docker builder: 8.7–12.6 s across **24 builds**
  (one predecessor rebuild, five candidate iterations, sixteen ablation variants,
  the frozen tree, and the frozen-tree verification).
- one 500-tick WASM match through the CLI: ~4 s.
- one 16-match sweep: ~37–40 s (two concurrent runners).
- **about 510 WASM sweep matches** across 32 sixteen-cell sweeps, plus 4
  diagnostic matches and two qualification suites. Ten early sweeps were
  invalidated and re-run from a corrected base — one by a mirror-accounting bug in
  my own aggregator, nine by the `OrderedDirections` finding — and are not cited.
- `qualify --suite frontline-qualification-5`: **7.8 s wall** for 17 probe variants
  from both sides across three cumulative tiers.
- parsing one 16-cell sweep for counters: ~10 s.
- the turn-by-turn divergence diff that caught the random-stream bug: ~1 s per
  match pair, and the highest-value second of compute in the pass.

## Strategy passes

One, as briefed: an IQ pass on multi-body coordination, with the wall doctrine
held fixed. Shipped — choke precedence, route yield with the sole-scorer
exemption, gate discipline, rally traffic, spacing, and the turret door clause.
Cut after measurement — the immobile-sibling-as-wall clause (exactly zero under
choke precedence, two wins worse in isolation), retained only as that clause's
floor for an arm this brief does not run. Everything else in the diff is the
contract reads for the two collision facts, the route-path and corridor-run
helpers, and `Column.cs`, which contains a march order and no tactics.

No revision-5 decision was changed, added to or removed. The four decisions in its
`README.md` are the four decisions in this one.
