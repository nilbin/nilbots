# Side objectives — opening the map without replacing the front (2026-07-30)

Status: **design exploration, nothing built, nothing registered.** Commissioned
against the owner's roadmap note in
[`DESIGN-MECHANISM-SLATE-2026-07-29.md`](DESIGN-MECHANISM-SLATE-2026-07-29.md)
("a second, lesser objective to open the map and add an allocation decision…
needs a design pass before any build") and the owner's verbatim direction:
*"spin an agent on investigating potential side quests on the map to unlock the
map and increase the depth of strategy — I'm undecided with an open mind so
think outside the box."*

Read before this: the class brief
([`EXPERIMENTAL-FRONTLINE-CLASSES.md`](EXPERIMENTAL-FRONTLINE-CLASSES.md)),
the slate above, the platform's expressive limits
([`GAME-MODE-ARCHITECTURE.md`](GAME-MODE-ARCHITECTURE.md) §§4, 6, 7), and
DECISIONS #171–#183.

---

## 1. Five laws every concept here obeys

These are not preferences. Each one is forced by something already in the
engine, the decisions log, or the arithmetic of a three-body team, and each one
kills otherwise attractive designs.

**L1 — Side control is measured in objective weight, so a turret cannot hold
one.** The turret bargain ("objective weight zero; fortifying removes that body
from every capture and contest count") is the single most load-bearing rule in
the class slate. If a side point is held by *body presence* rather than
*objective weight*, the bulwark gets a free asset — park a 7-HP turret on the
side point and it costs nothing, because the thing it normally forfeits (front
presence) is not what the side point pays. Every concept below reads objective
weight, exactly as `FrontlineControlSystem` does. Consequence worth stating
out loud: an AEGIS SHELL (weight 1) *can* hold a side point, and a turret
cannot — which is correct and is also a live counter-play question (see L-map).

**L2 — A side objective must not pay the timeout channel.** Frontline declares
exactly one score channel and ranks timeouts by exactly it
(`FrontlineGameModeDefinition` hard-validates
`ScoreCatalog.Length == 1 && TerritorialProgress`, and
`FrontlineVictoryDefinition.TimeoutRanking` has exactly one entry). Anything
that adds `TerritorialProgress` from off the front is a way to *win the match
without contesting the front* — the purest camping incentive the game could
have, and it inverts the whole point of the exercise. This makes the obvious
conservative design (a second capture point paying territory) the **most**
dangerous concept in this memo, not the safest. Side objectives pay in
**tempo, geometry, bodies, vision, or cooldown** — never in the victory
currency.

**L3 — One published fact per concept, two as an absolute cap.** The
ratchet-hold observability bump (#169) added exactly two nullable ints
(`holdOwnerTeamId`, `holdEndsAtTick`) and touched roughly thirty-five files:
`GenericActorRuntimeObservation`, `ReplayV3` + its serializer and projection,
`GenericActorSdkModelMapper`, `GenericActorContext`,
`GenericActorWireObservationCodec`, `BotProject`, the `ArenaBasics` template,
`mobile/src/components/arena/protocol.ts`, five `web/src/` modules, plus
fixtures and validator tests on both sides. That is the calibrated price of
"the bot can read it". The owner has already weighed allocation depth against
observation tensor cost once (the dual-live frontline was gated on exactly
this); the answer here is to spend that budget once, on one field, and reuse it
(§3).

**L4 — Absence must be readable, or it is a coin flip rather than depth.**
Vision is a facing quadrant at range 6. A body that walks off to a side point
is *invisible* — so if the side point's state is not published, the opponent
cannot tell a 3v3 front from a 3v2 front, and "should I contest the side?"
becomes a guess. Published side-point ownership is what converts the mechanic
into a read: *they own the flag, therefore they are one body light at the
front, therefore push now.* This is the same argument that killed the derived
ratchet hold (#169: "an owner without a clock… is a malformed observation") and
it is why every concept below names its published fact.

**L5 — Every side objective is a fabricator buff and a striker nerf, by
arithmetic.** Attending a side point costs one body. A three-body team pays
33% of its force; a four-body `wane` fabricator pays 25%. And the body most
likely to survive an errand and a fight at the end of it is the 5-HP bulwark
prime, not the 3-HP striker. The current ladder (#179/#180/#183) is
bulwark ≥ fabricator > striker with the striker losing **+0.852 / +0.778** in
its two cross-class edges, and #183's finding is explicit: *"the chassis is
losing, not just the fan."* Adding a body-attendance mechanic on top of that
pushes the wrong way unless the reward is **flat per team** (so the 4-body team
gains no more from it) and ideally **keyed to something the striker uses**.
Any side-objective arm must be measured against the class edges before adoption,
not only against pacing.

---

## 2. The map already has the room — and where it is

`frontline-labs-01` is 23×15. The five frontline positions run a diagonal:
low-left `(3–4, 8–9)`, high-left `(6–7, 5–6)`, centre `(10–12, 7–8)`,
high-right `(15–16, 5–6)`, low-right `(18–19, 8–9)`. Everything above row 4 and
below row 10 is **structurally dead**: rows 1 and 13 are fully open lanes
spanning x=1..21 that no objective, spawn, pad, or region ever touches.

Two sites are mirror-exact on the vertical centre line and currently unused:

| site | tiles | geometry |
|---|---|---|
| north alcove | `(11,1) (11,2) (11,3)` | 1-wide cul-de-sac; `(10,2) (12,2) (10,3) (12,3) (11,4)` are all wall — one entrance, from row 1 |
| south alcove | `(11,11) (11,12) (11,13)` | exact mirror; one entrance, from row 13 |

They are equidistant from both spawns by construction (x=11 is the centre
column), which satisfies the map validator's format/symmetry requirement for
free. Walking there is expensive and that is the point: from the centre
objective `(11,7)` the only route out of the middle band runs `(10,7) → (10,6)
→ (10,5) → (10,4) → (9,4) → (9,3) → (9,2) → (9,1) → row 1 → (11,1) → (11,2)`,
about eleven steps plus rotations under `facing-locked` — roughly 25–30 ticks
round trip, or two captures' worth of one body's time in a 500-tick match. That
cost is the tuning surface: pull the site down to row 3, or open a shoulder,
and the errand gets cheaper.

**One concrete hazard, worth a design ruling before any of this is built.** A
1-wide dead end plus an AEGIS SHELL is a degenerate holder: the shell deflects
every bolt arriving in its facing quadrant and returns it to the shooter, and
its published counter-play is "the shell's arc never tracks you, so going
around it always works" — which a 1-wide corridor deletes. So either widen the
alcoves (open `(10,2)/(12,2)` and their mirrors — a map edit, which is exactly
the map-as-tuning-surface direction of #176), or give the site two entrances,
or accept a shell-only fortress. **Any side site must have at least two
approach headings.** The outer lanes (rows 1 and 13) satisfy this natively; the
alcoves as drawn do not.

There is also precedent for shipping geometry as an arm:
`FrontlineLabsDuelMapArm.OuterShoulderBypass` already rewrites rows 6 and 8 to
`#....#...#...#...#....#`, and `ThinFronts` already reshapes every objective
region. A side-objective map is a third arm of the same kind, not a new format.

---

## 3. The engineering shortcut: build one capability, not eight mechanics

Most of the concepts below differ only in **what owning the site does**. Their
shared skeleton is identical: a typed region, control resolved by objective
weight, an owner latch, and one published fact. That is one closed typed
capability, which is precisely the architecture's stated bar — *"Existing typed
mechanics are tunable through immutable data. A genuinely new semantic adds one
closed typed capability without changing the common bot, replay, result, or
competition envelopes."*

```text
FrontlineSecondaryControlDefinition
  regionIds[]                     // ActorMapRegionDefinition.RegionKind.Objective
  captureThreshold                // reuse FrontlineCaptureDefinition arithmetic
  ownershipKind                   // latched-until-recaptured | held-while-present
  effect                          // ← the tagged part; one arm per variant
```

with `effect` a closed enum: `RallyPlacement`, `EnemyGainTax`,
`RouteCooldownRelief`, `PerceptionUnion`, `SlotReadiness`, `TerritorialTrickle`
(the L2 trap, registered so it can be *measured* and rejected on evidence
rather than argued about).

And one published mode fact for all of them:

```text
ModeObservationState.Frontline
  + secondaryOwnerTeamId : int?     // null = neutral
  + secondaryProgress    : int      // signed toward the claiming team, like captureProgress
```

Two fields, once, for every concept in the family. Concepts 1–6 below then cost
*an enum value plus a mode-driver branch* — days, not weeks — and each ships
behind its own registered arm token exactly as `--pendulum` / `--skills` /
`--volley` do. Concepts 7–11 sit outside this family and are priced honestly as
such.

---

## 4. The concepts

Ordered conservative → wild. Costs are against the machinery that exists today
(`FrontlineActorMatchModeDriver` is 219 lines; `FrontlineControlSystem` is 271;
lifecycle placement, route cooldowns, automatic returns, typed regions, and
tile tags are all built and tested).

---

### 1. RELAY — the honest baseline

**Mechanic.** A sixth typed Objective region sits off the frontline chain and
is captured by the same arithmetic at a slower threshold. While a team holds
it, that team gains a trickle of `TerritorialProgress` — the same currency the
front pays. Ownership latches until recaptured. It never advances the frontline
position; it only adds signed score. It is the design everybody thinks of
first, and it is included so the slate has an anchor to beat.

**Dilemma.** Send one of three bodies on a 25-tick errand to buy score you would
otherwise have to fight for.

**Classes.** Bulwark holds it best (5 HP, shell); turret cannot (L1). Fabricator
attends cheapest (L5). Striker is worst at both attending and defending.

**Map/tags.** One `RegionKind.Objective` region in a dead lane, outside
`OrderedObjectiveRegionIds`.

**Observation.** The shared `secondaryOwnerTeamId` + `secondaryProgress`.

**Reward channel.** Score — `TerritorialProgress`.

**Cost.** **S** on top of §3's capability (an enum value and a per-tick add).

**Failure mode.** **This is the camping design (L2).** Because timeouts rank on
`TerritorialProgress` and nothing else, a team that takes the relay and then
refuses to fight wins by clock. It also directly attacks the #168/#171 pacing
wins (cap share down, leader-extends up) by rewarding the leader for standing
still. Register it, measure it, expect to reject it — its value is as the
control arm that proves the family's other effects are the reason anything
improved.

---

### 2. MUSTER — the rally flag

**Mechanic.** A capturable site whose owner's **automatic returns and companion
arrivals appear on its own side of the active objective** — the `forward-rally`
placement policy that `keel` gives both teams unconditionally today, converted
into a contested asset. The loser of the flag walks home-to-front on every
death; the holder rejoins in a handful of ticks. Ownership latches until
recaptured, so it is worth defending, not just touching. Placement uses the
existing `FrontlineForwardRallyPlacement` derivation (rear-most free tile of the
region along the team's own advance direction), so both sides' arrivals remain
exact reflections. Nothing about capture, decay, or score changes.

**Dilemma.** The flag is worth roughly one body's travel time *per death* for
the rest of the match, so the errand pays compound interest — but taking it
costs the front a body now, and losing a body at the front while you are away
is exactly what the flag was going to fix.

**Classes.** Fabricator gains most in raw ticks (more bodies dying more often),
which is L5's warning made concrete — mitigate by scoping the effect to the
Prime's automatic return only, which is the 18-tick clock every class shares and
the fabricator's fourth body does not use. Bulwark defends the flag well; the
shell on the flag is a real fortress and wants two approach headings (§2).
Striker's respawn is its most-used mechanic (3 HP), so flag denial is
disproportionately a striker tool — the rare mechanic that helps the class on
the floor.

**Map/tags.** One Objective-kind region per site; the *arrival* tile is derived,
not authored, so no new placement region is needed. `SpawnProtected` tagging is
optional and probably wrong here (a protected rally is un-punishable).

**Observation.** `secondaryOwnerTeamId` only. `secondaryProgress` if it is
captured over time rather than touched.

**Reward channel.** Respawn geometry / tempo.

**Cost.** **M** — the placement policy, the derivation, the region kind, and
the reflection guarantee all exist; what is new is making the policy
owner-dependent and publishing the owner.

**Failure mode.** Snowball. A team that holds the flag wins attrition, which
buys the front, which buys the flag. `keel` was adopted partly *because*
leader-extends rose (#171), so some of this is desired — but a runaway from a
single early capture is not, and the mitigation (short latch, or ownership that
decays to neutral) is a number, which is the right shape for dump-then-tune.
Secondary risk: it makes deaths *cheaper*, and cheap deaths are already the
slate's structural complaint ("kills do not convert").

---

### 3. BATTERY — remote support fire

**Mechanic.** While a team holds the site, the **opponent's** capture gain at
the active frontline position is taxed — one fewer progress per sole-control
tick, or a decay interval that ticks faster against them. Nothing about the
holder's own capture changes, so the site cannot win a match by itself. The
effect is continuous and applies from anywhere on the map. Ownership latches.
It is the only concept here where a body that is *not* at the front keeps
changing what happens at the front, every tick.

**Dilemma.** The purest form of the question the owner asked for: is one body
worth more standing on the point or slowing the enemy's clock on it? The answer
should flip with the match state — behind and pushing, you want bodies; ahead
and defending, you want the tax.

**Classes.** Class-neutral by construction, which is its main virtue given the
fragile class band (#179/#183). Bulwark holds it best; turret cannot (L1).

**Map/tags.** One Objective-kind region, ideally in each team's own half so the
choice is "defend mine or take theirs" rather than one contested rock.

**Observation.** `secondaryOwnerTeamId`. The *effect* also needs to be legible:
a bot already reads `captureProgress` each tick and can see its own rate, but
inference is exactly what #169 rejected — publish the owner and let the bot
read the tax from the contract.

**Reward channel.** Capture economics (the enemy's, not yours).

**Cost.** **S/M** — one branch in `FrontlineControlSystem`'s gain arithmetic
plus §3's capability.

**Failure mode.** If the tax is large the site *is* the front and everyone
relocates; if it is small nobody walks 25 tiles for it and the mechanic is dead
map furniture (the turret's 0.13% usage is the cautionary precedent). The band
between those is narrow and only measurement finds it. It is also the least
watchable concept on the list — an invisible multiplier is not a viewer beat,
which matters given the owner's stated fun criteria.

---

### 4. KILN — the re-arming shrine

**Mechanic.** Standing on the site clears (or fast-decrements) your **route
cooldowns** — the slot-scoped clocks introduced in #181 and first consumed by
the salvo's `cooldownTicks: 8` volley entry. There is no ownership and no
latch: the effect applies while you are there, so the site is a place you visit,
not a place you hold. A striker that pays the walk gets its fan back on demand;
a bulwark whose anchor route ever gets priced gets its turret cycle back the
same way. It is the cheapest concept here and the only one that needs **no new
published fact at all**.

**Dilemma.** Trade position for frequency. The salvo is priced on the entry
clock precisely so that casts are spaced; the kiln says you may buy that spacing
back with map distance instead.

**Classes.** Striker-keyed today, because the volley entry is the only route in
the game that declares a cooldown — and the striker is the class on the floor
of every matchup (#179/#180/#183: "the striker loses to both classes and it is
the class, not the bots"). This makes the kiln the one concept on the list that
is *aimed* at the open balance problem. Inert-omitted in cells with no striker,
exactly like `--volley salvo` (#182), so one flag set still serves a whole wave.

**Map/tags.** A small region — 2–3 tiles on a flank of the corridor, close
enough that the errand is 6–10 ticks rather than 25. Could equally be a
`TileTagKind` on existing tiles, which is the literal reading of #176's
"granular tile classes with per-skill rules".

**Observation.** **None.** `self.routeCooldowns` already publishes
`{transitionId, readyAtTick}` and the region is static MatchStart contract
data; a bot computes "am I standing on a kiln tile" from facts it already has.

**Reward channel.** Cooldown.

**Cost.** **S** — a region, a legality-free per-tick effect on the slot clock,
and one arm token. It does not even need §3's capability.

**Failure mode.** Narrow. Today exactly one route in the game has a cooldown,
so in a bulwark-vs-fabricator cell the kiln is scenery. It also risks making
the salvo's entry clock meaningless (the clock is the skill's price; a shrine
that refunds it un-prices the skill) — the mitigation is partial relief, e.g.
halve the remaining clock rather than zero it. And a striker that parks on the
kiln casting fans is the artillery fantasy the owner explicitly killed
(#165/#182): the kiln must sit **away** from firing lanes onto the objective.

---

### 5. BEACON — vision infrastructure

**Mechanic.** Holding the site adds a fixed static sensor to the owning team's
perception union: a declared watch region (say, the two lanes flanking the
active objective) is permanently visible to the whole team while the beacon is
held. Team perception is already `ImmediateUnion` with `observedBy` provenance,
so the machinery for "the team sees what one sensor sees" exists. Ownership
latches until recaptured. It reveals bodies, not intentions.

**Dilemma.** Spend a body to delete the enemy's ability to rotate unseen — in a
game where vision is a facing quadrant at range 6, knowing where the third enemy
body is *is* the read that L4 says the whole family depends on.

**Classes.** Bulwark's omnidirectional range-4 vision benefits least; the
striker's range-6 quadrant benefits most (it is the class that needs to see a
target before a target sees it). Modest striker tilt, which is the right
direction.

**Map/tags.** Objective-kind region for the beacon, plus a second declared
region for what it watches — a natural use of typed regions, and the watched
region is a pure map-edit tuning surface.

**Observation.** `secondaryOwnerTeamId`, plus a schema wrinkle that is the real
cost: `ObservedEnemyState.ObservedBy` and `ObservedTile.ObservedBy` are
`ImmutableArray<ActorIdentity>` — a non-actor sensor has no actor identity. It
needs either a reserved synthetic identity per team or a nullable/tagged
observer, and that touches the codec, the replay, the validators, and both web
and mobile mirrors.

**Reward channel.** Vision.

**Cost.** **M/L** — the union plumbing exists, the observer-identity change does
not.

**Failure mode.** Two. First, the slate's own warning: *"adding hidden
information while the existing hidden information (the bend) is measured inert
inverts the diagnosis"* — this adds an information asymmetry to a game whose
last information mechanic measured worthless. Second, watchability: a vision
advantage is invisible on screen. The viewer sees nothing happen, which is the
opposite of the turret/shell/volley test the owner applies.

---

### 6. DROP — the roaming cache

**Mechanic.** Every N ticks a pickup materializes at a seeded position drawn
from a declared candidate region, announced in the mode observation the moment
it is scheduled — position and due tick both public, both derived from the
match seed via SplitMix64, so it is fully deterministic and fully replayable.
The first body to stand on it takes it; the reward is deliberately small and
boring (one slot's readiness clock cut, or a single route-cooldown clear). The
*location* is the mechanic, not the payload: it appears in the dead lanes, so
the map opens because the map is where the prize is.

**Dilemma.** It breaks the opening book. Every other concept here has a fixed
address, so a bot can compile a memorized route once and never think again;
this one forces a live comparison — "is the drop closer to me than to them, and
is the front stable enough to spare the trip?" — which is the reasoning depth
the owner is asking for.

**Classes.** Whoever is nearest, which is a genuine strategic-position value the
game does not currently price. Fabricator's extra body makes it likelier to have
someone near (L5).

**Map/tags.** One candidate region covering the dead lanes; the payload is a
lifecycle clock so no new entity kind is needed (the slate explicitly rejected
"salvage pickups — a fourth entity kind to deliver what two respawn numbers
can", and this design respects that: the drop is a *tile*, not an entity).

**Observation.** `dropTile` + `dropDueTick`, or reuse §3's two fields with a
position instead of an owner. Two fields.

**Reward channel.** Unit economy (slot readiness).

**Cost.** **M** — seeded scheduling, a published position, and the pickup
resolution in the joint tick.

**Failure mode.** Reads as a coin flip if the position is not comfortably
reachable by both sides — the seed decides, and "the RNG gave them the drop" is
a bad match narrative even when it is provably fair. Also risks pulling bodies
off the front on a metronome regardless of match state, which is noise rather
than decision.

---

### 7. SPUR — the branching front

**Mechanic.** The frontline chain stops being a line and becomes a graph: one
or more positions have two authored variants (a high road through the upper
lane and a low road through the lower), and a small junction capture decides
which variant is live when the front next advances into it. The pendulum still
swings; *where* it swings through is contested. Ownership of the junction
latches, so a team can pre-commit terrain before the front arrives. Everything
downstream — capture, decay, ratchet, breach — is unchanged.

**Dilemma.** Terrain preference is real and per-class: the bulwark wants a tight
choke where a shell's locked arc covers the only approach, the striker wants
long clean sightlines for a range-8 gun, the fabricator wants open ground where
four bodies can envelop. Choosing your own map is a strategic act that costs a
body now and pays a matchup edge later.

**Classes.** The strongest per-class differentiation of any concept here, and it
uses the map itself as the differentiator rather than a stat.

**Map/tags.** Alternate Objective-kind regions per position, plus the junction
region. `FrontlineActorModeMapBindingDefinition.OrderedObjectiveRegionIds`
becomes a small ordered *set* of alternates per index — a contained change to a
definition that already refuses to sort itself.

**Observation.** One field: the resolved active objective's region ID (or a
variant index). Today a bot derives the active objective's tiles from
`orderedObjectiveRegionIds[activePositionIndex]` in the static contract; making
that dynamic **breaks every existing bot that does so**, which is the honest
author cost and the reason this cannot ship quietly inside an existing arm.

**Reward channel.** Map routing / terrain.

**Cost.** **M** engine-side, **L** counting the doctrine rewrite across a whole
cohort.

**Failure mode.** If the two variants are not close to equally good, the
junction is not a choice — it is a checkbox, and the losing branch is dead map.
Balancing two variants per position is more level-design work than the whole
rest of this memo, and it multiplies map fingerprints per variant (which #176
frames as a feature — clean per-variant fingerprints — but is still real work).

---

### 8. SALLY GATE — the door that opens

**Mechanic.** A tagged wall run separating the outer lane from the objective
diagonal is **impassable at tick 0 and passable later** — either on a public
schedule declared in the contract, or when a team stands on a lever tile.
Geometry in the map file never changes, so map fingerprints stay static; the
gate is mode-owned movement legality, and a closed gate simply does not appear
in the movement legality mask. The map literally unlocks: a flank that did not
exist at tick 100 exists at tick 200. This is the most direct reading of the
owner's phrase.

**Dilemma.** Under a schedule, it is a shared clock both teams must plan
against — the front you fortified is the front that gains a back door at tick
250. Under a lever, it is an allocation cost with a geometry payoff.

**Classes.** Bulwark suffers most (a fortress with a new approach is not a
fortress; turret facings are fixed and its anchor is a windup commitment).
Striker gains most (new angles are what a range-8 quadrant gun wants). Another
rare striker-positive, and the mechanic the shell's locked arc is most exposed
to.

**Map/tags.** A new `TileTagKind` — `GatedPassage` — applied to the wall run's
floor tiles, plus a lever region if it is not purely scheduled. Tag kinds are a
closed enum with two members today; a third is an additive append of the exact
shape #156 established.

**Observation.** **Zero to one.** A scheduled gate needs nothing: the schedule
is MatchStart contract data and the tick is known, and the movement legality
mask is already truthful per tick, so a bot *discovers* the door in its own
mask. A lever-driven gate needs `secondaryOwnerTeamId`.

**Reward channel.** Geometry / passability.

**Cost.** **M** — the tag kind, the mode-owned legality branch, the pathing
implications for the validator (`automatic-return` reachability and fabrication
placement must remain satisfiable in both gate states).

**Failure mode.** A bigger map is a slower map: more walking is less fighting,
and this game's pacing wins (#171: cap share 0.43→0.24) came from making the
front *more* decisive. A gate that opens a long way around may simply add
travel. Scheduled gates also risk feeling arbitrary — the tick-250 flank
punishes whoever happened to commit, which is variance dressed as strategy.
Mitigate by making the gate a *shortcut between existing frontline positions*
rather than a new perimeter.

---

### 9. PYRE — where kills convert

**Mechanic.** A declared region in which a **destruction credits the killing
team with frontline tempo** — capture progress on the active objective, or a
cut to the enemy's next respawn clock. Nothing is captured and nothing is held;
the region only changes what a kill is worth inside it. It attacks the slate's
own structural diagnosis head-on: *"kills do not convert (18-tick full-health
respawn vs 15-tick capture)."* It creates a place both teams want to *fight*,
which is the opposite of a place one team wants to *sit* on.

**Dilemma.** Fighting away from the objective is normally free for the loser and
worthless for the winner; here it is neither. A team that is winning duels wants
to drag engagements into the pyre; a team that is losing them wants to refuse.

**Classes.** Rewards whoever wins fights, which today is the bulwark — the
wrong direction for the class ladder (L5), and the honest strike against this
concept. Mitigation: put the pyre where a 3-HP long gun holds the advantage
(open ground, long lanes, no cover to close through), which converts it from a
class buff into a **terrain-flavoured** one.

**Map/tags.** A `TileTagKind` on a lane, not a region to be captured. Purely
declarative; the mode driver reads the tag when it resolves a destruction.

**Observation.** **None.** The tag is static contract data, destruction events
are already published with position, and the credited progress already appears
in `captureProgress` / the scoreboard.

**Reward channel.** Kill conversion → capture tempo.

**Cost.** **S/M** — a tag kind and a branch in destruction finalization, which
already carries exact source-life attribution and canonical ordering.

**Failure mode.** A meat grinder that pulls both teams off the front
permanently — if fighting in the pyre is better than capturing, the pyre is the
game. Magnitude must be small enough that it prices *incidental* kills rather
than *sought* ones. Also: it rewards the class that already wins the ladder, so
it must be measured against class edges, not just pacing.

---

### 10. RELIC — the courier run

**Mechanic.** A neutral object sits at a fixed site; a body standing on it may
take a same-life transition into a `carrier` form with **objective weight 0**
and a degraded gun, and delivering it to the team's own home pad (another
same-life transition, legal only there) pays a substantial one-time reward —
an instant slot readiness, a cleared route cooldown, a chunk of capture
progress. If the carrier dies the relic drops and returns to neutral after a
delay. The carrier is fully public: enemies already read `FormId` on every
visible body, so "they have it" needs no new fact at all.

**Dilemma.** Carrying is the turret bargain applied to *movement* — you are
still alive, still shooting, still in the way, and worth nothing to the score
while you do it. The escort problem is the richest team shape available: the
carrier needs cover, cover means bodies, and bodies at the escort are bodies off
the front.

**Classes.** Bulwark carries (5 HP survives the run); striker intercepts (the
longest gun in the game against a body that must follow a known route);
fabricator escorts (bodies are its whole identity). That is a clean three-way
role split that emerges from the stats rather than being authored — the best
class interaction on this list. The shell is a genuine escort tool (deflect the
interceptor), the volley a genuine ambush tool.

**Map/tags.** A relic region and the existing home-pad regions as delivery
sites; both `RegionKind.TransitionPlacement` with tag-gated transition legality,
which is exactly what `requiredTileTags` / `forbiddenTileTags` already express
on same-life routes.

**Observation.** Two fields: relic position and holder (or a single tagged
"neutral at tile T / carried by actor A / dropped at tile T" state).

**Reward channel.** Unit economy or tempo — **not** score (L2), or the escort
becomes the match.

**Cost.** **M/L** — the carrier form, both transitions, and the drop-on-death
rule are all existing shapes, but the neutral object is a genuinely new piece of
mode state with its own lifecycle, causality events, and validator rules.

**Failure mode.** Turtling: a team takes the relic and refuses to run it, which
is a stalemate the front cannot punish if the reward is score. The
weight-0 carrier form is the structural answer (holding it costs you the front
continuously), but that only works if the front is still where matches are
decided. Secondary risk: with no communication channel, an escort that fails to
form leaves a lone carrier feeding kills — though ally state is a complete
shared union, so a competent author *can* derive escort duty as common
knowledge (#179: coordination is solvable from the frozen union).

---

### 11. TITHE — spend a body for tempo

**Mechanic.** At a declared altar region a body may retire itself deliberately,
and the team is paid immediately in front tempo: capture progress on the active
objective, or the enemy's live ratchet hold cleared, or a slot readied instantly.
The retirement is a lifecycle fact with its own reason code, not a destruction —
no Kill, no Death, exactly as replication retirement already works. It is the
only concept that converts the game's most abundant resource (bodies, which
respawn free) into its scarcest (tempo at the front).

**Dilemma.** Genuinely novel: you are choosing to be down a body for 18–30 ticks
in exchange for closing a capture *now*. That is a real read on the match state
— worth it at 13/15 progress with the enemy inbound, insane at tick 40.

**Classes.** Fabricator can afford it (four bodies, 22-tick rebuild); striker
cannot (the body it would spend is the one holding the lane). Points the wrong
way on the ladder (L5), and the mitigation is to price the tithe on the *body's
health* so a nearly-dead body is the cheap sacrifice — which makes it a salvage
mechanic rather than a suicide mechanic and helps the class that dies most.

**Map/tags.** A small altar region near the centre corridor so the sacrifice is
made under fire.

**Observation.** None for the mechanic itself (the altar is contract data, the
retirement is a published lifecycle event, the progress is already published) —
one field if the payout has a cooldown.

**Reward channel.** Bodies → tempo.

**Cost.** **L** — this is a new lifecycle action family or a typed
"self-retirement" transition; the replication family retires a source but always
produces bounded descendants, so zero-output retirement is a new shape with new
validator rules and a new causal fact.

**Failure mode.** Two opposite ones, both bad. Degenerate: if the exchange rate
is generous, the optimal line is a body-feeding loop and matches become a
respawn conveyor — watchable only as absurdity. Inert: if it is stingy, no bot
ever writes the code path (the turret's 0.13% precedent). And there is a
watchability objection with no fix: bots deliberately deleting their own bodies
looks like a bug to a viewer, whatever the scoreboard says.

---

## 5. The honest option: do nothing yet

There is a real case for shipping none of this in the next window, and it is
stronger than it looks.

**The class triangle is broken right now, and every concept above makes it
worse.** #179: *"the striker loses to both classes and it is the class, not the
bots"* — three inventive striker lineages across two waves could not close it.
#183's swell read is the first movement of the whole campaign (+1.000 → +0.852
and +0.778) and it is explicitly a **stale-doctrine lower bound**. Side
objectives are body-attendance mechanics (L5), and the striker is the worst
body the game has. Introducing a new allocation axis before the classes are in
band means measuring a mechanic through a distortion the lab already knows
about.

**The salvo has never been priced.** #182/#183 shipped damage-2 fan bolts, a
1-tick cooldown floor, a 1-tick entry, and an 8-tick route clock — untuned, by
the dump-then-tune doctrine — and the only reads so far are on doctrine authored
when the fan was correctly declined. Wave 7 exists to price that package. A side
objective landing in the same wave confounds it, and #174's fast-iteration rule
("batch, check at mains level") does not extend to two unrelated axes at once.

**Two whole tuning surfaces are already parked, unbuilt, and cheaper.**
Reclaim economics — the owner's own note that *"2x on the recapture is a bit
excessive"* — is four registered single-lever arms (erode multiplier, partial
credit flip, shorter hold, plain numbers) against a mechanic that fires in every
single match. And granular per-skill tile classes (#176) is the *substrate* every
concept in §4 wants: build tile classes first and RELAY, KILN, PYRE, and SALLY
GATE all become map edits instead of engine work.

**The bots are nowhere near the current ceiling.** #179 closed an entire
out-of-band class edge (+0.611 → +0.333) with **zero mechanical change** —
coordination alone. Nobody has yet written a bot that plays the ratchet hold
well, that routes on route-cooldown clocks, that uses forward-rally geometry
deliberately, or that reads `holdOwnerTeamId` to price a push. The depth is
already in the box and unmined; adding more before the population reaches the
existing ceiling risks measuring authorship, not design.

**The counter-argument, which I think wins on the owner's own criteria.** All
of the above is a *balance* argument, and #173 already ruled that the skills are
in the game for entertainment and depth and that the measurement program's job
is to make them land well, not to decide whether they exist. The same override
applies here. The map is 60% dead space and the pendulum is a diagonal through
the middle of it; that is a fun problem, not a balance problem, and it does not
get better by waiting. The synthesis is: **build §3's capability, ship exactly
one effect behind an arm, keep it class-neutral, and hold it out of the wave
that prices the salvo.**

---

## 6. Comparison

| # | Concept | Reward channel | New published facts | Cost | Opens the map? | Class tilt | Headline failure mode |
|---|---|---|---|---|---|---|---|
| 1 | RELAY | score (territorial) | 2 (shared) | S | weak | bulwark/fab | wins by timeout without fighting (L2) |
| 2 | MUSTER | respawn geometry | 1 (shared) | M | yes | striker-positive | snowball; cheapens deaths |
| 3 | BATTERY | enemy capture economics | 1 (shared) | S/M | yes | neutral | invisible; narrow tuning band |
| 4 | KILN | route cooldown | **0** | S | mild | striker-keyed | inert without a striker; un-prices the salvo |
| 5 | BEACON | vision | 1 + observer-identity schema | M/L | yes | mild striker | hidden info; nothing to watch |
| 6 | DROP | unit economy | 2 | M | yes (roaming) | fabricator | reads as a coin flip |
| 7 | SPUR | map routing | 1 (breaks existing doctrine) | M engine / L cohort | yes | strong, per-class | one branch is dead unless perfectly balanced |
| 8 | SALLY GATE | geometry | **0** (scheduled) / 1 (lever) | M | **most literally** | anti-bulwark | bigger map = slower map |
| 9 | PYRE | kill → tempo | **0** | S/M | yes | bulwark (wrong way) | meat grinder off the front |
| 10 | RELIC | unit economy / tempo | 2 | M/L | yes | clean 3-way split | turtling with the relic |
| 11 | TITHE | bodies → tempo | 0–1 | L | weak | fabricator | degenerate loop or never used |
| — | do nothing | — | 0 | — | no | — | the map stays 60% dead |

---

## 7. Recommendation

**Top three by depth per implementation cost.**

1. **MUSTER (the rally flag).** It buys the largest strategic swing for the
   smallest new machinery: `FrontlineForwardRallyPlacement` is already built,
   measured, and part of `keel`, so the work is making one existing policy
   owner-dependent and publishing one owner — and in exchange every death in the
   match gets a price that the players set themselves.
2. **BATTERY (remote support fire).** One branch in the capture arithmetic and
   one published owner buys the only mechanic on the list where an absent body
   keeps changing the front continuously, which forces the "did they leave?"
   read every single tick — and it is class-neutral, which the current ladder
   badly needs.
3. **KILN (the re-arming shrine).** It costs a region and a per-tick clock
   effect, needs **zero** new observation fields, and it is the only concept
   here aimed squarely at the campaign's open problem — the striker chassis —
   which makes it the cheapest thing on this page that could also matter to
   balance.

(PYRE is the honourable fourth: zero observation cost and it attacks the "kills
do not convert" diagnosis directly, but it rewards the class already at the top.
RELIC has the highest ceiling of anything here and is the natural second wave
once the family's plumbing exists.)

**The one I would prototype first: MUSTER**, as
`--side-objective muster` on top of the working game, with the shared
`FrontlineSecondaryControlDefinition` capability (§3) built underneath it so the
other effects are enum values afterward rather than new projects.

Why this one, concretely:

- It is the owner's roadmap note executed literally — a second, lesser objective
  that opens the map and adds an allocation decision — while paying in tempo
  rather than score, so it cannot win a match by camping (L2).
- Its reward is **flat per team** (one rally location, however many bodies you
  have), which is the L5 mitigation built into the mechanic instead of bolted on.
- Scoped to the Prime's 18-tick automatic return, it helps the class that dies
  most and is not amplified by the fabricator's fourth slot.
- It is **watchable**, which the owner's criteria demand and BATTERY fails:
  bodies visibly popping in beside the fight versus visibly trudging from home
  is a viewer beat, and the flag itself is a place things happen.
- It ships rough and tunes by numbers afterward — latch duration, capture
  threshold, site distance, and whether children rally too are all single levers,
  which is exactly the dump-then-tune shape (#173/#174).
- Placement goes in the dead north/south lanes on the x=11 centre line (§2),
  **with the alcoves widened to two approach headings first** — a 1-wide
  cul-de-sac plus an AEGIS SHELL is an unflankable fortress, and that map edit
  is itself the map-as-tuning-surface direction of #176.

Pre-registration notes for whoever builds it: register the mechanism factors
separately in `balance/frontline-ablation-debt-v1.json` (site placement, latch
duration, and rally scope are three levers and will be unattributable bundled);
carry RELAY as the registered control arm so the family's L2 hypothesis is
measured rather than asserted; and read the class edges, not only the pacing
gates, before adoption — a side objective that improves cap share while pushing
bulwark-vs-striker further above the band is a failure with good-looking
numbers.
