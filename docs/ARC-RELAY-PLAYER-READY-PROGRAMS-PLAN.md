# Arc Relay player-ready Tactical Programs follow-up

> **Historical (2026-08-18).** Superseded by whole-team Tactics —
> `DECISIONS.md` #331, `ARC-RELAY-TACTICS-IMPLEMENTATION-PLAN.md`,
> `SPEC-SHEET-TACTICS-V1.md`. Kept as design history; nothing here is
> current product guidance.

Status: **implemented for owner review** — see
[`ARC-RELAY-PLAYER-READY-PROGRAMS-REPORT.md`](reports/ARC-RELAY-PLAYER-READY-PROGRAMS-REPORT.md)
Date: 2026-08-16  
Baseline: current `main`, local/review loop profile `tapline-8`

## Authoritative amendment: Program-scoped Core custody

Owner ruling (2026-08-17). This section supersedes every later reference in
this plan to a linked banking Program, a post-pickup Program transition, or
`self-carrying = 0` as a universal Program exit. Those passages record the
earlier design and are not acceptance requirements.

- A Program never calls, links, or jumps to another Program. The one ordered
  Program list remains the only player-facing priority and ordinary generated
  Instructions remain the only runtime behavior.
- A durable Program parent owns its trigger, readiness, commitment, hard
  timeout, and authored tactical outcome. Once active, its `when` is not
  re-evaluated while it has an `until`; higher Programs may still pre-empt it
  through ordinary priority.
- **If this Program gains a Core** is Program-local authoring. Carrier, escort,
  transfer, Fight, healing, signature, wall, Handoff, Toss, and Drop branches
  sit beneath the participant's already-active parent. They inherit the parent
  episode instead of repeating its trigger or becoming carrying-triggered root
  Programs.
- A carrier branch may use current `self-carrying` and end when that individual
  becomes empty. A Toss or Handoff therefore ends only the old carrier's local
  branch; the receiving participant's branch starts and the Program continues.
- Program completion is an authored bank, explicit abandonment/loss, or hard
  timeout. When completion refers to the Core acquired by this tactic, ordinary
  historical facts correlate pickup/catch, Handoff/Toss, Drop/loss, and bank by
  authoritative Core identity and declared participant roles.
- Role-qualified carrier facts and selectors prevent an escort from attaching
  to an unrelated tactic. They are ordinary Instruction vocabulary, not an
  `active Program` runtime fact.
- Deliberate recovery filters never prevent incidental physical pickup. An
  active Program may handle an unwanted Core with ordered Drop/Handoff/Toss
  branches, with a visible warning that the reclaim interval prevents only
  immediate same-body pickup, not a permanent loop.
- A standalone Core-custody Program is still legal as an ordinary global
  fallback or intentional higher override, but nothing references it. The UI
  explains the real higher/local/lower result of the Program list.

The required proofs are one Program that remains committed across acquisition,
custody transfer, escort retargeting and banking, plus one incidental unwanted
Core that is visibly filtered and dropped/transferred before the Program
resumes or exits. Neither proof may require raw JSON or a second runtime.

## Directive

Implement this plan as the next Tactical Programs goal. It replaces the unsent
follow-up to `docs/ARC-RELAY-SIGNATURE-OPPORTUNITIES-REPORT.md` and builds on
Decisions #325–#328. Do not reinterpret it as a request to redesign Programs,
add a second scheduler, tune balance, deploy, or mint a playlist.

The outcome is a **player-ready strategy-authoring alpha**. At the end of this
round, a player must be able to start with a complete legal sheet, understand
its top-to-bottom Programs, author or refine precise spatial tactics directly
on the map, run Practice, and tell why the bodies did what they did without
opening generated Instructions or JSON.

The canonical authoring sentence is:

> Put this Mason on this exact tile, face it toward this point, and keep a
> Hardlight Block on that tile whenever its wall is missing.

That sentence must be easy to create, easy to read back, easy to drag one tile
later, and mechanically reliable. The same primitive must generalize to firing
posts, mines, sentries, smoke, flares, artillery targets, repair stations, and
other future signatures. It must not be a Mason-only recipe.

The second canonical alpha sentence is an ordinary role plan, not a new
behavior system:

> Patrol this route. Use these skills at these locations. Fight when this body
> runs into reachable enemies, recover a visible loose Core within five legal
> walking steps when empty-handed, and otherwise keep patrolling.

That complete sentence must be authorable and reorderable from one Program
participant card. Its displayed local order must be the exact generated
Instruction sibling order, with no hidden priority between the skill, fight,
Core-recovery, and patrol choices.

Patrol is the required integrated proof, not the feature boundary. The same
Fight, nearby-Core, and health-recovery opportunity cards must be available on
any Program participant behavior with an ordinary durable continuation—moving
to a destination with or without `via`, patrolling, following, holding a Post,
or responding/fighting. The player chooses the exact continuation they may
interrupt; adding one must never make it silently active during every setup,
waiting, and response phase of a Program.

## Product boundary

Preserve these constraints throughout:

- **Programs remain the only player-facing priority list**, evaluated top to
  bottom.
- **Instructions remain the only executable strategy language.** Program
  authoring materializes ordinary Conditions, capacities, map geometry, and
  Instructions.
- A Program, Post, formation, Opportunity, or maintenance policy adds no
  runtime identity, phase, reservation, atomic activation, state machine,
  replay field, or competing priority system.
- One body still takes at most one action per tick. An Opportunity shown
  “inside” a movement, Post, or response card remains an ordinary action sibling
  above the ordinary continuation. If it cannot execute, the next sibling is
  considered in the same tick.
- Capacity remains the existing generic “how many bodies may resolve this
  target” primitive. Do not invent fixed unit assignments or a hidden formation
  allocator.
- Intent-aware capacity defaults are allowed only as visible Program-authoring
  sugar that materializes the ordinary capacity field. In particular, nearby
  Core recovery defaults to **One bot per Core across this sheet's default
  recovery group**, and health recovery defaults to **One patient per healing
  tile across this sheet's default healing group**. Do not change the
  executor's generic default or silently reinterpret an existing sheet with no
  capacity.
- An **Opportunities** list—contextually labelled **Along the way** for
  movement—is not a second priority system. Its displayed top-to-bottom order
  is the actual order of direct-child Instruction leaves inside that Program
  participant behavior, followed by the exact continuation. Dragging it
  changes that one real order.
- **Fight is one behavior, not an Ambush mechanic plus an Opportunity
  mechanic.** Target order, style, chase limit, capacity, lifecycle, summary,
  and generated `fight` leaf use one editor/materializer. Ambush readiness and
  trigger may activate that behavior from its parent Program; ambient contact
  may activate it with a local distance Condition.
- Core custody may remain inside the already-active Program. The editor owns
  one semantic source for its participant-local carrier and escort branches,
  while the priority lens reports any real higher override and lower fallback.
  No Program reference or transition is emitted.
- Core value remains ordinary evidence and target filtering—not a separate
  custody-policy engine. Pickup preference, carrier routing, escorting,
  handoff/Toss, and voluntary drop all materialize through the same Conditions,
  target resolution, local Instruction order, and Program priority already in
  use elsewhere.
- Core-recovery filters decide which Core a body deliberately pursues; they do
  not veto the existing physical pickup rule. Any body that crosses a loose
  Core can become its carrier, so normal authoring and starter sheets must audit
  custody handling across the whole fielded roster, not only roles with a
  recovery card.
- Combined readiness means every participant sees the same ordinary gate; it
  does not promise simultaneous actions. A higher Program, death, displacement,
  or dynamic illegality may still affect one participant independently, and
  Practice must make that visible.
- Keep flat Condition expressions. Where a natural choice appears to require
  nested boolean logic, materialize a small, inspectable Instruction branch or
  several ordered opportunity leaves instead of adding a general expression
  language.
- Keep Advanced generated Instructions as a read-only explanation and escape
  hatch. Normal completion of every acceptance flow below must not require it.
- Existing Orders-only sheets, linked Programs, save/copy/reload, detach, drift
  protection, and workshop normalization must remain non-destructive.

## 1. Make **Post** the reusable spatial authoring primitive

“Post” is a player-facing editor composition, not a new runtime action and not
a new Program recipe. A Post binds the spatial choices a human thinks about as
one tactic while keeping their ordinary generated sources visible underneath.

### A Post must let the player choose

1. **Who stands here**
   - one role;
   - required body count or capacity;
   - required or optional for Program readiness.
2. **Where it stands**
   - an exact tile by default;
   - optional tolerance of 1 or more tiles, visualized as the actual readiness
     area;
   - an ordinary anchor plus a linked derived zone underneath.
3. **Where it looks after arrival**
   - face toward another point; or
   - choose an exact cardinal direction with a snapping arrow, represented
     underneath by a derived facing anchor on the same row or column.
4. **What it does while posted**
   - hold and preserve vision/facing;
   - respond/fight under the Program’s trigger;
   - attempt one or more descriptor-authorized signature opportunities;
   - optionally maintain an exact deployment target.
5. **Whether this setup is required before the response arms**
   - Post occupied;
   - maintained deployment present at its intended target;
   - both, or neither.

The normal summary should read approximately:

```text
Mason post · 1 body
Stand exactly at Mason Post → face Corridor Mouth
Keep Hardlight Block at Well Choke · rebuild when missing
Required before Home Defence arms
Otherwise hold this post
```

Use human labels in this surface. Generated identifiers belong only in
Advanced.

### Map interaction

The complete Post must be authorable without leaving the map:

- tap a tile to place the Post;
- drag from it to set the facing arrow, or select an existing point to face;
- tap another tile to set the signature effect target;
- see the caster-to-target line, range, static line-of-sight/terrain legality,
  wall or deployment footprint, and readiness tolerance before saving; label
  occupancy and other match-time legality as dynamic rather than pretending to
  predict it;
- see the body's rules-derived starting vision footprint from the selected
  Post and facing, plus an optional union overlay for the whole formation;
  distinguish static terrain occlusion from dynamic smoke, deployments,
  movement, and veteran vision upgrades;
- select any marker to open the same compact Post inspector;
- drag the Post, facing endpoint, or effect target independently;
- move a selected formation group together without breaking derived zones,
  Conditions, opportunity bindings, or capacities;
- undo or redo each human edit as one transaction.

Use visually distinct markers for **stand here**, **face here**, **use from
here**, and **affect here**. “Use from” and “affect here” must never collapse
into one ambiguous anchor.

At phone width, use a map-preserving bottom sheet or equivalent compact
inspector. The player must not have to scroll far enough that the selected Post
and its map context disappear. Require touch targets of at least 44 px, no
hover-only action, and no horizontal page overflow at 390×844. Every map-only
gesture must also have a keyboard/list-inspector equivalent, and readiness,
priority, target, and vision states may not rely on colour alone.

### Facing semantics

Reuse the existing `move.face.anchor` behavior:

- facing is applied only after the body reaches an authored anchor/zone
  destination;
- it does not alter patrol-route movement;
- bodies have four cardinal facings. The exact-direction affordance therefore
  snaps north/east/south/west and creates or moves a cardinally aligned derived
  anchor rather than promising an unsupported eight-way facing;
- **Face toward this point** remains available as the existing anchor behavior,
  but the map must preview the actual cardinal result. A diagonal point can
  resolve to either adjacent cardinal direction depending on arrival facing,
  so it must be labelled as non-exact (or rejected when exact facing is
  required), never shown as a guaranteed diagonal;
- for a tolerance area, exact facing is promised only when every legal standing
  tile resolves to the same cardinal direction. Otherwise show the possible
  facings and offer a cardinal arrow, a smaller tolerance, or a moved point;
- a zero-length facing arrow is invalid and is explained before save;
- a combat response may turn toward its combat target; when that response ends,
  the Post continuation restores the authored facing.

Pin these semantics in focused tests and communicate them in the Post summary.

### Capacity and exact formation rules

Capacity is mandatory in this design, not an Advanced afterthought:

- an exact one-body Post defaults to capacity 1;
- two Posts for a two-body role materialize two capacity-1 movement targets;
- each required Post receives its own role-qualified readiness zone/Condition,
  and the overlap rule below prevents one eligible body from satisfying two
  required slots;
- multiple different roles may participate in the same formation/readiness
  group;
- all required slots must be occupied before the group reports ready;
- optional slots never block arming;
- the map shows `filled / required` and the missing role/Post by name.

Allow a formation-level maximum spread. For exact Posts this can be enforced by
validating the authored Post geometry and tolerance envelopes; do not add a
runtime proximity state merely to rediscover static geometry. Reject or
visibly warn when the chosen tolerance areas could violate the configured
spread. This preserves the earlier acceptance rule that an ambush does not arm
until all required bodies are in their granular positions and sufficiently
nearby.

Independent count Conditions are not enough to make overlapping tolerance
areas distinct: one eligible body could otherwise be counted once in each
area. For required Posts whose eligible body pools overlap, the normal editor
must reject intersecting readiness envelopes and highlight the conflicting
tiles/Posts. Offer the player the direct repairs: move a Post, reduce its
tolerance, or assign disjoint roles. Do not merely warn and then materialize an
ambiguous all-slots-ready gate, and do not add a hidden runtime assignment or
matching system to compensate. Required Posts backed by disjoint role/body
pools may overlap because no body can satisfy both. Pin this validation for
zero- and non-zero-tolerance Posts.

