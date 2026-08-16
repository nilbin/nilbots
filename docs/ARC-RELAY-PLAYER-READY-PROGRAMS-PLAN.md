# Arc Relay player-ready Tactical Programs follow-up

Status: **proposed implementation plan; ready to assign, not yet implemented**  
Date: 2026-08-16  
Baseline: current `main`, local/review loop profile `tapline-8`

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

## Product boundary

Preserve these constraints throughout:

- **Programs remain the only player-facing priority list**, evaluated top to
  bottom.
- **Instructions remain the only executable strategy language.** Program
  authoring materializes ordinary Conditions, capacities, map geometry, and
  Instructions.
- A Program, Post, formation, signature opportunity, or maintenance policy
  adds no runtime identity, phase, reservation, atomic activation, state
  machine, replay field, or competing priority system.
- One body still takes at most one action per tick. A signature shown “inside”
  a movement or Post card remains an ordinary `use` sibling above the ordinary
  movement/fight continuation. If it cannot execute, continuation is considered
  in the same tick.
- Capacity remains the existing generic “how many bodies may resolve this
  target” primitive. Do not invent fixed unit assignments or a hidden formation
  allocator.
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
   - draw an eight-way direction arrow from the Post, represented underneath by
     a derived facing anchor.
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
hover-only action, and no horizontal page overflow at 390×844.

### Facing semantics

Reuse the existing `move.face.anchor` behavior:

- facing is applied only after the body reaches an authored anchor/zone
  destination;
- it does not alter patrol-route movement;
- the direction-arrow affordance creates/moves a derived anchor rather than a
  second facing mechanic;
- a zero-length facing arrow is invalid and is explained before save;
- a combat response may turn toward its combat target; when that response ends,
  the Post continuation restores the authored facing.

Pin these semantics in focused tests and communicate them in the Post summary.

### Capacity and exact formation rules

Capacity is mandatory in this design, not an Advanced afterthought:

- an exact one-body Post defaults to capacity 1;
- two Posts for a two-body role materialize two capacity-1 movement targets;
- each required Post receives its own role-qualified readiness zone/Condition,
  so one body cannot satisfy two exact slots;
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

Add one generic zone-qualified body fact:

```text
deployment-in-zone
```

Its exact contract is:

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

For an exact target, the Program owns a derived one-tile zone linked to the
effect anchor. Moving or renaming the target updates both in one edit. The
normal maintain policy materializes:

