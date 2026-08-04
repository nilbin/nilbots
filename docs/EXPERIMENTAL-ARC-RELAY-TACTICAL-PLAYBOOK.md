# Arc Relay tactical playbook v1

Status: **provisional evaluation infrastructure**. This is not the player-facing
sheet schema and is not a proposed editor UI.

The tactical playbook is a deterministic data layer for testing whether Arc
Relay can express coordinated, long-horizon strategies. One frozen algorithm
interprets a strict playbook package. Strategy-specific thresholds, roles,
formations, routes, and transitions live in data rather than in the mind.

## Compilation and identity

```text
authoring playbook JSON ─┐
                         ├─ strict compiler ─ canonical normalized IR ─ ATP1
exact-bound layout JSON ─┘
```

`nilbots experiment arc-relay-playbook --playbook <path> --out <directory>`
emits:

- `normalized-playbook.json`: property-canonical IR with explicit empty
  condition variants and optional order references;
- `normalized-layout.json`: separately canonicalized absolute geometry;
- `playbook.atp`: bounded runtime envelope carrying both canonical payloads;
  and
- `explain.json`: editor/debug view of the exhaustive orders expanded into
  each phase and conditional task, including lifecycle conditions and release
  orders.

The envelope records the SHA-256 of each source independently. A layout must
name the exact map and bind each supported match-contract fingerprint and home
side. The runtime refuses an unknown fingerprint, side, transform, composition,
schema, hash, trailing byte, or payload over 64 KiB. The compiler rejects nulls,
unknown fields, unknown references, duplicate IDs, out-of-range values, silent
defaults, and an authoring object that does not match its discriminant.

Absolute tiles are allowed only in the layout asset. The playbook refers to
named zones, anchors, and routes. `identity`, `mirror-x`, and `rotate-180`
bindings transform those names into the team's world perspective; route aliases
cover a topology whose opposite side needs a genuinely different path.

## Authoring model

### Roles and stable groups

A role declares:

- an ordered class candidate pool;
- minimum, preferred, and maximum cardinality;
- carrier preference;
- death and respawn policy; and
- the declared overflow role.

Allocation is stable by unit slot while the body remains eligible. Vacancies
are filled deterministically by candidate-class rank, health, then stable unit
ID. A group collects one or more roles and gives them a stable membership,
casualty, pre-emption, and overflow policy plus a local state machine. A new
life remains a joining replacement until it physically reaches the surviving
cohort; a complete wipe may establish a new cohort once the group's minimum is
present and coherent.

Those policies execute rather than merely label the data. `hold-vacancy`
counts a destroyed stable slot and will not steal a living body from another
role; `promote-best` fills the opening by declared candidate order; and
`rebalance` recomputes best fit. `stable-slot` retains eligible assignments,
while `best-fit` deliberately recomputes them. Pre-emption may be forbidden,
limited to an earlier role, or limited to a global phase boundary. `resume`
returns directly to the cohort; `rejoin`, `rally`, and `replace` mark the new
life as joining, while `replace` does not displace a living promoted
replacement. A role must belong to exactly one group, and its death policy must
agree with that group's casualty policy; the compiler rejects ambiguous or
contradictory ownership.

Group cardinality is executable too. Role minima are filled before preferences;
the group preferred count bounds normal growth, and its maximum caps stable
retention, pre-emption, and overflow. If the preferred group band is wider than
the sum of role preferences, unassigned eligible bodies fill it by lowest live
role count, candidate rank, health, and unit ID. The compiler rejects group
counts that cannot satisfy owned role minima/preferences or exceed their total
capacity, and the combined group maxima must be able to own all eight bodies.
The group minimum remains the tactical viability threshold used by cohort
re-establishment and understrength fallback; casualties may legitimately put a
live group below it.

### Formations

A formation has a shape and orientation, named role placements, and Chebyshev
minimum/preferred/maximum spacing. Its cohesion policy defines arrival, break,
and reform thresholds plus group pace. Its reflow policy declares what happens
to blocked slots and vacancies, a bounded search radius, and medic separation.

