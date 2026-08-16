# Arc Relay signature opportunities in Tactical Programs

Status: **implemented on `main` on 2026-08-16; owner review pending.**

Date: 2026-08-16

## Owner acceptance amendment — Mid Overload Fork

### Tactical Programs ruling — 2026-08-16

This ruling supersedes every conflicting use of player-facing **Play**,
**Order**, projection insertion points, or “no Play priority” below. Internal
`Play`, `PlayProjection`, `OrderNode`, `plays`, and `arc-relay-orders-v2`
identifiers remain unchanged for compatibility.

- Player-facing terms are **Program** and **Instruction**.
- Programs are the normal map-first authoring surface. Generated Instructions
  are an advanced, read-only explanation; ordinary Program authoring never
  requires opening them.
- The Program list is evaluated top-to-bottom and is the sole user-facing
  priority order.
- For each role, materialization filters that ordered list to Programs
  containing the role, then flattens its participant Instruction blocks in the
  same order. A participant with no executable Instruction falls through to
  the next Program.
- Reordering one Program moves every participant block together. Normal
  per-projection insertion controls are deleted.
- Programs remain authoring sugar over the existing Instruction order. They
  add no runtime Program state, atomic team activation, reservation, or second
  scheduler.
- Existing unlinked Instructions remain an advanced compatibility surface;
  historical sheet migration does not block this goal.

### Program-derived Conditions ruling — 2026-08-16

Normal Program authoring derives executable Conditions from the player's
direct tactical choices. Shared enemy-pressure triggers, each role's readiness
count and area, positional signature activation, signature commitment, and the
timeout/fallthrough structure are authored without manually wiring Condition
identifiers. The exact generated Conditions remain visible and editable under
Advanced as an escape hatch.

Several role-qualified readiness checks may be linked as one combined
readiness group. The editor materializes each role's ordinary
`role-in-zone-count` Condition and writes one flat `all` gate onto every
participant Instruction in that group. This is editor linkage only: it adds no
runtime Program state, scheduler, reservation, or priority model. For example,
one 1-body Mason role, one 1-body Longshot role, one 1-body Hush role, and one
2-body reserve role form a visible 5/5 gate from four generated Conditions.
Natural-language outcome exits such as “four enemies destroyed” or “Core
acquired” are an explicit follow-up rather than a milestone blocker.

The primary UX acceptance scenario is **Mid Overload Fork**, authored through
the generic blank-Program surface rather than a recipe. A player draws two ambush
locations and a Kestrel route, assigns 2/5/1 contingents, defines one shared
mid-overload condition, configures each contingent's own readiness and
continuation, and places its signature opportunities. The Program editor must
show the shared start, immediate contingent, 2/2 and 5/5 readiness, each
Mason's exact wall, Kestrel's route and Dash, the Program's one global priority,
and the ordinary outcomes after success, failure, timeout, or Core
pickup.

Refinement is part of acceptance: changing the enemy threshold, moving an
ambush to the opposite corridor, changing 2/5/1 to 3/4/1, swapping a role, or
changing commitment time must edit existing linked source in place. It must
not require raw JSON, duplicated conditions, manual anchor bookkeeping,
detachment, or another priority model. Implement missing affordances
generically; do not add a Mid Overload runtime concept or hard-coded recipe.

Implement this as a follow-on to
`docs/ARC-RELAY-LINKED-PLAYS-AUTHORING-PLAN.md`. That plan remains authoritative
together with Decisions `#294`, `#317`, `#323`, and `#324` wherever this
document does not explicitly narrow the implementation.

## Goal

Implement the complete signature-opportunity authoring path described here,
from one shared signature descriptor through linked Plays UX, ordinary Orders
projection, generic route/commitment correctness, all-16 class coverage,
focused replay proof, and owner-review delivery. Preserve one positional
priority tree and stop only at the explicit owner-ruling conditions near the
end of this document.

The result must make class signatures much easier to use as intentional pieces
of a strategy while preserving the best property of Orders: every body still
has one inspectable positional-priority tree. A linked Play remains editor
ownership and map-first authoring sugar. It does not become a gameplay object,
a scheduler, a coordinator, or a second state machine.

## Outcome

A player should be able to select a durable part of a linked Play—return along
a route, hold an ambush post, escort a veteran, guard a lane—and add one or more
signature opportunities to it directly from the map:

```text
Long enemy-Core return
  ALONG THE WAY
    Arc Toss to Relay B in this source window, if B is far enough ahead
  OTHERWISE
    continue along the authored return route
```

Underneath, that remains one ordinary nested Orders branch:

```text
long enemy-Core return
  forward Arc Toss       <- ordinary use leaf
  continue return route  <- ordinary move leaf
```

At each tick, the existing first-executable-leaf rule applies. If the signature
is unavailable, has no legal target, loses capacity, or fails its spatial
constraints, the movement/fight/hold continuation is considered immediately.
If the signature is accepted, it consumes that body's action for the tick; it
does not move and use simultaneously. The same parent episode remains the
body's plan and resumes as soon as the authoritative signature lifecycle
permits it.

The player-facing promise is:

> Choose where, when, on whom, and in support of which plan a signature should
> be attempted. If it cannot happen, the bot continues the same understandable
> plan. Nothing secretly competes with the role's Orders.

## Why this should improve the game

Signatures are the class identities and the most natural source of tactical
combination. They should create decisions such as:

- wall one lane and prepare fire on the remaining lane;
- dash only on a safe straight span of a return route;
- Toss to a named Relay only when that receiver has earned useful progress;
- repair the veteran while a screen holds around it;
- suppress signatures only after enough enemies enter the committed fight;
- smoke a crossing before the carrier enters it; or
- seed, mine, or block an exact point while ordinary Orders handle staging and
  continuation.

The strategic skill is choosing those relationships and fitting them into the
rest of the role's priorities. It should not be duplicating coordinates and
conditions across several trees, nor guessing which hidden executor policy
overruled the authored plan.

This design gives a novice a map-first scaffold and gives an expert the exact
generated Orders, local ordering, conditions, capacities, geometry, and
fallback to refine. It deliberately does not let the editor choose an opaque
"best tactical use" on the player's behalf.

## Non-negotiable invariants

### One execution priority system

