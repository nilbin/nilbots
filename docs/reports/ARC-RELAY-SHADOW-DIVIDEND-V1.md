# Shadow Dividend — Broadloom ladder rung 9

`shadow-dividend-v1` is admitted above all eight prior Broadloom-7 ladder
sheets. It wins every fixed six-game mirrored series: 42 wins and six losses
across 48 admission games. Every replay ended by reactor destruction and
passed movement sanity plus felt-degeneracy eligibility.

This is a fixed-set strategy result, not a population-balance conclusion. It
also does not turn the preceding sheets into nine independent archetypes:
Sixfold, Shrouded, Eclipse, Sevenfold and Rail are successive refinements of
one centre-concentration/Lattice family.

## Doctrine

Shadow Dividend uses eight different classes:

`Kestrel · Veil · Towline · Relay · Mason · Repulsor · Hush · Palisade`.

Its opening is an observable fork rather than an opponent-id counter.

- Four gallery raiders take the outer route toward the enemy Well. Towline
  reaches a plotted one-wall Tractor Hook line; Palisade screens the breach;
  Kestrel and Veil convert the opening. Two bodies pressure centre and two
  hold the own economy.
- During the opening assembly only, a visible hostile in either of two narrow
  enemy-Well guard areas aborts the raid. The sheet then selects the next
  ordinary top-to-bottom Tactic.
- The fallback uses four independently governed roles: two three-body jaws
  hunt the centre Core, Mason holds a lane and repeatedly builds one plotted
  Hardlight Block, and Relay alone converts the own +1 Core cadence.
- Against a thin enemy Well, the direct branch banks two +4 Cores. It rallies
  for the second Well birth rather than allowing the first surviving raider
  to trickle back alone, then finishes with centre/own conversion.

All places and Well filters are authored in mirrored `own`, `centre`, and
`enemy` vocabulary. No sheet id, side, seed, hidden enemy state, or global
north/south alias selects the branch.

## Admission record

Profile: `broadloom-7`. Cells, always in this order:
`w-9001 w-9002 e-9005 e-9009 w-9012 e-9004`.

| Opponent | Series | Per-cell Shadow Dividend outcomes |
|---|---:|---|
| Loomsplit | **4–2** | W338, W338, L261, L261, W338, W215 |
| Loomshears | **6–0** | W292, W309, W270, W318, W257, W265 |
| Crosscut | **6–0** | W269, W260, W292, W320, W317, W273 |
| Sixfold Pincer | **6–0** | W257, W309, W258, W288, W266, W319 |
| Shrouded Lattice | **5–1** | W316, L333, W308, W318, W319, W323 |
| Eclipse Lattice | **6–0** | W316, W251, W316, W318, W318, W320 |
| Sevenfold Eclipse | **5–1** | W354, L363, W356, W319, W350, W319 |
| Rail Shuttle Eclipse | **4–2** | W254, W269, W320, W320, L357, L283 |

The two Rail losses and one loss each to Loomsplit, Shrouded and Sevenfold are
kept as counterplay. Admission requires a winning mirrored series against
every prior sheet, not a sweep of every cell.

## Replay proof of authored behavior

The direct Rail `w-9001` cell is the cleanest enemy-Well proof.

- Gallery readiness releases at t48.
- Towline casts the authored Tractor Hook ray from `(21,5)` at t34, dealing
  its declared damage at t35; it repeats the plotted setup at t155/t156.
- The first enemy +4 banks at t116 and the second at t232.
- An own +1 banks at t254 and destroys the reactor. Shadow wins 3–2 Pulses.

The Eclipse `w-9001` cell proves the guarded-Well fallback.

- The visible guard-area fact aborts the opening and begins Shadow Reversal at
  t33.
- Relay banks five successive own +1 Cores; the jaws bank centre +2 Cores at
  t175 and t260.
- Mason does not chase either Core. It reaches its lane post, casts Hardlight
  Block repeatedly at the one authored tile, and remains within the Hold
  leash while fighting.
- Hush repeatedly uses its plotted Null Field opportunity in the centre lane.
- Shadow wins at t316.

The deterministic conformance reader reports 58 passes and nine deviations
for the direct sample, and 42 passes and three deviations for the fallback.
The direct deviations are attack posts missed by bodies killed, respawning, or
already carrying the scored Core. The three fallback deviations are opening
staging posts deliberately interrupted by the t33 abort. Neither report finds
a phase, branch, custody, Well-filter, bank-attribution, posture, or map-binding
contradiction.

## Mechanics and tooling addressed during this rung

### Authored Hook is now a real tactical tool

The one-wall plotted Tractor Hook path originally failed because authored-ray
eligibility still inherited a line-of-sight gate intended for Smart use. The
authored path now follows the declared line contract, remains physically
interposable, deals damage, pulls a surviving target to the closest legal tile
in front of Towline, and is covered by focused runtime tests. Shadow's direct
branch replay-proves the behavior rather than merely compiling it.

### Opening reads can expire without a permanent escape hatch

