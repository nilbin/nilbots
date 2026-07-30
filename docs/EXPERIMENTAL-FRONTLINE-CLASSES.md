# Frontline classes experiment (local-only)

Status: pre-registered candidate arms (DECISIONS #153/#154). Nothing here is
hosted, ranked, or balanced; the values below are hypotheses for the
class-pair factorial.

Each team plays one **class**: a chassis with its own stats and exactly one
exclusive verb family. Classes never change movement speed, projectile
speed, or damage. Both teams keep the same map family, objective rules,
scoring, and — outside the five-slot skill arm — three-slot topology.
Everything below is readable from the resolved contract at match start — form stats, allowed actions, transition
routes, unlock ticks, and both teams' form IDs — so a well-written bot
recognizes the opposing class and adapts instead of hard-coding.

## The slate

| | striker | bulwark | fabricator |
| --- | --- | --- | --- |
| **Exclusive verb** | one private bend per shot (`shoot`) | reversible Anchor, prime included (`transform`/`mobilize`) | explicit forward fabrication (`fabricate`) |
| Prime / child health | 3 / 3 | 5 / 4 | 2 / 3 |
| Mobile vision | facing quadrant, range 6 | omnidirectional, range 4 | facing quadrant, range 6 |
| Fire cooldown | 2 | 3 | 2 |
| Projectile range | 8 | 6 | 7 |
| Other fire | — | straight only (`shoot-straight`), or one bend after 1–2 tiles under `--bend universal` | straight only (`shoot-straight`), or one bend after 1–2 tiles under `--bend universal` |
| Turret forms | — | HP 7; **its own gun: travel 8, cooldown 1, eight headings** (not the mobile gun); windup **3** (prime) / **1** (child) | — |
| Companions | automatic at 120 / 260, auto-rebuild 30 | automatic at 120 / 260, auto-rebuild 30 | **explicit** at 60 / 180, Ready again after 15 |

Shared by every class: one tile of movement per tick, projectile speed two
with damage one, and Prime respawn after 18 ticks. Split does not exist in
any class arm.

## Class identities

- **Striker** duels through hidden trajectory commitments: straight or one
  private 45° bend after 1–4 tiles, longest gun on the fastest cadence.
- **Bulwark** fortifies: any of its bodies may Anchor into a tough
  omnidirectional turret and Mobilize back. Whether that is once per life
  (the historical rule) or an unlimited cycle is an ARM fact — read
  `irreversibleForLife` on the mobilize routes; under `--stance-ground
  open` it is false and health maps proportionally (see the open-game
  section). The prime's three-tick windup is a visible, punishable
  commitment — opponents see the route, the windup, and the
  reversibility in the contract before tick 0.
- **Fabricator** is the only class that fabricates, and its fabrication is a
  real decision: the child materializes beside the prime in the field (never
  on a protected pad), earlier and rebuilt faster than anyone else's — but
  every queue costs a combat action, and a Fabricator that never queues gets
  no companions at all. Lowest floor, highest ceiling.

## The turret bargain, stated plainly

A turret has **objective weight zero**: fortifying removes that body from
every capture and contest count. A bulwark that anchors before relief
exists has deleted its only scoring presence — this single fact is most of
the class's strategic depth, and both mirror stalemates and thrown games
trace back to ignoring it.

## Qualification runs a different contract than your class

The cumulative suites qualify your WASM on the **duel-depth union
profile**, not your class arm: it carries verbs your chassis may lack
(`fabricate` on the pad, shot programs) and lacks your class routes.
A class-armed bot must stay contract-driven to pass — read the action
catalog, routes, and legality masks; never assume your class's shape.

## Reading the class from the contract

- At `StartLife`, read `start.Contract.Topology.Teams[].ClassId` or the
  participant's `ClassId`; both sides' declared classes are explicit and must
  agree. A classless contract reports `null`.
- Each tick, `context.Self.ClassId` gives your class directly.
  `context.Participants[].ClassId` keeps both sides public, and visible allied
  or enemy bodies carry their own `ClassId`. Do not parse a `FormId` prefix to
  recover any of these facts.
- Prefer conditioning on the enemy's *stats and routes* (health, cooldown,
  anchor routes, fabrication routes) over its name — stat-based counters
  generalize to classes that do not exist yet.