- The ordered Program list is the sole user-facing tactical priority.
- For each role, materialization filters that list to participating Programs
  and flattens each participant Instruction block in the same order.
- A non-executable participant block falls through to that role's next Program.
- Within an active parent branch, existing child source order remains the only
  local priority.
- Reordering a Program moves all participant blocks together. There is no
  per-projection insertion point, signature priority number, opportunity score,
  runtime scheduler, reservation queue, or cross-role arbitration layer.
- Advanced unlinked Instructions remain after the Program band as compatibility
  fallback.
- Opportunity ordering shown in Programs must be the actual child order in the
  materialized Instructions tree. The sidecar must not store another
  authoritative order list.

The UI should explain the model in one sentence:

> Try Programs from top to bottom. Inside one Program, try this participant's
> Instructions from top to bottom; otherwise continue to the next Program.

### Programs remain editor-only linkage

- Do not add `plays`, opportunity ids, Play phases, participant reservations,
  or Play state to `arc-relay-orders-v2`, ATL2, the executor, match state,
  replay, content hash, or ladder identity.
- Workshop metadata may link to ordinary Orders and geometry, but deleting the
  metadata must not change what the materialized source means.
- Orders plus map plan remain a complete, independently compilable source pair.
- The executor may not recognize an Ambush, Running Relay, Escort, or any other
  recipe name.
- Practice diagnostics may correlate source paths back to the sidecar after a
  match. They must never feed information back into execution.

### No compound `move + use` runtime action

"Use while moving" is a good authoring phrase and a bad action contract. A
signature and a movement both consume the body's action opportunity. A
composite action would introduce unresolved timing, capacity, cancellation,
and replay questions.

The Plays workspace may visually attach a signature to a route or movement
card. Materialization must produce sibling leaves, with `use` above the
continuation. The fallback therefore uses the same selection and lifecycle
semantics as every other Order.

### Class mechanics remain authoritative

- Existing range, tell, channel, flight, cooldown, cancellation, deployment,
  line-of-sight, exact landing, and action-legality rules remain authoritative.
- This feature grants no free action, free target search, timing override,
  cooldown reduction, or hidden movement.
- Tapline-7 Arc Toss timing is not reopened by this project.
- A balance or rules change discovered while proving an opportunity is a
  separate owner decision. Do not hide it in workshop metadata or an executor
  exception.

### Capacity keeps its one current meaning

- Capacity remains an optional property of an ordinary action leaf.
- It continues to mean at most N bodies claiming the same resolved target
  through the same capacity scope.
- Claims are recomputed; they are not participant reservations.
- A body that loses capacity on a signature opportunity tries the next ordinary
  sibling that tick.
- Play slots and readiness still answer who is present and whether a formation
  is staged. They do not replace capacity, and capacity does not prove
  readiness.

## Terminology

Use these terms consistently in code, docs, and UI:

- **Program** — player-facing linked workshop object spanning geometry and one
  or more participating roles. Internal source may continue to call it Play.
- **participant Instruction block** — editor-owned Instructions contributed by
  one Program to one role at the Program's global position. Internal source may
  continue to call it a projection.
- **plan** or **episode** — an ordinary branch with current `when`, `until`,
  `timeout`, `cooldown`, and `urgency` semantics.
- **Instruction** — player-facing name for one existing Order/OrderNode.
- **signature opportunity** — editor linkage to one ordinary `use` leaf inside
  a plan. User-facing copy may call the group **Along the way**.
- **continuation** — the following ordinary siblings in the same branch, or
  the next eligible root Order after the parent exits.
- **signature authoring descriptor** — catalog data that tells both editors
  which target/spatial controls are valid for a signature. It is authoring
  policy, not mechanics or runtime state.
- **preset** — optional editor defaults for a recognizable use of a signature.
  It creates editable ordinary source and has no identity after generation.

Do not introduce synonyms such as task priority, projection priority, ability AI,
sequence state, or move callback.

## Current foundation and the seams to fix

The implementation must build on the code that is already present rather than
recreating it:

- `web/src/site/sheets/workshopTypes.ts` owns the versioned linked Play sidecar.
- `web/src/site/sheets/linkedPlays.ts` performs deterministic projection,
  fingerprint drift detection, detach, and ordinary root insertion.
- `web/src/site/sheets/PlaysWorkspace.tsx` presents the single top-to-bottom
  Program priority plus participant continuation.
- `web/src/site/sheets/sheetAuthoring.ts` defines the direct Orders/map-plan
  authoring types.
- `src/BotArena.TacticalPlaybooks/ArcRelayOrdersSheetDsl.cs` is the strict,
  engine-independent Orders validator.
- `arena-bots/arc-relay/tactical-playbook-mind-v1/OrderTreeRuntime.cs` owns the
  single lifecycle and priority evaluation.
- `arena-bots/arc-relay/tactical-playbook-mind-v1/ArcRelayTacticalPlaybookMind.cs`
  resolves targets and executes the selected leaf.
- `src/BotArena.Engine/ArcRelaySignatureDefinition.cs` already publishes
  signature category, argument shape, engagement range, timing, cooldown, and
  type-specific mechanics.

Four current seams are specifically in scope:

1. The web editor hard-codes skill ids in `signatureTargets(...)` and in the
   Orders `UseEditor` for friendly roles, forward Toss, and exact placement.
2. The compiler independently hard-codes class-to-skill and per-skill target
   restrictions. Adding a skill currently requires remembering several tables.
3. `aheadVia` is a useful Arc Toss proof, but copying that approach into
   `dashVia`, `wallVia`, and similar skill-specific fields would not scale.
4. the tactical mind currently clears a body's route cursor whenever the
   selected leaf path changes. A route move interrupted by a sibling `use`
   therefore risks restarting or re-projecting backward. Relocating signatures
   make that defect more visible.

A fifth seam must be handled deliberately: a movement fallback can cancel a
channel or rooted wind-up on the tick after a successful `use`. The editor must
not sell "continue the plan" while immediately sabotaging Repair Beam, Rail
Line, Target Paint, or another mechanically committed signature.

## Architecture decision

### Signature opportunities are links to real Orders

Extend `PlayProjection` with optional editor linkage, not another semantic
description of the action:

