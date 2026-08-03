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
- `explain.json`: editor/debug view with reusable orders expanded into each
  phase.

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

### Orders and arbitration

An order binds one group to:

- a typed movement target (`route`, `zone`, `anchor`, `reactor`, `carrier`, or
  `hold`);
- arrival/completion, stuck recovery, leash, and pace;
- one formation and engagement;
- optional support and Core-custody policies;
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

This does not promise that a projectile hits an enemy who chooses an uncovered
legal move. It provides the grammar needed to budget current-position damage
and deliberately cover dodges; whether a particular coverage policy is worth
its lost direct fire remains an evidence question.

### Repair and survival

A support policy declares provider roles, ordered target priorities, a
per-target provider cap, provider separation, reserve-health threshold, and
survival fallback. Eligible Patchbays allocate independently in stable order;
the per-target cap prevents both repair beams from being spent on the same body
unless the playbook explicitly permits it. Repair wins its arbitration channel
before firing or movement.

### Core custody

A custody policy declares authorized carrier roles, escort groups, source
Wells, pickup reservation tenure, transfer and delivery timeouts, accidental
pickup handling, drop recovery, unreachable fallback, and one or more typed
safe-conversion condition groups.

Visible loose Cores are sorted by Well and ordinal. Pickup reservations bind an
exact body life to an exact Core for a bounded tenure and expire on contrary
visible evidence, life replacement, or timeout. A carrier's committed return is
the highest tactical action and clears a deterministic movement lane through
allies. Unauthorized carriers seek an authorized handoff for exactly the
declared transfer window, then deliver rather than creating a voluntary
drop/re-pickup loop. Delivery timeout counts stagnant ticks, not useful travel,
before executing the declared unreachable fallback. Friendly drops retain their
causal source life and apply `same-carrier`, `nearest-authorized`, or
`guard-until-safe` recovery. Escort movement is explicit rather than magical:
an authorized `movement.kind: carrier` order follows the nearest carrier on the
same custody policy, with stable actor-identity ties. Merely listing an escort
group never overrides that group's current authored order.

### Global and local state machines

The global coordinator and every group local machine use named states,
minimum tenure, prioritized transitions, stable-tick hysteresis, a cause label,
and condition groups. `minimumPolicy: respect` cannot begin its stable streak
before minimum tenure; `minimumPolicy: interrupt` is an explicit critical
escape hatch and may stabilize earlier.

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

## Home Siege v2

The reference playbook is
`arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/home-siege-v2.json`.
Its separately hashed Counterflow layout provides the outer rush, short breach,
forward rally, enemy perimeter, siege leash, and Well-line relationships.

Its declared phases are assault, occupy, regroup, breach, harvest, and delivery.
The desired causal loop is:

1. eight bodies rush one outer lane as a column;
2. six establish a living enemy-home ring with shared fire and cross-repair;
3. a broken ring withdraws and waits for a stable five-body rally;
4. a rebuilt wave breaches together;
5. sufficient observed attrition and available Cores release a coordinated
   harvest; and
6. every carrier returns immediately while non-carriers support the conversion
   according to their current order.

This is deliberately a hard strategy proof. The v4 felt-degeneracy bars are
frozen: a formation freeze, passivity, handoff loop, stationary carrier, home
non-progress, or pickup/drop cycle is a failed tactic, never a reason to loosen
the detector.

## Fair benchmark controls

Two stock controls remain separate:

- the frozen historical baseline preserves the old comparison and its exact
  WASM artifact; and
- `coordination-parity-baseline.json` keeps the same stock composition, routes,
  custody, gambits, and movement doctrine while enabling shared target
  selection, expected-damage budgeting, zero overkill, a five-attacker cap,
  and a three-tick target lock.

The parity control is the fair tactical benchmark. It ensures Home Siege is not
credited merely for coordinating shots against an opponent denied that same
capability. The frozen baseline remains a regression/control row, not the sole
claim.

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