- If your forms allow `shoot`, you have the one-bend envelope; if they allow
  `shoot-straight`, the action takes no payload and fires along your facing.
  **Read the envelope, do not assume it** — `shotProgram.maxBendAfterTiles`
  differs by class in a universal-bend arm (see below), and a program outside
  the declared bounds is rejected.
- Companion timing comes from your slots' lifecycle assignments — do not
  hard-code 120/260.
- Movement-coupling arms declare `facingCoupling` on your form's movement
  profile (`face-movement-direction` or `facing-locked`). The field is
  **absent** on the baseline — canonical contracts omit the default — so
  read it as "missing means preserve-facing", and never plan routes
  against the movement legality mask under `facing-locked`: the mask
  offers only your current facing each tick; plan on map geometry and
  spend rotations explicitly. The scaffold's
  `TryAdvanceToActiveObjective` does this — it searches all cardinals
  and emits the unlocking rotation when the mask refuses its step.

## Running matches

```bash
nilbots experiment frontline-labs \
  --bot <generic-spec> --opponent <generic-spec> \
  --classes bulwark-vs-striker [--duel-map thin-fronts] \
  --seed 42 --runtime wasm --out /tmp/classes
```

Pairs are canonical in alphabetical order (`bulwark-vs-fabricator`,
`bulwark-vs-striker`, `fabricator-vs-striker`, and the three mirrors). Team 0
always plays the first class; use `--swap` to mirror bot assignments.
`--print-candidate-contract` emits the exact resolved identity for a spec.

Your bot has **one class, chosen at creation**: declare it in
`botarena.json` (`"class": "striker"`). Two class-declaring projects need no
`--classes` flag at all — the arm resolves from the manifests and each bot
is bound to its class's canonical team side automatically. A declared class
must agree with any explicit `--classes`.

## Pendulum arms compose with your class