### Player-friendly capacity authoring

Do not make a player translate every tactic into the abstract word
“capacity.” Label the same existing primitive in terms of its resolved target:

- **Bodies at this Post** for spatial occupation;
- **Pursuers per Core** for Core recovery;
- **Patients per healing tile** for health recovery;
- **Escorts per carrier** for a banking Follow group;
- **Attackers per enemy** for fight focus; and
- **Casters per target** where several eligible bodies share a signature
  opportunity.

The compact Program summary must always show the effective policy even when a
safe default supplied it. The expanded control explains that claims recompute
every tick, are not reservations, and that a body which loses a claim tries its
next eligible target and then its next local behavior in the same tick.

Nearby visible-Core recovery defaults to:

```text
Pursuers per Core · 1 (recommended)
```

The player may change that to 2–8 or deliberately choose **All eligible**;
**All eligible** removes the capacity field rather than materializing a magic
number. The recommended default is persisted in Program linkage and
materialized as an ordinary capacity; it is not an invisible executor rule.
New normal-surface nearby-Core cards join one visible sheet-wide **Core
recovery** coordination group by default, so two roles or Programs do not each
send one body to the same Core while both claim to mean “one per Core.” The
expanded control lets a player deliberately split off a separately named
coordination group when a tactic needs independent pursuit.

If the default group currently has one recovery leaf, use an inline capacity.
If it has several leaves across roles or Programs, materialize one shared named
capacity. Normalize back to an inline capacity if only one consumer remains,
without changing behavior or exposing that storage detail in the compact UI.
The summary still says whether the policy coordinates across the sheet or only
within a deliberately separate group.

Health recovery uses a separate visible sheet-wide **Healing** coordination
group and defaults to one patient per resolved healing tile. It follows the
same inline-versus-shared materialization and override rules as Core recovery.
Never share one named capacity between different target kinds merely because
both controls happen to show `1`.

Capacity is per resolved Core, not one collector for the whole map. With three
eligible visible Cores, three bodies may each pursue a different Core under the
default. With one Core, only one pursues it; the others try another in-range
Core and then the next Along-the-way behavior. If the pursuer dies, loses the
target, or becomes ineligible, another body may win the recomputed claim next
tick. Do not add a claim lifetime, target reservation, or hidden collector
assignment.

Do not invent intent defaults where the desired coordination is genuinely a
strategy choice. Encounter fighting must show its effective **Attackers per
enemy** setting but may default to **All eligible**; focus fire versus spreading
damage belongs to the player. Existing Programs and Orders retain their exact
authored capacity semantics until edited.

### Backward-compatible editor linkage

Represent Posts as additive editor linkage in the workshop sidecar. Either
generalize the existing `PlaySlot` linkage or add one normalized Post record,
but keep one owner for each anchor, readiness zone, facing anchor, capacity,
opportunity, and maintenance target. Do not copy those relationships into
several recipe-specific structures.

Older `arc-relay-workshop-v1` documents must normalize with optional defaults
and preserve byte-equivalent generated Instructions until edited. The Post
record itself stays out of Orders, ATL2, content hashes, match/replay identity,
and the executor. Its fingerprints must participate in the existing linked
source drift workflow so an external Instruction edit cannot be overwritten
silently.

## 2. Add location-aware deployment truth

The existing `deployment-active` fact is not enough for maintenance. It says
that a body owns a live deployment, but cannot distinguish “my wall is on this
choke” from “my wall is somewhere else.” Using it for the canonical sentence
would silently preserve the wrong wall.

Add two generic zone-qualified facts with deliberately different scopes:

```text
self-deployment-in-zone
role-deployments-in-zone-count
```

`self-deployment-in-zone` is the owner-local maintenance truth. Its exact
contract is:

- integer `0 | 1`;
- requires an authored `zone`;
- evaluates the current body’s stable-unit-slot-owned durable deployment;
- returns `1` when a live deployment in the Active phase occupies at least one
  tile in that zone;
- returns `0` when no such deployment exists there, including after destruction,
  expiry, or replacement at another location;
- a temporarily suppressed deployment still counts as present, so a silence
  does not cause replacement/recast churn; existing suppression presentation
  explains that it is present but temporarily ineffective;
- works for every durable deployment kind, not only Hardlight;
- mirrors normally through the authored map plan.

`role-deployments-in-zone-count` is the shared readiness truth. It:

- requires an authored participant `role`, target `zone`, and expected
  deployment signature/kind;
- counts live Active deployments with the expected identity whose owner stable
  slot belongs to that allied role and whose authoritative footprint intersects
  the zone;
- counts a suppressed deployment as present for the same anti-recast reason;
- returns the same team-relative value no matter which participating body
  evaluates the shared readiness gate; and
- reads the existing authoritative signature-state owner, kind/id, phase, and
  footprint. It creates no deployment registry or Program state.

Do not use the owner-local fact as a shared multi-role readiness clause. A Guard
or Kestrel evaluating “this body's deployment is at the choke” would otherwise
keep the whole formation permanently unready even though the Mason's wall is
correctly present. If several required deployment targets could be satisfied by
one footprint from the same eligible owner pool, apply the same visible
non-overlap/repair rule used for required Posts; do not count one construct as
several required setup slots.

For an exact target, the Program owns a derived one-tile zone linked to the
effect anchor. Moving or renaming the target updates both in one edit. The
normal maintain policy materializes:

```text
wall-at-choke:
  fact: self-deployment-in-zone
  operator: equals
  value: 1
  zone: wall-target-zone

wall-missing-at-choke:
  fact: self-deployment-in-zone
  operator: equals
  value: 0
  zone: wall-target-zone

home-wall-ready:
  fact: role-deployments-in-zone-count
  operator: at-least
  value: 1
  role: home-mason
  zone: wall-target-zone
  signature: hardlight-block
```

Update the shared Orders validator/compiler, `OrdersPackage`, tactical mind,
web authoring vocabulary, documentation, and focused parity/condition tests.
This is a read of existing authoritative signature state, not new match state.

### Maintenance policy

Normal Post authoring offers:

- **Rebuild when missing** — the required acceptance behavior;
- optionally **refresh when durability is at or below X%**;
- optionally **refresh when lifetime is at or below X%**.

Missing-at-target is checked separately from global deployment activity. If the
same body owns a wall elsewhere, maintaining this target intentionally recasts
and replaces the old wall under the existing one-deployment-per-stable-slot
rule.

Do not build nested boolean Conditions for refresh. Materialize ordered,
inspectable use leaves—missing first, then damaged/expiring policies—under the
same Post host. Gate damaged/expiring refresh with deployment presence so the
existing zero-when-missing percentage behavior cannot masquerade as damage.

Warn when the same role/body is configured to maintain incompatible targets in
several Programs. Do not forbid legitimate relocation tactics; show which
higher Program wins and that a recast replaces the lower Program’s deployment.

## 3. Project the canonical Post into ordinary Instructions

The ergonomic composition should materialize approximately this ordinary
shape (names are illustrative):

```text
MASON POST DUTY
  rebuild wall
    when: self is at Mason Post AND wall is missing at Well Choke
    capacity: 1
    use: Hardlight Block at Well Choke

  committed home response
    when: shared threat AND every required setup item is ready
    until: minimum commitment elapsed AND success outcome
    timeout: maximum commitment
    fight/response continuation

  stand and watch
    capacity: 1
    move: Mason Post
    face: Corridor Mouth
```

The exact structural nesting may differ to respect the existing depth and flat
expression limits, but these observable semantics are mandatory:

- while travelling, the body moves to the Post;
- on arrival, it rotates toward the facing point and holds there;
- only a body at the Post attempts the wall;
- if the correct wall is present, the use leaf is ineligible and execution
  immediately continues;
- if the wall is absent but the signature is cooling down or geometrically
  illegal, execution immediately continues and Practice says why;
- once legal, it casts at the intended target;
- after destruction/expiry it recasts when legal;
- a wall owned by that Mason at a different target does not satisfy this Post;
- if the response is active and the wall disappears, the maintenance
  opportunity may consume one response tick to rebuild, then the ordinary
  response resumes;
- success, timeout, death, pre-emption, and Program fallthrough use existing
  Instruction lifecycle rules.

This is the intended answer to “should `use` be inside `move`?”: it is inside
the **same player-facing Post**, but remains a higher-priority sibling of the
ordinary move/fight continuation in executable source.

## 4. Fix tactical commitment and stable staging

The current generic contingent’s “commitment timeout” is only a timeout. With
no `until`, the runtime re-evaluates `when` every active tick, so a visibility
or readiness loss can disarm the response immediately. Correct this before
calling Programs playable.

### Normal lifecycle controls

Expose direct Program controls with human language:

- **Start when** — shared tactical trigger plus required setup/readiness;
- **Commit for at least N ticks** — trigger/readiness loss does not cancel the
  active response during this window;
- **After that minimum, finish when** — optional success outcome;
- **Give up after N ticks** — maximum timeout;
- **Re-arm after N ticks** — ordinary cooldown;
- **Then** — return to Posts, fall through to the next Program, or execute an
  authored continuation.

Materialize the existing latch semantics:

```text
when  = trigger AND readiness
until = minimum-self-order-ticks AND optional-success
timeout = maximum
cooldown = re-arm delay
```

An active node with `until` already stops re-evaluating its `when`; use that
ordinary behavior. Do not add a Program commit flag.

Support the common success choices in the normal Program UI, derived from
existing facts rather than raw Condition ids:

- N confirmed enemy destructions in this area since response entry;
- this body is currently carrying a Core;
- an allied Core pickup/catch was observed since response entry;
- required area currently has zero **visible** enemies, labelled “visibly
  clear” rather than “known empty”;
- no observed entry during a fully observed interval, when the player chooses
  the stronger evidence requirement;
- required deployment present or gone;
- minimum duration only;
- maximum timeout only.

Do not conflate current custody with a remembered event. The first Core choice
uses existing `self-carrying` and can become false again after Handoff, Toss,
Drop, or banking. The second adds one ordinary historical count fact,
`friendly-core-pickups`, with the existing `windowTicks` / `since: node-entry`
contract, derived from authoritative `CorePickedUp` events. Label it **Core was
picked up/caught**, not the broader “acquired.” A successful Arc Toss catch
emits that event and therefore counts; a Toss attempt/miss, Handoff, Drop, or
bank does not. This is event evidence, not a Program success flag or mutable
observation store.

Validate that maximum timeout is not shorter than minimum commitment and make
zero/no-success choices explicit rather than manufacturing contradictory
clocks.

Keep Advanced exact exit Conditions available for uncommon cases.

### Stable preparation and fallthrough

Fix the current stage-node re-entry pattern. An unconditional stage with only
`until = ready` can re-enter after it exits and steal every other tick even
while already ready.

For conditional preparation, derive an ordinary complementary not-ready
Condition and materialize:

```text
when  = preparation-start AND not-ready
until = ready
```

Once ready, the stage must remain ineligible until readiness is genuinely
lost. If the response trigger is absent, lower Programs must receive every
tick unless the player explicitly chose **While waiting: hold these Posts**.

Make the waiting policy visible:

- **Hold these Posts** — this Program intentionally owns the participant while
  waiting;
- **Prepare only when …** — stage under a chosen condition, otherwise fall
  through;
- **Do nothing until response** — no staging ownership, fall through.

The Program card must state which choice currently prevents or permits lower
Programs. This is crucial to keeping top-to-bottom priority understandable.

When **Hold these Posts** is selected, arriving participants remain on their
assigned Posts until engagement, an authored abort/rearm condition, death, or
normal higher-priority pre-emption. This must be a stable ordinary hold
continuation, not an unconditional preparation node that repeatedly exits and
re-enters. The Mid Overload proof below must use this policy for both staged
formations.

## 5. Improve condition and opportunity ergonomics without another language

Replace the single disabled “When” dropdown behavior on signature
opportunities with direct clause controls:

- **Use from** this Post/tile/zone;
- **Affect** this body/role/route/tile;
- **Attempt when** one or more ordinary conditions hold;
- **Maintain** missing/damaged/expiring deployment at target;
- **Otherwise** show the exact continuation.

The activation-window button must remain available when another condition is
already present. The editor may combine flat `all`/`any` clauses within the
existing limit or create a transparent parent branch where mixed grouping is
needed. Never strand the player on “inspect Advanced to change this combined
condition.”

Surface a compact set of common tactical triggers directly:

- visible enemies in a drawn area;
- remembered enemies in a drawn area with a freshness window;
- observed enemy entries in an area over a window or since match start;
- confirmed destructions in an area since Program/response entry;
- role/body count in an area;
- self health and lowest health in a selected role;
- this body's nearest reachable visible-enemy distance;
- this body's nearest reachable visible-loose-Core walking distance;
- this body's nearest reachable healing-tile walking distance;
- Core carried/value/source state;
- friendly/enemy bodies queued for the next synchronized respawn wave and
  ticks until that wave;
- this body's level, highest level in a role, and the existing
  most-experienced-ally selector;
- own/enemy reactor charge, own pulses/integrity, and public Well outstanding-
  Core, next-birth, and socket state;
- deployment present/durability/lifetime.