```ts
export interface PlaySignatureOpportunityBinding {
  id: string;
  /** An ordinary branch in this projection. */
  hostOrderId: string;
  /** A direct child of hostOrderId whose sole action is use. */
  useOrderId: string;
}

export interface PlayProjection {
  // existing fields remain
  signatureOpportunities?: PlaySignatureOpportunityBinding[];
}
```

The names, skill, target, conditions, capacity, lifecycle, geometry references,
and actual child priority all remain in `projection.orders`. Do not duplicate
them in the binding. The binding exists only so the Plays UI can present and
edit the relationship as one map-linked concept.

The actual child position is authoritative. If two opportunity bindings share
one host, their `use` nodes are shown in the exact order in which those nodes
occur in `host.orders`. The first non-opportunity sibling is shown as the first
continuation; further siblings remain visible in advanced inspection.

The materializer still clones participant Instructions, resolves linked values,
and removes prior owned roots. It then walks the Program list once per role,
filters to participating Programs, and prepends those blocks in the same global
order ahead of advanced unlinked Instructions. Drift
fingerprints already cover the nested `use` nodes because they fingerprint the
owned root.

### Binding validation

Before save and after loading an older workshop, validate every binding:

- binding id is stable and unique within the Play;
- projection exists;
- host id resolves within that projection and is a branch;
- use id resolves as a direct child of that host;
- the use node has exactly one `use` action;
- the referenced skill belongs to at least one class in the projection role;
- all map, condition, capacity, role, and linked-value references remain valid;
- the generated tree stays within the current two-branches-plus-leaf depth;
- the opportunity appears before at least one ordinary continuation unless the
  player explicitly accepts that failure means normal root fallthrough.

Broken editor linkage is a workshop diagnostic, not permission to mutate
gameplay. Preserve the materialized Orders and offer the existing recovery
choices: repair/regenerate from the Play when deterministic, or detach to
ordinary Orders. Never silently move a use leaf to make the linkage fit.

### Turning an existing action into a host plan

Recipes should generate a suitable parent branch from the beginning. When the
player attaches an opportunity to a Play-owned ongoing `move` or `fight` leaf,
the editor may scaffold a parent branch in one undoable transaction:

1. retain the original node id, priority position, lifecycle fields, and
   inherited urgency on the new parent;
2. move the original action and any leaf-only capacity into a new child with a
   stable derived `-continue` id;
3. insert the new `use` leaf before that continuation; and
4. create the binding to the new parent/use child.

With no executable signature, that continuation must resolve the same target
and action as the original leaf. A branch left with only its continuation after
an opportunity is deleted may remain a valid ordinary branch; automatic
collapse is not required and must not risk ids or capacity scope.

Reject, with a precise explanation, any scaffold that would exceed maximum
Orders depth or wrap a one-shot `use`, `handoff`, or `drop` as though it were a
durable continuation. Offer the nearest existing parent branch instead.

### Existing workshop schema

This is an additive optional field inside a Play projection, so keep
`arc-relay-workshop-v1`. Missing arrays normalize to empty arrays in memory.
The sidecar remains under the current size bound and root validation.

Add a deterministic editor-only migration that can recognize current
Play-owned `use` leaves under branches and create bindings without touching the
Orders. At minimum, migrate the existing Running Relay Toss leaves and Mason
Hardlight leaf. A migration may add sidecar metadata on the next explicit save;
it may not rewrite materialized gameplay merely because a sheet was opened.

Unknown or future optional fields must survive load/save through clients that
do not edit them. Cover old v1 workshops with no opportunity arrays, migrated
v1 workshops, duplication, detach, and stale-client preservation.

## One shared signature authoring catalog

### Split mechanics from authoring vocabulary

There are two legitimate sources of information and they should not be
collapsed into another per-client table:

1. **Engine contract data** — signature id, class id, action id, category,
   argument kind, range, cooldown, tell/channel/flight timing, and numeric
   mechanics. These come from the exact resolved Arc Relay signature
   definition for the catalog's current rules profile.
2. **Orders authoring vocabulary** — which named selectors, friendly role
   targets, exact anchors, and route relations the Orders language permits for
   that signature. This belongs in the engine-independent
   `BotArena.TacticalPlaybooks` project because its strict compiler owns that
   language.

Create one public typed Orders signature-vocabulary catalog in
`BotArena.TacticalPlaybooks`. Refactor `ClassSkills`, `UseTargets`, and the
per-skill branches in `ValidateUse` to consume it. The App may combine it with
the resolved Engine definition when returning the sheet catalog. The Engine
must not reference TacticalPlaybooks, and TacticalPlaybooks must retain its
current no-Engine dependency.

The vocabulary needs, at minimum:

```text
class id
stable signature/skill id
allowed named selectors
allows a named friendly role
allows most-experienced-ally
allows exact authored anchor
allows route-ahead role constraint
allows route-forward target
default commitment preservation
```

Use typed enums/records rather than scattered string tests. A test must require
the vocabulary to cover exactly the launch class/signature band, once each.

### Sheet catalog response

Extend each `TacticalSheetClassResponse` with a nested signature-authoring
response. Keep existing flat ids/names for compatibility. The nested response
should include:

```text
category
argumentKind
engagementRange
cooldownTicks
initialPhase
maturityPhase
cooldownAnchor
rules-derived numeric parameters
allowed selectors
friendly-role capability
exact-anchor capability
route-ahead-role capability
route-forward capability
default commitment behavior
```

Project numeric parameters from the resolved signature definition/canonical
contract; do not maintain another copied list of range, tell, duration, radius,
movement, hull, or damage values in the App or web. A dictionary of stable
canonical parameter ids is acceptable for display-only facts, while the fields
that drive controls remain typed.

Change the named App response type, run
`bash scripts/generate-api-clients.sh`, and consume the generated type through
`web/src/site/api.ts`. Never hand-edit OpenAPI or generated clients.

Only `/api/sheets/catalog` needs the authoring payload for this goal. Do not
broaden unrelated public catalog contracts unless a current consumer genuinely
needs the same data.

### One reusable web editor

Extract the signature-use controls from the current Orders workspace into a
site-only reusable editor/model consumed by both:

- direct editable `use` leaves in Orders; and
- linked signature opportunities in Plays.

The control must render from the catalog descriptor. Remove core checks such as
`skill === 'arc-toss'`, arrays of placeable skill ids, and lists of skills that
may target friendly roles. Per-skill prose and optional presets may remain
data, but validity and available controls must come from the descriptor.