`--pendulum` adds one pre-registered structural counterweight to the
mean-reverting frontline (DECISIONS #158) on top of any class pair,
`--movement` arm, and `--duel-map`. Nothing about your class changes; what
changes is how territory is won and kept, and all of it is readable from the
resolved contract:

| token | what moves in the contract | what it means at the objective |
| --- | --- | --- |
| `control` (default) | nothing | today's measured baseline |
| `sticky-frontline` | `capture.redeployPolicy` + `capture.ratchetHoldTicks` | a completed advance holds for 40 ticks; an enemy capture inside the hold clears its own claim and moves nothing |
| `forward-rally` | `lifecycle.automaticReturnPlacement` | respawns and companion arrivals appear on your own side of the active objective, not at home — on the rear-most free tile of that region measured along your own advance direction, so both sides arrive at mirrored distances |
| `contest-majority` | `capture.controlPolicy` | surplus objective weight scales capture pressure, so one body no longer nulls two |
| `enemy-sole-decay` | `capture.decayClock` | empty and contested ticks stop eroding progress; only an enemy standing alone on the objective does |
| `ratchet` | sticky-frontline + forward-rally | the registered structural level |
| `ratchet-contest` | ratchet + contest-majority | the registered structural level with contest cost |
| `keel` | ratchet-contest + enemy-sole-decay | every counterweight at once — the registered phase-1b level, and the phase-2 baseline |

Any other ablation is spelled with comma-separated single-factor tokens.
A level is identified by *what it composes*, never by how you typed it:
`--pendulum keel` and
`--pendulum sticky-frontline,forward-rally,contest-majority,enemy-sole-decay`
are the same ruleset with the same fingerprint, and it is the short
registered token that appears in the ID either way — the spelled-out form
does not fit the 64-character canonical budget beside a class pair. Keel
composed with the skill kit or the bend envelope has registered identities of
its own for the same reason — see
[Phase-2 cells have registered identities](#phase-2-cells-have-registered-identities).

`--capture-threshold` and `--prime-respawn-ticks` are the numbers-only
level and compose the same way. None of these arms changes the action
catalog or any class stat, so a contract-driven bot needs no re-authoring:
read `gameMode.capture` and `lifecycle` from MatchStart if you want to
adapt, and expect a respawn to put you near the fight under
`forward-rally`. The arrival tile is derived from the objective
chain and the placing team's own advance direction — never from the team ID
— so the two sides' arrivals are exact reflections of each other. An older
`automaticReturnPlacement` value names the same forward rally ordered by
absolute map order instead; it is historical, still resolvable for archived
replays, and selected by no arm.

```bash
nilbots experiment frontline-labs \
  --bot <generic-spec> --opponent <generic-spec> \
  --classes bulwark-vs-striker --pendulum ratchet-contest \
  --seed 42 --runtime wasm --out /tmp/pendulum
```

### The live hold is published; do not infer it

A sticky frontline changes what standing on the objective is WORTH, and it
changes it differently for the two sides: inside a live hold the owner's
presence buys ground and the opponent's presence buys nothing, because a
capture completed inside another team's hold is **spent** — the claim resets
exactly as a successful capture does and the front does not move. A doctrine
that cannot tell which side of a hold it is on pays a full capture window for
a reset, over and over.

So the Frontline mode observation publishes the hold, beside the
`controlResumesAtTick` clock that was already there:

| field | meaning |
| --- | --- |
| `holdOwnerTeamId` | the team whose completed advance is currently protected, or **null** |
| `holdEndsAtTick` | the first tick on which that protection lifts, or **null** |

- **Null means no hold binds on this tick** — including every ruleset whose
  `capture.redeployPolicy` has no ratchet at all, where both fields are null
  for the whole match. The two always travel together: an owner without a
  clock, or a clock without an owner, is a malformed observation.
- **`holdEndsAtTick` reads exactly like `controlResumesAtTick`.** It names the
  tick the restriction lifts, so the hold binds while `context.Tick` is
  strictly below it, and `holdEndsAtTick - context.Tick` is the ticks
  remaining. It never appears in the past: a published hold is a binding one.
- **The lapse is an ordinary control change.** The tick a hold expires
  publishes a `mode-changed` fact like any other, carrying the post-change
  state, so you can react to the window closing without watching a number
  stop.
- **`capture.ratchetHoldTicks` is the DURATION, the observation is the
  CLOCK.** The contract field stays inert-omitted when the ruleset declares no
  hold; read it to price a push in advance, and read the observation to know
  what is running now.

This replaces a derivation that was expensive and partly wrong, and it is
worth knowing what it replaces if you are porting a bot. The hold's *start*
was recoverable as `controlResumesAtTick - capture.redeployPauseTicks`. Its
*owner* had no derivation at all — only a guess from the signed displacement
of the front, which is wrong the first time an opponent regresses from a lead,
and which a life born inside the hold cannot make at all, because private
memory is life-scoped and a fresh body has none. Delete that code; ask
instead. The scaffold's `ArenaBasics.LiveHold(context)` is the one-line
version.

Two projectile facts land in the same window, for the same reason —
they were authoritative engine-side and unreadable from an observation.
Every entry in `visibleProjectiles` now carries **`ticksPerAdvance`** (the
firing profile's cadence between advances) beside the existing
`ticksUntilAdvance` and `tilesPerAdvance`, and **`damagePerHit`** (what one
contact costs). Together they answer "should I eat this?" exactly: the bolt
crosses `tilesPerAdvance` tiles every `ticksPerAdvance` ticks with the next
advance `ticksUntilAdvance` away, so an exact arrival tick exists, and
`damagePerHit` says whether arriving matters. Both are **per projectile** — a
volley bolt and an ordinary bolt need not agree on either, and a deflected
bolt carries the damage class of the bolt that was returned. The scaffold's
`ArenaBasics.Threat(projectile, tile)` does the arithmetic.

## Class skills compose with your class too

`--skills` adds the pre-registered class-skill kit
(`docs/DESIGN-MECHANISM-SLATE-2026-07-29.md`) on top of any class pair,
`--movement` arm, `--pendulum` level, `--bend` envelope, and `--duel-map`.
**Each skill is owned by exactly one class**, so a cell carries only the skills
whose owning class is actually in it: `--skills kit` on `bulwark-vs-striker`
resolves to volley + shell and yields
`frontline-labs-1-bulwark-vs-striker-cast-break`. The ruleset ID names exactly
what resolved, by behaviour rather than by silhouette, and it is reminted
whenever the behaviour changes — the volley that fires once and is returned is
`cast`, and the shell that shatters on its third deflection is `break`. Nothing
about the classes you already know changes; what changes is what your class can
do.

| token | owner | what appears in the contract | what it means on the board |
| --- | --- | --- | --- |
| `none` (default) | — | nothing | today's measured baseline |
| `volley` | striker | `striker-prime-volley-stance` / `striker-child-volley-stance` forms, the `striker-volley` attack profile with `volley.projectileCount = 3`, `volley-striker-*` / `unstance-striker-*` routes, and `automaticReturn` on the return route | windup **2** into an immobile stance whose gun fires **three simultaneous damage-1 bolts** — your facing lane and both adjacent 45-degree headings — straight only. **Firing returns you.** The fan launches and the return begins on that same tick, so one entry buys exactly one cast: enter, aim by rotating, shoot. There is no exit to author, and a parked striker cannot become artillery. Objective weight stays **1** throughout. |
| `shell` | bulwark | `bulwark-prime-aegis-shell` / `bulwark-child-aegis-shell` forms carrying `projectileGuard`, `shell-bulwark-*` / `unstance-bulwark-*` routes, and `automaticReturn` on the return route | windup **1** into a stance that **deflects enemy bolts arriving inside its facing quadrant**: the incoming bolt dies on the arc and a **new bolt launches from the shell's tile along the exactly reversed heading, owned by the bulwark's team** — so poking a shell head-on shoots yourself. Flank and rear contacts hurt normally. The shell cannot move, shoot, **or rotate** — the protected quadrant is chosen before the shield rises; objective weight stays **1**, so it still holds ground. **The shield breaks on its third deflection**: the third bolt shatters it into a forced return, and the punish window is the exit plus a fresh entry windup. |
| `five-slots` | fabricator | five `unitSlots` for each fabricator team, the `fabricator-late-child-ready` lifecycle profile, a new topology profile and fingerprint (`…-asymmetric-slots-5-3-v1` against another class, `…-five-slots-v1` in a fabricator mirror) | the fabricator fields **prime plus four children**; a non-fabricator opponent keeps three. The extra two unlock at **300** and **420** (continuing the class's own 120-tick cadence 60/180/300/420) and rebuild on a **30**-tick clock instead of 15 — more bodies, deliberately not faster bodies. |
| `kit` | — | all three, filtered to the cell's classes | the whole slate at once |

### Every stance spends a budget, and the rule spends it for you

Both stances declare **how much they are worth** and leave when it is gone.
That is contract data, not etiquette: the return route carries

```json
"automaticReturn": { "counter": "…", "threshold": N }
```

with `counter` either `attacks-issued-since-entering-source-form` (the volley,
threshold **1**) or `projectiles-deflected-since-entering-source-form` (the
shell, threshold **3**). The property is **absent** on every route the engine
never fires by itself — canonical contracts omit inert fields — so read it as
"missing means this stance has no budget".

- The counter starts at zero when you enter the form and never survives it. A
  second entry starts a fresh budget; a respawn obviously does too.
- The engine begins the return on the exact tick the counter reaches the
  threshold, spending the same exit windup your own `mobilize` would.
- **Leaving early is still yours.** A `mobilize` below the threshold is a
  perfectly ordinary decision — cast nothing and walk away, or drop the shield
  after one deflection. Leaving *late* is what no longer exists.
- The form-transition events for an engine-started return carry
  `reason: "automatic-threshold-return"`; a requested one omits the property.
  You can read it on an enemy too: a shell that just broke is a shell you can
  punish, and its return is public with a start and a completion tick.

Reading it from the contract, without hard-coding:

- A stance is an ordinary same-life route. Read `sameLifeTransitions` for its
  source form, target form, windup, reversibility, and any `automaticReturn`
  budget, and read the `transform` legality mask for the target forms you may
  enter this tick. The return is the parameterless `mobilize` — one route per
  stance form, exactly like turrets — and it is also the route the engine
  fires for you when the budget runs out.
- **The volley is public data, not a surprise.** Your form's attack profile
  carries `volley: { projectileCount, spread, identityOrder }`. The field is
  **absent** on every ordinary gun — canonical contracts omit inert defaults —
  so read it as "missing means one bolt". Bolts are ordinary projectiles: they
  appear in `visibleProjectiles`, each has its own ID, and the IDs are
  contiguous ascending in launch order (leftmost lane first).
- **The shell is public data too, and now it shoots back.** The guarding form
  carries `projectileGuard: "facing-quadrant-contacts-deflected"`, again absent
  on every unguarded form. A deflection is published as its own observed event
  kind, `projectile-deflected`, naming the shell, the shooter, the bolt that
  died, and — in `deflectedProjectileId` — the bolt that was sent back. The
  return is an ordinary projectile: it appears in `visibleProjectiles`, it
  belongs to the **bulwark's** team, it starts on the shell's tile, it flies
  the exact reverse of the heading that arrived, it carries the same damage
  and range class as the bolt you fired, and it is fully dodgeable. Poking a
  shell's face is no longer free — it is a tempo tax with a bill: sidestep the
  return, shoot from the flank, or come from two angles. The shell's arc never
  tracks you, so going around it always works.
- **Never hard-code three slots.** Count your own and the enemy's entries in
  the topology's `unitSlots`, and take unlock ticks from your slots' lifecycle
  assignments. In a five-slot arm your fourth and fifth slots become Ready
  much later and rebuild more slowly than your first two.

```bash
nilbots experiment frontline-labs \
  --bot <generic-spec> --opponent <generic-spec> \
  --classes bulwark-vs-striker --skills kit \
  --seed 42 --runtime wasm --out /tmp/skills
```

## The curve grammar is its own factor

`--bend` decides who may bend a shot. It composes with everything above and
needs a class pair, because the grammar is handed to class chassis.

| token | what appears in the contract | what it means on the board |
| --- | --- | --- |
| `striker-only` (default) | nothing | today's measured baseline: only a chassis that declares shot programs bends, and everyone else fires the parameterless `shoot-straight` |
| `universal` | every class's mobile gun gets `shotProgram.enabled` and the `shoot` action; the ruleset gains a `-bend` token | **every class's mobile gun bends once**, at its own depth: the striker keeps **1–4** tiles, bulwark and fabricator get **1–2**. Their forms move from `shoot-straight` to `shoot`, whose payload stays optional, so a straight shot is still one decision |

**Specials never curve, in either arm.** The volley fan is straight by
construction — an attack profile carrying a `volley` refuses programmed shots
outright — and turret guns keep their absolute eight-way `shoot-direction`.
If you want a curve, it comes from your mobile gun.

```bash
nilbots experiment frontline-labs \
  --bot <generic-spec> --opponent <generic-spec> \
  --classes bulwark-vs-striker --skills kit --bend universal \
  --seed 42 --runtime wasm --out /tmp/kit
```

## Phase-2 cells have registered identities

Phase 2 (DECISIONS #169) runs every cell on **keel + facing-locked** and
factors only the kit (off/on) and the bend envelope
(striker-only/universal) across the six class pairs. Spell those cells the
ordinary way — the flags do not change — but the ruleset ID that comes back
is a single **registered composite token**, because the per-factor spelling
does not fit: `keel-bend` beside `fabricator-vs-fabricator` and
`facing-locked` needs 65 of the 64 canonical characters, and the full
candidate game needs 74.

| token | the combination it names | spelled form |
| --- | --- | --- |
| `keel` | the pendulum alone — the phase-1b replication anchor | `--pendulum keel` |
| `helm` | keel + the whole skill kit. The keel holds the course; the helm is what you steer with, which is what the per-class verbs are | `--pendulum keel --skills kit` |
| `veer` | keel + the universal bend envelope. Every class's mobile gun may bend, so every bolt may veer | `--pendulum keel --bend universal` |
| `rig` | keel + the kit + the universal bend: the whole working rig, and the phase-2 candidate game | `--pendulum keel --skills kit --bend universal` |

The rules are the same ones the other registered tokens follow.

- **The token names the combination, not the spelling.** `--skills kit`
  resolves per class, so on `fabricator-vs-fabricator` the whole kit *is*
  FIVE SLOTS — and asking for `--skills kit` and asking for
  `--skills five-slots` there produce one ruleset with one fingerprint,
  named `rig` (or `helm`) either way.
- **A partial kit gets no name and keeps spelling itself out.** Half a kit is
  not a registered level, so `--pendulum keel --skills five-slots` on
  `bulwark-vs-fabricator` stays
  `frontline-labs-1-bulwark-vs-fabricator-keel-slot5-facing-locked`, and
  adding `--bend universal` to that combination overflows and tells you so.
- **A lesser pendulum never borrows the name.** Every composite is keel-based;
  `--pendulum ratchet --skills kit --bend universal` spells itself
  `ratchet-cast-bend`.
- The map, the format, and the seed profile are held constant across all 24
  cells, so the only moving parts are the two factors under measurement.

### FIVE SLOTS tuning variants (DECISIONS #171)

Phase 2 measured the counter-cycle failing in one direction — the
fabricator dominant on both seed sets — and attributed the overshoot to
FIVE SLOTS. `--five-slots` selects a registered tuning variant of that one
skill; it is only legal in a cell that carries it, each variant moves
exactly one lever, and every variant appends its token to the ruleset
identity (after the arm tokens) and mints its own fingerprints.

| Variant | Lever | Contract effect |
| --- | --- | --- |
| `full` | none (default) | the phase-2 measured arm, byte-identical: unlocks 60/180/**300/420**, ordinary children rebuild at **15**, extra at **30** |
| `trim` | slot count | the fifth slot is dropped; the fourth keeps its 300 unlock. Mints its own topology profiles (`…-asymmetric-slots-4-3-v1`, `…-four-slots-v1` in a mirror) |
| `boom` | schedule | extra slots swing late: **360/480** on the class's own 120-tick cadence |
| `drag` | rebuild economy | ordinary children rebuild at **30** (the baseline clock) instead of the class's native 15; the schedule is untouched |
| `moor` | both round-1 winners | trim + drag composed: four slots AND the 30-tick ordinary rebuild — round 1 measured the two levers fixing different edges |
| `wane` | hedged composite | trim + a half-step **22**-tick ordinary rebuild, registered beside `moor` because `drag` alone stalled the fabricator mirror |

Read the actual unlock ticks and rebuild delays from your slots' lifecycle
assignments and profiles rather than assuming this table: a variant cell
is an ordinary contract, and a bot that hardcodes 60/180/300/420 plays the
`boom` arm one unlock early.

### Aim (DECISIONS #173)

`--aim offset` restores the ±1-sector initial launch offset on every
class's mobile gun: a bolt may launch at 45° off facing (aim-only, zero
bends) or combine the offset with the one-bend program. Read the bounds
from your attack profile's `shotProgram` (`minInitialAimSteps`/
`maxInitialAimSteps` are ±1 on this arm, 0 otherwise). Specials never
carry it — the volley aims by facing, the turret aims absolutely.
`rig` + aim is registered as `sail`; the whole tuned candidate game
(rig + aim + wane) is `crew`.

### Cooldown clock (DECISIONS #180)

`--cooldown ticking` moves the attack cooldown onto TIME: it decrements
every tick in every form, so a gunless stance (the shell) or a windup no
longer freezes your gun's recovery — the hidden stance tax a wave-6
author measured is gone on this arm. Everything else about cooldowns is
unchanged: one counter per body, firing sets it to the firing gun's
declared `cooldownTicks`, transitions carry it without refill, attacks
are legal only at zero, and it is public in observations. The contract
declares the clock at `rules.tickResolution.cooldownClock`
(`advances-with-time`; absent means the historical armed-form clock).
The whole open game on the ticking clock is the registered identity
**`tide`** (`sail-open-tick` where no fabricator is in the cell).

### Route cooldowns (DECISIONS #181)

A same-life route may declare `cooldownTicks` (read it on
`sameLifeTransitions[]`; absent means none). After the route COMPLETES,
requesting the same route from the same UNIT SLOT is refused (an
ordinary Blocked) while `tick < completionTick + cooldownTicks + 1`.
The clock survives the body — dying and respawning does not reset it —
and automatic (engine-caused) returns are exempt, so a forced return is
never trapped by its own clock.

Every live clock is public: `self.routeCooldowns` (and the same field on
each ally — allies share their complete gameplay state) lists
`{ transitionId, readyAtTick }` for every route of that body's unit slot
currently held shut, ordered by transition ID. The route accepts a
request again the first tick `tick >= readyAtTick`; the field is absent
while no clock is live, so contracts declaring no route cooldown look
exactly as before. Do not infer the window from your own completion
history — the clock survives your death, and a life born mid-window has
no history to infer from.

### Volley salvo (DECISIONS #182/#183)

`--volley salvo` re-arms the striker's fan. The aim restoration (#173)
had cannibalized it: the fan's spread is exactly the mobile gun's three
aim options, offered one at a time at twice the cadence without giving
up the step. On this arm the fan is worth the stance again:

- **Every fan bolt deals 2** (the mobile gun stays at 1). A diverging
  fan still lands at most one bolt on one body, so read this as "a
  landed volley hits twice as hard", not triple damage.
- **The fan no longer taxes your gun.** Its profile `cooldownTicks` is
  the 1-tick floor: launching the volley barely touches the shared
  body counter, so you re-enter mobile play with your gun essentially
  ready.
- **The stance enters in 1 tick** (#183) — the same grammar as every
  other stance. The measured fan's 2-tick entry was the game's only
  2-tick public telegraph, and a warned target had time to leave the
  three covered arcs or face the fan with a shell.
- **Frequency is priced on the ENTRY, not the shot**: the volley stance
  entry routes declare `cooldownTicks: 8` — the first consumer of the
  route-cooldown capability above. One cast per entry is unchanged;
  the entry clock is what spaces casts, it survives your death, and
  `self.routeCooldowns` names the exact tick the stance opens again.

The arm is inert-omitted where no striker is in the cell. `tide` +
salvo is the registered identity **`swell`** (the entry-2 first mint
was `surf`; behavior changed, so the token re-minted — read your entry
route's `windup.durationTicks`, don't assume).

### Stance ground (round 3)

`--stance-ground free` drops the `transition-placement-forbidden` tag kind
from the VOLLEY and AEGIS SHELL entry routes: a skill stance can rise on
objective tiles and in the central corridor. Turret anchor routes keep the
tag. Read your entry route's `placement` from the contract — under this
arm its `forbiddenTileTags` is empty, and stance legality on a tile you
hold means the shell can guard the point it is capturing. `wane` + `free`
is registered as the composite identity `berth`.

`--stance-ground open` (DECISIONS #176) goes further — the open game:

- EVERY transform placement is free, turret anchors included. A turret
  on an objective still scores nothing (objective weight zero) — the
  bargain is the price of fortifying a point.
- **The turret is a true cycle**: `anchor` ⇄ `mobilize` unlimited per
  life (`irreversibleForLife` is false on the mobilize routes — read
  it, don't assume the old once-per-life rule). Health maps by
  `preserve-ratio-floor-minimum-one` in BOTH directions with no entry
  heal: full cycles losslessly (4/4 ⇄ 7/7), partial health pays the
  floor each round trip, and a transform never kills (minimum one).
- A ground arm is inert-omitted where nothing it touches exists (a
  fabricator mirror), so the same flag set works on every pair.

The whole open game — keel + kit + universal bend + `wane` + `aim` +
`open` — is registered as the composite identity **`deck`** (`sail-open`
spells itself where no fabricator is in the cell).

## Side objective: MUSTER

`--side-objective muster` puts a second, lesser objective on the map
(`docs/DESIGN-SIDE-OBJECTIVES-2026-07-30.md`). It is not a second way to
score — Frontline declares exactly one score channel and ranks timeouts by
exactly it, so a side point that paid territory would be a way to win a match
by refusing to fight for the front. MUSTER pays in **respawn geometry**:

- **The site** is two mirror-symmetric 2-tile regions in the map's dead
  centre-column alcoves, `muster-site-north` and `muster-site-south`. They are
  one flag with two places to stand: presence is summed across both, so your
  body in the north alcove and an enemy body in the south alcove **contest
  each other**. The arm runs on its own map generation,
  `frontline-labs-02-muster`, because the alcoves are widened to at least two
  approach headings first — a 1-wide dead end plus an AEGIS SHELL is an
  unflankable holder, and the shell's published counter-play is that going
  around it always works.
- **The latch** is 12 consecutive ticks of **sole** positive objective weight.
  Any empty or contested tick puts a running claim straight back to **zero**,
  so walking in once is a real denial, not a pause. Completing the claim
  latches ownership: the owner keeps the flag while the site is empty and
  while an enemy stands on it, and loses it only when the other team completes
  a full claim of its own.
- **Objective weight, not bodies.** An anchored turret declares weight zero,
  so it can neither hold the site nor contest one — the turret bargain, again.
  An AEGIS SHELL (weight 1) can hold it.
- **The effect**: while your team owns the flag, your **PRIME's** automatic
  return lands on the forward rally tile — the rear-most free tile of your
  own-side chain-adjacent objective, measured along your own advance
  direction, exactly the placement `--pendulum keel` hands out for free.
  Without the flag it lands on your reserved home spawn. Companion
  activations, fabricated children, and replicas are untouched, so a
  four-slot team gains no more from the flag than a three-slot team does.
- **This arm takes the free rally away.** On `--side-objective muster` the
  lifecycle placement is the home spawn even under `keel`: the placement the
  keel gives both teams unconditionally is the thing you are now fighting
  over. Read `rules.lifecycle.automaticReturnPlacement` — do not assume the
  pendulum level still implies a rally.
- **The owner at your respawn tick decides.** A death queued while you held
  the flag still walks home if the flag was lost before your body lands, and a
  death queued while you had nothing still rallies forward if you took the
  flag in the meantime.

### Read the flag, do not infer it

A body that walks off to a side site is invisible — vision is a facing
quadrant at range 6 — so without a published fact, "are they one body light at
the front?" is a guess. Two fields on the Frontline mode observation, beside
the ratchet-hold clocks:

| field | meaning |
| --- | --- |
| `secondaryOwnerTeamId` | the team that owns the flag, or **null** while it is neutral |
| `secondaryClaimProgress` | signed sole-presence ticks on the running claim: **positive counts for team 0, negative for team 1**, zero when no claim stands |

- **Null and zero mean the same thing on a ruleset with no side objective at
  all**, for the whole match, so a bot reading these never has to branch on
  whether the mechanic exists.
- **The sign is the claimant.** `secondaryClaimProgress = -7` means team 1 has
  held the site alone for seven ticks. There is no separate claiming-team
  field: the sign carries it, in the same direction the public team-advance
  ordering uses.
- **The threshold is contract data, not an observation fact.** Read
  `rules.gameMode.secondaryControl.captureThresholdTicks` for what the claim
  is racing toward, `regionIds` for where the site is (resolve them against
  `map.regions` like any other region), and `effect` / `rallyScope` for what
  owning it buys. The whole block is **absent** on a ruleset without a side
  objective — read your contract, don't assume the map has a flag on it.
- **Ownership and claim changes are ordinary `mode-changed` facts**, carrying
  the post-change state, exactly like the ratchet hold's lapse. Watch that
  event to see the flag turn over; nothing new to subscribe to.

Because the claim resets on interruption and the owner does not, the two
fields answer different questions. `secondaryOwnerTeamId` prices the *front*
— an owner rejoins the fight in a handful of ticks and you trudge, so push
before they die rather than after. `secondaryClaimProgress` prices the *site*
— a claim at 11 of 12 is worth one body's walk to break, and a claim at 1 is
not.

```bash
nilbots experiment frontline-labs \
  --bot <generic-spec> --opponent <generic-spec> \
  --classes bulwark-vs-striker --pendulum keel --skills kit \
  --bend universal --side-objective muster \
  --seed 42 --runtime wasm --out /tmp/muster
```

The arm needs a cell to sit in: a class pair (explicit or manifest-declared)
or a `--pendulum` level. It is a **real arm on every pair** — unlike
`--volley salvo` it is never inert-omitted, because it changes the map for
both teams whatever classes are in the cell. The candidate game plus the flag
carries a registered identity per shape: `tide` + muster is **`ensign`**,
`swell` + muster is **`banner`**, and the tuned open game on the ticking clock
without the fabricator's `wane` (`sail-tick-open`) + muster is **`pennant`**.
Smaller cells spell their factors and append `muster`.