The underlying Condition remains visible in Advanced. This is guided
authoring, not natural-language parsing and not a hidden inference engine.

All signature target capabilities remain descriptor-driven. Adding a future
signature with an existing target shape must not require a switch in the Post,
Program, or map components.

### Route-forward movement signatures

Keep Kestrel's existing generic `RouteForward` signature target available in
the normal Program surface. It is the appropriate counterpart to route-aware
movement, but it is not Relay's `aheadVia` and it is not a Kestrel Program
recipe or runtime state. Dash needs a destination, so the ordinary signature
resolver derives one from the selected route's immediate legal forward course.

The complete normal authoring flow should read approximately:

```text
Kestrel corridor run
Move to Enemy Well via Long Corridor
While traversing that route inside Raid Dash Window
Dash at least 2 tiles forward along Long Corridor when legal
Otherwise continue to Enemy Well via Long Corridor
```

From the map-first Program surface the player must be able to:

1. select the Kestrel role (a one-body role in the proof, resolved through
   ordinary role allocation rather than a fixed unit id);
2. choose the ordinary final destination—a zone, anchor, Well, reactor, Core,
   or other existing `move.to` target;
3. draw or select a terminal route used as that movement's `via`, or select an
   existing terminal patrol route;
4. attach Dash to **this movement's route** by default, so the route is chosen
   once rather than copied independently into two normal controls;
5. draw the tile/zone/corridor span in which Dash may be attempted;
6. choose the minimum useful forward gain;
7. preview the eligible straight spans and understand that one Dash cannot
   turn a corner; and
8. keep ordinary movement to the destination through that same `via` route as
   the same-tick fallback.

Materialize the existing ordinary shape, with the activation zone included in
the opportunity's normal `when` clauses:

```json
{
  "skill": "vector-dash",
  "target": {
    "route": "long-corridor",
    "progress": "forward",
    "minimumForwardTiles": 2
  }
}
```

The use leaf remains immediately above the ordinary route-movement sibling.
For the recommended destination flow, that sibling is an ordinary
`move.to = destination` with `move.via = long-corridor`. A patrol continuation
remains valid when patrol is genuinely the player's intent. The workshop link
must derive the use target's route reference from the continuation by default:
changing, renaming, or removing the movement `via` updates or diagnoses the
linked Dash in the same undoable edit. Do not add `dashVia`, overload
`aheadVia`, or make the player maintain two route selectors that can drift.

The existing route-forward resolver must choose only a legal, heading-aligned,
unoccupied straight destination that advances sufficiently along the current
course. At a bend, outside the activation window, on cooldown, without enough
gain, or when the span is illegal, it declines with a non-empty reason and the
same route movement is considered in that same tick. It must never manufacture
an off-route shortcut, reverse progress, or require hard-coded waypoint
anchors. Existing signature commitment/tell handling remains authoritative.

Pin both continuation forms. In particular, a `move.to` episode traversing a
held `via` route must expose that same governed forward course to Dash. Once
the ingress route is complete, the Dash opportunity naturally becomes
ineligible and the ordinary move continues from the route endpoint to the
final zone/anchor/Well/etc. The first tick before a route course exists must
fall through to movement and establish it; it must not wait for Dash.

This workflow must be descriptor-driven: another future movement signature
that publishes `RouteForward` receives the same controls without a skill-id
switch. Kestrel supplies the required proof because it is the current class
with that capability.

### Route-linked opportunities on patrols

Patrol is an equally important continuation. A player must be able to select
an existing patrol route, tap one or more places along it, and attach ordinary
signature opportunities without turning the route into a waypoint script or
adding patrol runtime state.

The normal composition should read approximately:

```text
North watch patrol
Patrol North Loop

At Flare Overlook (within 1 tile)
  when the North Approach has remembered enemies
  use Survey Flare at North Approach

At Gate Service Point
  when the sentry at Gate Post is missing
  use Sentinel Seed at Gate Post

Otherwise continue North Loop
```

A route-linked opportunity reuses the same generic pieces already defined in
this plan:

- a route-linked source Post/anchor plus a derived `self-in-zone` tolerance;
- a separate effect anchor, role/selector target, or self target authorized by
  the signature descriptor;
- ordinary trigger clauses, optional minimum ticks since last signature use,
  or `self-deployment-in-zone` maintenance for durable deployments;
- a visible local opportunity order when several source windows overlap; and
- the original ordinary `move.patrol` as the final continuation.

This is editor linkage only. Materialization remains a parent branch containing
one or more conditional `use` leaves above one patrol-movement sibling. When a
source window or trigger is false, the signature is cooling down, its target
is illegal, or capacity is lost, patrol movement is considered in the same
tick. After an accepted use and any authoritative tell/channel completes, the
same patrol cursor resumes forward without restarting, reversing, skipping to
the closest waypoint, or forgetting its lap.

The map-first flow must let the player:

1. select a patrol continuation and choose **Add opportunity here** on a route
   point/segment;
2. set the source tolerance by dragging its visible envelope;
3. independently place or select the effect target;
4. choose a meaningful trigger, maintenance policy, and optional minimum
   interval rather than receiving an opaque “cast whenever possible” default;
5. add and visibly order several opportunities along one patrol;
6. drag a route station or target later without editing generated Conditions,
   anchors, or Instructions; and
7. inspect why an opportunity fired or why patrol continued past it.

When a source station is attached to a route waypoint, moving that waypoint
moves the source anchor/envelope in the same undoable edit. A station attached
to a segment must retain a visible route relationship or become a blocking
linkage diagnostic if route editing leaves it off the traversed corridor; do
not silently snap it to an unrelated nearest tile. The effect target remains
independently draggable.

Repeat behavior must remain ordinary and honest:

- engine readiness/cooldown always applies;
- the player may add an existing `self-ticks-since-signature-use` minimum
  interval;
- a non-durable Flare/Smoke/artillery opportunity may fire again on a later
  pass only when its authored trigger and readiness are true;
- a durable Mine/Hardlight/Sentinel/Prism opportunity with **maintain here**
  does not recast while the correct deployment is present at that target, and
  may rebuild on a later pass after destruction/expiry; and
- do not invent “once per lap,” waypoint callbacks, a patrol action queue, or
  hidden visit memory. If a future design truly needs per-lap truth, that is a
  separate explicit state/Condition decision.

One stable unit slot can still own only the rules-authorized live deployment.
If the same body has maintain/placement opportunities at several patrol sites,
the UI must explain that casting at the next site relocates/replaces the prior
deployment; it does not promise several simultaneous Sentries or Mines. Warn
about likely unintended churn while allowing an explicitly acknowledged
mobile-deployment patrol. Multiple eligible bodies remain governed by normal
roles and capacities.

Route-linked source windows work on valid closed patrols and on terminal route
continuations. This feature does not silently relax a signature target's own
descriptor/grammar contract: in particular, loop-forward Vector Dash remains
separate from the generic “use near this patrol station” composition unless it
is deliberately admitted and pinned by route-forward tests.

### Generic participant opportunities

Do not stop at signature-only **Along the way** cards. Any Program participant
behavior with an ordinary durable continuation must be able to combine four
normal opportunity kinds in one visibly ordered list:

1. **Use a skill** at an authored source station, Post, or activation area;
2. **Fight**, activated by the host Program response or an authored body-local
   contact range;
3. **Recover a nearby Core** when empty-handed and a currently visible loose
   Core is within an authored walking distance; and
4. **Recover health** by diverting to an in-range healing tile at low health,
   remaining until an authored higher-health threshold or maximum attempt.

Availability is generic; behavior is not automatic. A new participant keeps
only its authored base continuation until the player adds a card. Do not seed
Fight, Core pursuit, or health recovery invisibly into every Program. The
player's opt-in, order, scope, thresholds, and capacity policy are the strategy.

Use one underlying **Opportunities** component. Label it **Along the way** on a
movement continuation and **While assigned here** on a Post/hold when that reads
better, but do not implement separate priority or materialization systems.
Location-bound signature cards use their authored route stations or Posts.
Fight, Core recovery, and health recovery default to **Anywhere during this
behavior** because their source is the body; do not force the player to draw
fake Posts for them. The player may add an ordinary activation area if the
tactic should apply only in one spatial part of the behavior.

The patrol proof must be readable approximately as:

```text
North watch · Scout role
Patrol North Loop

Along the way · top to bottom
1. Below 30% health, seek a healing tile within 8 steps until 70% health
   Patients per healing tile: 1 · across this sheet
2. At Flare Overlook, Survey Flare the North Approach when enemies are known
3. Fight when this body can join a visible enemy within 4 steps
   Advance · chase at most 3 tiles · Attackers per enemy: All eligible
4. When empty-handed, recover a visible loose Core within 5 walking steps
   Pursuers per Core: 1 · across this sheet

Otherwise continue North Loop
On pickup: Bank carried Core takes priority
After banking: resume North watch if still active
```

The player may drag these cards into a different order. This is a consequential
strategy decision: if Fight is above Core recovery, contact wins that tick; if
Core recovery is above Fight, the objective diversion wins; if health recovery
is higher, survival wins while that episode remains active. The numbered list
must be the actual direct-child Instruction order, not advisory metadata. The
ordinary continuation is visibly pinned last. A false Condition, missing
target, lost capacity claim, illegal action, or cooldown proceeds to the next
sibling in the same tick.

Make the same opt-in cards available on these ordinary continuation shapes:

- `move.to` for a map or dynamic destination, with or without `move.via`;
- `move.patrol`;
- `move.follow`;
- a Post or `current-position` hold; and
- a durable fight/response continuation, where adding a second redundant
  Fight-on-contact card is replaced by editing the existing Fight behavior.

If one participant has distinct setup, waiting, response, and aftermath
continuations, the player selects which one owns the opportunity list. Adding
health recovery to “while waiting at these Posts,” for example, must not
silently let it interrupt the committed response too. A one-shot `use`,
`handoff`, or `drop` with no durable fallback is not a valid host; prompt the
player to choose or add the continuation instead of manufacturing one.

Require the complete integrated proof on a patrol, then pin the same
materializer and fallback semantics on `move.to`/`via`, follow, Post/hold, and
fight hosts. Do not add an “autonomy mode,” interrupt scheduler, route-local
state machine, or opaque behavior policy. The workshop sidecar owns only the
link between each human card and its ordinary Condition/action leaf.

#### One Fight behavior: Program response or contact

There is exactly one player-facing Fight behavior editor and materializer. It
always exposes:

- ordered target preferences;
- optional **Only fight targets in this area**;
- advance/hold/kite style;
- chase limit from the fight-entry position; and
- the visible **Attackers per enemy** capacity policy.

Its activation source is orthogonal and explicit:

- **When this Program responds** inherits the owning Program branch's shared
  trigger, all-Posts-ready relationship, minimum commitment, success, timeout,
  and cooldown. This is how an ambush springs;
- **On contact** adds the existing
  `self-nearest-visible-enemy-distance` Condition, labelled accurately as the
  legal walking steps this body needs to reach a firing tile—not geometric
  distance to the enemy; or
- **Additional Conditions** refine either form through the same ordinary flat
  Condition controls.

The optional target area is a generic filter on the ordinary Fight behavior,
not an activation trigger and not an Ambush mode. Materialize it as an optional
zone qualifier on the `fight` target selection. Filter hostile candidates by
their current occupied tile (or current footprint for an attackable hostile
deployment) before ordered target preference and capacity resolution. If no
legal hostile remains in the area, Fight yields to its ordinary continuation;
do not latch a target identity or chase it out of the authored area.

Allow the same optional zone qualifier on
`self-nearest-visible-enemy-distance` so **On contact in this area** measures
the body's legal walking distance to a firing tile for a qualifying enemy, not
one enemy for the Condition and another for the action. The Program trigger
area, contact/activation area, and Fight target area are distinct map overlays
and must never be silently inferred from one another.

These are activation bindings on the same Fight behavior, not separate
`AmbushFight`, `OpportunityFight`, engagement, or runtime concepts. Switching
from inherited response to On contact must preserve the target-area/order/
style/chase/capacity configuration and change only the ordinary Condition/
lifecycle linkage. A recipe may prefill those values but may not own another
Fight model or editor.

For a multi-role ambush, each participant role may share the Fight behavior or
refine its ordinary target-area/order/style/chase choices. Cross-role
**Attackers per enemy** uses the existing named-capacity linkage. No role
receives a hidden combat policy merely because another participant fights.

In the Mid Overload ambush, the shared enemy-in-mid trigger plus exact Post and
deployment readiness activates the committed response. Its Fight behavior
inherits that activation and explicitly limits targets to the authored return
killzone; it must not redundantly require the original enemy count or a fresh
per-body sighting every tick. Momentary visibility/readiness loss therefore
does not cancel the response. If a participant currently has no actionable
Fight target inside that area, the ordinary `fight` leaf yields to that
response's authored hold/continuation while the parent commitment remains
active. An enemy leaving the area is not pursued merely because it originally
sprang the trap.