Adding a new signature that uses an existing argument/authoring shape should
require:

1. its ordinary Engine definition and class catalog entry;
2. one Orders vocabulary descriptor; and
3. optional presentation copy/preset data.

It must not require edits to OrdersWorkspace, PlaysWorkspace, the materializer,
or a switch in `sheetAuthoring.ts`. A genuinely new target topology may add one
new generic editor control and compiler/runtime adapter for that topology; it
must not add a control named after the class.

## Spatial authoring model

A signature opportunity is composed from three independent questions. Keep
them visible as such; do not build an open-ended constraint language.

### 1. Where may the body try it?

Use current conditions and geometry:

- anywhere in the active parent episode;
- while `self-in-zone` for an authored activation area;
- while current facts such as carrying, health, enemy count, readiness,
  deployment state, or observed history hold; or
- a flat `all`/`any` combination within today's condition-expression limit.

The map-first flow may create and own a source zone plus its named condition in
one transaction. It must still materialize an ordinary `when`. Do not place a
hidden source area inside `use`.

For a long route, authors should normally select one or a few meaningful
activation windows—not create an anchor for every tile. The UI may derive a
small rectangular source zone from a selected straight span, but the resulting
zone and condition must be visible and editable in Map plan/Conditions.

### 2. What does it target or aim at?

Render only policies permitted by the descriptor:

- existing named selector;
- named friendly role;
- most-experienced ally;
- existing exact `use.at` anchor where supported; or
- the new generic route-forward target described below.

Broaden `use.at` from its current three construct skill ids to descriptor-driven
exact-anchor support. For a position signature it means the exact commanded
position. For a heading/direction signature it means aim toward/through the
anchor. The ordinary target remains the eligibility trigger where the action
also needs a unit. A descriptor must reject combinations the engine action
cannot represent—for example, an exact anchor cannot fabricate the unit lock
required by a locked line signature.

Keep the existing construct behavior byte-for-byte: Hardlight Block, Trip Node,
and Sentinel Seed may use a trigger target plus exact placement anchor; the body
must still stage legally beside the point and invalid placement falls through.

Do not add an automatic "best tile in zone" chooser in this goal. Exact anchor,
explicit selector, named role, and ordered route are understandable player
decisions. A generic optional actor-target `inZone` filter is pre-authorized
only if a mandatory proof shows that the selected signature repeatedly targets
the wrong actor outside the authored encounter. If needed, it must work for any
actor selector/role target, filter before ranking/capacity, and be exposed in
both editors; do not add a Longshot- or Ambush-specific filter.

### 3. What must happen if it cannot fire?

The following ordinary siblings answer this. The Plays UI must always show the
first continuation and offer exact Orders inspection of the rest.

Common continuations are:

- continue the same route;
- hold/fight from the current post;
- move beside a placement anchor;
- continue following the veteran;
- or exit the parent through current lifecycle and resume the role ladder.

There is no opportunity-owned transition or success callback.

## Generic route-forward target

Kestrel exposes the first real gap that should not become a class-specific
field. Add one generic route target shape to `use.target`:

```json
{
  "route": "safe-return",
  "progress": "forward",
  "minimumForwardTiles": 2
}
```

Initial contract:

- `route`, `progress`, and `minimumForwardTiles` are all required.
- `progress` admits only `forward` in this version. Do not imply that reverse,
  loop laps, or arbitrary route teleporting exist.
- The route must exist, have at least two waypoints, and be terminal rather
  than closed for this first implementation.
- The descriptor must explicitly allow route-forward targeting. Kestrel Vector
  Dash is the first admitted signature.
- The source must have one unambiguous projection onto the active ordered
  route/course. A folded route that touches itself must not guess.
- The executor resolves the body's live forward governed course and chooses
  the farthest legal candidate that the signature can actually aim at, up to
  its authoritative mechanical extent.
- The relevant outcome must advance at least `minimumForwardTiles` of authored
  route progress. For a movement signature, the relevant outcome is the
  predicted caster destination, not the aim marker.
- Every possible prefix of a relocation must remain on the accepted forward
  course/corridor. A bend before the signature's travel extent makes the
  opportunity ineligible; ordinary route movement handles the bend.
- Dynamic obstruction may shorten an otherwise legal relocation but may never
  make it leave the authored corridor.
- If no legal forward resolution exists, the leaf has no target and the next
  sibling runs in the same tick with a precise decline reason.

The route resolver must use the same route-progress/course primitives as
movement and Arc Toss rather than maintaining another geometric notion of
"ahead". Extract the current Arc Toss calculation into a generic internal
route-progress service usable by:

- role target ahead on route (`aheadVia` today);
- self destination ahead on route (Kestrel); and
- future descriptor-authorized route relations.

Do not remove or rename persisted `aheadVia` /
`minimumForwardTiles` in this goal. They remain the current named-role
route-ahead spelling and must preserve existing Running Relay source. The
executor may normalize both target shapes into the same internal route
relation. Do not add `dashVia`, `wallVia`, or another skill-named equivalent.

## Signature commitment without a second state machine

The Engine already owns tells, channels, maturity, cancellation, and active
operations. The Orders/Play layer must observe that truth rather than copy its
ticks into an editor timer.

Add one generic body condition fact:

```text
signature-commitment-active
```

It is `1` while this body's own signature is in a public tell/acquisition or
channel phase, and `0` otherwise. It does not include an independent in-flight
projectile or a persistent active deployment after the caster is free. Death,
cancellation, maturity, interruption, or completion changes it through the
authoritative operation state. The authoring descriptor—not this fact—decides
whether the editor scaffolds a commitment hold by default for a given skill.

Add one generic dynamic move target:

```json
{ "move": { "to": "current-position" } }
```

At its already-resolved destination it submits/records an intentional hold,
not a movement command. It therefore does not itself trigger authoritative
move-cancellation. It remains an ordinary move leaf with normal lifecycle,
urgency, priority, and debug evidence; it is not a sixth action.

For a descriptor whose default is to preserve commitment, the editor scaffolds
one ordinary child between signature opportunities and the moving fallback:

```text
repair veteran
maintain current signature commitment  <- when commitment active, hold here
continue escort
```

