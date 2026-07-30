# DX — VectorEdge revision 4 (wave 4, class striker)

**Lineage** vector-edge-v1 · **Revision** 4 · **Role** verdict-doctrine ·
**Target** T4 (`frontline-qualification-5`) · **Budget** one strategic revision,
mechanical and contract repairs free.

## Isolation statement

I read only the permitted material: the author packet
(`docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aa…`), the
rule card (`docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e…`), the classes
addendum (`docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `b91047df…`, read in
full), the `templates/botarena-generic-actor/` scaffold, `src/BotArena.Sdk/`
types and their XML documentation, my own frozen revision-3 source, my own
directories and replays, and the sandbox CLI at `sandbox/cli-publish/`. All three
addendum hashes were verified against the brief before reading.

I did not open any other entrant's source, replays, standings, or aggregate
balance reports, nor Engine/App implementation, nor any cohort directory other
than my own lineage's. My lineage's frozen predecessors
(`classes-wave-1-2026-07-29/`, `-revision-2-`, `-revision-3-`) were read but not
written; the revision-3 sparring baseline was **copied out** into private scratch
and rebuilt there specifically so that building it could not touch the frozen
directory.

Private scratch: `sandbox/vector-edge-w4-scratch-7f3a/` — a uniquely named
directory, not a shared or guessable path. Nothing was written outside that
directory and my own output directory. **No accidental exposure to another
entrant's material occurred.**

Everything I sparred against is my own source: the rebuilt revision 3, and four
variants of my own revision-4 source built solely to isolate one behaviour each
(see *Ablations*). Two of those variants declare a different `class` in
`botarena.json` so that the shell and five-slot contracts could be exercised at
all — a striker mirror resolves neither. They are diagnostic fixtures, not
population revisions, and their records are reported as capability checks rather
than as standings.

## Freeze identity

| Field | Value |
| --- | --- |
| Output directory | `arena-bots/frontline-labs/classes-wave-4-2026-07-30/vector-edge/` |
| Class (declared in `botarena.json`) | `striker` |
| `bot.wasm` sha256 | `16ab20f1785936f3537b6db0629f50b2250c0c372805401f83a8d5fbc031488d` |
| Build | `sandbox/cli-publish/nilbots build <project> --no-cache` |
| Build-cache key | `8baaa3819792899b989e22b2b6c483fd66e9ad8e2a54dabc020741bd5027330c` |
| CLI / SDK / rules | nilbots 0.9.15 · SDK+Guest 0.10.6 · game rules 0.5 |
| Compiler | NativeAOT-LLVM 10.0.0-rc.1.26306.1 (platform-matched Docker builder) |
| Qualification suite | `frontline-qualification-5` (`frontline-duel-depth-union-t4-v1`) |
| Qualification exit code | **0** |
| Tier awarded | **T4** · `balanceEvidenceEligible: true` · `profileComplete: true` |
| Probes | prerequisite T3, suppression-choke, entry-initiative, prediction-chamber, front-rotation, map-holdout — all PASS |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| Evidence | `evidence/t4/qualification.json` + every probe replay and viewer |
| Per-file source hashes | `sha256s.txt` |
| Sparring baseline | revision 3 rebuilt from source, `bee30744d0701a3e88cf46b41a701adfff607a95ccd89ab445aaaebfa50a0c8c` |
| Git | nothing committed |

Doc-comment-only edits after the measurement run did not change the artifact:
the final `--no-cache` build reproduced `16ab20f1…` byte for byte, which is the
hash the qualification report names.

## Doctrine, in one paragraph

Ground is the only score, so every tick either takes a tile, holds a tile, or
removes the body standing on it, and every weapon is priced against the tick it
displaces. Revision 4 keeps that ledger and sharpens its two biggest inputs from
the contract. First, what a target can *reach* is now the contract's own
legality rather than a ball of tiles with a stickiness fudge on top: under
`facing-locked` a body may only step where it faces and must spend a whole tick
turning to do anything else, so the solver searches pose — a tile and a facing,
a tick that is either a rotation or a step along it — which re-prices every
straight bolt and every bend on the phase-2 board and is where this revision's
measured gain comes from. Second, the class-skill kit is read by shape and
priced, not adopted: a form declaring `projectileGuard` deflects whatever arrives
inside its fixed facing quadrant and cannot rotate while raised, so a bolt into
that arc is scored as dead with the returned bolt taxed and the bend that curls
to a flank wins instead; a fan is read from the stance route's own windup,
budget, spread and cooldown and compared against the whole window of ordinary
fire those ticks would have bought, and because that arithmetic says a
three-ray fan cannot pay for four ticks and a doubled cooldown against a single
body, this revision declines almost every cast — a conclusion I measured three
ways rather than assumed. Both specials punish a shared bearing, so bodies
envelop from different bearings instead of stacking into a lane, step out of a
fan that can launch this tick because movement resolves before combat, and never
concede an objective tile to do either. The live advance hold is asked for
(`holdOwnerTeamId`, `holdEndsAtTick`) instead of reconstructed, and every other
fact — slot counts, unlock ticks, stance routes and budgets, fan width, guard
arcs, capture policy, decay clock, arrival placement, spawn reservations,
per-projectile cadence and damage — comes from `StartLife.Contract`, the frozen
observation, or the per-tick legality mask, so the same source plays all four
arm combinations and the base contracts without branching on any of them by
name.

## Measured records — revision 4 vs rebuilt revision 3

All matches: `striker-vs-striker`, `--movement facing-locked`, WASM runtime,
14 seeds (42, 104729, 7, 1337, 20260730, 99, 3, 5, 11, 23, 8191, 65537, 2026,
314159), both sides (`side a` = candidate on team 0, `side b` = `--swap`).
112 matches per variant. `prog` is mean signed territorial progress for the
candidate; `brW/brL` counts wins and losses decided by base breach.

| cell | spelled | side | n | W-L-D | brW/brL | prog | casts | fans fired | faults |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `keel` | `--pendulum keel` | a | 14 | 6-6-2 | 0/0 | −0.1 | 0 | 0 | 0 |
| `keel` | | b | 14 | 11-3-0 | 11/0 | +20.4 | 0 | 0 | 0 |
| `keel` | | **both** | 28 | **17-9-2** | 11/0 | **+10.1** | 0 | 0 | 0 |
| `helm` | `--pendulum keel --skills kit` | a | 14 | 6-6-2 | 0/0 | −0.1 | 0 | 0 | 0 |
| `helm` | | b | 14 | 11-3-0 | 11/0 | +20.4 | 0 | 0 | 0 |
| `helm` | | **both** | 28 | **17-9-2** | 11/0 | **+10.1** | 0 | 0 | 0 |
| `veer` | `--pendulum keel --bend universal` | a | 14 | 6-6-2 | 0/0 | −0.1 | 0 | 0 | 0 |
| `veer` | | b | 14 | 11-3-0 | 11/0 | +20.4 | 0 | 0 | 0 |
| `veer` | | **both** | 28 | **17-9-2** | 11/0 | **+10.1** | 0 | 0 | 0 |
| `rig` | `--pendulum keel --skills kit --bend universal` | a | 14 | 6-6-2 | 0/0 | −0.1 | 0 | 0 | 0 |
| `rig` | | b | 14 | 11-3-0 | 11/0 | +20.4 | 0 | 0 | 0 |
| `rig` | | **both** | 28 | **17-9-2** | 11/0 | **+10.1** | 0 | 0 | 0 |
| **all** | | | **112** | **68-36-8** | **44/0** | **+10.1** | **0** | **0** | **0** |

Two things in that table need saying plainly rather than being left to look like
a copy-paste error.

**The four cells are two rulesets.** On a striker mirror, `keel` and `veer` are
byte-identical contracts, and so are `helm` and `rig`. I diffed the resolved
contracts embedded in the replays: the only differing fields are `rulesetId` and
`matchContractFingerprint`, and the **`rulesFingerprint` is the same value in
both** — the ruleset ID is an alias outside the content hash. This is correct and
follows from the addendum: `--bend universal` upgrades bulwark and fabricator
from `shoot-straight` to a 1–2 tile bend, and the striker already carries the
deepest envelope (1–4) under `striker-only`, so in a striker mirror the bend
factor changes nothing at all. Identical `keel`/`veer` and `helm`/`rig` rows are
therefore the right answer, not a harness fault. The bend factor is only
measurable for my class against a *different* class.

**The two kit cells match the two kit-off cells because this bot casts nothing.**
That is a measured decision, evidenced below, not an unimplemented feature.

**Side asymmetry is real and is not mine.** Side a is 6-6-2 / −0.1 and side b is
11-3-0 / +20.4 across 14 seeds each. It is not a directional bias in my code:
revision 4 against *itself* is a 0-progress draw on both sides at every seed I
tried, and so is revision 3 against itself. The two sides of an asymmetric
matchup are simply different games here, because per-life deterministic streams
differ by team and my mirror-fair tie-break consumes them. I report both sides
and the pool; the pooled figure is the one I would stand behind.

## Ablations — how I know the fan is not worth casting

Every variant below is my own revision-4 source with exactly one thing changed,
built through the same controlled toolchain, and run over the same 112 matches.

| variant | artifact | W-L-D | brW/brL | mean prog | casts | fans fired |
| --- | --- | --- | --- | --- | --- | --- |
| **shipped** (ground- and health-gated cast) | `16ab20f1…` | **68-36-8** | 44/**0** | **+10.14** | 0 | 0 |
| cast compiled out entirely | `000df561…` | 68-36-8 | 44/0 | +10.14 | 0 | 0 |
| permissive gate (no health, no ground test) | `5d0cb6c3…` | 66-38-8 | 44/**8** | +8.77 | 100 | 90 |
| permissive + health gate only | `6a689a5b…` | 66-38-8 | 44/**8** | +8.77 | 34 | 34 |
| shipped gate, value bar effectively removed | `b5489dc9…` | 68-36-8 | 44/0 | +10.14 | 0 | 0 |

Read down that table: allowing casts costs two wins and 1.3 territorial points,
and **every breach loss in the entire wave belongs to a variant that casts.**
Refusing to cast at 1 HP removed two thirds of the casts and changed the record
by nothing, so recklessness was not the problem — tempo was. Removing the value
bar changed nothing either, which says the binding constraint was never the
expected-value comparison but the ground the ticks displaced.

The arithmetic behind it is all contract data. `striker-volley` declares
`cooldownTicks: 5` against `striker-bolt`'s `2`; entry is a 2-tick wait-only
windup; the return counter is `attacks-issued-since-entering-source-form` at
threshold 1 with a 1-tick exit. So one cast costs four immobile ticks and five
ticks of cadence and delivers three bolts that can take at most **one** damage
off any single body. The same window buys two mobile bolts and two steps. What
the fan uniquely sells is bearings — its rays diverge from tile one, covering the
diagonals a striker's gun can never open on, because `striker-bolt`'s shot
program declares `minInitialAimSteps: 0, maxInitialAimSteps: 0` and its earliest
bend is a tile out, which makes a diagonally adjacent body literally unhittable.
One body is not enough bearings to pay for that. The shipped gate therefore
permits a cast only where the ticks were genuinely free — from a seat whose
stance weight holds the tile, on a tick with no step worth taking, or against a
body that cannot move at all — and on this board those conditions and a fan worth
casting never co-occur.

**I would expect this verdict to flip against multiple clumped bodies**, because
fan coverage sums across targets while a single bolt's cannot, and against the
five-slot fabricator specifically. I could not measure that without another
entrant, and I did not.

## Capability checks against the other two skills

A striker mirror resolves neither the shell nor the five slots, so I built two
fixtures from my own source with a different declared class.

**Shell / deflection geometry** — fixture: my own source, class `bulwark`, with a
three-line patch that raises the aegis shell whenever the route is legal and a
contact is visible (a fixture, not a doctrine). `bulwark-vs-striker` + `rig` +
`facing-locked`, 5 seeds, my striker on team 1:

| build | deflections it fed the shell | bent shots | straight shots |
| --- | --- | --- | --- |
| revision 3 | **9** | 161 (26%) | 449 |
| revision 4 | **0** | 249 (**42%**) | 345 |

Revision 3 put nine bolts into a raised arc across five matches — each one
absorbed and returned at it under the bulwark's ownership. Revision 4 put in
none, and shifted markedly toward bent trajectories, which is the arc being
flanked rather than fed. The score line in that probe is meaningless (the fixture
is a crippled bot that raises a shield and sits in it), so I am reporting the
deflection count, which is the thing the code is supposed to control.

**Five slots** — fixture: my own source, class `fabricator` (which fabricates on
every legal tick), `fabricator-vs-striker` + `rig` + `facing-locked`, 4 seeds.
Both revision 3 and revision 4 lost 0-4-0 by breach at −30. **Zero runtime
faults** on either side, the asymmetric `…-asymmetric-slots-5-3-v1` topology and
its 60/180/300/420 unlock ticks were read without incident, and my slot-count
reader took both sides' counts from `topology.unitSlots` rather than assuming
three. I am not drawing a balance conclusion from a fixture built out of my own
source; the check here is that the contract is handled and the revision did not
regress against a numerically superior opponent.

**Zero runtime faults across every match in this wave** (112 × 5 matrix runs,
plus 10 shell probes, 8 slot probes, 4 self-mirrors, and the whole qualification
suite).

## Repairs and contract reads made this pass

- **Hold inference replaced by the published fields.** `ArenaBasics.LiveHold`
  now supplies the owner and end tick; revision 3's derivation (redeploy clock
  run backwards for the start, signed front displacement for the owner) survives
  only as a fallback for a contract that declares `ratchetHoldTicks` and
  publishes no live hold while inside the redeploy pause — a window in which a
  hold longer than the pause must be running, so a null there is an absent field
  rather than an absent hold. `FrontLedger` is demoted to that fallback.
- **Per-projectile cadence and damage.** The threat sweep used the *minimum*
  `ticksPerAdvance` across the whole attack catalog as a global constant. It now
  reads `ticksPerAdvance` and `damagePerHit` off each bolt, so a fan bolt, an
  ordinary bolt and a returned bolt are timed and priced separately as the
  contract intends. (On this cell every profile happens to declare cadence 1, so
  this is a correctness repair with no measurable effect here.)
- **Observed spawn reservations.** Reserved tiles were derived statically from
  lifecycle assignments plus spawn anchors. The observation's per-tile
  `spawnReservation` is now merged in, which additionally covers queued
  fabrication and replication outputs that no static derivation reaches.
- **Deflected bolts as hostile.** Verified rather than assumed: the threat map
  keys purely on `OwnerTeamId != my team`, so a returned bolt is dodged like any
  other. The probe above confirms the dodge path sees them.
- **`ClassId` read, never parsed.** `Skills` recognizes stances and guards from
  `sameLifeTransitions`, `automaticReturn`, `volley` and `projectileGuard`; no
  form ID is ever split on `-` to recover a class, and the scaffold's
  prefix-parsing `ClassOf` helper is deliberately unused.
- **Ambiguous action lookup fixed.** The kit adds a second same-life-transition
  action (`mobilize`, code 104, parameterless) beside `transform` (101,
  form-target). My revision-3 helper picked the lowest action code within a
  kind, which stops being unambiguous the moment two exist. Stance routes are now
  resolved by the `actionId` the route itself declares, and the emplacement
  search scans available actions for one that actually takes a form target.
- **Emplacement cannot be a stance.** The fortify search additionally excludes
  any target form whose return route carries a budget, so a cast can never be
  mistaken for a lasting commitment.

## Frictions — top 3

1. **The CLI is not named what every document calls it.** The brief, the rule
   card and the addendum all invoke `sandbox/cli-publish/nilbots`; the file on
   disk is `sandbox/cli-publish/botarena`, and it prints `nilbots 0.9.15` when
   asked for its version. There is no `nilbots` name or symlink in that
   directory. Every command in the permitted material has to be silently
   translated, and the first thing I did in this wave was fail a command that
   the brief states verbatim. A symlink in `cli-publish/` would end this
   permanently.

2. **Nothing warns you that half the phase-2 factorial is a no-op for your
   class, and the ruleset ID actively suggests otherwise.** `keel` and `veer`
   are byte-identical contracts on a striker mirror, as are `helm` and `rig` —
   same `rulesFingerprint`, different `rulesetId`. Two of my four assigned cells
   were therefore duplicate runs, which I only established by diffing resolved
   contracts out of replay headers. The addendum does say `universal` gives
   bulwark and fabricator 1–2 tiles while the striker keeps 1–4, from which the
   consequence follows, but it is never stated, and the four distinct registered
   tokens read as four distinct games. Either the ID minting or the addendum's
   phase-2 table should say "on a same-class cell the bend factor may resolve to
   the identity"; better still, the CLI could print `bend envelope: universal
   (no effect on this class pair)` where it already prints a bend-envelope line.

3. **A stance's true cost is spread across five contract fields and one of them
   is easy to miss.** Pricing a cast honestly needs the entry route's
   `windup.durationTicks`, the target form's attack `cooldownTicks`, the return
   route's `automaticReturn.threshold`, the return route's own
   `windup.durationTicks`, and `combatState.cooldownContinuity` on both legs —
   because the stance gun's cooldown is what the body carries back out. The
   addendum's prose ("windup 2 into an immobile stance… firing returns you")
   makes the cast sound like a three-tick affair; it is a four-tick commitment
   with a five-tick tail, and that difference is the entire verdict. A worked
   cost line in the skills table — "one cast occupies N ticks and leaves M ticks
   of cadence" — would have saved me two full measurement cycles.

### Smaller notes

- **Documentation gap:** the addendum states the shell "deflects enemy bolts
  arriving inside its facing quadrant" but never defines the quadrant
  geometrically, and `projectileGuard`'s enum documentation says "approaching
  from inside the life's facing quadrant" without saying whether the test is on
  the bolt's heading or on the tile it arrives from. I implemented it as "the
  approach tile lies in the quadrant", reusing the same predicate the vision
  cone uses (the only quadrant definition the contract offers). It behaves
  correctly against the fixture, but I am matching an unstated convention.
- **Hardcoding temptations resisted:** three unit slots (read
  `topology.unitSlots` per team); unlock ticks 120/260 (read lifecycle
  assignments); fan width 3 (derived from `volley.projectileCount` plus the
  `symmetric-adjacent-heading-fan` spread policy); stance windups 2 and 1 (read
  per route); the shield's break at 3 (read `automaticReturn.threshold`); one
  damage per bolt (read `damagePerHit`, per projectile and per profile);
  cooldowns 2 and 5 (read per attack profile).
- **Confusing terminology:** "range" remains three unrelated numbers, as the
  rule card warns. Newly confusing in this arm: `automaticReturn` on a route
  means *the engine will fire this route for you*, while
  `automaticReturnPlacement` in `lifecycle` means *where a destroyed body
  reappears*, and `automaticReturnFormId` in a lifecycle profile means *what
  form it reappears in*. Three different mechanisms sharing a prefix; I had to
  read all three definitions to be sure `automaticReturn` on a stance route was
  not about respawning.
- **Timings:** cold `--no-cache` WASM build ≈ 9 s (platform-matched Docker
  builder on macOS); warm cache hit ≈ 0.06 s; full `frontline-qualification-5`
  including the hash-linked T3 prerequisite ≈ 5.6 s; one 500-tick WASM match
  ≈ 3 s, so a 112-match matrix is about 5 minutes. Iteration cost was never the
  bottleneck — deciding what to measure was.
- **Strategy passes:** one. The reach model, the guard geometry, the fan pricing
  and the bearing logic are one idea applied in four places (price a decision
  against the tick it displaces, using contract legality rather than a prior),
  and the fan's verdict is the same idea returning a negative answer.