If no legal target is actionable, the leaf yields to the next opportunity or
continuation that tick. When the Fight activation/lifecycle exits, the exact
host continuation resumes: the same patrol cursor, held `move.via` course,
current follow target selection, or Post/hold destination. Entering combat must
not silently restart a route or convert the chase limit into distance from a
waypoint. Existing minimum-commitment and maximum-attempt controls remain
available when the player wants an On-contact fight to survive a momentary
sighting loss, but the card does not create its own latch. An inherited ambush
response uses the parent Program commitment instead.

#### Recover a nearby visible Core

Add one body fact:

```text
self-nearest-visible-loose-core-distance
```

It is the fewest legal movement steps from this body to the exact tile of a
currently team-visible loose Core that this body may collect. A sighting from
any allied sensor counts, but distance is computed independently for each
body. Known durable blockers, protected terrain, and spawn reservations count;
body occupancy remains transient traffic. Comparisons use the same `0..31`
upper-bound convention as visible-enemy approach distance. With no reachable
currently visible loose Core, every finite upper bound is false. This fact is
current perception, not the persistent known-Core ledger.

Add the matching dynamic move target and a reusable bounded-target qualifier:

```json
{
  "move": {
    "to": "nearest-visible-loose-core",
    "withinWalkingSteps": 5
  }
}
```

`withinWalkingSteps` is optional in `0..31`; **Any distance** omits it. It is a
generic filter for dynamic move targets that publish an ordered set of exact
terrain destinations. In this goal, support it for
`nearest-visible-loose-core` and the existing `nearest-heal-zone`; reject it on
targets without that capability rather than silently ignoring it. It filters
**every** candidate before target-capacity allocation, not merely the first
candidate.

The optional qualifier must not silently change historical unqualified target
resolution. Existing Orders using bare `nearest-heal-zone` retain their exact
behavior; the bounded form opts into the shared legal-walking candidate helper.
Program documents remain behavior-preserving until the player adds/edits this
Opportunity.

For a Core, this matters when the nearest candidate has already reached its
one-pursuer capacity: the losing body may try another visible Core only if that
Core is also within its authored bound. The target orders the remaining
candidates by this body's legal walking distance and then stable Core identity.
No reachable in-bound candidate means the move yields to the next sibling in
the same tick.

The normal recovery card materializes both
`self-nearest-visible-loose-core-distance <= N` and `self-carrying = 0`, plus
the identically bounded target. Condition and target must share one canonical
distance/candidate implementation so the UI cannot say “within five” while the
action chases something farther away. Walking onto the Core remains the
existing physical pickup rule; do not add a `pickup` action.

Be explicit about the consequence: value/distance filters govern deliberate
pursuit, not pickup permission. A patrol, ingress, retreat, displacement, or
Toss landing can put any body onto a loose Core and the physical rule will load
it. The normal UI calls these **pursuit filters**, explains incidental pickup,
and never promises that a rejected Core is avoided.

##### Value-aware pickup and custody

The underlying Instructions language already exposes
`self-carried-core-bank-value` and
`highest-bank-value-known-loose-core`. Preserve both. The first can already
gate return movement, Fight, signature use, handoff, or `drop`; the second can
prefer a distant higher-value Core from the persistent known ledger. That is
not yet sufficient normal Program UX: it cannot express “take a 4-point Core
within seven walking steps, otherwise take any Core within three,” and the
known target is not the currently-visible nearby behavior promised here.

Add one reusable Core-value comparison shape:

```json
"coreBankValue": { "operator": "at-least", "value": 2 }
```

The operator is `equals`, `at-least`, or `at-most`; value is `1..4`. The normal
UI labels this **Core is worth** and obtains the current attainable values and
point terminology from the rules catalog. On current Tapline these are
team-relative 1/2/4 bank points. Do not infer value from map coordinates in the
editor.

Allow that shape on the matching visible-Core distance fact and target:

```json
{
  "fact": "self-nearest-visible-loose-core-distance",
  "operator": "at-most",
  "value": 7,
  "coreBankValue": { "operator": "equals", "value": 4 }
}

{
  "move": {
    "to": "highest-bank-value-visible-loose-core",
    "withinWalkingSteps": 7,
    "coreBankValue": { "operator": "equals", "value": 4 }
  }
}
```

`nearest-visible-loose-core` remains the default and orders by legal walking
distance then stable Core identity. The new
`highest-bank-value-visible-loose-core` orders by team-relative bank value,
then legal walking distance, then stable Core identity. Both filter by
`coreBankValue` and `withinWalkingSteps` **before** capacity allocation. The
qualified distance fact uses the identical candidate filter; no qualifying
Core makes the fact false and the target empty. Existing unqualified visible
and known targets retain their exact behavior.

Expose two independent, compact recovery choices:

- **Core is worth:** Any, Exactly N, At least N, or At most N;
- **Prefer:** Closest or Highest value, then closest.

Allow several normal recovery cards in the same real local order. This makes a
player-authored ladder such as the following ordinary rather than a hidden
utility calculation:

```text
1. Recover a 4-point visible Core within 7 walking steps
2. Recover a visible Core worth at least 2 within 5 walking steps
3. Recover any visible Core within 3 walking steps
Otherwise continue North Loop
```

Each card still uses the visible **Pursuers per Core** capacity relationship.
False value/distance filters, no candidate, and capacity loss proceed to the
next candidate or card in the same tick. The editor shows the qualifying Cores
and their values on the map so “highest value” is inspectable, not an opaque AI
score.

Expose the existing `self-carried-core-bank-value` fact normally as **Carried
Core is worth**, automatically paired with `self-carrying = 1`. A linked Bank
Program may then contain visibly ordered carrier cases without duplicating the
source recovery configuration:

```text
Bank carried Core
1. Worth 4: Long Safe Return · two escorts
2. Worth at least 2: Centre Return · one escort
3. Any carried Core: Direct Return
```

These are ordinary value-gated participant branches. The first executable
branch wins; no Core-policy evaluator chooses a route. A recovery card may link
to this one banking Program regardless of which qualifying Core it collects.

Permit the same `coreBankValue` filter on
`nearest-friendly-core-carrier` Follow so an escort branch can say “escort a
4-point carrier” through ordinary target resolution and **Escorts per
carrier** capacity. An unqualified lower Follow branch can handle every other
carrier. No team-level carrier-value mode or dynamic role assignment is added.

Finally, expose ordinary **Handoff Core**, legal Core-carrying signature
opportunities such as Arc Toss, and **Drop Core** as one-shot leaves above a
Bank participant's durable return continuation. They use the same **Carried
Core is worth** and Additional Conditions controls as any other leaf. Drop is
the existing physical action: it chooses the legal adjacent tile nearest the
owner's reactor, does not destroy or bank the Core, does not start that Well's
recharge, and invokes the normal source-body reclaim delay. The UI must state
those consequences and warn about a repeatable pickup/drop loop; it must not
present Drop as discard or permit an authored landing tile. A false Condition,
no receiver, illegal signature, or unavailable Drop falls through to the next
ordinary sibling in the same tick.

Use **Pursuers per Core: 1** by default according to the player-friendly
capacity rules above. A body that loses the claim tries another visible,
reachable, in-bound Core and then the next Opportunity card. Claims remain
tick-local and target-local. Do not reserve a Core, latch its identity, or
silently fall back to `nearest-known-loose-core`. A player who wants persistent
recovery after vision is lost can author the existing known-Core recovery as a
separate Program with explicit lifecycle consequences.

Adding nearby recovery must also make the post-pickup consequence explicit.
The recommended normal choice is:

```text
On pickup · Bank carried Core (Program 1) takes priority
After banking · resume this behavior if still active
```

Only the Core-recovery leaf is intrinsically empty-handed. On the next tick,
the whole Program list re-evaluates and the visible higher-priority carrier
Program wins normally, even if the interrupted behavior has an active
lifecycle. After banking, ordinary evaluation returns to that behavior if it
is still active/eligible, preserving its existing route/Post state and clocks.

Preview the actual carrier Program and list position. A lower carrier Program
is not a safe default beneath an always-actionable host because it may never
win. The source card requires coverage for every role/body pool it can
deliberately load. In addition, run a roster-wide custody audit because every
fielded body can pick up incidentally under the physical rule. Offer one-click
extension of the same ordinary **Bank carried Core** Program to uncovered
roles, or an explicit visible **Keep carrying in this behavior** / value-gated
Drop handling choice. Starter sheets and the playable-alpha gate require a
banking or explicit custody consequence for all eight bodies; do not describe a
sheet as covered merely because its recovery cards are covered.

##### Reuse one linked banking tactic

Persist the **On pickup** choice as a stable workshop-sidecar reference to an
ordinary higher Program. The reference provides navigation, summaries, drift
diagnostics, and coverage validation only; it emits no executable call,
transition, return address, or Program id. One banking Program may be linked
from many recovery cards without copying any of its configuration.

The source card shows a compact live summary and an **Edit linked Program**
action, for example:

```text
On pickup → Safe bank · Program 1
Carrier: all 8 fielded bodies · South Return → own reactor
Escort: up to 2 per carrier · Guard + Medic · fight on contact
After banking: resume North watch if still active
```

Adding nearby-Core recovery must not require the player to anticipate this
separate Program. The normal flow:

1. reuses and links the sole compatible higher banking Program when one exists;
2. asks the player to choose when several compatible banking Programs exist;
3. offers an inline **Create Bank carried Core** setup when none exists; and
4. offers one-click **Extend carrier coverage** or **Move above this Program**
   repairs rather than cloning a partial return tactic.

All route, Toss, Fight, signature, escort, and recovery settings remain owned
and edited in the linked banking Program. Editing them from the source card
opens that same owner in context; it must not create source-local overrides.
Deleting, moving, or narrowing the linked Program updates every source summary
and becomes blocking when the promised takeover is no longer structurally
valid.

The linked Program may be a complete multi-participant tactic:

- every covered fielded role gets an ordinary `self-carrying` return branch to
  the own reactor, with its authored route and opportunities; the recommended
  generated coverage is all roster roles, including bodies that can only pick
  up incidentally;
- selected empty-handed escort roles become eligible while
  `friendly-carriers >= 1`, follow the resolved carrier, and use the same
  generic Fight/signature/health controls available on any Follow host; and
- when the last Core is banked, dropped, or lost with its carrier, these
  ordinary Conditions become false and every survivor falls through to its
  next Program. The source behavior resumes only if it remains active/eligible.

Add one small generic Follow selector so this works when any of several roles,
or one body inside a multi-body role, becomes the carrier:

```json
{
  "move": {
    "follow": { "selector": "nearest-friendly-core-carrier" },
    "posture": "rear"
  }
}
```

It resolves live allied Core carriers that match its optional
`coreBankValue` filter by legal terrain-path distance and then stable unit id.
With an ordinary capacity on the Follow leaf, a losing escort tries the next
carrier and then its next Instruction in the same tick. Expose that existing
target-local capacity as **Escorts per carrier**. No target means the Follow
leaf yields; there is no dynamic role assignment, escort reservation, sticky
carrier claim, hidden route constraint, or escort state. The carrying body's
return branch remains above its empty-handed escort branch when one static
role is eligible for both.

If pickup should end the tactic rather than merely interrupt it for banking,
the player chooses **Core was picked up/caught since response entry** through the
Program's normal success/end controls. If the intent is instead current
custody, choose **This body is carrying a Core** and accept that a later
handoff/Toss/drop can make it false before the minimum duration elapses. Do not
encode either as a hidden Opportunity transition. If a flat success expression
cannot represent the requested exit honestly, diagnose it and require the
player to refine that ordinary lifecycle rather than inventing nested
Opportunity state.

#### Recover health

This card uses existing health truth and physical healing. It is not a healing
AI, a new `heal` action, or Patchbay's Repair Beam. Materialize an ordinary
`move.to = nearest-heal-zone`; reaching the tile and waiting there invokes the
existing healing rule.

Add one matching body fact:

```text
self-nearest-heal-zone-distance
```

It is the fewest legal movement steps from this body to an exact healing tile,
using the same durable-obstacle and transient-body treatment as the other
body-local walking facts. It uses the `0..31` finite upper-bound convention. If
no healing tile is reachable, the fact fails every finite upper-bound
comparison. When a maximum detour is authored, this fact and the bounded target
must share one canonical candidate implementation.

Expose these direct controls:

- **Start below** N% health;
- **Recover until** M% health, where M must be greater than N to provide
  visible hysteresis rather than heal/leave oscillation;
- **Maximum walking detour**, using `withinWalkingSteps` or **Any distance**;
- **Give up after** an optional maximum attempt and **Try again after** an
  optional re-arm delay;
- **Patients per healing tile**, defaulting visibly to `1 · across this sheet`;
- optional activation area; and
- **Allow Core carriers to divert**, shown explicitly rather than inferred.

Healing tiles must be visible on the Program map at desktop and phone widths.
Selecting this card highlights every candidate tile and, where the host has
authored geometry, previews which route/Post areas lie within the static
walking bound. Label occupancy and destructible-blocker effects as dynamic; do
not imply the authoring preview can predict match-time traffic. In Practice,
show the selected body's actual legal path distance and resolved tile.

The ordinary materialization is:

```text
when     = self-health-percent <= start threshold
           AND self-nearest-heal-zone-distance <= maximum detour (when bounded)
           AND carrier policy
until    = self-health-percent >= recovered threshold
           OR became a carrier when carrier diversion is disabled
timeout  = maximum attempt
cooldown = re-arm delay
action   = move to nearest-heal-zone withinWalkingSteps N
capacity = shared Healing policy (default 1 per resolved tile)
```