This child may be shared by several opportunities in one host and should not be
duplicated per skill. It does not prevent a higher-priority root Order from
pre-empting the whole parent. An emergency can therefore still move the body
and accept the signature's normal cancellation cost. Advanced authoring may
turn off the generated hold where deliberate voluntary cancellation is the
strategy, but the UI must state that consequence.

Pin the semantics with Repair Beam plus at least one rooted tell. Do not use an
authored `commitTicks` approximation: actual operation state is both simpler
and correct when a signature is interrupted early or its mechanics later
change.

## Route continuity

The current route cursor is useful movement memory, not a new strategic state.
Correct it generically before accepting route-attached opportunities.

Required behavior:

- A non-movement sibling such as `use` must not erase the active authored route
  cursor merely because its leaf path differs for one tick.
- A transient movement course/traffic reservation may be rebuilt after the
  interruption; only durable forward route progress is preserved.
- Selecting a genuinely different movement route/state replaces the cursor
  through the existing route/state identity checks.
- Death/new life clears it.
- Higher-priority pre-emption may pause the route and later resume it, matching
  the existing Order lifecycle promise that pre-emption does not reset an
  active node.
- After Dash, Exchange, push, or another relocation, re-project the body
  monotonically onto the same route at the furthest unambiguous progress not
  behind its prior cursor. Never choose an arbitrary nearest waypoint on a
  folded route and never move progress backward merely because a later point
  is geographically close.
- If no safe forward projection exists, retain the prior cursor and let normal
  rejoin/no-path behavior speak. Do not teleport route state.

A minimal implementation may stop clearing `_routeCursors` on non-movement
leaf changes and let `RoutePlan` replace mismatched movement identities, but it
must also add forward re-projection after relocation. Do not key continuity to
a Play id or opportunity binding; the mind receives neither.

## Materialized examples

### Running Relay

The current source remains structurally ordinary:

```text
running return
  toss at source window 1 to named partner ahead on corridor
  toss at source window 2 to named partner ahead on corridor
  ...
  bank final approach
  continue carrying on corridor
```

The new sidecar binds each Toss leaf to `running return`, so the Plays UI can
show the source window, receiver, forward requirement, and continuation in one
place. Migrating the linkage alone must not change normalized Orders, ATL2, or
the focused Running Relay behavior.

`aheadVia` remains valid generated detail. A later deliberate language
migration may choose a more uniform spelling, but this goal must not break
saved sheets to make names aesthetically uniform.

### Kestrel route Dash

```text
safe return episode
  when in authored dash window and signature is ready
    use Vector Dash toward forward progress on safe-return, minimum 2
  while signature commitment is active
    hold current position
  otherwise
    continue safe-return route
```

The author selects the route and activation window on the map. The editor does
not create a list of throw/dash anchors. Dash fires only on a live straight
course whose predicted relocation stays on the route and genuinely advances
it. At a bend, under suppression, on cooldown, or with ambiguous progress,
ordinary movement continues.

### Mason ambush

```text
ambush engage
  when formation is armed and wall is absent/replaceable
    use Hardlight Block at hardlight-choke
  otherwise
    hold/fight from mason-post facing corridor-mouth
```

All existing 5/5 role-slot readiness, hostile trigger, commitment lifecycle,
capacity, and local root placement remain outside the signature opportunity.
The opportunity merely makes the already ordinary `use` leaf spatially linked
and easy to move with the choke geometry.

### Patchbay veteran protection

```text
protect veteran episode
  when veteran is damaged and Repair Beam can resolve
    use Repair Beam on veteran role / most-experienced ally
  while signature commitment is active
    hold current position
  otherwise
    continue authored escort/follow behavior
```

If the target moves or either body is damaged, the Engine may end the channel.
The plan then continues normally. A higher-priority Recover/Core/emergency
Order may pre-empt and move Patchbay, paying the public cancellation behavior.
There is no copied channel duration in the Play.

### Multi-role ambush

Each role keeps its own projection and local opportunity:

```text
Mason projection:    place block -> hold/fight
Longshot projection: rail the selected hostile -> hold sightline
Hush projection:     null when local threshold is met -> fight/guard
Reserve projection:  ordinary fight/guard only
```

Shared readiness/trigger conditions may link their `when` fields. They do not
make the actions simultaneous. Every role still resolves independently through
the same Orders tree and current information.

## Tactical Programs UX

### Primary map-first flow

1. Select a Program and one participant.
2. Select an existing durable branch/movement/fight continuation, or choose
   **Add signature along this plan**.
3. If needed, let the editor scaffold the ordinary parent branch described
   above.
4. Choose the role-owned signature. In a mixed-class role, state which class
   acts; other bodies naturally fall through.
5. Mark the activation area/route/anchor directly on the existing map surface.
6. Choose a descriptor-valid target policy and ordinary conditions.
7. See the signature above its exact continuation immediately.
8. Drag/reorder multiple opportunities inside that local child list.
9. Review the Program's one top-to-bottom position. Moving it moves every
   participant block together.
10. Save, Practice, return, and inspect why the opportunity fired or did not.

Do not build a second map. Reuse the current Tapline editor, west/east binding,
geometry ownership, group move, selection, undo/redo, and replay-track overlay.

### Suggested panel

```text
RETURN THROUGH SOUTH CORRIDOR

ALONG THE WAY
1  Vector Dash
   When: inside dash-window · signature legal
   Aim: forward on safe-return · gain at least 2
   Commitment: hold through public wind-up

2  Smoke Canister
   When: carrier enters crossing-window
   Aim: crossing-smoke anchor

OTHERWISE
   Continue safe-return route

PROGRAM PRIORITY
   2 of 4 · every participant moves together
```

The numbered list is not a second priority system; it is a focused rendering
of the exact child source order. Moving item 2 above item 1 moves the ordinary
use leaf in `host.orders` in the same undoable transaction.

### Progressive disclosure

The first view should ask only:

- where may it happen;
- what should it target/aim at; and
- what happens otherwise.

Advanced controls expose the exact `when`, `until`, timeout, cooldown, urgency,
capacity, selector, minimum progress, linked values, source ids, and generated
Instructions. Do not hide those values; keep them advanced and read-only.

Every generated/linked use card has:

- **Inspect Instructions** — advanced and read-only while linked;
- **Edit on map** — focuses owned/referenced geometry;
- **Move up/down** — changes real sibling order;
- **Detach Program** — preserves generated Instructions as unlinked advanced
  source; and
