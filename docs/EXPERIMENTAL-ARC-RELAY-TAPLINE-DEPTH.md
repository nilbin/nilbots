# Arc Relay Tapline depth pack — implementation brief (owner-set 2026-08-14)

Status: **historical implementation brief**. The first six mechanics shipped
as Tapline-4 under Decision #316. Destination-facing and historical evidence
shipped directly into the current Orders v2 product language under Decision
#317; they have no experimental loop profile or alternate rules identity. The
delayed centre opening later promoted as Tapline-5 and hosted playlist v23
under Decision #319.

## Goal

Create more durable, visible strategic identities without bringing back a
second top-level play/state-machine grammar. The candidate pack contains exactly
eight ideas:

1. team-relative `1 / 2 / 4` Core charge for own / centre / enemy Wells;
2. veteran-centred "ace and escort" strategies through narrow Orders v2
   additions;
3. synchronized public respawn waves;
4. one duplicate-class wildcard in each eight-body roster;
5. persistent deployment control, led by a targetable three-baseline-hit,
   long-duration Mason barrier that is replaced when its owner deploys another;
6. a tenfold integer combat scale with proportionately larger hull, damage,
   repair, and vitality values so ordinary upgrades and future weak effects
   have useful tuning room;
7. destination-only facing toward an authored anchor after a `move.to` reaches
   an authored zone or anchor; and
8. finite, read-only historical evidence facts so Orders can react to prior
   team observations without writable variables or another state machine.

Sections 7 and 8 close the current Orders-expansion scope. Do not add a runtime
Plays system, shared mutable flags, authored counters, phases, calls, or a
second command grammar. Reusable authoring convenience may later compile to
ordinary Orders, but it must not acquire independent runtime semantics.

These are **candidate changes, not one pre-approved bundle**. Implement each
behind independently selectable, canonically serialized rules/grammar fields.
First prove each arm against unchanged Tapline-3; only then expose a combined
`depth-pack` arm. Do not use a bundled result to claim which individual change
helped.

## Experiment identities

At minimum, retain these distinguishable study cells:

| Cell | Difference from Tapline-3 |
| --- | --- |
| control | none |
| score-124 | origin-valued Core charge and sufficient Well supply only |
| veteran-command | veteran Orders affordances only |
| respawn-wave | synchronized respawns only |
| roster-wildcard | roster diversity rule only |
| durable-deployments | deployment fact and Mason barrier changes only |
| combat-scale-10 | 30 / 40 / 50 hulls, damage 10, revised veteran damage ordering, and proportionately rescaled secondary quantities |
| durability-plus-10 | `combat-scale-10` plus 40 / 50 / 60 hulls; no other difference |
| destination-facing | destination-only `face` on authored zone/anchor `move.to` orders only |
| historical-evidence | finite team-observation history facts only; no writable sheet memory |
| depth-pack | all eight using `combat-scale-10`, after the isolated cells execute correctly |

Exact registered IDs should follow the existing immutable loop-profile and
ruleset naming conventions. Every replay and receipt must identify the exact
cell; no candidate may masquerade as `tapline-3`.

## 1. Team-relative 1 / 2 / 4 Core charge

### Rule

A Core's bank value is determined by its source Well **relative to the banking
team**, not by an absolute north/south label:

| Source relative to scorer | Charge |
| --- | ---: |
| own/near Well | 1 |
| centre Well | 2 |
| enemy/far Well | 4 |

The same outer Core is therefore worth 1 to the nearby team and 4 if the other
team steals it and completes the long return. Centre is worth 2 to both teams.
The value is settled at bank time and never depends on which team first picked
the Core up.

Keep the existing objective identity:

- 3 charge produces one Pulse;
- 3 Pulses destroy the opposing reactor and win;
- charge above a threshold carries into the next Pulse;
- resolve **every** threshold crossed by one bank immediately and emit one
  ordered Pulse event for each.

Equivalently, for a non-terminal bank:

```text
available = previousCharge + bankValue
pulsesEarned = available / 3
remainingCharge = available % 3
```

Stop when the third Pulse wins; never decrement reactor integrity below zero.
This makes total charge order-independent:

- `4` -> one Pulse plus 1 charge;
- `4 + 4` -> two Pulses plus 2 charge;
- `4 + 4 + 1` -> win;
- `2 + 4` -> two Pulses immediately;
- `2 + 4 + 1 + 1 + 1` -> win;
- `2 + 2 + 2 + 2 + 1` -> win;
- nine 1-value banks -> win.

The current bank path awards at most one Pulse per bank. That is not acceptable
for this arm: for example, a 4-value bank onto 2 stored charge must award two
Pulses rather than leave a full-but-unresolved 3 charge.

### Supply needed for the safe strategy

Tapline-3 currently schedules only seven birth rounds per Well, so nine own-Well
banks cannot occur. The initial score-124 prototype uses:

- first global beat: 25 (unchanged);
- Well cadence: 65 ticks;
- scheduled rounds: 9;
- birth jitter: +/-6 ticks (unchanged);
- match maximum: 600 ticks (unchanged).

The ninth outer-Well beat is nominally tick 570 and remains inside the match at
maximum positive jitter. Thus the nine-safe-Core route is possible but slow,
while centre control and deep raids exchange safety for speed.

### Information and presentation

- The public contract declares the three team-relative values.
- `CoreBanked` evidence exposes the actual charge awarded.
- Post-bank reactor state always has charge `0..2`; Pulse count remains public.
- Add an Orders fact for the team's own scored Pulse count (for example
  `reactor-pulses`); `reactor-charge` retains its existing remainder meaning.
- Add body fact `self-carried-core-bank-value`, returning `0` while empty-handed
  and otherwise the carried Core's `1`, `2`, or `4` value to this team.
- Add team fact `enemy-reactor-charge` for the enemy's public `0..2` remainder;
  existing `reactor-integrity` already reveals how many Pulses the enemy has
  scored.
- Add move target `highest-bank-value-known-loose-core`. Resolve the highest
  value to this team, then terrain distance, then stable Core ID, using only
  the existing team objective ledger. An unknown Core is never a candidate.
- Player-oriented views label Wells `1`, `2`, and `4`. A neutral spectator view
  labels the outer Wells `Blue 1 / Red 4` and `Blue 4 / Red 1`, with `2 / 2`
  at centre.
- Before a bank, the viewer must make the resulting Pulse count and carried
  remainder understandable, including a possible two-Pulse bank.

### Why this should be deeper and more fun

Safe logistics, centre control, and a defended deep raid become genuinely
different paths to the same victory. Score state changes route value during a
match: a 4-value Core can become a comeback attempt or immediate finishing
threat, while a one-point Core remains a reliable closeout. This creates
defence, feints, escorts, and route switching without forbidding any origin.

### Main risks

- A successful far-Well raid may shorten a match too sharply.
- Centre may remain the reliable default despite the far-Well reward.
- A two-Pulse bank may be exciting or may feel like an unexplained burst.
- Faster supply can create loose-Core clutter if sheets cannot settle Wells.

Do not dilute `1 / 2 / 4` before viewing it. If pacing fails, compare target or
supply tuning in a separately named arm rather than silently changing this one.

## 2. Veteran-centred team strategy

### Preserve the existing veteran economy

Do not buff veterancy numbers in the first arm. Keep:

- level 1 start, 2 XP per level, maximum level 5;
- one ordinary kill XP plus the existing victim-level bounty;
- damage, vision, reach, and vitality upgrade paths;
- a quiet action tick to invest;
- complete reset on death.

Those values are retained specifically in the isolated `veteran-command` arm.
Section 6 versions damage and vitality only in `combat-scale-10`; do not
smuggle that numerical change into the veteran Orders-affordance experiment.

The reset and bounty are the built-in counterweight to an ace: protecting a
veteran has value, while killing that veteran both removes the investment and
accelerates the hunter.

### Orders v2 additions

Keep the current role priority tree, shared orders, capacities, branch
lifecycle, and `follow` behavior. Add only these general-purpose primitives:

1. `role-highest-level`: a role-scoped integer fact returning the highest level
   among its live bodies, or 0 when none are live. It is exact for a singleton
   `ace` role and unambiguous for every other role.
2. A friendly unit target `{ "role": "ace" }` for `use`. Resolve the nearest
   legal live body in the role by terrain distance, then stable unit ID. This
   lets Repair Beam and future friendly-target skills deliberately support the
   authored ace instead of merely selecting the globally weakest ally.
3. `{ "selector": "most-experienced-ally" }` for `follow` and compatible
   friendly `use` actions. It is unresolved while every eligible ally is level
   1, otherwise selects highest level, then terrain distance, then stable unit
   ID. Levels change only at explicit kill events, preventing tick-to-tick
   target noise.
4. Hostile target `veteran` for `fight` and compatible enemy-targeted `use`
   actions. It is unresolved unless a visible enemy is above level 1; otherwise
   select highest level, then reachable terrain distance, then stable unit ID.
   An unavailable veteran preference falls through to the next authored target
   exactly like `carrier` or `marked`.

Existing syntax remains valid and byte-identical. Do not add `plays`,
formations, calls, phases, operation records, XP transfer, kill reservation,
or a hidden leader state.

### Required strategy demonstrations

- **Authored ace:** one singleton `ace` changes behavior with `self-level`;
  screeners follow it and a Patchbay can repair that role specifically.
- **Emergent ace:** supporters fall back normally at level 1, then rally around
  whichever eligible ally first becomes the most experienced.
- **Bounty counter:** the opponent can prioritize, isolate, and kill the ace;
  support returns to authored fallback after the death/reset.