Once entered, the existing `until` lifecycle keeps the recovery episode latched
when health rises one point above the start threshold; it ends at the authored
recovered threshold or maximum attempt. If the maximum expires while the start
Condition remains true, a zero re-arm delay would immediately restart the same
episode, so diagnose that combination and offer a positive retry delay. Do not
add a recovery governor or hidden health state.

The latch does not create a new priority rule. Opportunity cards above health
recovery may still pre-empt it through their ordinary sibling order; cards
below it run when its move has no legal/capacity-winning target or after its
lifecycle exits. Show this plainly in the Program summary and Practice trace.

`withinWalkingSteps` filters every healing tile before capacity allocation.
The nearest tile being claimed may send a body to another tile only when that
tile is also within its maximum detour. If every in-range tile is claimed,
unreachable, or absent, the leaf yields to the next Opportunity or exact host
continuation that tick while its ordinary lifecycle remains explainable.

After success, carrier-abort when disabled, or timeout, resume the exact host
behavior: the held `move.via` course, patrol cursor, current follow selection,
Post/facing, or fight continuation. Ordinary higher-priority pre-emption does
not rewrite the recovery lifecycle; when the higher behavior ends, the still
active recovery episode resumes, otherwise the host does. Preserve the existing
per-leaf movement progress through the interruption; do not restart a completed
ingress or select a nearest patrol waypoint as a substitute for the authored
cursor. This reuses existing lifecycle/movement state rather than creating an
Opportunity state machine.

Default **Allow Core carriers to divert** to off for a general-purpose card so
healing does not silently outrank banking. If the selected host is already
carrier-only, require a visible yes/no choice instead of generating an
impossible card. As with every Opportunity, the player may reorder health
recovery relative to Fight, Core recovery, and signatures and sees the exact
consequence in the one local priority list.

### Alpha signature breadth without bespoke skill editors

The existing descriptor catalog already covers all sixteen current classes.
Preserve that breadth and make it usable through the new Post/Program surface;
do not replace it with sixteen recipes or skill-id branches. The alpha needs
one honest normal authoring path for each of these generic families:

| family | current classes | normal Program composition |
| --- | --- | --- |
| route movement and custody | Kestrel, Relay | attempt Dash/Toss along an authored movement episode, then continue that exact movement |
| exact point or area effect | Lantern, Mortar, Minesmith, Mason, Veil, Nest | use from a Post/zone or route-linked patrol station at one map anchor; maintain only durable deployments, otherwise rely on trigger/cooldown |
| watched lane or hostile target | Palisade, Towline, Longshot, Sunder | stand/face or move through an authored area, choose a legal hostile priority, attempt contextually, then hold/fight/move |
| protected friendly | Patchbay, Switchback | name a role or veteran, use health/location/threat Conditions, preserve the public channel/tell, then resume escort/formation behavior |
| self-centred local response | Hush, Repulsor | occupy or enter an authored local area, trigger on enemy pressure, use on self, then continue the response |

The Program summary must distinguish **what caused the opportunity to start**
from **how its target is selected**. For example, “enemies entered North Lane”
does not silently mean “nearest enemy is restricted to North Lane.” The player
must choose **Only fight targets in North Lane** when that restriction is part
of the tactic. Show both clauses and map overlays accurately rather than
implying hidden target intelligence.

In addition to the Mason, Kestrel, and Relay headline proofs, retain or add
small deterministic Program-authored scenes for the mechanically distinct
seams:

- Patchbay repairs a damaged named one-body role or veteran and its ordinary
  continuation does not cancel the authoritative channel;
- Palisade aims Prism Wall from a Post toward its authored lane, and Longshot
  completes a rooted Rail Line tell before returning to its Post continuation;
- Mortar uses Falling Star at an exact watched anchor when its area trigger is
  true, preserves the tell, and falls through when the shot is illegal; and
- Hush or Repulsor triggers its self-centred effect from the intended defended
  area and otherwise continues its ordinary hold/fight response.

These are focused mechanism/usability scenes, not four additional starter
doctrines, galleries, or balance samples. Every other current signature still
needs automated create/edit/regenerate/save/reload/detach/compile coverage
through the same descriptor-driven UI. A future seventeenth signature using an
existing family must acquire its normal controls without changing the Post,
Program, or map components.

### Prove veteran- and timing-centred strategy is authorable

The alpha should expose the game's strategic clocks and veteran truth as normal
Program clauses, not leave them technically available only in Advanced. Use
the existing facts/selectors; add no scheduler or new team state.

Pin two small deterministic authoring scenes:

- **Veteran spearhead:** a level-gated veteran moves or holds the lead while a
  capacity-bounded support group follows the existing most-experienced-ally
  selector, fights/heals through ordinary opportunities, and cleanly selects a
  new veteran or falls through after death/level change. The map and Practice
  identify who currently satisfies “most experienced” and why.
- **Wave/Well window:** survivors stage or continue lower work while ordinary
  Conditions inspect friendly/enemy respawns queued for the next wave, ticks to
  that wave, and a selected Well's outstanding-Core/next-birth state; they push
  when the authored flat clauses become true. Reactor charge/pulse/integrity
  truth is available in the same palette for score-aware variants.

These are focused strategy-language proofs, not additional starter doctrines,
automatic timing logic, or claims that the authored tactic is optimal.

## 6. Make Practice explain Programs well enough to iterate

Because Programs remain editor-only, derive these views by correlating the
workshop sidecar with generated Instruction paths and authoritative replay/debug
events. Do not add Program ids to ATL2 or replay.

That correlation must be revision-safe. Starting a Practice match stores an
immutable, owner-only **Practice authoring snapshot** beside its analysis
metadata: sheet id/revision, normalized workshop JSON, and either the generated
source-path mapping or enough normalized linkage to reproduce it. It is not
part of executable Orders, ATL2, the match content/gameplay hash, opponent
input, or public replay data. Editing, renaming, reordering, or deleting a
Program after the run must never cause an old replay to be explained using the
new draft.

Practice labels the replay and draft revisions when they differ and offers
**Open the revision used for this run** and **Rerun current draft**. A legacy
run without a snapshot may explain raw Instructions but must say that Program
attribution is unavailable rather than guessing from the current sidecar.

### Make top-to-bottom reachability legible

Before Practice, show a compact per-role coverage/fallthrough strip across the
one Program list: which fielded bodies a Program can own, its visible gate, and
the next Program they can reach when it yields. Run conservative structural
diagnostics for facts the materializer can prove:

- an unconditional durable continuation that makes lower Programs unreachable
  for the same role;
- a linked Bank carried Core Program shadowed by an earlier carrier-eligible
  continuation;
- a fielded role with no terminal useful behavior;
- a participant phase whose promised fallback is absent; and
- a required readiness/role/deployment relationship that cannot be filled.

Block save only for proven invalid generated structure or a normal-surface
promise such as a linked banking takeover that cannot occur. Warn for potential
dynamic shadowing and direct the player to Practice; do not pretend to solve
arbitrary Condition satisfiability. Reordering a Program updates this strip and
all linked takeover summaries in the same edit.

For a selected Program and body, Practice must answer:

- which Program currently supplied the body’s eligible/accepted Instruction;
- which higher Programs were ineligible and why;
- Posts filled and missing, by role;
- required deployment present, missing, damaged, expiring, or suppressed;
- waiting, preparing, ready, committed, success, timeout, cooldown, and
  fallthrough in terms derivable from ordinary source;
- signature attempted, accepted, or declined, with the exact reason (cooldown,
  range, blocked line, missing target, wrong route progress, etc.);
- each Opportunity card considered in its visible order and why it acted or
  yielded, including encounter distance, Core visibility/walking distance,
  health start/recovered thresholds, healing-tile detour, carrying policy, and
  target-capacity loss;
- the source recovery card's linked banking Program, whether that structurally
  valid higher Program actually took over, which carrier each escort resolved,
  and any **Escorts per carrier** capacity loss or later fallback;
- every visible Core's team-relative bank value considered by a recovery card,
  the value/distance filter and preference that selected or rejected it, the
  carried-value branch that won, and the exact consequence of handoff, Toss, or
  Drop;
- whether a Core pickup followed a selected recovery target or happened
  incidentally while another movement crossed it, and which roster-wide
  carrier branch then won;
- whether Fight inherited a Program response or used On-contact activation,
  shown separately from its target-area/order/style/chase configuration;
- capacities in their player-facing terms—such as “one pursuer already owns
  this Core this tick” or “nearest healing tile already has a patient”—rather
  than only a generated capacity id;
- the exact host continuation chosen when an Opportunity did not execute or
  after its lifecycle ended;
- the response’s minimum-commit and maximum-time clocks;
- the exit/fallthrough cause.

Clicking a body or status row should highlight its current Post, resolved
cardinal facing and rules-derived visible footprint, signature target/window,
separate Fight target area, base route, resolved Core/healing target and
authored walking bound, and relevant readiness group on the map.

Add a compact post-run Program timeline/summary sufficient to answer:

- Did it ever prepare and arm?
- How long did it remain committed?
- Did the intended signature fire, and where?
- Did contact or a nearby Core divert the body, and why did any closer-looking
  target fail the authored walking-distance or capacity test?
- Did health recovery start, reach its recovered threshold, abort for cargo,
  or time out, and which base behavior resumed?
- What success/timeout/failure ended it?
- Which Program took over next?

Never infer success that the replay cannot prove. Say “not observed” or show
the exact source-path evidence when necessary. Every executor action, decline,
and hold continues to require a non-empty debug reason.

## 7. Deliver two complete starter doctrines

Do not force the first player to begin from a blank sheet. Deliver two complete,
legal, editable starter sheets with a visible high-priority carrier-banking
Program plus sensible lower-priority loose-Core recovery, healing, and combat
fallbacks so every body has useful behavior when its headline Program is
inactive.

The carrier-banking Program covers all eight fielded bodies, including roles
with no normal recovery card, because ordinary movement can pick up a Core
incidentally. Do not demonstrate a global-looking fallback that silently omits
one participant role.

Their ordinary loose-Core recovery uses the player-facing **One bot per Core**
default and previews the higher **Bank carried Core** Program that will pre-empt
after pickup. Their ordinary health recovery uses **One patient per healing
tile**, visible start/recovered thresholds, a maximum detour, and a real base
continuation. These are not doctrine-specific allocation rules; the starter
sheets simply demonstrate the generic Program behaviors described above.

They must be separate sheets: the one-duplicate-wildcard roster rule permits
either twin Masons or twin Relays in one eight-body roster, not both.

### A. Mid Overload Fork — duplicate Mason

Use an eight-body roster with seven distinct classes and one duplicate Mason.
The exact class mix may be refined, but a valid illustrative split is:

- **2-body home defence:** one Mason plus one ally at exact Posts; maintain a
  Hardlight target at the own-Well entry;
- **5-body return ambush:** the other Mason plus four mixed-role bodies at five
  exact Posts; maintain a second wall on the predicted return lane;
- **1-body raid:** Kestrel takes the authored long corridor toward the enemy
  Well and uses the normal route-forward Dash Program flow only in its legal
  activation span.

The shared trigger is at least five **remembered/known** enemies in the drawn
mid area within an explicit freshness window. It must not depend on them all
remaining visible every tick.

Acceptance behavior:

- each ambush has multiple roles and capacity-1 exact slots;
- it reports every slot and required deployment separately;
- neither ambush arms until all its required setup is ready;
- the shared Program trigger/readiness springs each committed response, then
  every participating role fights through the same ordinary Fight behavior,
  editor, capacity policy, and generated `fight` leaf used by On-contact
  behavior elsewhere;
- the return ambush's Fight target area is the authored return killzone, so the
  mid trigger does not make posted bodies chase unrelated enemies or pursue a
  target that leaves the trap;
- both staged groups use **Hold these Posts** and remain stable while waiting,
  without preparation-node re-entry churn;
- the response stays committed for its configured minimum even when an enemy
  leaves mid or visibility changes two seconds later;
- each Mason actually casts and maintains its wall at its own intended tile;
- the Kestrel contingent proceeds independently from tick one and actually
  accepts at least one contextual route-forward Dash in its authored span;
- success/timeout returns participants to an explicit continuation rather than
  leaving them without work;
- changing 2/5/1 to 3/4/1, moving either ambush to the opposite corridor,
  changing the freshness/threshold, or moving a Post/wall one tile is a linked
  Program edit, not raw Instruction surgery.

Provide a deterministic practice opponent/drill that actually places enough
known enemies at mid and traverses the relevant return lane. The proof is not
complete if the trigger never occurs.

### B. Running Relay — duplicate Relay

Use an eight-body roster with seven distinct classes and one duplicate Relay.
Keep the sparse passing pockets, but make this exact staged race prove that it
does **not** need `aheadVia`, a race-specific Toss target, or any other hidden
Relay coordination primitive. Do not add a runtime “Toss race” action or state.

Each Toss in this starter/proof has enough ordinary evidence already:

- the carrier is in an exact zero-tolerance source zone;
- the named receiver is a one-body role in an exact zero-tolerance receiver
  Post;