- **Remove opportunity** — removes only the owned use leaf and previews any
  now-unused helper condition/geometry cleanup.

### Capacity and roles in the UI

- Show capacity on the use leaf under advanced participation, next to the
  resolved target description.
- Explain that capacity winners are recomputed each tick and losers continue
  to the next sibling.
- A multi-body role may share one opportunity; only bodies owning the selected
  skill can execute it.
- A one-body role is the way to guarantee a particular class/body assignment.
- Required Play slots remain visible separately from opportunity capacity.
- Never label a capacity winner as reserved or assigned for the remainder of
  the Play.

### Phone and accessibility

- The same flow must work at 390×844 with no hover dependency or horizontal
  page overflow.
- Keep the selected source window/route/anchor visible while the bottom sheet
  edits the opportunity.
- Reordering, map selection, removal, and generated-source inspection need
  keyboard and touch paths.
- Returning from Orders or Practice restores the selected Play, projection,
  host plan, and opportunity.

## Explainability and Practice diagnostics

This feature succeeds only if a player can determine why a strategy did or did
not happen.

Every action and decline retains a non-empty executor reason. Add precise,
generic causes where current tracing collapses them:

- parent/root lost ordinary priority;
- activation condition false, naming the existing condition evidence;
- signature action unavailable/cooling down/suppressed;
- no selector/role target;
- exact anchor illegal or out of range;
- route projection absent or ambiguous;
- forward course bends before a safe use;
- forward gain below the authored minimum;
- receiver/landing not exact;
- target lost to capacity;
- commitment hold active;
- use accepted, with resolved target/anchor/route progress; and
- continuation selected after the use was unavailable.

Do not put a Play id or hidden phase in runtime reasons. Use authored role and
Order paths, skill id, target/geometry ids, and current public facts. After the
match, the editor can map those paths back to a linked opportunity.

The Practice return for a selected opportunity should show:

- first/last tick the body entered its activation area;
- number of eligible evaluations;
- accepted uses and authoritative signature events;
- dominant decline cause counts;
- the selected target/landing/path at an inspected tick;
- the next continuation selected after a decline/use; and
- route progress before/after a movement or custody signature where relevant.

This is diagnostic interpretation of the replay/trace. It is not an execution
input, performance score, or balance claim.

## Presets and full roster coverage

The generic editor must work without a preset. After that is true, ship one
small, editable, data-only suggestion for every current signature so players
can discover the strategic shape of the class:

| class | signature | suggested opportunity preset |
| --- | --- | --- |
| Kestrel | Vector Dash | advance on a selected straight route window |
| Palisade | Prism Wall | face a selected lane from a guard area |
| Towline | Tractor Hook | pull a selected hostile priority through a sightline |
| Patchbay | Repair Beam | preserve a named role or current veteran |
| Lantern | Survey Flare | reveal an exact approach before commitment |
| Mortar | Falling Star | punish a watched/staged hostile area |
| Minesmith | Trip Node | place at an exact alternate-route trap point |
| Hush | Null Field | trigger when enough enemies enter a local zone |
| Relay | Arc Toss | pass to a named receiver ahead on a route |
| Switchback | Exchange | swap with an endangered named role/veteran |
| Longshot | Rail Line | hold a chosen firing lane against a hostile priority |
| Mason | Hardlight Block | close an exact choke while the plan continues elsewhere |
| Sunder | Target Paint | mark the carrier/veteran inside a committed fight |
| Repulsor | Kinetic Burst | trigger when surrounded in a defended zone |
| Veil | Smoke Canister | smoke an exact crossing before a transit episode |
| Nest | Sentinel Seed | deploy an exact guard post before holding the lane |

Presets may create ordinary source zones, anchors, conditions, and use leaves.
They may not add recipe-specific runtime words. If a new class has no preset,
its descriptor-driven generic editor still has to work.

At least one compile fixture per signature must be generated from the same
descriptor/preset data. This is both product coverage and the guard against
adding a seventeenth skill to Engine while forgetting its sheet surface.

## Implementation phases

Commit after each stable phase. Preserve unrelated work already present in the
shared checkout and finish the assigned branch clean.

### Phase 0 — baseline and contract audit

- Read the current linked Plays plan/report, Orders authoring spec, executor
  silent policy, and repository `sheet-tuning` skill.
- Start from the exact current `main` after any concurrent Running Relay work
  has landed. Do not silently revert or freeze an older corridor recipe.
- Capture the current generated Running Relay Orders and focused Idle Field
  trace as a behavior baseline. This is mechanics evidence, not balance.
- Enumerate all 16 current resolved signatures, Engine argument/timing data,
  compiler-authorized targets, current web controls, and executor resolution.
- Write the coverage test first and observe it fail against the current
  hard-coded surfaces; land it green with the descriptor work in Phase 1 rather
  than committing a deliberately failing suite.
- Trace a move -> use -> move route interruption and a Repair Beam -> movement
  cancellation before changing either, so the new tests represent real seams.

Exit: a checked-in test/fixture matrix names every signature and the exact
generic authoring shape it should use. No broad gameplay change has happened.

### Phase 1 — consolidate signature authoring descriptors

- Add the typed authoring vocabulary to TacticalPlaybooks.
- Refactor compiler class/skill/target/anchor validation to use it without
  changing accepted current source.
- Add rules-derived signature mechanics/timing plus vocabulary capabilities to
  the sheet catalog response.
- Regenerate API clients.
- Replace `signatureTargets` and skill-id conditionals in the web editor with
  descriptor-driven pure functions and one shared signature-use editor.
- Preserve current direct Orders UX and source output.
- Add exact all-16 coverage, mismatch, invalid capability, and API projection
  tests.

Exit: all existing source compiles identically; a hypothetical fixture skill
using an existing shape can appear in both editors without component changes.

### Phase 2 — linked opportunity model and existing-vocabulary slice

- Add optional bindings to Play projections and normalization/migration for old
  v1 workshop data.
- Add pure binding validation, host lookup, scaffold, reorder, remove, rename,
  undo/redo, drift, detach, and cleanup operations.
- Add **Along the way** to the Plays workspace and two-way map/Orders
  navigation.