- **Objective ace:** a veteran may become the carrier or escort for a 4-value
  raid, but carrying still blocks a pending investment exactly as today.

Every selection and fallback must appear in the existing debug/explain trace.

### Why this should be deeper and more fun

One body can acquire a match-specific identity that changes both teams' plans:
feed and protect our ace, pressure it before investment, or collect its bounty.
Because death erases the build, the veteran is a visible strategic asset rather
than permanent score-to-power snowballing. The mechanic also makes meaningful
use of `upgradePath` and `self-level`, which the retained Tapline Orders sheets
currently do not exploit tactically.

### Main risks

- Supports may overcommit to an ace while objectives are unattended.
- Stable tie-breaking can make unit ID look like a hidden preference.
- Repair plus vitality may make a protected veteran too hard to finish.
- Last-hit XP may make an intended funnel unreliable; do not add assist XP
  without a separate design decision.

## 3. Synchronized respawn waves

### Rule

Replace each body's fixed 30-tick respawn delay in this arm with one global,
public wave clock shared by both teams:

- wave period: 30 ticks, anchored at ticks `30, 60, 90, ...`;
- minimum dead time: 15 complete ticks;
- a destruction on tick `D` is eligible no earlier than `D + 1 + 15`;
- its due tick is the first global wave at or after that eligibility tick;
- every eligible stable slot respawns together and may act on that wave tick.

This yields 15..44 complete dead ticks (mean 29.5), nearly preserving the
current average while grouping reinforcements. Both teams use the same phase;
no seed, side, or first-death offset changes it.

Expose the next wave tick/countdown in the public state and viewer. Also add
the general-purpose Orders team facts `ticks-until-respawn-wave`,
`live-enemies`, `friendly-respawns-next-wave`, and
`enemy-respawns-next-wave`. The two respawn counts include dead stable slots
eligible at the next global wave; every value is derived from public lifecycle
state. A body that misses the eligibility cutoff by one tick visibly waits for
the following wave. Veterancy resets at destruction as today, not at the later
wave.

### Why this should be deeper and more fun

Deaths create readable regrouping windows instead of a continuous trickle.
Teams can time a push, retreat for the next wave, exploit a temporary numbers
advantage, or synchronize an escort. Spectators can understand when pressure
will arrive.

### Main risks

- The one-tick cutoff can create a 30-tick difference in downtime.
- Full waves may create repetitive deathballs or overwhelm a reactor defence.
- Burst kills may become more decisive when several opponents share one long
  absence.

Boundary tests must cover destruction immediately before, on, and after every
eligibility cutoff; simultaneous cross-team waves; occupied spawn traffic; and
deterministic replay round-tripping.

## 4. One duplicate-class wildcard

### Rule

An eight-body candidate roster must contain at least seven distinct class IDs:

- exactly eight bodies total;
- no class more than twice;
- at most one class ID may be duplicated;
- equivalently, `distinctClassCount >= 7`.

There is no separate wildcard token. The duplicated class is simply whichever
single pair the author includes. `Mason, Mason` is legal; `Mason, Mason,
Patchbay, Patchbay` and any triple are illegal.

Keep the Orders v2 compiler capable of reading historical max-two sheets.
Enforce the new minimum-diversity rule through the candidate mode/playlist
admission contract and candidate editor validation so existing Tapline sheets
and replay history remain readable. If promoted, mint a new playlist; do not
retroactively invalidate v21.

### Why this should be deeper and more fun

Seven classes force broad tool coverage while one pair still lets a sheet make
a memorable strategic statement such as double Mason fortification. A strict
max-one rule would make the eight-class starter roster a forced composition and
remove that identity; the wildcard preserves one concentration choice and its
opportunity cost.

### Main risks

- One duplicated utility class may still become mandatory.
- Several retained strategies currently use only five or six distinct classes,
  so candidate-native sheets must be adapted rather than treated as balance
  voters unchanged.

Acceptance cases must include eight distinct starters, every possible legal
single pair, two different pairs, a triple, locked-class admission, and exact
historical-profile behavior.

## 5. Deployment activity and Mason's barrier

### What “hardlight” means

“Hardlight” is science-fiction shorthand for projected light or energy made
solid enough to behave like a physical object. In Nilbots it means Mason
projects a temporary blocking tile. Keep the stable IDs `hardlight-block` and
Mason in this prototype, but player-facing copy should explain it plainly:

> Projects a solid 3-health barrier. It blocks movement and shots until
> destroyed, expired, or replaced by Mason's next barrier.

That copy is exact for the isolated `durable-deployments` cell. When combined
with Section 6's ten-damage combat scale, display 30 health so the barrier
keeps the same three-baseline-hit meaning.

A later owner decision may rename the display label to “Projected Barrier” or
“Barrier Block”; this brief does not require an identity migration.

### Existing behavior to preserve explicitly