- the source and receiver Posts are linked to known stations on the same
  ordered movement route;
- the editor validates that the receiver station is the configured minimum
  route distance ahead; and
- ordinary Arc Toss legality validates range, eight-way straight flight,
  terrain, occupancy, and cargo at execution time.

The generated use target is therefore just the ordinary partner role. The
named route remains an ordinary movement continuation that keeps both Relays
on the corridor; it is not a special `via` attached to Toss. If either body is
displaced from its exact activation Post, the existing zone/readiness gate
simply makes the Toss ineligible and movement continues.

Keep the already-shipped `aheadVia` grammar readable for existing sheets and
as an Advanced option for a genuinely dynamic tactic that passes to a moving
receiver anywhere along a route. Do not generate it for this staged race, do
not expose it in the normal race authoring flow, and do not destructively
migrate historical source merely to remove it.

Through normal Program controls the player must be able to:

- select/draw the shared long return route;
- place and drag exact Toss-source and receiver Posts/pockets;
- see which Relay waits and which carries at each handoff;
- select the named receiver and set the minimum forward gain as a visible
  authoring-time relationship between the two route stations;
- prevent the non-carrier from outrunning its next rendezvous;
- keep both bodies on the authored corridor through ordinary movement
  continuations;
- express Toss attempts through ordinary carrying-state gates so a successful
  catch naturally swaps carrier and receiver duties without a Program phase or
  role reassignment;
- change a rendezvous or minimum gain and regenerate linked Instructions
  without editing Conditions, source zones, or derived anchors manually.

An unavailable or illegal Toss must consider the carrier's ordinary forward
movement in the same tick. A missed opportunity must never introduce waiting,
reversal, backtracking, or a local orbit; only the receiver's explicitly
authored rendezvous hold may wait, and it must not outrun the next useful catch
Post. Pin these as route/motion assertions rather than judging only the final
bank.

The focused proof only needs to run against Idle Field, but it is not accepted
merely because the Core eventually banks. Compare it deterministically with a
solo Relay carrying along the exact same route and current rules: the
coordinated Program must bank sooner. This is an execution/usability proof of
the authored tactic, not a population balance claim. Inspect the generated
Instructions and assert that the race contains no `aheadVia` or other
race-only executable field.

Design audit on 2026-08-16: the current generated exact-station race was copied
to a temporary diagnostic and all three `aheadVia`/`minimumForwardTiles` pairs
were removed, leaving ordinary partner-role targets. On Tapline-8 seed 9001
against Idle Field, both versions accepted Tosses at
`t73/126/149/240/385`, destroyed the reactor at `t462`, and produced the same
gameplay-state digest
`6dc664434a8f482a412cda2d107102d291694647698124bcd62169e688a87260`.
This establishes redundancy for the exact-Post construction; it is not a
general argument against the Advanced dynamic-route qualifier. The same-route
solo control finished at `t359`, so the current `t462` staged baseline is a
known failing starting point for the stronger proof—it succeeds mechanically
but does not accelerate delivery. First refine the ordinary Program geometry,
conditions, rendezvous behavior, and continuation using trace evidence. Do not
weaken or silently omit the sooner-than-solo gate, and do not add hidden race
state or restore `aheadVia` merely to pass it. If current general Arc Toss
mechanics make acceleration impossible after honest authored alternatives are
exhausted, report that blocker with exact replay evidence rather than silently
tuning the signature inside this authoring goal.

## 8. Acceptance scenes and tests

Implement trace-first and pin the smallest scenes before broad UI work.

### Runtime/compiler scenes

1. `self-deployment-in-zone` is 1 at the intended tile and 0 for no deployment,
   a deployment elsewhere, destruction, expiry, and replacement. The matching
   `role-deployments-in-zone-count` gives every allied participant the same
   result for the correct role/signature/zone, rejects the wrong role, kind, or
   zone, and lets non-deployer participants arm from the Mason's wall.
2. A suppressed deployment remains present while its public suppressed state
   remains separately observable.
3. West/east map mirroring preserves exact target-zone truth.
4. A body reaches an anchor, rotates toward the face anchor only at
   destination, and does not apply face-to-anchor semantics on patrol.
5. Missing wall + caster at Post + ready signature accepts one exact Hardlight
   cast.
6. Present wall falls through to hold/response without recast.
7. Missing wall during cooldown falls through with a non-empty decline reason,
   then recasts when ready.
8. A wall elsewhere triggers intended relocation/replacement.
9. Trigger/readiness loss before minimum commitment does not exit a latched
   response.
10. Success after the minimum exits; maximum timeout exits without success;
    cooldown bounds re-entry.
11. A ready stage does not re-enter every other tick and cannot starve the next
    Program.
12. Signature opportunity failure still considers the exact continuation in
    the same tick.
13. An exact source-zone + exact one-body receiver-Post Arc Toss resolves and
    advances through an ordinary role target with no `aheadVia`; displacement
    from either Post makes it fall through to corridor movement.
14. A Kestrel moving to a zone/anchor/Well through `move.via` exposes that held
    route to its linked route-forward opportunity: the first unavailable use
    falls through and establishes movement, then a legal in-window Dash is
    accepted on a later straight span.
15. Outside the activation zone, at a bend, on cooldown, obstructed, or below
    minimum gain, route-forward Dash selects the ordinary same-destination,
    same-`via` movement sibling in the same tick. After ingress completion it
    continues from the route endpoint to the final destination.
16. Route-forward Dash works with both `move.via` and terminal patrol, never
    chooses an off-route/reverse destination, and resumes the same route after
    its authoritative commitment.
17. A successful Toss changes ordinary carrying state so the two one-body
    Relay roles exchange carrier/receiver behavior without Program state; an
    unavailable Toss creates no wait, reversal, backtrack, or local orbit.
18. Patchbay's generated continuation preserves a real Repair Beam channel;
    Palisade/Longshot preserve their authored direction/rooted tell; Mortar
    preserves its exact-target tell; and the chosen Hush/Repulsor local scene
    activates only under its authored area pressure.
19. A legal route-station Flare/Sentinel opportunity consumes its use tick and
    then resumes the same patrol cursor forward; false trigger, cooldown,
    illegal target, absent capacity, and already-maintained deployment each
    choose the patrol sibling in the same tick with a non-empty reason.
20. A durable deployment present at the intended patrol target prevents
    recast; destruction permits rebuilding on a later pass. An explicitly
    acknowledged second target replaces/relocates the first rather than
    creating an extra deployment.
21. `self-nearest-visible-loose-core-distance` measures the shortest legal
    path to the exact collectible tile for each body, changes with durable
    blockers/reservations, ignores transient body traffic, and fails its finite
    upper bound for an unreachable, merely remembered, or no-longer-visible
    Core.
22. `nearest-visible-loose-core` with `withinWalkingSteps: 5` exposes only
    currently visible, reachable candidates inside that body's bound. When its
    nearest Core is at capacity, it may choose another in-bound Core but never
    a farther sixth-step Core merely because the Condition was true for the
    first target.
23. The default shared one-pursuer capacity across two different roles and
    Programs sends at most one body to each resolved Core, can distribute
    bodies across several Cores, recomputes after loss/death, and sends
    capacity losers to their next local behavior without waiting. A deliberately
    separate coordination group is independent and visible as such.
24. One Program participant combining a route-station signature, health
    recovery, encounter Fight, nearby-Core recovery, and Patrol executes the
    first actionable leaf in the displayed order. Reordering the recovery and
    Fight cards reverses their contested choice deterministically; unavailable
    leaves reach Patrol in the same tick.
25. Fight exit, a vanished Core, and a capacity loss each resume the same
    patrol cursor without restarting or reversing. Successful pickup makes the
    displayed higher **Bank carried Core** Program take over on the next tick;
    after banking, the still-active patrol resumes its same cursor.
26. `self-nearest-heal-zone-distance` and bounded `nearest-heal-zone` use the
    same legal-path candidates. The fact fails for no reachable/in-bound tile;
    the target filters every tile before capacity allocation, may choose
    another in-bound tile after a claim loss, and never sends the body to an
    out-of-bound tile. An unqualified historical `nearest-heal-zone` fixture
    remains gameplay-identical.
27. Health recovery enters below its start threshold, remains latched while
    travelling and healing through intermediate health, waits to invoke the
    physical healing rule at the tile, and exits only at the higher recovered
    threshold, carrier-abort when configured, or maximum attempt. A finite
    timeout plus re-arm cannot churn every tick; ordinary higher pre-emption
    neither resets nor pauses its authoritative lifecycle clock.
28. The default shared one-patient capacity distributes injured bodies across
    resolved healing tiles, keeps later bodies on their next Opportunity/base
    behavior while all in-bound tiles are claimed, and admits another body
    after a patient exits or dies without a reservation.
29. The same Fight, nearby-Core, and health-recovery Opportunity materializes
    and executes above `move.to` with and without `via`, Patrol, Follow,
    Post/hold, and a durable fight continuation. False/declined opportunities
    consider that exact host continuation in the same tick.
30. An Opportunity that falls back or returns to its host preserves the host
    leaf's existing movement state: held ingress course, patrol cursor, dynamic
    follow selection, or Post destination/facing. Successful Core pickup uses
    its explicit higher carrier-Program consequence instead. An Opportunity
    attached only to a waiting continuation cannot activate during the
    committed response.
31. Core pickup inside an active committed response makes a higher carrier
    Program pre-empt without resetting the response lifecycle; after banking,
    the response resumes if still active. Selecting historical **Core was
    picked up/caught** as the ordinary success survives later custody transfer and
    exits according to the authored minimum/end semantics; selecting current
    **This body is carrying** does not. No lower carrier Program or hidden
    transition is assumed to win.
32. Mid Overload's shared trigger/readiness springs its response and the same
    ordinary Fight behavior used by On-contact opportunities executes beneath
    it. Losing the original enemy count, Post readiness, or one body's current
    sighting does not cancel parent commitment; no actionable target yields to
    the authored response hold, an enemy outside/leaving the return killzone is
    not selected or chased, and a later in-zone target resumes Fight without a
    second activation mechanism.
33. A body from each of two differently named recovery roles can pick up a
    Core and be handled by the same higher banking Program. Its carrying branch
    takes the authored return route; after banking, dropping, or carrier death,
    ordinary eligibility makes that body resume the correct lower behavior
    without a Program call or return stack.
34. `nearest-friendly-core-carrier` Follow selects only a live allied carrier
    satisfying its optional `coreBankValue` filter, orders candidates by legal
    path distance then unit id, and yields when none exists. Shared **Escorts
    per carrier** capacity distributes eligible escorts across two carriers,
    sends a loser to its next candidate or local continuation in the same tick,
    and releases immediately after the target banks, drops, or dies without a
    reservation or sticky assignment.
35. Qualified `self-nearest-visible-loose-core-distance`,
    `nearest-visible-loose-core`, and
    `highest-bank-value-visible-loose-core` use one team-relative value,
    visibility, legal-path, blocker, reservation, and walking-bound candidate
    helper. No matching Core makes the fact false and both targets empty;
    historical unqualified visible/known targets are unchanged.
36. Closest preference orders qualifying Cores by walking distance and stable
    identity; highest-value preference orders by value, walking distance, then
    identity. Filtering precedes target capacity, and a capacity loser can try
    another in-bound qualifying Core and then the next differently filtered
    recovery card without pursuing a rejected value or waiting.
37. A 4-point carried Core selects the first value-gated safe-return branch and
    its high-value escort Follow; 1- and 2-point Cores select their authored
    lower branches. Handoff, Arc Toss, and Drop leaves read the same existing
    `self-carried-core-bank-value` truth and false/illegal one-shots consider
    the durable return sibling in the same tick.
38. An accepted value-gated Drop uses the engine's deterministic adjacent
    toward-home landing rule, leaves the Core outstanding, starts no Well
    recharge, and enforces the existing source reclaim delay. The carrier
    falls through after custody ends, and a focused scene rejects an accidental
    repeatable pickup/drop cycle rather than treating Drop as discard.
39. A Fight with a target area selects/capacitates only hostile bodies or
    attackable deployment footprints currently in that area. Its matching
    zone-qualified visible-enemy-distance fact uses the same candidates; an
    enemy leaving the area is not chased, and the committed response holds
    until a new in-area target or lifecycle exit.
40. Exact multi-body Posts converge from crossed starting positions, recover
    after displacement/death/respawn, and never let one body fill two required
    Posts. Tick-local capacity may change winners, but it must not create an
    endless two-Post swap or readiness flicker under the deterministic scene.
41. A body whose ordinary patrol/move crosses a loose Core picks it up even
    when no recovery filter selected that Core. The roster-wide higher banking
    Program takes over next tick, banks it, and returns the survivor to the
    lower continuation; Practice labels the pickup **incidental**.
42. With two live carriers crossing, capacity-bounded escorts may recompute the
    nearest carrier but do not repeatedly reverse, orbit, or stall the carriers.
    Practice explains each target change. If ordinary tick-local Follow cannot
    satisfy this scene, report the limitation rather than adding sticky escort
    assignment or reservations inside this goal.
43. **Veteran spearhead** and **Wave/Well window** execute using the existing
    level/most-experienced selector, respawn-wave, Well, and reactor facts;
    veteran death/reselection and a changed timing window produce ordinary
    fallthrough with non-empty reasons.

