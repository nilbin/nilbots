# DX notes — march-wall, wave 5 (revision 5)

## Isolation statement

Everything in this revision was authored from the three permitted documents
(author packet, Labs rules card, class addendum — all three hash-verified before
reading, see the identity table), the `templates/botarena-generic-actor/`
scaffold, `src/BotArena.Sdk/` types, my own wave-4 directory and its replays, and
the sandbox CLI. No other entrant's source, standings, replays, aggregate report
or scratch directory was opened, and no directory under
`arena-bots/frontline-labs/classes-wave-5-2026-07-30/` other than my own was
listed or read. The frozen wave-4 predecessor directory was read and left
byte-untouched: all thirteen of its source hashes, its `out/bot.wasm`
(`48e69714fced…`) and its `evidence/t4/qualification.json`
(`48ad91dea1e6…`) were re-verified after this pass against the table in its own
`DX.md` and are unchanged. All working files live in
`sandbox/march-wall-w5-scratch-c19e4d7a/`, a uniquely named private directory
created for this pass. Every match run in this pass had march-wall source on both
sides: the rebuilt wave-4 predecessor, this revision, or one of its own ablation
and candidate variants. Nothing was committed to git; `git status` shows the
wave-5 tree untracked and nothing staged. Nothing to disclose under the packet's
exposure clause.

Two coordinator messages arrived mid-pass and both are reflected here: a
correction that the addendum's "Mobilize back once per life" line describes the
historical arm only (my reading came from the resolved contract and already
matched — see friction 2), and a housekeeping directive to delete sweep output
after extracting numbers (adopted; see the disk-full methodology note, which is
the same incident from the other side).

## Identity