```text
wall-at-choke:
  fact: deployment-in-zone
  operator: equals
  value: 1
  zone: wall-target-zone

wall-missing-at-choke:
  fact: deployment-in-zone
  operator: equals
  value: 0
  zone: wall-target-zone
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
- Core acquired / carrier now loaded;
- required area currently has zero **visible** enemies, labelled “visibly
  clear” rather than “known empty”;
- no observed entry during a fully observed interval, when the player chooses
  the stronger evidence requirement;
- required deployment present or gone;
- minimum duration only;
- maximum timeout only.

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

## 5. Improve condition and signature ergonomics without another language

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
- Core carried/value/source state;
- respawn-wave timing;
- veteran level;
- deployment present/durability/lifetime.

The underlying Condition remains visible in Advanced. This is guided
authoring, not natural-language parsing and not a hidden inference engine.

All signature target capabilities remain descriptor-driven. Adding a future
signature with an existing target shape must not require a switch in the Post,
Program, or map components.

## 6. Make Practice explain Programs well enough to iterate

Because Programs remain editor-only, derive these views by correlating the
current workshop sidecar with generated Instruction paths and authoritative
replay/debug events. Do not add Program ids to ATL2 or replay.

For a selected Program and body, Practice must answer:

- which Program currently supplied the body’s eligible/accepted Instruction;
- which higher Programs were ineligible and why;
- Posts filled and missing, by role;
- required deployment present, missing, damaged, expiring, or suppressed;
- waiting, preparing, ready, committed, success, timeout, cooldown, and
  fallthrough in terms derivable from ordinary source;
- signature attempted, accepted, or declined, with the exact reason (cooldown,
  range, blocked line, missing target, wrong route progress, etc.);
- the continuation chosen when the signature did not execute;
- the response’s minimum-commit and maximum-time clocks;
- the exit/fallthrough cause.

Clicking a body or status row should highlight its current Post, facing arrow,
signature target/window, route, and relevant readiness group on the map.

Add a compact post-run Program timeline/summary sufficient to answer:

- Did it ever prepare and arm?
- How long did it remain committed?
- Did the intended signature fire, and where?
- What success/timeout/failure ended it?
- Which Program took over next?

Never infer success that the replay cannot prove. Say “not observed” or show
the exact source-path evidence when necessary. Every executor action, decline,
and hold continues to require a non-empty debug reason.

## 7. Deliver two complete starter doctrines

Do not force the first player to begin from a blank sheet. Deliver two complete,
legal, editable starter sheets with sensible lower-priority scoring, loose-Core
recovery, healing, and combat fallbacks so every body has useful behavior when
its headline Program is inactive.

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
  Well and uses route-forward Dash only in its legal activation span.

The shared trigger is at least five **remembered/known** enemies in the drawn
mid area within an explicit freshness window. It must not depend on them all
remaining visible every tick.

Acceptance behavior:

- each ambush has multiple roles and capacity-1 exact slots;
- it reports every slot and required deployment separately;
- neither ambush arms until all its required setup is ready;
- the response stays committed for its configured minimum even when an enemy
  leaves mid or visibility changes two seconds later;
- each wall is maintained at its own intended tile;
- the Kestrel contingent can proceed independently;
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
- change a rendezvous or minimum gain and regenerate linked Instructions
  without editing Conditions, source zones, or derived anchors manually.

The focused proof only needs to succeed against Idle Field. Compare it with a
solo carrier on the exact same route and current rules so the replay shows
whether the Tosses actually accelerate delivery. This is a behavior/stress
proof, not a balance claim. Inspect the generated Instructions and assert that
the race contains no `aheadVia` or other race-only executable field.

Design audit on 2026-08-16: the current generated exact-station race was copied
to a temporary diagnostic and all three `aheadVia`/`minimumForwardTiles` pairs
were removed, leaving ordinary partner-role targets. On Tapline-8 seed 9001
against Idle Field, both versions accepted Tosses at
`t73/126/149/240/385`, destroyed the reactor at `t462`, and produced the same
gameplay-state digest
`6dc664434a8f482a412cda2d107102d291694647698124bcd62169e688a87260`.
This establishes redundancy for the exact-Post construction; it is not a
general argument against the Advanced dynamic-route qualifier. The same-route
solo control finished at `t359`, so this baseline succeeds mechanically but
does not currently demonstrate acceleration. Preserve that honest comparison
until the new authoring round produces and explains a better staged tactic;
do not convert the diagnostic into a balance claim or silently omit it.

## 8. Acceptance scenes and tests

Implement trace-first and pin the smallest scenes before broad UI work.

### Runtime/compiler scenes

1. `deployment-in-zone` is 1 at the intended tile and 0 for no deployment, a
   deployment elsewhere, destruction, expiry, and replacement.
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
   expression/depth limits, and missing fallback behavior.
9. Running Relay source/receiver Post geometry computes forward route gain in
   the editor, rejects insufficient or ambiguous station relationships, and
   generates an ordinary role-target Toss with no `aheadVia`.

### Browser acceptance

Run the production web build at desktop and 390×844 touch width. From a copied
starter sheet, without opening Advanced:

1. select the defending Mason Post;
2. move its standing tile one step;
3. redraw its facing direction;
4. move the Hardlight target one step;
5. toggle “rebuild when missing” and “required before arming”;
6. change its capacity/readiness relationship;
7. run the deterministic drill;
8. click the Mason and explain whether/why it moved, faced, cast, held,
   committed, and exited;
9. save, reload, and rerun the same scenario/seed.

No raw id, Condition, Instruction, or JSON edit is permitted in this acceptance
flow. Capture desktop and phone evidence with zero console warnings/errors.

## 9. Playable-alpha gate

This round is not complete merely because the new controls render or compile.
It is complete only when all of the following are true:

1. The canonical Mason sentence is authorable and editable entirely from the
   normal map-first Program surface.
2. The exact target is maintained correctly across cast, cooldown, damage,
   destruction, expiry, suppression, relocation, death, and respawn semantics.
3. Exact Posts, multiple roles, capacity, all-slots-filled readiness, and
   maximum formation spread are visible and reliable.
4. Tactical commitment survives transient trigger/readiness loss and exits for
   understandable reasons.
5. Waiting/staging choices do not silently starve lower Programs.
6. Mid Overload actually triggers in its deterministic drill and exposes its
   2/5/1 behavior in Practice.
7. Running Relay advances and banks against Idle Field, generates no
   `aheadVia`/race-only executable field, and remains editable through
   Programs; the same-route solo control is linked beside the proof.
8. Both starter sheets are legal eight-body, seven-class rosters with one
   duplicate and complete fallbacks.
9. A player can identify the current Program and “why” for any selected body
   from Practice without reading generated Instructions.
10. The full create/refine/practice/save/reload flow works on desktop and phone.
11. Focused tests, the full relevant web suite/build, compiler tests, and
    deterministic gameplay scenes pass.
12. Documentation describes Posts as authoring sugar, the new Condition fact,
    commitment/fallthrough, capacity, and the two starter doctrines accurately.

After focused diagnosis, deliver the normal standard six-cell phone exhibit
for one complete starter sheet against one named current practice control,
using the exact same pairing in every cell. Deliver the deterministic Mid
Overload drill and Relay-vs-solo proof as separately labelled mechanical
evidence. Outcomes are behavior records only; make no balance verdict and do
not run a battery or sweep unless the owner separately asks.

Owner review requires a verified public Cloudflare tunnel URL, production
browser evidence, a concise implementation report, a clean worktree, and the
final commit SHA. Do not deploy. Do not rebuild the tracked tactical WASM
mirror during this design loop; that remains an explicit release checkpoint.

## Implementation order

1. **Baseline and traces** — preserve current generated Program/Running Relay
   source and record the failing commitment, stage re-entry, and wrong-location
   deployment scenes.
2. **Semantic foundation** — implement `deployment-in-zone`, commitment
   materialization, not-ready staging, and focused runtime/compiler tests.
3. **Post linkage** — add backward-compatible workshop linkage, derived
   geometry synchronization, capacities, readiness, maintenance, drift, and
   undo/redo tests.
4. **Map-first UX** — build the Post inspector, facing arrow, target/activation
   distinction, legality preview, normal trigger/outcome controls, and mobile
   editing flow.
5. **Practice explanation** — correlate Programs/Posts with ordinary source
   and replay evidence; add the selected-body why/timeline views.
6. **Starter doctrines** — finish Mid Overload and Running Relay as two legal,
   fully fallback-equipped sheets and add the deterministic drills/controls.
7. **Validation and delivery** — run focused and full relevant tests, browser
   QA, exact mechanical proofs, standard exhibit, docs/Decision update, public
   tunnel verification, report, commit, and clean-status check.

## Explicitly deferred

Do not expand this goal into:

- runtime Programs, atomic multi-body activation, reservations, or a second
  state machine;
- arbitrary nested Conditions or a visual programming language;
- simultaneous move-and-use actions;
- fixed unit-id assignments in place of roles and capacities;
- backup rosters or between-match substitutions;
- new classes, signatures, maps, scoring, health/damage tuning, Core economy,
  or other balance changes;
- a generic AI that decides “optimal” signature use for the author;
- ladder/playlist/rating migration, deployment, release, or WASM checkpoint;
- balance conclusions from the drill, solo comparison, or six-cell exhibit.

Those are later decisions. This round is narrowly about making the strategy
language dependable, spatially ergonomic, explainable, and complete enough for
the owner to begin playing and evaluating the actual game.