The current Hardlight Block already has capacity/hull 3, lasts 12 ticks, has a
14-tick cooldown, consumes hostile projectile contacts, and removes an older
same-owner construct when a replacement is created. The prototype must not
mistake that hidden implementation for a completed player-facing mechanic.

### Candidate Mason rule

- maximum health: 3 in `durable-deployments`, or 30 when
  `combat-scale-10` is also active;
- duration: 30 ticks;
- cooldown: 14 ticks (unchanged);
- blocks ground movement and consumes the shot that destroys it;
- remains until health reaches zero, duration ends, or its stable Mason slot
  successfully deploys a replacement;
- a replacement removes the old barrier atomically with reason `replaced`
  before the new one becomes active;
- one active Hardlight Block per stable Mason slot, including across a death
  and respawn. A surviving old-life barrier therefore occupies the new life's
  deployment slot and is replaced if that Mason casts again.

Treat health as damage, not merely an unexplained contact counter. Ordinary
shots deal their declared damage plus the shooter's applicable veterancy damage
upgrade; a barrier has no facing and receives no rear-arc or target-paint
multiplier. A projectile/strike stops on contact even when that contact destroys
the barrier. Route all damaging signatures through an explicit, tested policy
rather than letting some destroy the barrier accidentally and others ignore it
by implementation happenstance.

Visible hostile deployments must be deliberately attackable. Add a narrow
`deployment` fight target that selects a visible hostile deployment with a
legal firing line, then distance and stable operation ID. It may resolve to a
suppressive heading/position rather than pretending a construct is an actor.
The ordinary authored target priority decides whether clearing it outranks an
enemy body.

### Deployment Orders fact

Add body fact `deployment-active`, integer `0 | 1`, for the durable construct
family already governed by replacement semantics: Prism Wall, Trip Node,
Hardlight Block, and Sentinel Seed.

Add `deployment-lifetime-percent` and `deployment-durability-percent` beside
it. Both are scoped to the stable slot's one extant deployment, normalized to
`0..100`, and return 0 when none exists. Lifetime is remaining scheduled
duration; a non-expiring active Trip Node returns 100. Durability is remaining
hull for Trip Node, Hardlight Block, and Sentinel Seed, and remaining
projectile-contact capacity for Prism Wall. Suppression leaves all three facts
extant and does not pause the lifetime clock.

For this fact, active means an extant deployment owned by the body's stable
team/unit slot, including an extant temporarily suppressed deployment. It
becomes 0 on destruction, trigger/removal, expiry, or permanent cancellation.
This definition lets an author distinguish:

- initial placement: `signature-ready` and `deployment-active == 0`;
- deliberate relocation/recast: `signature-ready` and
  `deployment-active == 1`;
- ordinary work while the existing deployment holds.

Do not silently prevent recasting. The sheet chooses whether an active
deployment should be preserved or relocated; the rules guarantee only the
one-per-slot replacement invariant.

Public observation, replay, viewer, and explain/debug output must expose owner,
kind, tile(s), current/max health where applicable, expiry, suppression, and
replacement/destruction reason. A health bar or equally legible
three-baseline-hit damage read is required for the Mason barrier.

### Why this should be deeper and more fun

A long-lived barrier makes lane ownership and planned fortification persist
long enough for both teams to react. Requiring three baseline hits creates real
dismantling counterplay, while replacement on recast prevents accumulation and
makes relocation a meaningful choice. `deployment-active` lets sheets express
that choice without a new play framework.

### Main risks

- A 30-tick barrier may seal a narrow route too cheaply.
- A sheet that recasts every cooldown receives little benefit from the longer
  duration and may churn visual state.
- Veteran damage may make three baseline hits trivial; no intentional target
  may make the barrier oppressive. Both cases need replay inspection.
- Stable-slot persistence through death can create confusing ownership unless
  the viewer keeps the team/Mason relationship visible.

## 6. Tenfold combat scale and durability resolution

### Why tenfold rather than twofold

The owner proposal solves a real integer-resolution problem. At current damage
1, the smallest possible `+1` upgrade is a 100% increase. A twofold scale would
make ordinary damage 2 and `+1` a healthier 50% increase, but it would still
leave only one non-zero damage value below a normal hit. A tenfold integer
scale leaves values 1 through 9 for future chip damage, weak deployables,
damage-over-time, armor leakage, and partial repair without introducing
fractions or another later rescale.

Tenfold is a unit conversion, not permission to multiply every game number.
Scale only health-like, damage-like, and repair-like magnitudes. Ranges,
durations, cooldowns, movement, contact counts, Core charge, XP, levels, and
respawn timing keep their current units.

Doubling or multiplying both hull and ordinary attack damage does **not** by
itself make baseline combat longer. Treat these as two named questions:

1. `combat-scale-10` asks whether larger integers make veteran damage and
   vitality fairer while preserving ordinary Tapline tempo.