### Materializer/editor tests

1. Blank Program → exact multi-role Posts → combined readiness creates one
   condition per required slot and the same flat gate for all participants.
2. Capacity 1 remains attached to every one-body Post and its resolved
   deployment target.
3. Moving a Post updates its readiness zone and facing arrow; moving a target
   updates its target zone and condition; renaming propagates all references.
4. Adding/removing maintenance, requirement-for-arming, commitment, or success
   outcome is one undoable edit and leaves no orphan source.
5. Save, reload, copy, detach, delete-with-preview, external drift detection,
   and older workshop normalization preserve behavior.
6. Existing recipe Programs remain readable and editable.
7. A fixture signature using each existing target shape needs no component
   switch.
8. The editor clearly warns about contradictory same-owner maintenance
   targets, impossible range/terrain, over-capacity roles, unfillable slots,
   expression/depth limits, map geometry/source/workshop size limits, and
   missing fallback behavior. Preflight the complete atomic edit before
   materialization so approaching a quota is visible and exceeding it cannot
   leave partial Posts, routes, Conditions, or generated Instructions; suggest
   reusing/splitting existing geometry where appropriate.
9. Running Relay source/receiver Post geometry computes forward route gain in
   the editor, rejects insufficient or ambiguous station relationships, and
   generates an ordinary role-target Toss with no `aheadVia`.
10. Required Posts with overlapping eligible-body pools and intersecting
    tolerance envelopes cannot be materialized; the editor highlights the
    overlap and offers spatial/role repairs. Disjoint body pools remain legal.
11. A blank Program can create the complete Kestrel corridor sentence by
    selecting a final destination, one shared movement `via`, activation zone,
    and minimum gain; it generates the descriptor-driven route-forward use
    plus `move.to`/`move.via` continuation without Advanced edits, duplicate
    route selectors, or hard-coded anchors. Changing the continuation route
    updates the linked use reference atomically.
12. All sixteen current signature descriptors can create, edit, regenerate,
    save, reload, detach, and compile at least one honest Program opportunity;
    the five generic families render without skill-id switches.
13. A fixture seventeenth signature using each existing family/target shape
    requires only descriptor data, not a Post, Program, or map component edit.
14. Selecting **Add opportunity here** on a patrol creates one route-linked
    source Post/envelope, independent effect target, ordinary trigger or
    maintenance Conditions, and a `use` leaf above the existing patrol
    continuation. Several markers preserve their visible local source order.
15. Moving a linked waypoint carries its source marker atomically; editing a
    segment away from a station creates a repairable blocking diagnostic rather
    than silently snapping it. Moving an effect target does not move the source
    station.
16. Patrol opportunity generation creates no lap counter, waypoint callback,
    visit memory, action queue, or second order list. Save/reload/copy/detach,
    drift detection, undo/redo, route rename, and deletion remain
    non-destructive.
17. Fight-on-contact, nearby-Core, and health-recovery cards materialize as
    ordinary sibling `fight`/`move` leaves in exactly the order displayed above
    the selected durable continuation. Drag reorder, undo/redo, save/reload,
    copy, detach, and drift detection preserve that single order.
18. A nearby-Core card generates the same authored bound in its body-local
    Condition and `withinWalkingSteps` target qualifier, plus the ordinary
    `self-carrying = 0` gate; editing the visible distance updates both
    atomically and cannot leave mismatched evidence/action ranges.
19. Nearby-Core recovery defaults visibly to **Pursuers per Core: 1 · across
    this sheet**. One consumer generates an inline capacity; several cards
    across roles or Programs generate one shared capacity; removing consumers
    normalizes storage without changing the policy. Choosing 2–8, All eligible,
    or a deliberately separate coordination group is one undoable
    normal-surface edit.
20. The post-pickup default leaves only the recovery leaf empty-handed and
    previews the real higher **Bank carried Core** Program plus the behavior
    that resumes afterward. Missing, role-incomplete, or lower non-winning
    carrier paths create a blocking diagnostic; the offered add/extend/reorder
    repair changes ordinary Programs, not a hidden transition.
21. Capacity controls use target-specific labels and summaries, including
    **Patients per healing tile**, while retaining the exact existing
    target-capacity semantics. Opening an older Program or Orders-only sheet
    does not synthesize or change a missing capacity.
22. A health-recovery card generates distinct start/recovered health
    Conditions, the matching heal-zone-distance Condition, ordinary `until`
    hysteresis, optional timeout/re-arm, `nearest-heal-zone` with the same
    visible `withinWalkingSteps`, carrier policy, and the default sheet-wide
    Healing capacity. Editing any human control updates every owned source
    atomically.
23. The one Opportunity component attaches to `move.to`, `move.to` + `via`,
    Patrol, Follow, Post/hold, and fight hosts without host-specific copies of
    Condition, lifecycle, capacity, or priority editing code. Unsupported
    one-shot hosts offer a continuation repair rather than silently scaffolding
    a hidden fallback.
24. In a multi-phase participant, choosing setup, waiting, response, or
    aftermath as the host links the generated leaves only into that ordinary
    continuation. Moving or copying the cards to another phase is one visible
    undoable edit.
25. The editor rejects a recovered-health threshold at or below the start
    threshold, unsupported bounded targets, missing healing tiles, and a
    finite failed attempt that immediately re-arms without acknowledgement. A
    carrier-only host cannot silently receive a never-eligible nearby-Core
    card or a health card whose carrier policy makes it impossible.
26. Ambush response Fight and On-contact Fight use the same editor linkage and
    generated `fight` leaf shape. Switching activation preserves target order,
    style, chase, and capacity while adding/removing only the inherited/local
    Condition relationship.
27. Ambush and other recipes may prefill that common Fight behavior but cannot
    persist an `AmbushFight`/engagement configuration, invoke another
    materializer, or add a runtime combat mode.
28. Several nearby-Core cards may hold stable sidecar links to one higher
    banking Program. The links survive rename, reorder, save/reload, copy, and
    undo/redo; update their summaries together; detect deletion or lost role
    coverage; and never materialize a Program id, call, return address, or a
    second copy of banking/escort configuration.
29. Inline create/choose/extend/reorder repairs produce one ordinary banking
    Program whose eligible carrier roles use `self-carrying` return branches
    and whose selected empty-handed escorts use
    `nearest-friendly-core-carrier` Follow leaves with ordinary target-local
    capacity. **Edit linked Program** always edits that same owner.
30. The recovery card exposes value comparison and Closest/Highest-value
    preference once, then materializes the identical `coreBankValue` qualifier
    into its distance Condition and visible-Core target. Editing either human
    control updates all owned sources atomically and never produces mismatched
    candidate sets.
31. Several differently filtered recovery cards remain separate ordinary
    siblings in the exact displayed order while sharing the intended
    **Pursuers per Core** coordination group. Reorder, undo/redo, save/reload,
    copy, detach, and drift checks preserve filters and actual priority.
32. **Carried Core is worth** reuses
    `self-carried-core-bank-value` plus `self-carrying = 1` across return,
    Follow, Handoff, signature, and Drop authoring; it does not create a second
    fact, Core-policy document, or action-specific condition editor.
33. Adding value-gated Handoff/Toss/Drop above a Bank return uses the same local
    one-shot and fallback materializer as other opportunities. Drop's normal
    UI explains fixed landing, outstanding supply, no recharge, reclaim delay,
    and cycle risk, and offers no discard or landing-target control.
34. Exact facing arrows snap to north/east/south/west and generate a cardinally
    aligned face anchor. A diagonal watched point and a tolerance envelope show
    every possible cardinal result and cannot be labelled exact when arrival
    state could change it. The rules-derived vision footprint/formation union
    updates as Posts, facing, terrain, and selected veteran preview change.
35. A maintained deployment uses `self-deployment-in-zone`, while required
    shared readiness uses `role-deployments-in-zone-count` with role,
    signature/kind, and zone. Every participant receives the same gate; wrong
    owner/kind/target and one footprint satisfying overlapping required targets
    produce exact diagnostics.
36. The common Fight editor owns one optional target-area field. Its generated
    `fight` selector and optional zone-qualified contact-distance Condition
    share the same zone, while trigger/contact/target overlays remain separately
    editable. Switching inherited response/On contact preserves target area.
37. The custody audit distinguishes deliberate recovery coverage from physical
    pickup exposure, offers extension of one banking Program to every fielded
    role, and never claims that a Core failing a pursuit filter is avoided.
38. The per-role priority strip updates on add/reorder/gate edits, proves simple
    unconditional shadowing and missing fallbacks, blocks a structurally
    impossible linked banking takeover, and labels dynamic overlap as a warning
    rather than claiming a complete Condition solver.
39. **This body is carrying** materializes current `self-carrying`; **Core was
    picked up/caught** materializes historical `friendly-core-pickups` with a visible
    interval. Editing the minimum commitment does not silently change one into
    the other.
40. Starting Practice snapshots the normalized workshop/source mapping and
    sheet revision. Editing/renaming/reordering Programs afterward leaves the
    old run's attribution byte-stable, displays the revision mismatch, and
    supports opening the run revision or rerunning the current draft. A legacy
    run without linkage never guesses.

### Browser acceptance

Run the production web build at desktop and 390×844 touch width. From a copied
starter sheet, without opening Advanced:

1. select the defending Mason Post;
2. move its standing tile one step;
3. redraw its cardinal facing direction and inspect the exact personal and
   formation vision overlays;
4. move the Hardlight target one step;
5. toggle “rebuild when missing” and “required before arming”;
6. change its capacity/readiness relationship;
7. open the ambush response's ordinary Fight behavior, confirm activation says
   **When this Program responds**, and edit target area, target order, style,
   chase, and Attackers-per-enemy without entering a recipe-specific editor;
8. switch that same behavior to **On contact** and back, confirming its Fight
   configuration survives while only activation linkage changes;
9. run the deterministic drill;
10. click the Mason and explain whether/why it moved, faced, cast, fought, held,
   committed, and exited;
11. save, reload, and rerun the same scenario/seed.

In a second normal-surface flow, without opening Advanced:

1. select the Kestrel raid participant;
2. choose the enemy Well/anchor/zone as its final destination and draw/select
   Long Corridor once as the movement `via`;
3. attach Dash to **this movement's route** and draw its activation span;
4. change the minimum forward gain;
5. inspect the straight-span preview and same-destination/same-`via` fallback
   summary;
6. run the Mid Overload drill and observe a contextual Dash; and
7. drag or replace the shared `via`, activation span, or destination and
   regenerate/save/reload without breaking linkage or exposing two route
   selectors.

In a third normal-surface flow, without opening Advanced:

1. select or draw a valid patrol route;
2. tap **Add opportunity here** at one route station and configure Lantern to
   Flare a separate watched anchor under an enemy-memory trigger;
3. add a Nest opportunity at another station with **maintain Sentinel here**;
4. add **Recover health**, choose different start/recovered thresholds, maximum
   walking detour, retry lifecycle, carrier policy, see the healing tiles/bound
   on the map, and observe **Patients per healing tile: 1 · across this sheet**;
5. add **Fight on contact** through the same Fight editor used by the ambush,
   choose its encounter/target areas, distance, target order, style, chase
   limit, and visible Attackers-per-enemy policy;
6. add **Recover a nearby Core**, set five walking steps, observe the visible
   default **Pursuers per Core: 1 · across this sheet**, and confirm **On
   pickup: Bank carried Core takes priority** names a real higher Program and
   previews the post-bank return;
7. drag a source tolerance, route waypoint, and effect target independently;
8. reorder Fight, health, and Core recovery and inspect an exact
   generated-priority preview without leaving Programs;
9. run a deterministic patrol scene and observe accepted signature use,
   already-present deployment fallthrough, destruction/later rebuild,
   health diversion/healing/re-entry, encounter fighting, one-pursuer
   nearby-Core diversion, a farther Core being ignored, same-route resumption,
   pickup, Bank-carried-Core takeover, banking, and patrol resumption; and
10. save/reload and refine the route, stations, local priorities, thresholds,
    distances, chase, and capacity without raw identifiers or generated
    Instruction edits.

In a fourth normal-surface flow, without opening Advanced:

1. select a participant moving to a destination through a non-patrol `via`;
2. add the same Fight, nearby-Core, and health-recovery cards to that movement
   and confirm its exact destination/`via` remains the pinned fallback;
3. move the Opportunity stack between that participant's setup, waiting, and
   response continuations and verify only the selected phase owns it;
4. run a deterministic scene in which a declined contact/Core option falls
   through, health recovery diverts and completes, and the original held
   ingress resumes without restarting;
5. attach/copy the same cards to a Follow and a Post/hold continuation and
   inspect the contextual fallback summaries; and
6. save/reload/refine all three hosts through Programs with one consistent
   Opportunity editor.

In a fifth normal-surface flow, without opening Advanced:

1. add nearby-Core recovery to two differently named participant roles;
2. from the first source card, use inline **Create Bank carried Core**, choose a
   return route, and see the new Program placed above both sources;
3. from the second source card, reuse that same Program and extend missing
   carrier coverage rather than creating another return configuration;
4. use the roster custody audit to extend that same Program across all eight
   fielded bodies, including roles with no deliberate recovery card;