The shape and sector labels are semantic editor data; offsets are relative to
the order's named anchor. Route orders use the route as a common marching spine
until their final approach, then apply role-relative placements. Runtime motion
claims, legal-path checks, and bounded reflow arbitrate collisions without
changing authoritative simulation truth.

The separately hashed layout's `corridorWidth` bounds that intermediate-spine
reflow: the runtime uses the smaller of route width and formation search radius.
At the final waypoint the route hands control to the authored relative
formation, whose offsets and own reflow policy may deliberately be wider. Thus
map-specific travel geometry stays in the layout and cannot become a decorative
editor field or a duplicated playbook coordinate.

Every role receives enough distinct authored slots for its maximum cardinality;
the compiler rejects missing or overlapping slots. `preserve` leaves a dead
body's slot open, `compress` closes ordinal gaps, and `rebalance-role` spreads
the surviving role members across the full authored slot band. When terrain or
another resolved slot forces reflow, candidate tiles are selected
deterministically by minimum spacing, medic separation, maximum connectivity,
distance from preferred spacing, and finally tile order. `rotate-shape` uses a
clockwise ring order, distinct from `nearest-legal`. Thus minimum, preferred,
maximum, vacancy, blocked-slot, and medic-separation controls all affect either
authoring validity or runtime placement.

Break/reform is an explicit lifecycle rather than a startup shortcut. A
formation first arms only after its arrival ratio holds for `reformTicks`. Once
armed, cohesion at or below `breakRatioPercent` for `breakTicks` marks it
broken; cohesion at or above `arrivalRatioPercent` for `reformTicks` repairs
it. `free` pace never gates movement. While an established formation is
broken, `slowest` lets the tail catch up before bodies farther ahead advance,
and `leader` lets followers catch but not overtake the stable lowest-unit-ID
leader. The compiler requires an order's movement pace to match its formation's
cohesion pace so two editor controls cannot silently contradict each other.

Orientation is executable. A current shared focus assignment owns facing so a
cooldown tick does not turn a shooter away from its next shot. Otherwise,
`enemy-reactor` and `own-reactor` restore the formation's look direction,
`route` looks toward the current movement target, and `focus-target` or `fixed`
preserves the last legal facing. Authors can put a dedicated observer in its
own formation instead of forcing a whole firing line onto one bearing. Combat,
repair, custody, and movement still win through the declared arbitration
order, so orientation never manufactures an observation or cancels an action.

### Maneuver catalog and tuning parameters

The human-authored form is deliberately smaller than the exhaustive runtime
IR. It declares a `maneuver-catalog` containing:

- bounded named parameters for values that need empirical tuning;
- keyed catalogs: the JSON object key is the identity, so entries do not repeat
  `parameterId`, `predicateId`, `maneuverId`, `trackId`, and `orderId` fields;
- named leaf predicates and condition sets composed as OR rows of predicate-ID
  conjunctions rather than copied fact objects;
- named fallback and group-assignment profiles;
- named maneuvers containing one or more concurrent tracks; each track has one
  shared movement/formation intent and explicit per-group assignments;
- standing orders reused across maneuvers, such as concurrent formation
  recovery; and
- named condition sets referenced by phase transitions.

There is no implicit inheritance. An assignment explicitly names the profile
that supplies group, local state, priority, stuck recovery, and support; its
own entry supplies the remaining order fields. Every phase names its maneuver
plus all standing orders. Catalog keys are validated lower-kebab identities and
are expanded in ordinal order, independent of JSON property order. The compiler
deterministically expands the catalog to the exhaustive `orders`/`orderIds` IR
and then applies the same strict reference, coverage, and range validation as a
fully expanded source. The runtime never parses authoring shortcuts.

Formation sources use ordered `placementBands`: one role/sector plus one or
more offsets. Declared band and offset order becomes explicit runtime slot
order. This keeps a formation readable as "five line slots, two medic slots,
one runner slot" without repeating the same role and sector on every slot.

Tracks are the authoring unit for a coordinated split: for example, a delivery
maneuver may send carriers and medics toward the bank while a line track moves
back toward the enemy perimeter. They are concurrent orders inside one phase,
not nested scripts or a strategy-specific runtime special case.