2. `durability-plus-10` asks whether every chassis should then require one more
   baseline hit.

Never use `durability-plus-10` as evidence for `combat-scale-10`, and do not
place the extra durability into the first combined `depth-pack` arm without a
later owner selection.

### Breakpoint math

The governing breakpoint is:

```text
hitsToKill = ceil(maxHealth / damagePerHit)
```

Tapline currently has 3 / 4 / 5 hull, ordinary damage 1, an effective veteran
front-damage progression of 1 -> 2 -> 3, and short / medium / long firing
intervals of 2 / 3 / 5 ticks. The tenfold candidate uses 30 / 40 / 50 hull,
ordinary damage 10, and veteran front damage 10 -> 15 -> 20:

| Hull tier | Current hits at damage 1 / 2 / 3 | `combat-scale-10` hits at damage 10 / 15 / 20 |
| --- | ---: | ---: |
| squishy: 3 -> 30 | 3 / 2 / 1 | 3 / 2 / 2 |
| medium: 4 -> 40 | 4 / 2 / 2 | 4 / 3 / 2 |
| heavy: 5 -> 50 | 5 / 3 / 2 | 5 / 4 / 3 |

Baseline hits therefore stay exactly 3 / 4 / 5. The first damage point still
changes a squishy from three hits to two, but it no longer removes two whole
hits from medium and heavy bodies. Each extra required hit buys roughly another
2 / 3 / 5 ticks of uninterrupted exposure against the corresponding gun.

Do not keep the upgrade at `+1` merely because the new scale permits it. That
would turn a formerly decisive investment into a 10% increase that rarely
crosses a breakpoint. The initial candidate prices the intended half-hit step
explicitly at `+5`; values 1..4 remain available for genuinely weaker future
effects.

### Candidate chassis, gun, and upgrade values

`combat-scale-10` authors these resolved form values:

| Tier | Classes | Maximum health |
| --- | --- | ---: |
| squishy | Kestrel, Lantern, Mortar, Switchback, Longshot, Veil | 30 |
| medium | Towline, Patchbay, Minesmith, Hush, Relay, Sunder, Nest | 40 |
| heavy | Palisade, Mason, Repulsor | 50 |

Every ordinary class gun starts at damage 10. Keep its range, 1-tick strike
windup, 2 / 3 / 5 firing interval, aim rules, and every movement value
unchanged.

One damage investment adds exactly 5 ordinary damage, with an effective bonus
cap of +10: front damage progresses 10 -> 15 -> 20. Further points must not
make the displayed or applied damage exceed 20. Repeated terminal `damage`
entries in an Orders upgrade path require explicit tests so a capped track
cannot create a hidden difference between displayed state, action legality,
and applied damage.

One vitality investment adds 10 maximum health to the investing body's current
life. It retains the existing no-rescue rule: buying vitality raises the
ceiling but never heals the body that is already standing; repair or a heal
zone must fill the new headroom. Death still resets every veterancy track.
Repeated vitality investments remain additive.

### Positional damage ordering

The legacy calculation adds veteran damage before the rear-arc multiplier. On
the proposed scale, the first damage point would therefore turn a rear hit from
20 into 30 and immediately one-shot a 30-health squishy. That preserves the
most explosive part of the current upgrade and defeats the purpose of the
scale.

In `combat-scale-10`, calculate ordinary projectile damage in this order:

```text
positionalDamage = baseDamage * (rearArc ? 2 : 1)
veteranBonus = min(damagePoints * 5, 10)
finalDamage = positionalDamage + veteranBonus + targetPaintBonus
```

The veteran bonus is consequently literal +5 even from behind. Rear damage
progresses 20 -> 25 -> 30 rather than 20 -> 30 -> 40. A first-rank rear hit
leaves a fresh 30-health squishy on 5 health; the second damage rank earns the
one-shot. Target Paint remains after rear and veteran calculation.

### Rescale every health-like and damage-like quantity

Do not enlarge bodies and guns while quietly shrinking the relative value of
signatures, repairs, or deployments. `combat-scale-10` carries this complete
magnitude table:

| Quantity | Tapline-3 | `combat-scale-10` |
| --- | ---: | ---: |
| ordinary gun damage | 1 | 10 |
| Falling Star damage | 1 | 10 |
| Trip Node hull / trigger damage | 1 / 2 | 10 / 20 |
| Rail Line damage | 2 | 20 |
| Sentinel hull / bolt damage | 2 / 1 | 20 / 10 |
| Repair Beam hull per repair / activation cap | 1 / 2 | 10 / 20 |
| heal-zone recovery per 3 waited ticks | 1 | 10 |
| Target Paint bonus damage | 1 | 10 |
| Hardlight Block hull | 3 | 30 |
| vitality health per point | 1 | 10 |