- Bind the existing Running Relay Toss leaves and Mason Hardlight leaf without
  changing their materialized Orders.
- Support source-zone creation and current selector/role/exact-placement
  controls through visible UI.
- Prove save/reload, duplicate, Practice return, detach, and old-workshop
  preservation.

Exit: Arc Toss and Hardlight can be created/edited as opportunities through
visible controls, and removing the workshop still leaves the same ordinary
source.

### Phase 3 — route-forward use and route continuity

- Add the generic `{ route, progress: "forward", minimumForwardTiles }`
  target to TypeScript, compiler, package parser, executor, docs, and direct
  Orders editor.
- Extract one internal route-progress/course service and have current Arc Toss
  route-ahead checks use it without behavior drift.
- Implement descriptor-authorized Kestrel route-forward resolution.
- Preserve route cursors across non-movement siblings and reconcile forward
  after relocation.
- Add exact decline/debug reasons.
- Add compiler, route-fold, corner, obstruction, suppression, cooldown,
  pre-emption, relocation, death, and deterministic tie tests.
- Build the Kestrel route-Dash Play through visible map controls against Idle
  Field.

Exit: Kestrel visibly dashes on safe straight spans, never leaves/cuts the
authored route, does not backtrack afterward, and ordinary movement continues
whenever Dash is not legal.

### Phase 4 — authoritative commitment continuation

- Add and document `signature-commitment-active`.
- Add and document `move.to = current-position` with intentional-hold tracing.
- Add descriptor-driven commitment-hold scaffolding between use and moving
  continuation.
- Prove Repair Beam is not cancelled by its own generated fallback before it
  can produce its intended channel effect.
- Prove a rooted tell is maintained by default.
- Prove a genuinely higher-priority emergency Order can still pre-empt, move,
  and accept normal cancellation.
- Verify persistent deployments and independent in-flight effects do not pin
  their caster after it is free.

Exit: timing changes in authoritative signature definitions do not require
copying duration constants into Plays.

### Phase 5 — roster presets and map-first refinement

- Add the optional data-only preset for each of the 16 signatures.
- Ensure each preset uses the generic editor and ordinary source rather than a
  per-skill component.
- Add source/effect/route previews from current catalog mechanics.
- Add impact-aware geometry/condition/capacity edits and deletion previews.
- Add selected-opportunity Practice summaries and trace correlation.
- Test west/east binding, map renames, shared condition/capacity consumers, and
  source-size warnings.
- Add the generic `inZone` actor-target filter only if a mandatory proof has
  demonstrated the need under the rule above.

Exit: every class has a discoverable starting point; removing preset data for
one class still leaves its generic authoring path functional.

### Phase 6 — focused behavior proofs and product hardening

Complete these focused proofs before the broad exhibit:

1. **Running Relay regression** — current corridor/receiver implementation,
   useful Tosses, uninterrupted carrier progress, and equivalent behavior
   before/after editor linkage.
2. **Kestrel route Dash** — safe straight-window Dash plus fallback and route
   continuation against Idle Field.
3. **North ambush** — 5/5 readiness before Hardlight opportunity, exact choke
   placement, ordinary hold/fight continuation, and no Play runtime state.
4. **Veteran protection** — Repair Beam targets the intended role/veteran,
   maintains its channel by default, and resumes/pre-empts understandably.
5. **All-16 authoring** — visible UI can add, inspect, save, reload, and compile
   one descriptor-valid opportunity for every signature.

Then:

- run focused compiler/runtime/web/App tests;
- run `cd web && npm test` and `npm run build` when the UI is stable;
- run the current Arc Relay movement-scene/motion-sanity gates affected by
  cursor changes;
- use `scripts/arc-relay-exhibit.sh` for the standard six-cell current-profile
  exhibit only after focused scenes pass;
- audit the generated sample manifest and every horizon result;
- serve the owner review through a verified Cloudflare URL at desktop and
  390×844 phone width; and
- report the six cells as behavior records only, with no balance conclusion.

Do not run a battery or sweep. Do not rebuild the tactical WASM mirror, mint a
frozen artifact, bump a playlist/rating era, or perform release work in this
design loop. A later owner-authorized deployment checkpoint handles those
surfaces.

### Phase 7 — documentation and handoff

- Update `docs/SHEET-AUTHORING.md` with opportunity projection examples and
  every admitted ordinary Orders word.
- Update `docs/SPEC-SHEET-ORDERS-V2.md` for route-forward target,
  commitment fact, and current-position move target if they land.
- Extend `docs/ARC-RELAY-LINKED-PLAYS-AUTHORING-PLAN.md` with a short link to
  this implemented follow-on rather than duplicating it.
- Update `docs/EXECUTOR-SILENT-POLICY.md` if any formerly hidden signature
  behavior is removed or made authored.
- Record the final architecture and any admitted grammar words in
  `docs/DECISIONS.md`.
- Write an implementation report with commits, test counts, focused replay
  evidence, screenshots, honest limits, and the public owner-review URL.
- Finish clean and cite the final commit SHA.

## Verification matrix

### Pure editor/model tests

- old workshop without opportunity arrays normalizes without source changes;
- binding resolves only a direct child `use` under its host branch;
- invalid/missing host, use, role, condition, capacity, route, anchor, or skill
  produces a local workshop issue;
- attachment scaffolds one branch transaction and preserves continuation
  semantics;
- maximum depth is rejected rather than exceeded;
- local reorder changes the actual `host.orders` sequence;
- root projection placement does not change during local reorder;
- remove leaves continuation intact and cleans only unshared owned helpers;
- rename role/condition/route/anchor updates all current references;
- drift blocks overwrite; detach preserves exact materialized Orders;
- undo/redo restores the entire multi-object edit;
- all 16 descriptors render at least one valid target policy; and
- there are no skill-id validity switches in the shared web editor.

### Compiler tests

- descriptor catalog exactly covers the class/skill table once;
- advertised selector/role/anchor/route capabilities compile;
- unadvertised combinations fail at the precise source path;
- existing current Orders canonicalize identically after catalog refactor;
- route-forward requires all fields, known terminal route, valid minimum, and
  a descriptor-authorized skill;
- existing Arc Toss `aheadVia` source remains accepted;
- `signature-commitment-active` validates as the body-local boolean fact;
- `current-position` validates only as a move target; and
- map-reference ledgers count all new route/anchor references for unused and
  missing-object diagnostics.

