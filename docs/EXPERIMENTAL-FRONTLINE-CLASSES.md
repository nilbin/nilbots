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
  omnidirectional turret and Mobilize back once per life. The prime's
  three-tick windup is a visible, punishable commitment — opponents see the
  route, the windup, and the reversibility in the contract before tick 0.
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
| `forward-rally` | `lifecycle.automaticReturnPlacement` | respawns and companion arrivals appear on your own side of the active objective, not at home |
| `contest-majority` | `capture.controlPolicy` | surplus objective weight scales capture pressure, so one body no longer nulls two |
| `enemy-sole-decay` | `capture.decayClock` | empty and contested ticks stop eroding progress; only an enemy standing alone on the objective does |
| `ratchet` | sticky-frontline + forward-rally | the registered structural level |
| `ratchet-contest` | ratchet + contest-majority | the registered structural level with contest cost |

`--capture-threshold` and `--prime-respawn-ticks` are the numbers-only
level and compose the same way. None of these arms changes the observation
schema, the action catalog, or any class stat, so a contract-driven bot
needs no re-authoring: read `gameMode.capture` and `lifecycle` from
MatchStart if you want to adapt, and expect a respawn to put you near the
fight under `forward-rally`.

```bash
nilbots experiment frontline-labs \
  --bot <generic-spec> --opponent <generic-spec> \
  --classes bulwark-vs-striker --pendulum ratchet-contest \
  --seed 42 --runtime wasm --out /tmp/pendulum
```

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