Prism Wall's projectile-contact capacity is counted in contacts rather than
health, so it stays 3. Do not scale ranges, durations, cooldowns, movement,
Core values, XP thresholds, level thresholds, respawn time, or number of
enhanced Target Paint hits. A value intentionally left unscaled later is a
separate balance arm, not an omission in this one.

Author the resolved values in the immutable rules contract. Do not implement a
viewer-only or implicit global multiplier. Version any newly variable mode
semantics needed for vitality-per-point, maximum veteran damage bonus,
post-positional veteran ordering, and heal amount; legacy defaults must retain
Tapline-3's exact bytes and behavior.

### Optional actual-durability arm

`durability-plus-10` changes only the three `combat-scale-10` body hulls from
30 / 40 / 50 to 40 / 50 / 60. Damage and all other scaled quantities stay the
same. Against baseline damage 10 this produces 4 / 5 / 6 hits to kill: exactly
one more ordinary hit than current Tapline for every chassis tier.

Use 40 / 50 / 60 rather than the lower ends of those same hit-count bands. The
higher values produce smoother upgraded-damage breakpoints: damage 15 requires
3 / 4 / 4 hits respectively. They also make two first-rank damage attackers
deal 30 and leave a fresh durability-arm squishy alive on 10, whereas they
exactly kill a 30-health squishy in the pure scale arm.

### Why this should be deeper and more fun

Larger base numbers make damage 1..4 available for subtle new class effects
while keeping the ordinary `+5` veteran investment meaningful rather than
binary. Medium and heavy bodies receive time to turn, retreat, hand off a Core,
receive repair, or let escorts intervene after a veteran begins firing. +10
vitality remains proportionate and can visibly support an ace build without
making ordinary level-1 combat slower in the pure scale arm.

The two cells also answer the owner question honestly: `combat-scale-10` tests
upgrade fairness and future tuning resolution; `durability-plus-10` tests
longer fights. Their separation keeps a fun result attributable.

### Main risks

- Pure `combat-scale-10` does not make level-1 duels longer, and two first-rank
  attackers can still synchronously delete its 30-health squishy.
- `durability-plus-10` may reduce kills, veteran activation, Core drops, and
  respawn-wave opportunities while increasing MaxTicks finishes.
- Repair plus repeated vitality can make a protected ace or centre deathball
  too difficult to finish; do not pre-nerf it, but measure it explicitly.
- One extra required Longshot hit costs five firing ticks, so added hull does
  not affect all gun cadences equally.
- A single forgotten fixed-damage, repair, or deployment value silently
  changes a class's role.
- Larger raw numbers can create false complexity. Player views should lead with
  a readable proportional health bar and show exact current/maximum numbers on
  inspection.
- Repeated vitality can raise candidate lives as high as 90 health in
  `combat-scale-10` or 100 in `durability-plus-10`; replace tiny per-health pips
  with legible segments before judging the mechanic visually.

## 7. Destination-only facing toward an anchor

### Candidate grammar

Add one optional `face` member to the existing `move` leaf:

```json
{
  "move": {
    "to": { "anchor": "west-ambush-post" },
    "face": { "anchor": "open-lane-mouth" }
  }
}
```

`face` names exactly one existing authored anchor. It is legal only when the
same move uses `to` with an authored `{ "anchor": id }` or
`{ "zone": id }` destination. Reject it on `patrol`, `follow`, Wells,
reactors, loose-Core selectors, and every other dynamic destination. A `via`
route may still lead to a legal zone/anchor destination, but facing has no
effect anywhere on that route.

This is an end-of-move instruction, not a movement stance:

1. Until the body reaches its resolved destination tile, path choice, facing
   coupling, posture, traffic priority, and movement are byte-for-byte the same
   as the equivalent move without `face`.
2. At the destination, preserve the current facing when the face anchor is
   already inside the body's bare 90-degree facing quadrant. Otherwise submit
   one legal left/right rotation toward the nearest cardinal facing whose
   quadrant contains the anchor. Exact diagonal edges are valid for either
   adjacent quadrant; preserving an already-valid facing wins, otherwise the
   shorter rotation wins.
3. If the body and face anchor occupy the same tile, preserve facing and hold;
   never rotate indefinitely or make the move unavailable.
4. Rotation consumes the body's ordinary action tick. Once correctly faced,
   the ongoing `move.to` holds at destination exactly as it does today.
5. Displacement makes returning to the destination take precedence over
   re-facing. A higher-priority fight, use, handoff, drop, or survival order
   may pre-empt the move normally. When the move resumes at its destination,
   it re-establishes the authored facing.
6. Walls and smoke do not invalidate the directional intent. `face` points the
   body toward the anchor; it does not promise that the anchor is currently
   visible or that a shot has line of sight.

Emit an explicit reason such as `at-target:face-anchor:<id>` for a submitted
rotation and `at-target:facing-anchor:<id>` for the resulting hold. Replay and
viewer intent must show both the destination and the face anchor so an author
can distinguish pathing failure, rotation, occlusion, and successful overwatch.