5. add two escort roles, choose **Escorts per carrier: 2**, attach ordinary
   Fight-on-contact to their Follow behavior, and inspect the linked summary
   from either source card;
6. run a deterministic scene where either recovery role—and one unrelated body
   crossing a Core incidentally—can become carrier,
   the carrier returns on the authored route, eligible escorts follow the
   actual carrier, and every survivor resumes its lower Program after banking;
   and
7. rename, reorder, edit, save/reload, and delete/undo the banking Program while
   every linked source updates or reports one actionable blocking repair—never
   duplicated hidden configuration.

In a sixth normal-surface flow, without opening Advanced:

1. stack three nearby-Core cards: 4 points within seven steps, at least 2
   points within five, and any value within three;
2. switch the first card between **Closest** and **Highest value, then closest**
   and inspect the exact qualifying/ranked Cores on the map;
3. run a deterministic scene containing several visible Cores at conflicting
   values, distances, and capacities, and observe the displayed first
   actionable card select the same Core reported by Practice;
4. open the one linked banking Program and author visible 4-point safe-return,
   2-point normal-return, and any-value direct-return carrier cases;
5. add a high-value-only escort Follow plus an unqualified lower escort Follow,
   then add value-gated Handoff, Arc Toss where legal, and Drop leaves above
   the durable return;
6. prove a false or illegal transfer/drop reaches return movement in the same
   tick, while an accepted Drop lands adjacently toward home, starts no Well
   recharge, and does not enter a pickup/drop cycle; and
7. reorder/refine/save/reload all filters and cases while every source card,
   map highlight, generated-priority preview, and Practice reason stays in
   agreement.

In a seventh normal-surface flow, without opening Advanced:

1. gate a lead role by its visible veteran level and attach a
   capacity-bounded support Follow to **most experienced ally**;
2. add a lower Program that stages or works until the authored friendly/enemy
   respawn-wave and selected-Well timing clauses become true;
3. inspect the per-role priority/fallthrough strip, deliberately create one
   provable shadow, apply its suggested repair, and distinguish one dynamic
   overlap warning from a blocking error;
4. run the Veteran spearhead and Wave/Well window scenes and explain veteran
   reselection, waiting, activation, and fallthrough from Practice; and
5. rename/reorder a Program after the run, reopen the old replay, observe its
   immutable run-revision attribution and revision-mismatch label, then rerun
   the current draft.

No raw id, Condition, Instruction, or JSON edit is permitted in this acceptance
flow. Capture desktop and phone evidence with zero console warnings/errors.

## 9. Playable-alpha gate

This round is not complete merely because the new controls render or compile.
It is complete only when all of the following are true:

1. The canonical Mason sentence is authorable and editable entirely from the
   normal map-first Program surface.
2. The exact target is maintained correctly across cast, cooldown, damage,
   destruction, expiry, suppression, relocation, death, and respawn semantics.
3. Exact Posts, cardinal facing with honest vision preview, multiple roles,
   capacity, all-slots-filled readiness, shared role/deployment truth, and
   maximum formation spread are visible and reliable.
4. Tactical commitment survives transient trigger/readiness loss and exits for
   understandable reasons.
5. Waiting/staging choices do not silently starve lower Programs.
6. Mid Overload actually triggers in its deterministic drill and exposes its
   2/5/1 behavior in Practice.
7. Running Relay banks sooner than the same-route solo Relay against Idle
   Field, naturally swaps duties through carrying state, produces no missed-use
   wait/reversal/backtrack/orbit, generates no `aheadVia`/race-only executable
   field, and remains editable through Programs; the deterministic solo
   control is linked beside the proof.
8. Both starter sheets are legal eight-body, seven-class rosters with one
   duplicate and complete fallbacks.
9. A player can identify the current Program and “why” for any selected body
   from Practice without reading generated Instructions, and an old run remains
   attributed to its immutable authoring revision after the current draft is
   edited.
10. The full create/refine/practice/save/reload flow works on desktop and phone.
11. Focused tests, the full relevant web suite/build, compiler tests, and
    deterministic gameplay scenes pass.
12. Documentation describes Posts as authoring sugar, the new Condition facts,
    bounded visible-Core/healing targets, commitment/fallthrough, capacity,
    and the two starter doctrines accurately.
13. All sixteen current signatures remain normally authorable and persistent
    through Programs; Patchbay channel, Palisade/Longshot lane/tell, Mortar
    exact tell, and Hush/Repulsor self-local scenes prove the distinct families
    without introducing bespoke recipe/runtime behavior.
14. Kestrel can Dash along the same single-authored `via` used by a move to a
    zone, anchor, Well, or other ordinary destination, with honest same-tick
    fallback and no `dashVia`/`aheadVia` field.
15. A player can attach several intelligible signature opportunities to a
    patrol from the map, including separate source and effect points, without
    creating patrol scripting state; accepted uses resume the same route and
    durable placement obeys honest one-deployment replacement/maintenance.
16. The integrated patrol sentence—patrol, location-bound skills, health
    recovery, encounter Fight, visible-Core recovery within five legal walking
    steps, and patrol fallback—is authorable, reorderable, explainable, and
    refinable from one normal Program participant without Advanced edits or a
    second priority system.
17. Nearby-Core recovery visibly defaults to one pursuer per resolved Core
    across the sheet's default recovery group, distributes several eligible
    bodies across several in-bound Cores, never diverts a capacity loser to an
    out-of-bound Core, and makes the visible higher **Bank carried Core**
    Program pre-empt before the original behavior resumes. Pursuit filtering is
    clearly distinct from physical pickup, and the starter's carrier Program
    handles incidental pickup by every fielded role.
18. Fight, nearby-Core, and health-recovery cards are the same generic opt-ins
    on destination moves, `via` moves, patrols, follows, Posts/holds, and
    durable fight responses. Each is scoped to the selected participant phase,
    and every decline/returning exit resumes the exact ordinary host
    continuation; successful Core pickup follows its explicit higher
    carrier-Program consequence.
19. Health recovery has visible low/high hysteresis, bounded detour, retry and
    carrier policy, defaults to one patient per resolved healing tile across
    the sheet, makes healing tiles and the authored bound legible on the map,
    and cannot create oscillation, route restart, hidden recovery state, or an
    indefinite unreachable-heal diversion.
20. Ambush response and ambient contact use one Fight behavior editor,
    materializer, configuration shape, capacity policy, and generated `fight`
    leaf. The ambush springs because its parent Program response activates and
    commits—not because Fight has an ambush-only trigger or mode. Switching the
    same Fight behavior between **When this Program responds** and **On
    contact** preserves target area/order, style, chase, and attacker capacity;
    target area is distinct from activation and keeps the return ambush inside
    its killzone. No `AmbushFight`, engagement scheduler, or second combat state
    is added.
21. Adding nearby-Core recovery makes its post-pickup consequence unavoidable
    in normal authoring: the player can create, choose, inspect, and edit one
    reusable higher banking Program from the source card. It covers every
    fielded body unless an explicit custody consequence says otherwise, may
    coordinate capacity-bounded escorts around the actual
    carrier, owns its configuration once, and resumes ordinary lower Programs
    after custody ends without a runtime Program reference or transition.
22. A player can prefer/filter visible Cores by team-relative value and walking
    distance, stack those choices in the real local order, and branch the one
    linked banking tactic by carried value—including escorts, Handoff, legal
    signature use, and explicit Drop—without Advanced edits or a separate Core
    policy system. Every value, candidate rejection, takeover, and Drop
    consequence is visible in the map editor and Practice.
23. Veteran spearhead and synchronized-wave/Well-window tactics are authorable
    from normal human-labelled facts/selectors, execute in focused deterministic
    scenes, and need no scheduler or hidden team state.
24. The per-role priority/fallthrough strip exposes provable shadowing, missing
    fallback, and carrier-takeover failures without pretending to solve dynamic
    Conditions; Practice handles the uncertain cases.
25. Generated source, workshop, expression/depth, and map-geometry limits are
    preflighted before an atomic map edit, so a complex tactic cannot fail late
    with half-materialized Posts or Instructions.

After focused diagnosis, deliver the normal standard six-cell phone exhibit
for one complete starter sheet against one named current practice control,
using the exact same pairing in every cell. Deliver the deterministic Mid
Overload drill and Relay-vs-solo proof as separately labelled mechanical
evidence. Outcomes are behavior records only; make no balance verdict and do
not run a battery or sweep unless the owner separately asks.

Owner review requires a verified public Cloudflare tunnel URL, production
browser evidence, and a concise implementation report that links the exact
deterministic replay/trace evidence and states every remaining limitation. It
also requires a clean worktree and the final commit SHA. Do not deploy. Do not
rebuild the tracked tactical WASM mirror during this design loop; that remains
an explicit release checkpoint.

## Implementation order

1. **Baseline and traces** — preserve current generated Program/Running Relay
   source and record the failing commitment, stage re-entry, and wrong-location
   deployment scenes; also preserve one current patrol cursor through signature
   use, combat interruption, health recovery, and target-loss fallthrough.
2. **Semantic foundation** — implement `self-deployment-in-zone`,
   `role-deployments-in-zone-count`,
   `self-nearest-visible-loose-core-distance`, the matching
   `nearest-visible-loose-core` and
   `highest-bank-value-visible-loose-core` targets, `withinWalkingSteps`, and
   their shared `coreBankValue` qualifier,
   `self-nearest-heal-zone-distance` with bounded `nearest-heal-zone`,
   value-filtered `nearest-friendly-core-carrier` Follow resolution,
   Fight target-area/zone-qualified contact resolution,
   historical `friendly-core-pickups`, commitment materialization, not-ready
   staging, and focused runtime/compiler tests.
3. **Post linkage** — add backward-compatible workshop linkage, derived
   geometry synchronization, human-labelled/defaulted Core and Healing
   capacity groups, owner-local versus shared deployment readiness,
   roster-wide custody and per-role priority audits, drift, quota preflight, and
   undo/redo tests.
4. **Map-first UX** — build the Post inspector, cardinal facing/vision preview,
   separate trigger/contact/Fight-target overlays, overlap prevention,
   descriptor-driven route-forward Dash
   controls/preview with shared movement-`via` linkage, route-linked patrol
   opportunity markers, one generic Opportunity component for all supported
   host continuations/phases, one shared Fight editor with explicit inherited-
   response versus On-contact activation, nearby-Core and health-recovery cards
   in the one real local order, linked higher carrier-Program
   create/choose/extend/reorder/edit UX, escort Follow/capacity controls,
   visible Core-value filters/preferences and carried-value return/transfer/drop
   controls, all-signature family coverage, legality preview, normal
   trigger/outcome controls, and mobile editing flow.
5. **Practice explanation** — snapshot the owner-only authoring revision at
   trial creation, correlate Programs/Posts with ordinary source and replay
   evidence, and add selected-body why/timeline views including local
   Opportunity order, host/phase, health lifecycle, walking-distance rejection,
   capacity loss, resumed continuation, and post-pickup takeover.
6. **Starter doctrines and signature seams** — finish Mid Overload and Running
   Relay as two legal, fully fallback-equipped sheets; add their deterministic
   drills/controls, including proof that Mid Overload and ambient contact share
   the same Fight editor/materializer, plus the focused Patchbay,
   Palisade/Longshot, Mortar, and Hush/Repulsor family scenes and the integrated
   skill/Fight/Core/Health/Flare/Sentinel patrol scene plus non-patrol host
   matrix, Veteran spearhead, and Wave/Well window.
7. **Validation and delivery** — run focused and full relevant tests, browser
   QA, exact mechanical proofs, standard exhibit, docs/Decision update, public
   tunnel verification, report, commit, and clean-status check.

## Explicitly deferred

Do not expand this goal into:

- runtime Programs, atomic multi-body activation, reservations, or a second
  state machine;
- executable cross-Program calls, return stacks, source-local overrides of a
  linked banking Program, or dynamic carrier/escort role assignment;
- persistent Core claims, target-identity latches, sticky escort/Post
  assignment, a new `pickup`/pickup-veto action, automatic Core-avoidance
  pathing, or a hidden transition to Core Return;
- a recovery governor, automatic health triage, hidden healing priority, or a
  new `heal` action;
- an automatic Core-utility score, custody-policy document, hidden
  value-to-route chooser, discard action, or authored Drop landing tile;
- arbitrary nested Conditions or a visual programming language;
- waypoint action scripts, patrol queues, hidden visit/lap state, or a second
  route-local priority system;
- `AmbushFight`, recipe-specific engagement/Fight configuration, a combat
  scheduler, or any second runtime combat mode;
- simultaneous move-and-use actions;
- fixed unit-id assignments in place of roles and capacities;
- backup rosters or between-match substitutions;
- cross-match observation memory or opponent-specific automatic adaptation;
- new classes, signatures, maps, scoring, health/damage tuning, Core economy,
  or other balance changes;
- a generic AI that decides “optimal” signature use for the author;
- ladder/playlist/rating migration, deployment, release, or WASM checkpoint;
- balance conclusions from the drill, solo comparison, or six-cell exhibit.

Those are later decisions. This round is narrowly about making the strategy
language dependable, spatially ergonomic, explainable, and complete enough for
the owner to begin playing and evaluating the actual game.