An assignment profile also declares which members of its stable group receive
the order. `all` preserves whole-group behavior. A split uses one or more
`take` selections followed by exactly one `remainder`; the compiler rejects a
split that can silently leave a body unassigned. A `take` names eligible roles,
an ordered class preference, and a bounded count. Selection follows assignment
priority, authored class order, and stable unit ID. This permits a sheet to say
"detach one Kestrel, otherwise one Relay" without naming a body or changing its
stable role. A casualty deterministically promotes the next eligible survivor;
the remainder continues its authored order.

### Persistent intent and conditional tasks

Global phases own the team's persistent intent. Their exhaustive orders keep
running unless a declared conditional task leases particular body lives. A task
is a bounded interruption, not another whole-team phase and not an imperative
script. Bodies that are not leased continue their current phase orders.

Each task declares:

- a stable identity, ordered priority, `rising-edge` or `while-true`
  activation, phase eligibility, trigger hysteresis, minimum tenure, timeout,
  and cooldown;
- one or more participant assignments with minimum/preferred/maximum counts,
  eligible roles, ordered class preference, carrier requirement, and a
  deterministic distance reference (`none`, a named layout anchor, or either
  reactor);
- an exhaustive task order for every assignment plus explicit completion and
  failure condition groups;
- whether a lost exact life aborts the task, lets surviving leases continue,
  or deterministically selects a replacement; and
- immediate return to the current primary order or an explicit bounded
  release-order state with its own completion condition and timeout.

Candidate selection is deterministic: authored class rank, declared distance,
then stable unit ID. The selector is evaluated once when the task activates.
The resulting lease binds the exact life, so picking up a Core or an unrelated
primary-formation state change cannot silently replace the courier. Death is a
participant loss and follows the authored policy. A respawn is a different life
and may be selected only by an explicit replacement or a later activation.
While leased, that life is excluded from the primary group's cohesion, stuck,
rejoin, and pace cohort; otherwise a correctly authored detachment would make
the remainder falsely declare its own formation broken. The task order still
owns the leased body's complete formation and movement behavior.

Disjoint tasks run concurrently. A body already leased to another task is
unavailable unless the existing task explicitly permits higher-priority
preemption and the claimant has the earlier priority. Equal priorities use
task ID as a stable scheduler tie; they never manufacture shared ownership.
Preemption, completion, failure, timeout, phase exit, participant loss, release,
and replacement are retained as deterministic task transitions and summarized
in the mind trace. `explain.json` embeds every assignment and release order so
an editor or reviewer does not need to chase IDs.

Task orders use `members.kind: all` because task assignments already own the
selection. `release-orders` must cover every local state of every assigned
group exactly once; this makes runtime dispatch total rather than relying on a
fallback. There is deliberately no task-dependency field in v1, so dependency
cycles are structurally unrepresentable and an attempted dependency key is an
unknown-field compile error. Coordination between tasks is expressed through
causal facts and priorities, not an invisible task graph.

A predicate may name exactly one bounded parameter instead of copying a number.
Compilation resolves it to an ordinary integer condition before hashing the
normalized IR. Home Siege separates
`conversion-front-enemy-unavailable` from
`conversion-occupied-enemy-unavailable`: leaving the initial assault and
dispatching scorers from an established siege are different decisions. A
development sweep can compare three, four, five, or another legal value by
changing one bounded entry, without editing custody and several transitions.
Selected values remain explicit source data, not runtime defaults or adaptive
hidden state.

### Orders and arbitration

An order binds one group to:

- a typed movement target (`route`, `zone`, `anchor`, `reactor`, friendly
  `carrier`, observed `enemy-carrier`, or `hold`);
- arrival/completion, stuck recovery, leash, and pace;
- one formation and engagement;
- optional support and one required, explicit Core-custody policy;
- a required local group state; and
- explicit no-path, understrength, and invalid-target fallbacks.

Every tick uses one declared global channel order. Home Siege currently uses:

```text
custody emergency → self-preservation → repair → coordinated signature
→ focus fire → movement → facing → hold
```

The first legal action wins. Every submitted command explains itself as
`tp:<phase>:<group>:<order>:<channel>`; focus and custody commands add their
target or recovery reason. This is presentation/debug provenance only and does
not alter the canonical rules.

Movement completion is observable state, not an implicit movement cancel.
`continuous` never completes; `leader-arrived`, `all-arrived`, and
`cohesion-arrived` become true from the order's declared arrival radius and the
formation's arrival ratio. A transition may consume that state through the
typed `movement-complete` fact whose `subject` must be a declared order ID.
This keeps the author—not an invisible executor default—in charge of what the
team does after arrival. `group-formation-broken` similarly exposes the
formation lifecycle to global and local transitions.

`enemy-carrier` is a bounded interception order, not omniscient pursuit. Its
`target` names the fallback layout anchor and its movement `chaseLeash` is the
maximum Chebyshev radius around that anchor. It first considers currently
observed carried Cores, then an exact last-seen carrier life remembered only
until a causal pickup, handoff, drop, bank, death, or memory expiry says
otherwise. It selects the carrier nearest its own reactor, then uses anchor
distance and stable actor identity as ties. When no legal candidate is inside
the leash, the group reforms on the fallback anchor. This lets an editor
express a bounded intercept without encoding a unit ID, inventing a position
through fog, or putting absolute coordinates in the playbook.

Fallbacks are explicit and phase-safe. Each order declares what no-path,
understrength, and invalid-target mean. `continue`, `hold`, `alternate`, and
`reflow` stay local; `regroup`/`fallback-phase` require a declared `phaseId`
that the compiler resolves. A phase fallback is queued for the next tick so a
mid-tick failure cannot make half the team execute a different phase. Stuck
recovery is also executable: `yield` gives peers a tick and resets its counter,
`repath` drops route progress, `reflow` searches bounded legal slots, `hold`
stays put, and `regroup` queues the explicit fallback phase.

Movement target types link against the separately hashed layout at compile
time: routes name routes, zones name zones, static and dynamic fallback targets
name anchors, reactors are exactly `own`/`enemy`, and hold uses an empty target.
Condition zones are linked in the same pass. A movement `chaseLeash` is the
maximum bounded reflow away from that resolved target; route motion additionally
obeys the narrower route corridor. `carrier`, `enemy-carrier`, and
`secured-core` treat the named anchor as their fallback and consider a dynamic
target only inside that leash. Formation search radius remains the outer
generic reflow capability, so all three bounds have distinct jobs.

Every order names a custody policy even when deliberate collection is not part
of its purpose. An explicit incidental policy can authorize delivery after an
accidental pickup while using an unsatisfiable safe-conversion condition to
forbid deliberate collection. The runtime never substitutes the first custody
policy or another implicit default.

### Engagement and coordinated attacks

An engagement declares participants, target priorities, deterministic
tie-breakers, group or shared scope, target-lock tenure, an attacker cap, an
expected-damage/overkill budget, formation chase leash, signature policy,
optional dodge coverage, release conditions, and self-defense fallback.

Allocation excludes current Core carriers and Patchbays already committed to a
repair. It sorts legal visible targets once per scope, retains a visible locked
primary for the bounded tenure, and assigns shooters in stable unit order. A
target stops receiving direct-fire assignments when expected damage reaches
health plus the declared overkill allowance. A requested escape-lane policy may
then reserve additional shooters for distinct legal one-tick lanes without
relaxing the direct-damage budget. Coordinated signatures choose at most one
controller per `(target life, signature)` deterministically.

Focus locks declare their preemption policy. `never` holds the current legal
lock for its tenure, `higher-priority` permits a target from an earlier target
priority, and `urgent-carrier` permits a carrier closer to its bank to replace
a less urgent lock. Tie breakers may compare distance to either reactor before
stable actor identity. These are authored policies, not executor heuristics.