### Executor tests

- illegal/unavailable use falls through to the next sibling in the same tick;
- capacity loser falls through with a reason;
- source condition false does not invoke target resolution;
- route-forward target selects deterministic furthest safe progress;
- route bend, folded ambiguity, blocked ray, insufficient progress, and
  suppression all decline explicitly;
- Arc Toss route-ahead behavior remains pinned;
- non-movement use interruption preserves route progress;
- Dash relocation advances/rejoins the cursor without reverse travel;
- switching routes and new life reset correctly;
- commitment fact follows actual tell/channel start, cancellation, maturity,
  interruption, completion, and death;
- current-position emits intentional hold without a movement cancellation;
- higher root priority can still cancel through real movement; and
- in-process determinism remains exact for fixed contract/sheet/map/seed.

### API/persistence tests

- sheet catalog mechanics agree with the exact resolved Engine signatures;
- sheet catalog vocabulary agrees with the compiler catalog;
- generated clients contain the new nested response;
- workshop-only changes retain the same content hash;
- old clients omitting workshop replacement preserve opportunity bindings;
- save/reload/copy preserve bindings and generated source; and
- malformed editor linkage cannot become compiler/runtime input.

### Browser tests

- create every mandatory proof through visible controls, not JSON;
- direct Instructions and Programs use the same descriptor-driven editor;
- map selection creates/focuses the real source zone/route/anchor;
- generated Instructions inspection shows exact sibling priority and continuation;
- the Program list is top-to-bottom and no participant insertion controls exist;
- save -> Practice -> return restores exact context and diagnostics;
- detach yields editable normal Orders that compile to the same gameplay;
- desktop and 390×844 touch layouts work without hover or overflow; and
- browser console remains free of warnings/errors through the complete flow.

## Acceptance criteria

The goal is complete only when every item below is true:

1. A linked signature opportunity is editor linkage to an ordinary `use` leaf;
   no opportunity or Play identity reaches ATL2/runtime/replay.
2. There is exactly one tactical priority model. Program order and child source
   order fully explain every competition; one reorder moves all participants.
3. The Programs UI always shows the opportunity's actual continuation and the
   Program's global position.
4. A failed or unavailable signature falls through in the same tick without a
   hidden wait, alternate target policy, or movement rewrite.
5. A successful signature consumes the action and later resumes through the
   same parent plan; it never executes simultaneously with movement.
6. Channels/rooted commitments are not accidentally cancelled by the generated
   continuation, while higher-priority authored work may still cancel them.
7. Route movement interrupted by a signature resumes forward without route
   restart/backtracking; a relocation reconciles monotonically and refuses
   ambiguous folds.
8. Running Relay's existing useful behavior and accepted source remain intact
   when its Tosses gain opportunity linkage.
9. Kestrel can be told, through map controls, to Dash forward only within an
   authored route window without hard-coded per-tile anchors or `dashVia`.
10. Mason Hardlight can be moved with its choke/post geometry and still arms
    only through the existing required-slot/trigger Orders.
11. Patchbay can protect a named role/veteran without its own fallback
    immediately cancelling Repair Beam.
12. Every current signature can be added, inspected, saved, reloaded, detached,
    and compiled through the descriptor-driven UI.
13. Adding a fixture signature with an existing target shape requires no
    OrdersWorkspace, PlaysWorkspace, or materializer component change.
14. Capacity remains the current target-scoped leaf primitive and is visible,
    editable, and traceable on opportunities.
15. Workshop migration, drift, duplicate, stale-client preservation,
    undo/redo, delete, and detach remain non-destructive.
16. Orders/map-plan-only export retains identical behavior and merely loses
    linked authoring convenience.
17. The complete mandatory create/edit/practice flow works at desktop and
    390×844 touch width.
18. Every executor action/decline/hold has non-empty reason evidence and the
    Practice view can answer why a selected opportunity did or did not fire.
19. Focused scenes and the standard six-cell exhibit pass the current movement
    and felt-eligibility gates; their report makes no unsupported balance
    claim.
20. Documentation, generated contracts, focused tests, implementation report,
    clean status, final commit SHA, screenshots, and verified public review URL
    are delivered together.

## Explicitly deferred

Do not grow this goal into any of the following:

- a runtime Play/sequence/phase machine;
- atomic or simultaneous cross-role actions;
- a global signature priority list or numerical priority score;
- a compound move-and-use action;
- sticky body reservations or a second capacity system;
- arbitrary condition nesting or a general scripting language;
- automatic combo discovery, tactical optimization, or "best tile" AI;
- hidden executor signature use outside authored `use` Orders;
- backward/loop route-target semantics before a concrete strategy needs them;
- a broad target-zone query language (only the narrow pre-authorized actor
  `inZone` filter may be added if mandatory proof requires it);
- backup-roster/match-to-match adaptation;
- new classes, signature balance/timing changes, map changes, or scoring work;
- replay-format Play awareness; or
- hosted promotion, playlist/rating migration, or release artifacts.

## Stop conditions requiring an owner ruling

The implementation agent should continue autonomously through ordinary code,
UX, and test choices within this plan. Stop rather than improvise if:

- a mandatory use case cannot be represented without a new priority/lifecycle
  system;
- route-forward use requires class-specific runtime knowledge that cannot be
  expressed by authoritative signature capability/mechanics;
- preserving a signature commitment requires a copied duration rather than
  observing its public operation;
- a proposed workshop field would affect gameplay while being absent from
  Orders/map plan;
- compatibility would require silently rewriting a user's detached or drifted
  Orders;
- the implementation needs a sixth action rather than the admitted
  current-position move target;
- the all-16 descriptor audit discovers that an existing signature cannot be
  meaningfully targeted through current/generalized shapes; or
- focused traces show that the required behavior depends on a balance/rules
  change rather than authoring or executor correctness.

Everything else—component boundaries, DTO names, local helper layout, visual
copy, and test fixture organization—may be resolved by the implementation
agent while preserving these contracts.

## Final product test

The feature is successful when a player can say:

> "While this role carries out this understandable plan, try its signature in
> this authored place against this authored kind of target. If it works, keep
> going; if it cannot, keep going. Show me exactly why."

And the generated Orders tree says exactly the same thing.