| | |
| --- | --- |
| Entrant | `march-wall` |
| Population / wave | Frontline Labs classes, wave 5 (`classes-wave-5-2026-07-30`) |
| Authoring lineage | `march-wall-v1`, revision 5 |
| Doctrine | A WALL THAT CAN BE PICKED UP IS A WALL THAT CAN ADVANCE (advancing wall, fifth lineage) |
| Class | `bulwark` (declared in `botarena.json`, unchanged since v1) |
| Role | `verdict-doctrine` |
| Target | cumulative T4 (retain) |
| Budget | one strategic revision; mechanical/contract repairs free |
| Predecessor | wave-4 `march-wall`, untouched and re-verified |
| The arm (one, no matrix) | `--classes bulwark-vs-bulwark --movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open` |
| Resolved ruleset | `frontline-labs-1-bulwark-vs-bulwark-sail-open-facing-locked` (the `deck` game; `wane` is inert with no fabricator in the pair and is omitted from the identity automatically) |
| Rules / map / match fingerprints | `77f07162e1615a89b9901c2cc4fc903c0f9edd4f037a44e93054d46ddb74af05` / `61f477904dfaf048093d5fb164f5d580f8b41f5c884eb357446de9b8739d1a3d` / `6d239aa54f890cc33a0340e124ec348e7902abb9c5fcb0c4d363abf67cc1df6f` |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` (unchanged since wave 4) |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` (unchanged since wave 4) |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `3cb2814b7a853d0547038d2d4d65a498c0d82e06357392957caf4efbdd365e5c` (moved this wave; wave 4 read `b91047df…`) |
| Template helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` (byte-identical copy, unchanged since wave 4) |
| CLI | `sandbox/cli-publish`, **nilbots 0.9.21** (SDK 0.10.6, game rules 0.5, runtime protocol 0.1). The brief names 0.9.20; the published sandbox CLI reports 0.9.21 and that is what every number here was produced with. |

## Freeze identity

| | |
| --- | --- |
| Submitted sources | `AnchorPlanner.cs`, `ArenaBasics.cs`, `ContractView.cs`, **`Cycle.cs`**, `FireControl.cs`, `Geometry.cs`, `Lane.cs`, `MarchWall.cs`, `Navigation.cs`, `Pendulum.cs`, `Stance.cs`, `Threat.cs` (5 968 lines) |
| Project metadata | `botarena.json`, `MarchWall.csproj` |
| **`out/bot.wasm` sha256** | **`d4e5e7899aff020fe4a0b7aabb491490efbb231b3fee459e64f2a72237311408`** |
| Canonical WASM | `out/bot.wasm`, 3 447 306 bytes, built by `nilbots build <project> --no-cache`; a second `--no-cache` build reproduced the hash byte-for-byte |
| Deterministic source-tree hash | `04cbc2ed2144fa3672a2dde61c13e82c403c2f7f49d8701af86e5d902ddf41ed` (sha256 over the sorted sha256 list of `*.cs` + `botarena.json` + `*.csproj`, same recipe as v1 through revision 4) |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/guest 0.10.6, WASI p1 core module, platform-matched Docker builder on macOS arm64 |
| Qualification report | `evidence/t4/qualification.json`, sha256 `56dc9f60215b17699045756a5caf2ceb6ec6e6b231113307c8a3813459a0f751` (produced by building and qualifying **from this frozen directory**; see the note on the project name below — a run from the scratch copy produces the same verdict and a different report hash) |
| Verified probe replays | 36 replays under `evidence/t4/`, both team sides, three cumulative tiers |
| Sparring baseline | wave-4 source rebuilt unchanged with SDK 0.10.6 → `5be07b5ff2ac4468b1b780b3686af66025c7a52864ddd40a5912cbdde664803a` (the frozen wave-4 artifact `48e69714…` was never run, per the brief) |

### Per-file source hashes

Five files changed, one is new, and **seven are byte-identical to wave 4** —
including the two that would have had to change if the aim envelope had been
hard-coded anywhere.

| file | sha256 | vs wave 4 |
| --- | --- | --- |
| `AnchorPlanner.cs` | `b0e23e445713378cafa5ac03ea15702f06d35cd5765bdc7ed707cca12161db26` | changed |
| `ArenaBasics.cs` | `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` | identical |
| `ContractView.cs` | `05d113530ae2daad00333f3579a0a5566cc7d8167d78fb19327be86e357c5ba6` | changed |
| `Cycle.cs` | `974d60836f93f52c598e4c88fd08b60ba583310f4412738f7df777010af01542` | **new** |
| `FireControl.cs` | `5497b7c28069d26806cc5e6258e5da52d8f12ac017702a79c9314bf01fe7d87a` | identical |
| `Geometry.cs` | `6b5933c7582df5025cc9b5b3eaafcd58bc58e415007591f50a5dd7e6f25028ea` | changed |
| `Lane.cs` | `03d5f2c92ddc398e8c547d7e3e991a2cc4cd36d0f196d277bcd375dda543f8cd` | identical |
| `MarchWall.cs` | `31405864f82cf320f90904968916ec6de645884efafa940791bf3a20d630cbe5` | changed |
| `Navigation.cs` | `f62c0a15f86bef9caada28071a863fab213ec69ffb6eb787e429cb5462c178dc` | changed |
| `Pendulum.cs` | `be9502f662baee0334e730d503e322ec301609ff1df2d61efedb33297d770868` | identical |
| `Stance.cs` | `05bd646affab0812ab0e71c4095e518606fdcfe6c54b62120b8223ea19ccad25` | changed |
| `Threat.cs` | `d6b7bcd90d193016b353b669983aaa19048719147ac6428f46f00f47b0158695` | identical |
| `botarena.json` | `43d359abe4262852ffdfb64249b255e3ece348bb59cbe297adb04e05bf552ecc` | identical |
| `MarchWall.csproj` | `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573` | identical |

## Qualification outcome

`experiment frontline-labs qualify --suite frontline-qualification-5`, profile
`frontline-duel-depth-union-t4-v1`, WASM runtime, artifact `d4e5e7899aff…`.

**Exit 0 — T4 awarded.** Prerequisite T3 PASS (which re-ran and hash-linked T2).
All five T4 probes PASS: `suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`.
`balanceEvidenceEligible` is **`true`** in the report body and the report's
`artifactHash` equals the frozen artifact hash above. Suite wall time 5.9 s, no
probe repair needed, first attempt. The suite carries no pendulum, no skills, no
bend envelope, no aim offsets and no classes, so passing it is also the check
that this revision's cycle, gate and shell code is genuinely inert on a contract
that declares none of them: `Cycle.Reversible` is asked of routes that do not
exist, `Geometry.IsCorridor` is asked about a map with no gate the planner is
allowed to take, and the swarm veto is asked of a form with no guard.

## Doctrine, in one paragraph

The wall is still the set of straight lanes our guns close, revision 3's price
list still decides what a tick of presence is worth, and revision 4's shield is
still how a lane we are losing shoots back. What revision 5 adds is that
fortification has stopped being a destination: the routes declare
`irreversibleForLife` false in both directions and health maps by ratio with a
floor and no entry heal, so a full-health body cycles for nothing and a wounded
one pays the remainder every trip — which makes the valuable half of the cycle
the half that did not exist, **picking the wall up**. A segment stands up when
the point needs weight and it cannot shoot the point instead, because a turret's
objective weight is zero and on this ruleset presence is not a tactic but the
scoring channel; it refuses to stand up merely to reposition unless the round
trip is free, because the floor turns a wounded repositioning into a slow
suicide. Placement opened at the same time and only one new placement is worth
taking: a one-tile corridor upstream of the point, where bodies block bodies and
a segment is a gate as well as a gun, taken only when our own side can still
reach the objective without it. Everything else the open ground offered is
priced, not seized — most of all the objective itself, where the right occupant
turned out to be the SHELL, which keeps objective weight 1 while it deflects and
so holds the ground it is standing on for the first time in five revisions. And
the shield learned to say no: it is strong against one poke and a trap against a
swarm, so it declines the raise once more bodies can reach our tile than one arc
can answer.

## Measured records against the rebuilt predecessor

Sparring baseline: wave-4 source rebuilt unchanged with the current SDK. Eight
seeds (104729, 130363, 155921, 202961, 224737, 262147, 293459, 350377), both team
sides, WASM runtime: **16 matches per arm**. Territorial progress is summed over
the sixteen matches from revision 5's side.

| arm | flags | ruleset token | record (W-L-D) | territorial | early endings |
| --- | --- | --- | --- | ---: | --- |
| **the open game** | the brief's full flag set | `sail-open` (`deck`) | **16-0-0** | **+189** | 0 |
| one flag away | same, minus `--stance-ground open` | `sail` | 8-7-1 | −46 | 2 |

Per-match territorial on the open game: `14 14 14 14 14 14 14 14 3 8 15 15 3 3
15 15`. Sixteen wins, no losses, no draws, and no cell closer than +3.

**The mirror floor that makes those numbers readable is exactly zero, and it was
measured rather than assumed.** With one artifact on both sides the swapped and
unswapped cells of a seed are the same match relabelled, so reading team 0 from
one and team 1 from the other must sum to zero: the rebuilt predecessor against
itself over the same sixteen cells is **8-8-0, +0**, and this revision against
itself is **3-3-10, +0**. Both confirm the harness has no side bias, and the
second says something about the doctrine — against an opponent that also holds
the point behind a shell, ten of sixteen cells are draws.

The `sail` row is the arm contrast and the honest one. Drop the single
`--stance-ground open` flag and this revision reads a mobilize that is
`irreversibleForLife: true`, an anchor that pays a flat +2 heal, and stance routes
whose `forbiddenTileTags` cover every objective tile and the whole central
corridor — so it keeps revision 4's ladder and lands at parity, 8-7-1 and −46 on
n=16. Its shell rises **20 times and deflects once** there, against 1 818 raises
and 845 deflections on the open arm. That single pair of numbers is what
`--stance-ground open` did: revision 4's headline friction — a shell that can
never stand on the ground it holds — is the whole difference between parity and
16-0-0.

## Attribution: what each decision is worth

Same sixteen cells, same opponent, same arm. Each row is **this exact source with
one decision changed and nothing else**, so the difference is that decision.

| variant | record | territorial | objective body-ticks | worth |
| --- | --- | ---: | ---: | --- |
| **shipped revision 5** | **16-0-0** | **+189** | 6 161 | — |
| swarm decline OFF (veto every shooter, never) | 4-11-1 | −73 | 5 700 | **+12 wins, +262** |
| open placement OFF (wave-4 exclusions restored) | 10-1-5 | +87 | 5 765 | **+6 wins, +102** |
| cycle doctrine OFF (routes read as one-way) | 16-0-0 | +222 | 6 194 | **0 wins, −33** |
| widest-facing posture ON (the candidate I cut) | 10-5-1 | +36 | 5 597 | −6 wins, −153 |
| parked ration relaxation ON (the candidate I cut) | 6-10-0 | +10 | 3 818 | −10 wins, −179 |

The `objective body-ticks` column explains every row in the table, and it is the
one number this wave taught me: **a turret's objective weight is zero, and on
this ruleset objective presence is not a tactic, it is the scoring channel.**
Every variant that spends presence loses in proportion to how much it spends, and
the two candidates I cut spend the most.

**The cycle doctrine is shipped at a measured cost of 33 territorial and zero
wins, and that deserves stating plainly rather than burying.** Its own ablation —
the same source reading both routes as one-way — is 16-0-0 and +222. Against this
particular opponent the cycle fires eight anchors and three mobilizes in sixteen
matches and cannot do better than neutral, because the opponent never leaves the
point and the only thing worth doing on the point is holding it with weight 1. It
is shipped because it is the wave's assigned doctrine, because it is correct on
the contract, and because the same code against *itself* fires 59 anchors and 49
mobilizes — the cycle's trigger is a property of the opponent, and a population
measurement that only ever sees it dormant has not measured it. A reader who
weights this one pairing's margin over doctrine coverage should prefer the
cycle-off artifact; the source differs by three blocks, all named in
`TryMobilize` and `FortifyPermitted`.

## Skill and diagonal usage counts

Sixteen matches on the open game, this revision's side only. The predecessor's
column is given where the comparison is the point.

| counter | revision 5 | rebuilt wave 4 |
| --- | ---: | ---: |
| volleys cast | **0** | 0 |
| shells raised | 1 818 | 1 853 |
| shells dropped by our own decision | 1 803 | 1 817 |
| shells broken by the declared budget | 240 | 235 |
| deflections made | 845 | 841 |
| shell ticks | 5 500 | 5 368 |
| — of those, **standing on the contested objective** | **3 064** | 3 020 |
| anchors completed | 8 | 5 |
| mobilizes completed | **3** | 0 |
| turret ticks | 181 | 152 |
| — of those, standing in a one-tile gate | 72 | 12 |
| — of those, standing on the objective | 0 | 0 |
| diagonal launches (initial aim offset ≠ 0) | **198** | 202 |
| bent launches (bend count > 0) | 260 | 276 |
| slots fielded | **3** | 3 |
| advances completed | 20 | 8 |
| bodies lost | 196 | 210 |

Five of those rows deserve a sentence.

- **Volleys: zero, and structurally so.** The fan is the striker's. The stance
  machinery recognizes a fan by `volley.projectileCount > 1` on the target form's
  attack profile and would cast if handed one; `Stance.VolleyRoute` returns null
  on every arm a bulwark plays, so the cast predicate remains the one piece of
  this lineage no measurement covers, four waves running.
- **Shells broken: 240, where revision 4 measured zero in 118 raises.** The
  forced return is a rule this lineage had never once reached. It reaches it
  constantly now, and for a readable reason: open ground lets the shell stand on
  the point, and a shell on the point is poked from both sides until the third
  deflection shatters it. The break mechanic priced nothing for revision 4 and
  prices a real punish window here.
- **Mobilizes: three.** The verb the wave is about fires twice per hundred
  matches' worth of ticks against this opponent. See the attribution table for why
  that is reported rather than tuned away.
- **Turret-on-objective ticks: zero, by choice and not by rule.** The route would
  allow it — `forbiddenTileTags` is empty — and the planner scores it rather than
  excluding it. It never wins the score, because a turret there trades the tile's
  scoring weight for sightlines the same body already had from beside it.
- **Diagonal launches: 198, from zero lines of new fire-control code.** Discussed
  next.

## The ±45° offsets cost nothing to adopt and one candidate to get wrong

`FireControl.cs` and `Lane.cs` are byte-identical to wave 4. Both read
`shotProgram.minInitialAimSteps` / `maxInitialAimSteps` and the bend bounds out
of the declared attack profile and enumerate whatever is there, so the arm's
change from 0/0 to −1/+1 turned on 198 diagonal launches per sixteen matches
without an edit. The cost ordering was already right too: `Cheaper` sorts by bend
count first, so an **aim-only diagonal — zero bends — is now the cheapest way to
arrive from a bearing an enemy arc does not cover**, and it is preferred over the
bend that used to be the only answer. That is revision 4's "never poke an arc"
getting cheaper for free, and it is the whole practical content of the brief's
"punishes flankers without rotating".

What I added on top of it was wrong, and the failure is the more interesting
half. The reasoning: one facing now commands three launch bearings, so the best
resting pose is no longer the one aimed at a body but the one whose fan covers
the most of the ground the next body has to cross — never owe a rotation to a
flanker. Implemented as `Lane.WidestFacing` plus an approach set, measured over
the full sixteen cells: **10-5-1 and +36 against 16-0-0 and +189**, with 25 more
bodies lost and 564 fewer objective body-ticks. The counters name the mechanism:
the approach set moves every tick, so the widest facing moves with it, and under
`facing-locked` a rotation is also the unlock for a step — a body re-posturing
every tick neither steps nor shoots. A rule that spends a tick to be better
positioned is a rule that has to beat the tick, and on a three-tick cadence with
one-tile movement almost nothing does. The rule is gone; `HoldTheLine` carries
the paragraph.

## The candidate that the whole doctrine was supposed to be

The first draft of this revision read the two contract changes and drew the
obvious conclusion: if the door is two-way and the trip is free, the ration that
protected the team's only scorer is rationing nothing, so a body that anchors has
been *parked* rather than spent — and since the turret gun beats the mobile gun
on both numbers a stationary duel is decided by (cooldown 1 against 3, travel 8
against 6, absolute eight-way aim), the shield should stand down anywhere a
fortification is available. Two clean clauses, both contract reads, both wrong.

**6-10-0 and +10, against 16-0-0 and +189.** The mechanism is in one column:
3 818 objective body-ticks against 6 161. It anchored 85 times, mobilized 63,
spent 2 073 ticks fortified and 440 ticks in gates, fired 301 diagonals, took 55
advances against the shipped artifact's 20 — and lost ten matches, because
advances are not the score at the tick cap and presence is. A reversible door does
not make fortifying cheap. It makes *leaving* cheap. The ration prices the thing
that did not change, which is that a turret cannot capture, and it is shipped
exactly as revision 2 wrote it.

One clause of that draft survived, because it costs no presence at all: a **gate**
denies ground without standing on it, so it is worth a fortification the coverage
rule would refuse and worth the Prime's three-tick windup once relief exists.

## Repairs

- **`IsFortified` was ambiguous and got the right answer by accident.** "No
  movement action in the declared mask" stopped meaning "is a gun emplacement" the
  moment the kit added a second immobile form. From one mobile form the contract
  declares a route to a turret *and* a route to a shell, and both targets are
  immobile, so every route search keyed on that predicate matched either. Revision
  4 got the anchor route because `anchor-bulwark-child` sorts before
  `shell-bulwark-child` in transition-ID ordinal order, which is not a contract
  fact. A guard declares itself; an emplacement is what is left. `MobilizeRoute`
  and `Stance.ReturnRoute` now key on the positive predicate — the target can
  walk — for the same reason.
- **`Navigation.Reachable` gained a hypothetical.** Asking "would putting a body
  here seal my own side out?" needs one walk with a tile treated as a wall. It is
  run once per gate candidate, of which a map has a handful.
- **`Geometry.IsCorridor`** is a shape test with no rules value, no tile tag and
  no coordinate in it. On a map with no pinch it returns false everywhere and the
  gate doctrine silently does not exist.

## Top three frictions

**1. The map still publishes `transition-placement-forbidden`, and on this arm no
route consults it.** Under `--stance-ground open` every same-life route's
`placement.forbiddenTileTags` is `[]` — and the map still carries a 112-tile
`transition-placement-forbidden` tag covering all 22 objective tiles across five
positions, both home pads, and the entire central corridor including the one-tile
chokes at `(8,7)`, `(9,7)`, `(13,7)`, `(14,7)`. Nothing anywhere says the tag has
become vestigial. Any bot that reads the tag as the authority on "where may I
transform?" — which is the natural reading, which is what revision 4's own prose
said in as many words, and which is what a shared helper named after the tag
would do — keeps the entire wave-4 restriction, never discovers the arm, and
cannot tell that anything changed. My planner happened to read the route's list
rather than the tag kind, so it saw the change; a lineage that had cached the tag
would have measured this wave as a null. The cost of getting it wrong is
measurable from my own ablation: **six wins and 102 territorial**. One sentence in
the addendum's stance-ground section — "the tag remains on the map and is no
longer referenced by any route; read `placement.forbiddenTileTags`, never the tag
kind" — closes it. Note that the `--stance-ground free` row already has this
problem in miniature and the `open` row inherits it.

**2. `irreversibleForLife` answers a different question on each leg, and the two
neighbouring arms disagree about which leg.** The field means "can this life
reverse this change", so on the *anchor* route it is about mobilizing and on the
*mobilize* route it is about anchoring again. That is fine once stated, and it is
never stated. What makes it a trap is that one flag apart the two arms put the
`true` on opposite legs: on `sail` the anchor is reversible and the **mobilize is
irreversible** (one round trip per life, the historical rule), and on `open` both
are false. So "is fortifying a posture or a destination?" requires reading BOTH
legs, and an author who reads one gets a confidently wrong answer on exactly one
of the two arms — with no error, because the wrong answer is a decision never
taken rather than a decision rejected. The addendum's class-identity paragraph
said "Mobilize back once per life" for this arm until it was corrected mid-wave; I
had derived the truth from the resolved contract before the correction arrived,
which is the only reason this is a friction and not a lost revision. A route table
that stated the *cycle* property once — "this pair of routes forms an unlimited
cycle / a single round trip / a one-way door" — would remove the inference
entirely.

**3. The health policy is a formula ID, and the only number the doctrine needs is
not published.** `preserve-ratio-floor-minimum-one` with
`floor-current-times-target-maximum-divided-by-source-maximum-then-minimum-one`
is a complete and honest specification, and it is not an answer to any question a
doctrine asks. The question is "what does a round trip cost from here", and
getting it required building the table by hand: with 4 ⇄ 7 a child round-trips
losslessly at 4/4 and at 1/4, and loses exactly one health at 3/4 and at 2/4; with
5 ⇄ 7 the Prime is the same shape. So the real rule — *cycle freely at full
health, cycle once at anything else, and never cycle at all to reposition while
wounded* — is three sentences derived from two policy IDs and two form maxima, and
every author on this arm has to derive them independently. The brief gets closest
("partial health pays the floor each round trip, so cycling at low health grinds
you down") and the contract itself says nothing an author can act on. Worse, the
policy reads like a *heal* if you skim it: anchoring at 4/5 genuinely raises
absolute health to 5/7, and a reader who stops there will believe a wounded
turret repositions for free. Publishing the mapped health on the transition, or
even just naming the fixed points, would retire an arithmetic exercise that has
exactly one right answer.

## Other documentation gaps

- **Revision 4's headline friction is fixed by this arm, and the fix is worth
  recording as a fix.** The shell's advertised property — "objective weight stays
  1, so it still holds ground" — was unreachable on the shipped map because the
  route's tags covered every objective tile. Under `--stance-ground open` it is
  not only reachable, it is the strongest thing this class does: 3 064 of 5 500
  shell-ticks are spent on contested ground, against **zero** one flag away, and
  that difference is the difference between 16-0-0 and 8-7-1. The class card's
  sentence is now true.
- **Revision 4's other balance finding survives the aim offsets.** It measured
  two straight-only shields meeting as a total mutual null — 96 raises a side and
  zero deflections. On `sail`, with the ±45° offsets restored and the tags still
  in place, my sparring produced **1 deflection in 16 matches across 35 raises**
  between two shield-capable doctrines. The offsets did not unfreeze that mirror;
  open placement did, by giving the shell somewhere worth standing.
- **`ObjectivePresence` is still a lower bound, and this is the first revision
  where it matters which form is asking.** A mobile bulwark sees 4 tiles; a turret
  sees 6. The mobilize-for-weight rule keys on that read and is asked *by the
  turret*, which is the better sensor — which is part of why the rule fires at all
  now where revision 4 deleted it as unmeasurable.
- **A ruleset token can be shorter than its arm.** The brief's flag set is `deck`,
  and a bulwark mirror resolves it to `sail-open` because `wane` touches nothing
  without a fabricator in the cell. The addendum says this ("a ground arm is
  inert-omitted where nothing it touches exists") and the CLI prints it, but the
  brief's own freeze instruction says "the crew game" while the flag set is the
  deck one. Every number here is labelled with the resolved ruleset ID rather than
  a token, which is the only labelling that survives an inert flag.

## A methodology trap worth more than a friction

**A full disk produces replays that parse, report plausible standings, and lie.**
Mid-pass another author filled the volume (each match writes ~15 MB of replay plus
a self-contained viewer that embeds it again). `nilbots experiment` exited 0 for
every affected cell. The replays loaded as valid JSON, carried a
`result.standings` block with a winner and territorial scores, and produced a
sweep summary that looked entirely ordinary — and was wrong by **+212
territorial and five wins**, which briefly convinced me that a candidate I was
about to cut was the best thing I had measured. Replay v3 carries `partial: true`
and that is the only tell; the standings block does not distinguish an early
breach from a truncated write. My aggregator now refuses a partial replay by
name, and the whole sweep is re-run rather than patched. Two lessons, one
mechanical and one methodological: a sweep harness should assert `partial ==
false` before it reads a single counter, and a result that reverses a decision
should be reproduced before it is believed. The invalidated sweep was re-run and
both discarded candidates were then re-measured from the shipped source rather
than from the intermediate one, which is where the clean numbers in the
attribution table come from.

The housekeeping half is now standard here: every sweep deletes its `viewer.html`
files immediately and its replay tree as soon as the summary JSON is written. The
scratch directory holds summaries, four scripts, seven source trees and their
artifacts.

### The project's DIRECTORY NAME is an input to the qualification report hash

Same source, same `bot.wasm` hash, same verdict, different report. Qualifying the
identical artifact from `sandbox/…/w5/` and from
`arena-bots/…/classes-wave-5-2026-07-30/march-wall/` produces two
`qualification.json` files whose only semantic difference is
`artifactName: "w5"` against `artifactName: "march-wall"` — and because the name
lands in each probe replay's `header.provenance.participants[].name`, **all
twenty-two probe replay hashes change, the hash-linked T3 prerequisite report
hash changes, and the top-level report hash changes.** The packet asks the freeze
to preserve "qualification JSON, its SHA-256"; that pairing is only meaningful
alongside the directory the run happened in, which no field records. Wave 4
recorded the neighbouring version of this trap — a replay hash is not a
behavioural fingerprint, because provenance embeds the artifact hash — and this is
the same edge through a field that is not even an identity: it is a folder name.
The frozen report here is the one produced *in* the frozen directory, which is the
only convention that makes the recorded hash re-derivable. A `--name` flag, or
omitting the display name from the hashed provenance, would close it.

## Confusing terminology

- **"Mobilize" is now two verbs sharing an action.** The parameterless `mobilize`
  action is the return leg of the turret cycle *and* the return leg of both
  stances, so "the bot mobilized" is ambiguous between "the wall picked itself up"
  and "the shield came down". The transition IDs distinguish them
  (`mobilize-…` against `unstance-…`) and the prose does not.
- **"Open" collides with itself.** `--stance-ground open` (every placement is
  legal), an open tile (not a wall), and "the open game" (the composite arm) all
  appear in the same paragraph of my own notes. The arm name is the one that could
  have been anything.
- **"Objective weight"** remains the most doctrine-relevant number with the least
  descriptive name — a franchise, not a multiplier — and this is the wave where
  that bit hardest: every wrong decision in the attribution table above is a
  decision that misread what weight zero costs.
- **"Cycle" now means three things**: the anchor/mobilize round trip, the gun's
  cooldown ("riding the cadence"), and revision 4's `HoldTheShield` drop rule,
  which is named the cycle rule. All three appear in `MarchWall.cs`.
- The `frontline-qualification-N` / `TN` off-by-one is still jarring, five waves
  in.

## Timings (Apple Silicon, warm)

- managed edit/compile loop: ~0.5 s.
- `build --no-cache` through the Docker builder: 8.5–9.5 s across eleven builds
  (one predecessor rebuild, four candidate iterations, four ablations, two cut
  candidates).
- one 500-tick WASM match through the CLI: ~4 s.
- one 16-match sweep: ~32 s (two concurrent runners; 18 cores saturate at two).
- **about 310 WASM matches** across fourteen 16-cell sweeps plus smoke and
  contract-dump probes. One sweep was invalidated by the full disk and re-run;
  three early sweeps were superseded when the doctrine changed and are not cited.
- `qualify --suite frontline-qualification-5`: **5.9 s wall** for 17 probe
  variants from both sides across three cumulative tiers.
- parsing one 16-cell sweep for counters: ~12 s.

## Strategy passes

One, as budgeted: spend the cycle and the open ground. Shipped — the mobilize-for-
weight rule with its floor discipline and its "shoot the point instead" clause,
the gate placement with its self-seal check, the priced objective placement, and
the swarm decline. Cut after measurement — the parked ration relaxation with its
shield-stands-down clause (−10 wins), and the widest-facing resting posture
(−6 wins). Everything else in the diff is the `IsFortified` repair, the
hypothetical-reachability helper, the corridor shape test, and `Cycle.cs`, which
is a contract reader with no policy of its own.