The dodge fallback is executable rather than decorative. `best-coverage`
selects the best legal aim even when it adds no new covered lane;
`current-position` uses a dodge aim only when it adds coverage and otherwise
returns to direct aim. Self-defense is similarly bounded. A body outside its
formation leash may answer a threat inside `threatDistance`; when
`returnToFormation` is true, a successful defensive shot or signature starts a
return excursion. The body is withheld from coordinated focus until it reaches
the current order's arrival radius again. Respawned lives never inherit that
excursion.

This does not promise that a projectile hits an enemy who chooses an uncovered
legal move. It provides the grammar needed to budget current-position damage
and deliberately cover dodges; whether a particular coverage policy is worth
its lost direct fire remains an evidence question.

### Repair and survival

A support policy declares provider roles, ordered target priorities, a
per-target provider cap, provider separation, reserve-health threshold, and
survival fallback. Eligible Patchbays allocate independently in stable order;
the per-target cap prevents both repair beams from being spent on the same body
unless the playbook explicitly permits it, and a permitted pair must also meet
the declared minimum separation. `evade`, `regroup`, `hold`, and
`self-defense` are distinct low-health fallbacks. Repair wins its arbitration
channel before firing or movement.

### Core custody

A custody policy declares authorized carrier roles, escort groups, source
Wells, pickup reservation tenure, transfer and delivery timeouts, accidental
pickup handling, drop recovery, unreachable fallback, and one or more typed
safe-conversion condition groups.

Visible loose Cores are sorted by Well and ordinal. Pickup reservations bind an
exact body life to an exact Core for a bounded tenure and expire on contrary
visible evidence, life replacement, or timeout. A carrier's committed return is
the highest tactical action and clears a deterministic movement lane through
allies. During an authorized transfer window, the accidental carrier holds the
perimeter while the authorized carrier owns the rendezvous; on expiry the
accidental carrier delivers rather than creating a voluntary drop/re-pickup
loop. Delivery timeout counts stagnant ticks, not useful travel,
before executing the declared unreachable fallback. Friendly drops retain their
causal source life and apply `same-carrier`, `nearest-authorized`, or
`guard-until-safe` recovery. Escort movement is explicit rather than magical:
an authorized `movement.kind: carrier` order follows the nearest carrier on the
same custody policy, with stable actor-identity ties. Merely listing an escort
group never overrides that group's current authored order.

Role carrier preference participates in allocation before distance:
`require`, then `prefer`, then `allow`; `forbid` is ineligible. The compiler
rejects a custody policy that authorizes a forbidden role, or a `require` role
that no custody policy authorizes. Home Siege's safe surge explicitly marks
medic and line roles `allow`, because its authored whole-group sweep permits
any surviving body to pick up and immediately return a Core.

An explicit `secured-core` movement order follows only causally observed enemy
drops from its custody policy's allowed, perspective-resolved Wells. Its target
is a fallback layout anchor and its movement leash bounds how far the guard may
shift. A relative formation offset keeps the body adjacent without forcing an
unsafe pickup; ordinary custody allocation may step onto the Core only after
the declared safe-conversion conditions pass.

### Global and local state machines

The global coordinator and every group local machine use named states,
minimum tenure, prioritized transitions, stable-tick hysteresis, a cause label,
and condition groups. `minimumPolicy: respect` cannot begin its stable streak
before minimum tenure; `minimumPolicy: interrupt` is an explicit critical
escape hatch and may stabilize earlier.

Each global phase must cover every declared `(group, localState)` pair. Coverage
is either one `all` order or a deterministic split consisting of one or more
`take` orders and exactly one final `remainder` order. The compiler rejects a
missing pair, mixed `all`/split coverage, duplicate priorities, a non-final
remainder, or an order whose local state does not belong to its group. Runtime
selection is exact and has no cross-state fallback. This makes local recovery
and explicit detachments genuinely concurrent with the global phase. Order
priority controls member and claim arbitration, followed by authored class
preference and stable unit ID.

A condition group is exactly one of `all` or `any`. Facts are typed by their
required fields:

- zone facts require `zone`;
- group, role, or Well facts require `subject`;
- `remembered-enemies-in-zone` and `secured-cores` may add a bounded
  `freshnessTicks`; and