The raid needs to reconsider a defended enemy Well during assembly, but must
not abandon a Core already won because a new guard later becomes visible.
Tactic aborts therefore gained an optional `throughPhase` boundary. It is
compiled through the existing top-to-bottom Tactic coordinator; there is no
second scheduler or hidden opponent classification state.

### Core custody keeps the pickup Well's route identity

Enemy-Well return policy must remain stable after the carrier leaves the enemy
zone and after drops/reacquisitions. Materialization now binds the source-Well
route with pickup-phase custody rather than re-evaluating a generic shortest
route every tick. This removed route/direct oscillation without adding a new
carrier state machine.

### A Tactic may have up to eight Groups

The fallback naturally has two jaws, a lane interdictor, and a home runner.
The former three-Group ceiling forced the jaws into one artificial container.
Tactics, named formation readiness, generated Orders custody groups, editor
validation, and the add-Group control now accept one to eight Groups—the squad
size. The editor palette now has eight distinct Group colors. A materializer
proof compiles eight independent one-bot Groups, selects all eight in one
readiness relationship, and emits eight custody cohorts.

## Authoring friction and missing depth

- **Temporal conjunction is brittle.** `groupsSet AND coreSoon` can miss when
  the two facts overlap for only one tick. Shadow needs a second ordinary
  `groupsSet AND held(50)` alternative to prevent a full Well-recharge freeze.
  The desirable player fact is “this Well's Core is in play, loose or carried,”
  not a collection of timing proxies.
- **Signature geometry needs map assistance.** An early Null Field mark landed
  on a wall and correctly failed validation, but raw authoring offered no
  nearby legal suggestion. The map-first editor should suggest valid adjacent
  tiles while preserving the player's exact choice.
- **Core collision can override intent.** `ignore` prevents a bot from choosing
  Core work; it does not make a loose Core intangible. A non-preferred body can
  still physically collide with and acquire it. That is mechanically coherent
  but easy for an author to misread.
- **Conformance treats attack posts literally.** A successful raid can report
  missed posts when the body is killed or takes custody before settling on the
  nominal endpoint. The deviations are useful evidence, but the editor should
  distinguish staging posts from aspirational combat destinations.
- **Four Groups are expressible but visually demanding.** Raising the cap
  removes an arbitrary limitation; it does not make eight-role authoring a good
  default. Recipes/presets should create the common arrangements and leave
  manual splitting as refinement.

## Strategic-depth verdict

Shadow is more adaptive than Rail, but the comparison remains a warning.
Rail is 143 source lines / 5,221 bytes: two phases, a static seven-centre / one-
home allocation, and no opponent-dependent branch. Shadow is 579 lines /
18,279 bytes: two Tactics, six total phases, observed guard geometry, bounded
abort, timed reassembly, two custody routes, plotted Hook, Null Field and
Hardlight opportunities. It beats Rail only 4–2.

That does not mean the extra decisions are fake—the replays prove both branches
and their signatures. It means the game still rewards a robust concentration
script almost as highly as a much more authored counter. The monotonic ladder
also selects toward one generalist family: requiring every successor to beat
every predecessor naturally produces refinements of the dominant shell.

Keep all existing entrants as regression opponents and an evolutionary
archive, but classify them honestly:

- Loomsplit is the training/outer-economy experiment.
- Loomshears and Crosscut form the first split-pressure/counter family.
- Sixfold through Rail are primarily Lattice/centre-concentration variants.
- Shadow is an information-gated enemy-Well raid with a Lattice-like fallback.

Future work should add matchup-matrix evidence alongside the monotonic record.
A healthy meta needs genuinely different families—veterancy economy,
deployment control, mobile interception, enemy-Well siege, and distributed
Core denial—with visible strengths and weaknesses. If one trivial script beats
all of them, that is a balance finding, not a successful ladder.

## Validation and reproducible artifacts

- 48/48 admission replays ended by reactor destruction.
- 48/48 passed replay-visible movement sanity.
- 48/48 passed the Broadloom felt-degeneracy eligibility bars.
- Tactics materializer suite: 95/95 passed.
- Tactic coordinator plus authored Hook tests: 16/16 passed.
- Web suite: 656/656 passed; production build passed.
- Tactics SHA-256:
  `0a314e06c02eac5824e54890c31628f2b1f2c6967ede1315233307bc0695d46d`
- Materialized Orders SHA-256:
  `b54a1b8d3f2c50fd47fd9a1b445ed1f6ee8dbdc80b47f578fd7cc686806a37ae`
- Map-plan SHA-256:
  `0eb719ce0ff780ef528de32a680ff0c26be8a04a10f7afeaff20a01e621fc310`
- Linked package/content hash:
  `31ddde07179e2c4cdde9ca938bb5cce45f3b210dcb130788fc70b9819c3547b5`
- Tactics source:
  `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/shadow-dividend-v1.json`

The outcome-visible ten-replay review selection is available in the public
[Shadow Dividend replay gallery](https://universe-random-rights-hazards.trycloudflare.com).

The sheet is available in the owner's local account database as paired,
non-ranked Tactical Sheet and Arc Relay Entrant
`0f671132-77f9-4edc-9e4b-bfc052215f1d`.