Do not maintain facing while travelling, add strafing policy, infer facing from
the next order, or extend this first arm to patrol/follow. Those are different
planner problems and are deliberately deferred.

### Why this should be deeper and more fun

Authored facing makes overwatch, crossfires, rear guards, deliberate team-vision
coverage, and readable ambush posts possible. A Mason can block one route while
allies at fixed posts watch the remaining lane, yet the composition remains an
ordinary priority tree of move, fight, and use orders. Opponents can answer by
destroying the barrier, approaching from a blind side, using smoke, baiting the
trigger, or forcing the watchers off their destinations.

### Main risks

- Facing may twitch on diagonal boundaries unless the preserve-current rule is
  exact and tested under rotation symmetry.
- Re-facing after every harmless displacement may cost too many action ticks.
- Authors may mistake pointing toward an occluded anchor for guaranteed vision.
- Too many perfectly tiled watchers may reduce uncertainty; vision coverage
  and counterplay must remain legible in exhibits.

## 8. Match-local historical evidence facts

### Evidence, not writable sheet memory

Orders need a bounded way to ask what this team previously observed, not a way
for sheets to mutate arbitrary state. Add a finite engine-owned evidence ledger
and expose it only through ordinary atomic conditions. The ledger is:

- team-shared and match-local;
- retained across body death, respawn, and role-capacity reassignment;
- reset completely at the start of every match;
- derived only from information or events legitimately exposed to that team at
  the time, with no omniscient or retrospective backfill; and
- deterministic and replayable from the canonical observation/event record.

A sheet may name a condition over this evidence, but it may not declare a
memory cell, set or clear a flag, increment a counter, emit an event, or choose
a reset transition. Existing branch lifecycle remains the only latch:
`when` adopts a response, `until` abandons it, and `timeout`/`cooldown` bound it.

### Initial finite fact catalog

Add these integer facts rather than a generic event-query language:

| Fact | Exact evidence returned |
| --- | --- |
| `observed-enemy-entries-in-zone` | Confirmed outside-to-inside transitions for an enemy life whose preceding and resulting positions were both in this team's observation |
| `distinct-enemy-lives-observed-in-zone` | Distinct enemy life IDs actually observed in the zone during the selected interval |
| `confirmed-enemy-destructions-in-zone` | Enemy destruction events in the zone that the existing public/team event rules disclosed to this team |
| `zone-fully-observed-throughout-interval` | `1` only when every occupiable tile in the authored zone belonged to this team's immediate visible-tile union on every completed tick in the selected interval; otherwise `0` |

Every fact requires `zone` and exactly one interval qualifier:

- `windowTicks: 1..1200` reads the immediately preceding completed ticks;
- `since: "match-start"` reads the completed portion of this match; or
- `since: "node-entry"` reads from the current branch/leaf entry tick and is
  legal only in that active node's `until` expression.

At decision tick `T`, evidence stops before that tick's actions: a rolling
window of `N` reads event/observation ticks `[max(0, T-N), T)`. Node-entry
evidence begins at the tick on which that node entered and likewise ends at
`T`. Counts never include hidden movement merely because a later sighting makes
it plausible. A respawn is a new life for the distinct-life fact.

Example rolling evidence:

```json
"north-traffic": {
  "fact": "observed-enemy-entries-in-zone",
  "zone": "north-corridor-gate",
  "windowTicks": 240,
  "operator": "at-least",
  "value": 3
}
```

Example episode evidence used by an active ambush branch's `until`:

```json
"ambush-earned-three-kills": {
  "fact": "confirmed-enemy-destructions-in-zone",
  "zone": "north-killbox",
  "since": "node-entry",
  "operator": "at-least",
  "value": 3
}
```

“No enemy entered” is not a valid inference from zero sightings alone. To end
an unused ambush after 100 ticks, the author must combine all three statements:

- this node has been active for at least 100 ticks;
- `observed-enemy-entries-in-zone == 0` since node entry; and
- `zone-fully-observed-throughout-interval == 1` since node entry.

If smoke, occlusion, displacement, or poor facing interrupts coverage, the
sheet may truthfully say “no observed entry,” but not “the watched lane stayed
empty.” Small authored gate zones make continuous coverage attainable without
pretending an entire corridor was watched.

The explain trace must show the interval, count, and contributing evidence—for
example, `north-traffic=true: 4 entries by 3 lives in ticks 310..549`. An
absence condition must also show whether coverage was continuous and the first
coverage gap when it was not. The replay/viewer need not surface the full ledger
by default, but inspection must make every condition result reproducible.

### Required strategy demonstrations

- **Ambush payoff:** an active killbox exits after three disclosed enemy
  destructions in its zone, without a custom flag or play phase.