- facts without those variants reject the extra fields.

Facts use only the current causal team observation and bounded memory derived
from it. Fog disappearance never becomes a death or proof of an empty theater.
Confirmed enemy destruction expires no later than the configured return
window. Last-seen enemies and secured Cores expire globally and may be narrowed
again by a condition-specific freshness window.

The playbook's memory horizons also bound formation-stability and
objective-progress counters. `custody-state-ticks` measures the longest current
custody state's causal tenure; it is not an alias for general objective
stagnation. Repair priority `focus-participant` means a non-carrier body whose
active engagement includes its role, computed before repair allocation without
mutating the focus-lock state. The final coordinated-fire allocation then
excludes the chosen repair providers.

## Home Siege v2

The reference playbook is
`arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/home-siege-v2.json`.
Its separately hashed Counterflow layout provides the outer rush, short breach,
forward rally, enemy perimeter, siege leash, and Well-line relationships.

Its declared primary phases are assault, occupy, regroup, and breach. Scoring
and carrier denial are bounded tasks layered on established occupation rather
than whole-team phase replacements.
The desired causal loop is:

1. eight bodies rush one outer lane as a column;
2. five establish a living enemy-home ring with shared fire and cross-repair;
3. a broken ring withdraws and waits for a stable five-body rally;
4. a rebuilt wave breaches together;
5. the currently selected, bounded observed-attrition threshold and a causally
   outstanding Core lease one courier and one escort while the five-body
   blockade remains on its primary order;
6. every carrier returns immediately when its declared custody policy permits,
   then releases back into the live blockade; and
7. one bounded remembered-carrier interceptor may coexist with that conversion
   pair without owning or dissolving the rest of the formation.

The frozen candidate allocation is **5 + 1 + 1 + 1**: five primary blockade
bodies, one remembered-carrier interceptor, one courier, and one escort. The
task scheduler enforces this through `minimumPrimaryBodies: 5`; it is not a
runtime heuristic and it is not a game rule. A six-body reserve prevented the
conversion pair from functioning, while leasing two interceptors broke the
blockade in both mirrored representative assignments. The exact formula is
therefore part of this candidate's hashed evaluation identity. A future sheet
may author a different legal allocation, but it must earn its own evidence.

The exact attrition and return thresholds are development hypotheses, not
owner-locked rules. They are selected by retained mirrored trials and frozen
only before the final cohort is read. This is deliberately a hard strategy
proof. The v4 felt-degeneracy bars are
frozen: a formation freeze, passivity, handoff loop, stationary carrier, home
non-progress, or pickup/drop cycle is a failed tactic, never a reason to loosen
the detector.

## Fair benchmark controls

Two stock controls remain separate and both use their schema-compatible frozen
interpreter/artifact pair:

- the frozen historical v2 baseline preserves the old comparison with
  `stock-mind-v4` and its exact WASM artifact; and
- `coordination-parity-baseline.json` keeps the same stock composition, routes,
  custody, gambits, and movement doctrine while enabling shared target
  selection, expected-damage budgeting, zero overkill, a five-attacker cap,
  and a three-tick target lock.

The parity control is the fair tactical benchmark. It ensures Home Siege is not
credited merely for coordinating shots against an opponent denied that same
capability. The frozen baseline remains a regression/control row, not the sole
claim.

The primary dominance cohort must therefore use the v2 parity control with
`stock-mind-v4`, never the
historical uncoordinated stock sheet. Both sides receive visible-target locking,
expected-damage accounting, deterministic attacker caps, and zero-overkill
allocation. Their target priorities and tactical reasons may differ because
those are authored strategy; the low-level ability to coordinate attacks does
not.

## Scope boundary

This package is intentionally not:

- the format a player draws or edits;
- an unlock or entitlement schema;
- a web editor;
- an opponent-identity classifier;
- hidden-information access;
- a game-rule, class, map, or replay-contract change; or
- evidence that a successful spawn siege is healthy balance.

The later player-facing design may compile into this IR, but it receives its own
post-audit UX pass.