- **Unused trap:** a fully watched ambush abandons after 100 ticks with no
  confirmed entry; the same sheet does not make that claim when coverage was
  interrupted.
- **Corridor adaptation:** repeated observed northern entries activate a
  north-focused branch; a separately authored quiet-window `until` eventually
  returns the team to its baseline rather than preserving stale certainty.
- **Death continuity:** the team retains evidence when a watcher dies and a
  role capacity assigns its job to another eligible body, while a new match
  starts empty.

### Why this should be deeper and more fun

Sheets can probe, learn, adapt, and be counter-adapted within one match. Scouts
and deliberate facing create useful information, an unsuccessful ambush can be
abandoned, and repeated route usage can be punished without granting hidden
knowledge. Sliding evidence plus the existing lifecycle supports hysteresis:
authors can demand strong evidence before changing plan and different evidence
before changing back.

### Main risks

- Historical counts may look omniscient unless observation provenance is
  exact and visible.
- One enemy oscillating across a boundary can inflate an entry count; authors
  can combine entries with distinct-life evidence, and exhibits must test the
  behavior.
- Long match-start windows can preserve a conclusion after the opponent adapts;
  rolling windows should be the ordinary choice.
- Too many facts or interval forms would turn conditions into a query language.
  The initial catalog and three interval scopes are closed for this arm.
- Independently entered nodes have independent `node-entry` boundaries. Common
  team evidence can coordinate reactions, but it does not create shared mutable
  lifecycle state.

## Implementation boundaries

- Version all gameplay-affecting fields in the immutable Engine contract and
  canonical writer/reader. Defaults preserve Tapline-3 byte-for-byte.
- Keep Engine, SDK, Runtime, Guest/WASM, replay-v3, CLI summary, editor, and
  viewer mirrors consistent wherever a new public field or selector crosses
  those boundaries.
- The shared Orders compiler remains the only compiler. New syntax is additive,
  strictly validated, represented in explain output, and inert for old sheets.
- Destination facing remains a local `move.to` parameter. Historical evidence
  remains a read-only condition source. Neither may introduce Plays, authored
  variables, shared flags, operation phases, or another executor state machine.
- Do not mutate a frozen sheet, profile, playlist, artifact, map, or golden
  replay. Do not promote the experiment to `Current` or hosted admission.
- Do not refresh the tracked tactical WASM mirror merely for local iteration;
  that remains the explicit release checkpoint.
- Every accepted executor command keeps a non-empty debug reason.

## Minimum verification before owner review

1. Focused engine and contract tests for every numerical boundary and all
   team/rotation symmetries.
2. Golden tests proving historical rules, fingerprints, and replays unchanged.
3. Orders compiler/runtime tests for fixed ace, emergent ace, role-targeted
   repair, deployment preservation, explicit relocation, and capped/repeated
   upgrade paths.
4. Destination-facing compiler/runtime tests for legal zone/anchor targets;
   rejection on patrol, follow, and dynamic destinations; travel equivalence;
   cardinal/diagonal/overlap facing; displacement; priority pre-emption; and
   team/rotation symmetry.
5. Historical-evidence tests for rolling, match-start, and node-entry
   boundaries; current-tick exclusion; visible versus hidden crossings;
   continuous-coverage interruption; death/respawn continuity; new-match
   reset; distinct life IDs; deterministic explain evidence; and independently
   entered node epochs.
6. Exact combat breakpoint tests for all three hull tiers, every veteran damage
   rank, front/rear ordering, Target Paint, vitality headroom/no-rescue, every
   rescaled signature/deployment quantity, and simultaneous focus damage.
7. Deterministic replay round-trip tests for multi-Pulse banks, wave births,
   construct damage/replacement, combat-scale values, destination-facing
   intent, evidence provenance, and new public observations.
8. Candidate-native sheets that deliberately exercise each mechanic. Historical
   sheets are compatibility sentinels, not product-quality voters for the new
   objective and roster rules.
   Use the first tranche in
   [`ARC-RELAY-TAPLINE-SHEET-STRATEGY-CATALOG.md`](ARC-RELAY-TAPLINE-SHEET-STRATEGY-CATALOG.md)
   as the initial breadth target rather than inventing near-duplicate sheets
   during implementation.
9. Trace-first, six-cell phone exhibits for replay-visible behavior changes,
   including destination-facing intent, evidence-driven branch changes, and
   legible 30..100 health presentation.
   Do not run or claim a balance sweep merely because implementation is done;
   use the repository's balance workflow only when the owner requests that
   measurement gate.

The implementation report should be
`docs/reports/ARC-RELAY-TAPLINE-DEPTH-PACK.md` and state what is implemented,
what remains isolated, exact profile/fingerprint identities, focused test
results, exhibit links, known risks, and the next owner decision. It must not
claim that the pack is balanced, deeper, or ready to ship from implementation
evidence alone.
