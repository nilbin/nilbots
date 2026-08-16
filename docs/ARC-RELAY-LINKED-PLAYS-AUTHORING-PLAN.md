# Arc Relay linked Plays authoring plan

Status: **implemented on `main` on 2026-08-16. No Orders, compiler, executor,
or gameplay-language expansion was required.**

Implement against `docs/SHEET-AUTHORING.md` and
`docs/SPEC-SHEET-ORDERS-V2.md`. Decisions #294 and #317 remain authoritative:
one direct positional-priority Orders tree, current capacity/lifecycle words,
destination facing and observed history, and no named-play runtime or second
state machine.

## Owner amendments — 2026-08-16

These amendments change the implementation order and proof posture of this
plan:

- Do not build a disposable mock-up before the product path. The first phase is
  a thin but real vertical slice of the **Plays** workspace, including the App
  API/persistence required to save it and the web UI required to author it.
- Build and prove the North corridor ambush and dual Relay chain through visible
  editor controls. Do not author either acceptance Play by editing JSON and then
  merely load it into the editor.
- Use the exact current authoring vocabulary in UI labels. A role contains
  **Orders**; Orders use `when`, `until`, `timeout`, `cooldown`, `urgency`,
  `capacity`, and the five action names `move`, `fight`, `use`, `handoff`, and
  `drop`. Zones, routes, and anchors retain their map-plan names. Do not create
  synonyms such as “task”, “staffing rule”, or “commitment field” for an
  existing Orders concept. Recipe explanations may use natural prose, but every
  editable control and generated-source explanation names the underlying
  Orders word.
- The behavior proof may use the fully idle practice opponent. The proof is
  about whether the authored Orders execute the intended ambush and Relay
  sequence, not whether either Play beats a competent sheet. Compilation,
  persistence, deterministic projection, editing, detach, desktop/phone UX,
  and generated-Orders inspection remain required.

## Implementation result — 2026-08-16

The production path now follows this plan:

- `arc-relay-workshop-v1` is persisted with a sheet in PostgreSQL and copied
  with it, but is excluded from the sheet content hash, compiler input, ATL2,
  matches, and replays. An old client that omits workshop fields preserves the
  existing sidecar.
- The editor has the fourth `Plays` workspace on desktop and phone. It uses the
  real Tapline map, exact Orders names, west/east binding, drag and atomic group
  movement, role projections, linked values, readiness ranges, capacity,
  priority neighbors, generated-Orders inspection, drift recovery, detach,
  delete, and undo/redo.
- Six editor lenses use one deterministic projection path: blank, Ambush,
  Relay chain, Veteran escort, Safe return, and Lane closure. They materialize
  only ordinary `move`, `fight`, `use`, `handoff`, and `drop` Orders plus
  ordinary map-plan objects.
- Practice return restores the selected Play, opponent, seed, and workspace.
  Its map overlay filters actual unit tracks to the selected participants and,
  for a required-slot Play, reports the best observed readiness frame.

The acceptance sources were authored through visible UI controls and persisted
as sheet `e058bbd2-8443-4e0a-97b6-3338225a5698` (`Linked Plays Fieldbook`).
Against Idle Field, the North ambush reached all five required posts at tick 40.
The Relay chain selected `relay-chain-a-toss-r0-r1` at tick 54 and
`relay-chain-b-toss-r1-r2` at tick 64, then continued homeward; the match ended
at tick 257. Because Idle Field has no enemies, the Ambush proof deliberately
proves staging/readiness rather than hostile engagement. Its authored northern
traffic threshold was restored after that focused staging run.

Sheet `b5dcabcc-37f0-4c40-ab78-3433aa8f0d23` is the visible-UI detach proof:
both Plays were detached, the sidecar contains no Plays, and the materialized
Orders/map plan still compile server-side. The focused implementation record,
test matrix, limitations, and screenshots are in
`docs/reports/LINKED-PLAYS-AUTHORING-REPORT.md`.

## Outcome

A human should be able to author and maintain a spatially coordinated tactic
without manually reproducing linked decisions across role trees. The hardest
initial case is an anchor-precise multi-role ambush: every required slot must be
filled by an eligible body within an authored tolerance before it can arm. A
second, deliberately different case is two Relays leapfrogging a Core down the
long return corridor through bounded receiver posts and Arc Toss.

The author should be able to move either Play, change its participants, shared
route/anchors, readiness or timing where applicable, and local priorities
without hunting through generated leaves.

The existing authoring paths remain first-class:

- **Squad** continues to cast the eight bodies into roles.
- **Orders** continues to author and reorder ordinary role priorities.
- **Map plan** continues to create, name, and edit zones, routes, and anchors.
- A new additive **Plays** workspace offers map-first coordinated authoring.

The feature succeeds only if it improves both first creation and later
iteration. Generating a pile of disconnected Orders once is not success.

## Product decision: call the sugar layer “Plays”

Use **Play** in the workshop because that is the player's natural description
of “these bodies coordinate around this place when this situation occurs.” In
implementation and contract discussions, call it a **linked editor Play** to
keep the boundary explicit.

> A Play is a linked workshop object that owns map selections and projections
> into one or more role priority trees. It materializes ordinary Orders. It has
> no runtime state, global priority, gameplay identity, or execution semantics.

A Play may involve one role, several roles, or several bodies in one role. The
North corridor ambush deliberately spans Mason, Longshot, Hush, and a two-body
reserve role. The Relay chain deliberately spans two one-body Relay roles whose
projections refer to one another. Number of roles is never a Play-type
restriction.

This is not a revival of the retired tactical-playbook language, a named-play
state machine, or the replay play-awareness proposal. A match executor does not
know that a Play existed in the editor.

## Hard boundaries

This work must not:

- add a `plays` key, play reference, anchor set, selector, condition, action, or
  lifecycle word to `arc-relay-orders-v2`;
- add anything to `arc-relay-map-plan-v2` beyond its existing zones, routes,
  and anchors;
- add compiler desugaring or another intermediate representation;
- introduce global Play priority, Play phases, sticky unit reservations, or a
  second state machine;
- replace or diminish the current Squad, Orders, Map plan, JSON import/export,
  or hand-authored workflow; or
- infer behavior that the materialized Orders do not state.

The output of the new workflow must be a normal valid Orders document plus a
normal valid map plan. Compiling those two files without workshop metadata must
produce the same gameplay as compiling them from the linked editor.

If a desired Play cannot be expressed by the current language, the editor must
explain that limitation. It must not silently broaden Orders as part of this
project.

## UX model

### Generic linked Orders, recipe-specific lenses

Do not make `ASSEMBLE`, `READY`, and `ENGAGE` mandatory fields in the base Play
model. They are the useful human lens for an Ambush recipe. The generic editor
model is smaller:

- one or more participating roles/bodies;
- one or more linked Orders projections in the participating roles;
- selected map geometry and class skills;
- shared editable values such as a trigger, route, anchor, capacity, timeout,
  or partner role;
- an explicit projection position in every affected role tree; and
- ordinary continuation after each projection.

Recipes arrange those ingredients for a human problem. **Ambush** adds required
slots, proximity readiness, arming, and commitment. **Relay chain** adds two
partner roles, carrier/receiver tasks, one editable corridor with derived
bounded hop routes, and Arc Toss. Neither recipe introduces a runtime type, and
an empty/custom Play remains possible.

### Four complementary workspaces

The current three-section editor becomes four sections:

```text
Squad | Orders | Map plan | Plays
```

**Orders-first authors** can ignore Plays completely. **Map-first authors** can
create a Play and still inspect its exact position and generated behavior in
Orders. A sheet may freely mix ordinary hand-authored orders and linked Plays.

The Plays workspace reuses the real Tapline map and the same geometry editing
component rather than building a second map implementation. It adds a
contextual Play panel beside or beneath the map:

```text
┌──────────────────────── Tapline map ────────────────────────┐
│ watched zone       ● exact body posts       → watched angle │
│ Mason block        participant paths        replay tracks   │
└─────────────────────────────────────────────────────────────┘
┌─ North corridor ambush ─────────────────────────────────────┐
│ ASSEMBLE  northern traffic observed                         │
│ SLOTS     Mason · Longshot · Hush · two reserve posts       │
│ READY     5/5 required slots within their authored ranges    │
│ ENGAGE    four enemies visible in north entry                │
│ COMMIT    once engaged, at least 30 ticks                    │
│ RELEASE   four confirmed destructions · maximum 90 ticks    │
│ PRIORITY  after Recover, before ordinary objective work      │
│ NEXT      each role’s named fallthrough                      │
└─────────────────────────────────────────────────────────────┘
```

On a phone, the map remains the primary surface and the Play panel becomes a
bottom sheet. The selected anchors and affected bodies must remain visible
while changing a field. No step may depend on hover.

### One Play, several visible role projections

Each participating role receives one or more ordinary generated nodes at an
explicit point in its existing priority tree. Orders presents those nodes as a
single linked projection card where possible:

```text
Recover
[PLAY · North corridor ambush · crossfire slot]   Edit on map →
Score a Core
Baseline patrol
```

The projection card is read-only in content. Expanding it reveals the exact
generated conditions, actions, targets, capacities, and lifecycle in a
read-only explanation, so the sugar never conceals execution. Its neighboring
ordinary orders show exactly what can pre-empt it and what follows it.

The whole projection may be moved as one atomic priority item if that proves
clearer in UX testing; its generated children may never be edited separately
while linked. Moving one role's projection changes only that role's priority
and updates the Play metadata. The Plays workspace also offers a bulk placement
control such as “after Recover, before Score” with per-role overrides.

### Two-way navigation

- Selecting a Play highlights every zone, route, anchor, facing target, role,
  condition, capacity, and generated order it consumes.
- Selecting a Play-owned map object identifies its purpose, such as “Hush
  firing post” or “corridor observation zone,” and offers **Edit Play**.
- Selecting a Play projection in Orders highlights its geometry and opens the
  Play at the relevant role/slot.
- Selecting an ordinary map object still lists its ordinary Orders consumers.
- Renaming geometry remains reference-aware across Orders and Play metadata.

The current Map plan workspace remains a valid way to move a Play's authored
anchor or watched zone. Because projections reference the same map-object id,
moving its geometry there immediately changes the linked Play without
regeneration of unrelated Orders. A readiness zone derived from a slot anchor
is visibly labelled as Play-owned and cannot be dragged independently; moving
the anchor or changing its range updates that derived zone in the same
transaction.

## Generic authoring flow

The primary flow should follow the player's thought process rather than the
JSON structure.

1. **Create and name the Play.** Start from an empty Play or a small recipe such
   as Ambush, Relay chain, Escort, Screen, or Return chain. A recipe only
   preconfigures the editor; it is not a runtime type.
2. **Mark the ground.** Select existing geometry or draw/name the watched zone,
   exact posts, facing anchor, deployment anchor, and optional routes directly
   on Tapline. Multi-select and group movement should make relocating a whole
   setup practical.
3. **Choose roles and linked Orders.** Select one or more roles, including
   multiple bodies from one role where useful. Give each role its current action
   and lifecycle intent; link the values that should edit together rather than
   copying them.
4. **Add coordination gates where the tactic needs them.** An Ambush uses exact
   required slots and a full-formation arm gate. A Relay chain instead branches
   on each Relay's current carrying state and partner availability. Do not make
   formation readiness universal.
5. **Place each role projection.** Show the order immediately above and below
   every insertion. There is no separate Play priority.
6. **Review continuation.** Before saving, state what every participant does on
   success, timeout, pre-emption, unavailable signature, destruction, and
   respawn. Continuation is the next existing eligible order, not a Play-owned
   transition.
7. **Save, practice, and refine.** Compile through the current server path. On
   return from Practice, overlay actual tracks and reopen the same Play without
   losing selection or context.

Each recipe uses progressive disclosure appropriate to its tactic. Ambush first
asks for assembly, required participants/ranges, hostile trigger, release, and
priority. Relay chain first asks for two Relays, corridor, toss direction, and
fallback. Exact generated leaves, cooldown, urgency, capacity, and advanced
condition details remain available without crowding the initial flow.

## Capacity limits participation

Plays must expose the current target-scoped capacity rule rather than inventing
a new roster allocator.

Two slot styles cover the important cases:

- **Role-specific slot:** a named role receives a particular job and anchor.
  A one-body role gives exact body assignment; a multi-body role continues to
  use current eligibility and capacity semantics.
- **Pooled slots:** any eligible body from selected roles may reach a series of
  exact anchors. The generated leaves share current named or inline capacities,
  so each resolved anchor admits the intended number of bodies. Distance and
  stable unit id continue to settle claims exactly as they do today.

The UI must say that capacity is recomputed and is not a sticky reservation.
If an author needs a particular class permanently at a particular post, the
editor should recommend a role-specific slot rather than promising that a pool
will stay assigned.

Capacity answers **who may claim each destination**. Readiness answers
**whether the required claimants have actually arrived**. The Play editor must
show both; neither is a substitute for the other.

Capacity controls belong next to the slots on the map. The author should see
“2 bodies available for 2 posts; max 1 per post,” not have to discover a shared
integer in a separate library. The resulting capacity remains visible and
editable through the existing Capacity library, with an impact list if it has
consumers outside the Play.

## Ambush readiness and arming without a Play state machine

For formation recipes, the editor may use **assembling**, **armed**, and
**committed** to explain the author's design, but these are derived authoring
concepts rather than new runtime phases or universal Play fields:

- **assembling:** ordinary lower-priority movement leaves are taking eligible
  bodies to their slot anchors;
- **armed:** every required slot is occupied by an eligible role within its
  authored range, so the readiness expression is true; and
- **committed:** the ordinary higher-priority engagement branch has entered
  because readiness and the enemy/event trigger were both true. Its existing
  `until`/timeout lifecycle now latches it.

For the first implementation, materialize exact readiness with current words:

1. Each required slot has an existing anchor, one eligible role or role pool,
   and a non-negative tile tolerance.
2. The editor generates or owns a readiness zone around that anchor. Range zero
   is the anchor tile; range one is its clipped 3×3 square, and so on.
3. A current `role-in-zone-count >= 1` condition proves that an eligible role
   occupies that slot. A reserve pool should therefore be one explicit role
   when exact pooled readiness matters.
4. A current flat `all` expression combines every required-slot condition with
   the hostile engagement trigger.
5. The engagement branch sits above the assembly/hold leaves. It is ineligible
   while any slot is empty; when the last body arrives and the hostile trigger
   is true, it wins through ordinary positional priority.

The materialized per-role shape is conceptually:

```text
...ordinary emergency work above...
ENGAGE
  when  = hostile trigger AND every required slot ready
  until = minimum commitment AND success
  timeout = engagement deadline
ASSEMBLE / HOLD
  when  = assembly trigger
  until = hostile trigger AND every required slot ready
  timeout = unused-setup deadline
  cooldown = engagement deadline plus the authored rearm delay
...ordinary continuation below...
```

`ASSEMBLE / HOLD` latches after its assembly trigger, so a temporary loss of
that evidence does not scatter a half-built formation. When the complete arm
gate becomes true, that branch exits and the higher `ENGAGE` branch enters
through the same current condition expression. `ENGAGE` then latches through
its own existing lifecycle. After success or its timeout, ordinary fallthrough
continues while the assembly branch remains on its ordinary cooldown; no
editor-only phase is consulted. The Play UI should present that cooldown as
**rearm after**, rather than making the author coordinate two node clocks
mentally.

Readiness zones for slots drawing from the same role pool must not overlap. If
they overlap, one body could satisfy two current zone counts, which would be a
false claim that every slot is filled. The editor must reject that setup,
require a smaller range/different geometry, or require role-specific slots. It
must also respect the current flat-expression limit: if the requested number
of gates cannot be represented, explain the limit rather than manufacture a
condition-of-conditions feature in this project.

An unrelated friendly must not make a required slot ready. Use role-qualified
facts for required readiness; a broad `friendlies-in-zone` count is suitable
only for an explicitly broad, non-exact optional screen. This is why readiness
is part of the primary acceptance example rather than polish: it forces the
editor to model exact bodies, pooled capacity, space, and lifecycle honestly.

For the first slice, a required pooled set must draw from one explicit
multi-body role, as the two-body reserve does below. A union such as “any body
from any of these three roles may satisfy either required slot” cannot be
combined honestly with every-slot readiness under today's flat condition
expressions. The editor may support that for optional staffing, but must reject
it for a required readiness gate or ask the author to assign explicit roles.

## Primary acceptance use case: North corridor ambush

The first vertical slice must implement this complete task using only current
Orders semantics.

### Intent

After northern traffic has been observed, a Mason, Longshot, Hush, and two
available bodies from a two-body reserve role assemble at five exact posts and
face the remaining opening. The ambush is not armed until all five required
slots are occupied within their authored tolerances. When four enemies then
enter the northern corridor, Mason closes one route and the formation commits.
The group stays committed despite momentary loss of visibility or formation,
tries to earn four confirmed destructions, and eventually returns to each
role's ordinary priorities.

### Human construction

1. Draw or select `north-entry`, the zone whose visible enemy count triggers
   engagement once the ambush is armed.
2. Place `north-corridor-mouth`, the common facing anchor.
3. Place `mason-post`, `hardlight-choke`, `longshot-post`, `hush-post`, and two
   `reserve-crossfire` anchors.
4. Assign Mason, Longshot, and Hush to their role-specific slots. Assign one
   explicit two-body reserve role to the two crossfire anchors with capacity
   one per resolved anchor.
5. Mark all five slots **required** and set each readiness tolerance. The
   editor displays `0/5` through `5/5`, generates role-qualified readiness
   zones/conditions, and refuses overlapping ranges that could let one reserve
   body fill both reserve slots.
6. Choose a historical northern-traffic condition to begin assembly. Choose an
   existing `visible-enemies-in-zone >= 4` condition, or create it in place, as
   the hostile engagement trigger.
7. Verify the arm gate in plain language: “Armed only when Mason 1/1,
   Longshot 1/1, Hush 1/1, and reserve 2/2 are within range.” The generated
   engagement `when` requires that readiness plus the enemy trigger.
8. Set a minimum commitment of 30 order ticks, release after four confirmed
   destructions since node entry, and cap the attempt with a 90-tick timeout.
   The editor may materialize the current flat `all` expression for minimum
   time plus success; no nested condition feature is required.
9. Place every projection below emergency recovery/Core-carrying work and
   above ordinary objective or patrol work. Show the exact surrounding order
   names per role.
10. Review the generated behavior: every role first assembles/holds through
   ordinary movement. Mason's assembly projection moves into skill range,
   places or maintains Hardlight using ordinary body-local `use` eligibility,
   then holds/faces. Only the fully ready body formation can enter engagement;
   every shooter moves to and faces from its exact post; capacity losers fall
   through according to their existing order tree.

The first slice gates arming on the five required **body slots**, not on
team-wide knowledge that Hardlight is active. Current deployment facts are
body-local, so the editor must not claim that every role knows Mason's wall
state. If wall-active readiness later proves essential, record that as a
separate minimal Orders question rather than faking it in the Play sidecar.

### Continuation preview

The Play card must answer, per role:

| Event | What happens |
| --- | --- |
| one required slot is empty | the engagement branch cannot enter; eligible bodies continue ordinary assembly movement |
| one staged body is just outside its tolerance | the formation remains unarmed and the UI identifies that slot |
| an unrelated ally crosses a slot | role-qualified readiness does not count it |
| the final required body arrives | readiness becomes true on the next ordinary decision read; the Play is armed |
| enemies arrive before all slots are ready | the engagement does not enter merely because its hostile trigger is true |
| a body is displaced before engagement | readiness becomes false and the formation returns to assembling/holding behavior |
| enemies leave view two ticks after engagement | the entered branch remains latched because it has `until` |
| a body is displaced after engagement | the committed branch follows its ordinary latched lifecycle rather than silently disarming |
| four kills occur before tick 30 | participants remain committed until the minimum window is satisfied |
| only two kills occur | the Play continues until success or the 90-tick timeout |
| Recover or Core-carrying work outranks it | that ordinary higher priority pre-empts it; current lifecycle timing remains authoritative |
| Hardlight is active or unusable | Mason falls through to its next generated hold/facing leaf, then later ordinary orders when the Play exits |
| the Play succeeds or times out | each survivor re-evaluates its existing tree and continues with the next eligible order |
| a body dies | current death/respawn lifecycle applies; no Play roster reservation or replacement state is invented |

### Required edit test

After saving and practicing, the author must be able to perform all of these
without editing generated leaves:

- drag the wall and firing line one tile south as a group;
- replace one reserve role with another;
- change two pooled posts to three and see the capacity/roster consequence;
- change a slot's readiness tolerance and immediately see overlap/false-fill
  validation;
- change the minimum commitment from 30 to 40 ticks;
- change the Mason projection's priority relative to scoring work; and
- rename the Play and one anchor without breaking any reference.

Every linked projection updates in the same undoable transaction. Unrelated
orders retain their ids, content, and position.

## Second acceptance use case: dual Relay long-corridor chain

This is the generality check. The editor architecture is too Ambush-specific if
it cannot express this without pretending that the Relays occupy a formation or
wait for a global arm gate.

### Intent

Two one-body Relay roles bring an enemy-side Core home through the authored
long corridor through alternating receiver posts. The empty Relay advances to
exactly its **next** homeward receiver post and holds there; it must never keep
running down the generic return route or skip ahead to a later post while still
empty-handed. The carrier holds at the current launch post until its partner
occupies that next legal catch area, then uses ordinary Arc Toss. Each catch
post doubles as that Relay's current launch/hold post for the following leg.
Possession, station position, and an ordinary latched move lifecycle determine
which Relay advances and which carries, so the two roles take turns without an
authored turn counter or writable Play state.

This is a class-specific Play, which is desirable. The Relay chain recipe is
available only when the selected participants own `arc-toss`, and its value is
precisely that it makes a signature mechanic into an editable team strategy.

### Human construction

1. Draw the long enemy-to-home corridor once in the Play workspace. Place an
   ordered series of stations `R0, R1, R2...` along it, alternating Relay A /
   Relay B ownership. Adjacent stations may be no farther apart than current
   legal Arc Toss geometry allows. Give each station a small role-qualified
   catch area; it is both the catch destination for one leg and the launch/hold
   point for the next. The recipe derives the ordinary, used route segments
   required by each hop from this one editable corridor gesture.
2. Select two one-body Relay roles. The editor creates two linked projections
   and visibly pairs each role with the other; it does not collapse them into
   an opaque two-body unit.
3. For each Relay and station, define the carrier task: while carrying at `Ri`,
   use `arc-toss` on the partner role only when that role is inside `Ri+1` and
   the signature is ready. Until then, a lower leaf uses `move.to Ri`, whose
   existing destination behavior holds the carrier at the launch post. It does
   not continue toward the reactor and close or overshoot the rendezvous.
4. Define a bounded empty task for the Relay that just tossed from `Ri`: enter
   while empty-handed in `Ri`, move to that same Relay's next assigned station
   `Ri+2` **via a bounded route segment for that hop**, and remain in that
   active move until `self-carrying` becomes true. `move.to Ri+2` therefore
   holds after arrival instead of continuing farther. The next advance (`Ri+2`
   to `Ri+4`) is a later ordinary node and cannot win while the earlier
   advance-and-wait node remains latched. Order these hop nodes from enemy side
   to home side so this relationship is visible.
5. Use current capacity where a receiver anchor admits only one body. Current
   station zones, carrying state, `role-in-zone-count`, and lifecycle select the
   authored leaves; the editor does not create a turn counter. If the author
   adds an abort or timeout, it must fall out to an explicit recovery/staging
   behavior, never into the next empty-handed hop.
6. Place both projections relative to emergency recovery, generic scoring, and
   ordinary collection. The card shows which behavior wins if a Relay acquires
   a Core outside the long corridor.
7. Practice and inspect one Core journey. The selected Play overlay shows both
   Relay tracks, every accepted/declined Toss, possession changes, and the
   ordinary fallback when a catch is unavailable.

The generated station logic should remain legible as ordinary Orders. For one
Relay, the essential shape is:

```text
TOSS Ri -> Ri+1
  when carrying AND self in Ri AND partner in Ri+1 AND signature ready
  use Arc Toss on partner role
HOLD CARRIER AT Ri
  when carrying AND self in Ri
  move.to Ri
ADVANCE Ri-2 -> Ri, THEN WAIT
  when empty-handed AND self in Ri-2
  until carrying
  move.to Ri via bounded-hop-Ri-2-to-Ri
ADVANCE Ri -> Ri+2, THEN WAIT
  when empty-handed AND self in Ri
  until carrying
  move.to Ri+2 via bounded-hop-Ri-to-Ri+2
```

`until` and `via` have different jobs. The earlier active advance-and-wait node
prevents the empty receiver from immediately satisfying the next station's
start condition and running a second hop. The per-hop `via` route keeps its run
on the selected corridor course; the exact destination anchor stops it. This is
existing Orders latching, route intent, and `move.to` holding, not hidden Play
state.

Do not attach every mid-corridor hop to one full non-looping `via` route. A
current ingress begins at that route's first waypoint, so a later hop could
backtrack toward the enemy side before coming home. Instead, materialize one
ordinary route segment per advance node. Each segment begins at its launch
station, follows the relevant slice of the corridor (including its turns), and
ends at the next same-Relay receiver station. These generated segments may
overlap and remain linked to the one corridor gesture in Play metadata; each is
referenced by an ordinary move, so the map plan contains no decorative unused
route.

Routes remain authored intent rather than rails: normal collision detours and
rejoin behavior still apply. The Idle Field proof should show that both bodies
select the expected hop route and remain on its corridor course except for
ordinary legal detour/rejoin. The map overlay should mark the receiver as
**waiting at the next post**, display the active segment, and show a validation
error if any adjacent launch/catch pair exceeds legal Toss range.

The expected rhythm is concrete:

| Moment | Relay A | Relay B | Core |
| --- | --- | --- | --- |
| setup | carries/holds at `R0` | waits at `R1` | A |
| after first Toss | advances only to `R2`, then waits | carries/holds at `R1` | B |
| after second Toss | carries/holds at `R2` | advances only to `R3`, then waits | A |
| after third Toss | advances only to `R4`, then waits | carries/holds at `R3` | B |

The linked edit test is to move or replace the logical corridor, swap either
Relay role, or change a receiver anchor once and see every affected bounded
route and both partner projections update. Orders still shows the exact two
local priority positions and each role's carrier/empty-handed fallthrough.

Phase 0 must confirm the intended alternating catch geometry and bounded
advance-and-wait lifecycle against current Arc Toss legality and movement
behavior before freezing this recipe. The gameplay proof target is one Core
making at least two accepted alternating partner Tosses on the long corridor
and then continuing toward a bank. During every wait, the empty Relay must stop
at the immediately next receiver post; reaching or pathing toward a later post
before catching the Core, taking a non-authored shortcut instead of the bounded
hop route, or restarting a later hop from the corridor's first station fails
the proof.

### Proof environment and Arc Toss timing dependency

Run this focused proof only against the existing `idle-field-v1` (**Idle
Field**) Practice opponent. The opponent does not need to fight, counter, or
make the route strategically sound; this proof asks whether the author can
build the chain and whether two Relays can physically execute it. It is not a
balance result.

Test today's Arc Toss first. It currently has a one-tick tell before launch,
then a distance-based public flight. If the linked Orders and route are correct
but that timing prevents reliable leapfrogging, treat a reduced/removed Toss
windup as a separate, explicit gameplay dependency—not as editor behavior:

1. Trace the failed catch/overtake chronology and rule out route, target,
   capacity, cooldown, reclaim, and priority mistakes first.
2. Compare the current one-tick tell with the smallest candidate timing change
   in the focused Idle Field scene.
3. Keep flight duration, landing legality, catcher pinning, cooldown, and Core
   reclaim rules unchanged unless the trace proves one of them is independently
   at fault.
4. Mint/version any accepted gameplay timing change through the normal rules
   path; the workshop sidecar cannot alter Arc Toss timing.

A timing change does not pass on mechanics alone. Arc Toss presentation is
bound to its authoritative tell → launch → in-flight → catch/landing chronology.
The verification must cover:

- WebGL and Canvas tell/target treatment, Relay body motion, the one real Core's
  complete flight arc, and continuous catch at carried height;
- launch and landing audio firing once at the correct authoritative events;
- no one-tick missing Core, duplicate Core, floor snap, premature carried pose,
  or stale destination ring;
- short and maximum-range Tosses at normal speed and frame-step; and
- replay seek, refresh, phone Canvas fallback, and WebGL context fallback.

In particular, the current presentation derives the first in-flight tick from
the established one-tick tell. If the tell becomes zero, that derivation and
its tests must follow the actual authoritative phase boundary rather than keep
assuming `startedTick + 1`.

If neither current timing nor an explicitly approved timing candidate makes
the chain deterministic, record the exact missing expression and seek an owner
ruling. Do not introduce a hidden turn flag or special Relay-chain executor
behavior.

## Other use cases

### Veteran escort

Select the veteran's route plus two screening anchors, staff up to two
available escorts through existing capacity, and generate ordinary follow,
fight, and recovery priorities gated by current level/role facts. Moving the
escort route or changing its screen from two bodies to one is one Play edit.
The veteran remains an ordinary role/body selected by current facts; the Play
does not own or promote it.

### Safe enemy-Core return with Relay handoff

Select the enemy-side pickup zone, safe return route, Relay exchange post, and
home continuation. The generated carrier branch enters while in the enemy zone
and carrying, latches until empty-handed, and therefore keeps its authored safe
route after leaving that zone. A Relay projection uses ordinary Arc Toss or
handoff eligibility to accelerate the chain. Changing the return route or toss
post updates both projections together.

### Mason lane closure and alternate-lane screen

Place a Hardlight anchor on one corridor and two exact guard posts facing the
open corridor. Staff Mason specifically and draw guards from any eligible
reserve through capacity. The Play makes the spatial relationship obvious,
while the generated Orders remain an ordinary deployment attempt plus movement
and fighting fallthroughs.

These examples are recipes only. They prove that the editor abstraction can
cover different existing-order combinations without creating runtime “Ambush,”
“Escort,” or “Return” verbs.

## Linked ownership and safe editing

### Persist a non-gameplay workshop sidecar

Linked Plays must survive refresh, another device, duplication, and later
editing. Local component state or local storage is insufficient. Persist an
optional, versioned workshop sidecar with the sheet, conceptually
`arc-relay-workshop-v1`.

The sidecar is **not a third gameplay document**. It is excluded from ATL2,
sheet gameplay hashes, match identity, replay identity, and the shared
compiler. Existing sheets with no sidecar continue to behave exactly as they
do now.

The exact JSON shape should follow the UX prototype, but it needs at least:

- stable Play id and human name;
- selected/owned map-object ids and their purposes;
- editor-level geometry groups and derivation links where one gesture owns
  several ordinary objects—for example, a Relay chain's logical corridor and
  the bounded map-route ids generated from its hops;
- participating roles/bodies, linked role tasks, map objects, class skills, and
  shared editable values;
- slot and capacity intent where the Play uses spatial staffing;
- required/optional status, readiness tolerance, and derived readiness-zone
  ownership where a formation recipe uses an arm gate;
- lifecycle and action intent needed to regenerate owned projections;
- every role projection's owned order ids and priority placement;
- ownership of helper conditions/capacities created solely for the Play; and
- a fingerprint of each last-generated projection for drift detection.

Orders and map plan remain stored and exported as complete standalone sources.
The sidecar cannot be required to compile or execute them.

### One source of edits, one source of gameplay

- While linked, the Play editor is the source of edits for its owned generated
  nodes.
- The materialized Orders document remains the sole source of gameplay and the
  sole compiler input.
- Saving regenerates only the Play-owned projections, validates the ordinary
  source pair through the existing compiler, and persists sources plus sidecar
  in one optimistic revision.
- The generator must be deterministic and preserve stable ids whenever the
  corresponding Play element still exists.

This deliberate duplication needs reconciliation. If imported JSON, an old
client, or an unexpected mutation changes a generated subtree, compare it to
the recorded fingerprint and stop. Offer two explicit recoveries:

1. **Regenerate from Play** — replace only the owned projections.
2. **Detach to ordinary Orders** — preserve the current materialized nodes,
   remove their Play ownership, and make them editable in Orders.

Never silently overwrite divergent Orders and never silently discard a Play.

### Delete, detach, and shared-library behavior

- **Detach Play** keeps all current Orders and geometry, removes the linkage,
  and turns the projection cards into ordinary editable nodes.
- **Delete Play** previews the exact generated nodes it will remove. It offers
  to retain or remove geometry and helper conditions/capacities that have no
  other consumer.
- Editing a condition or capacity shared outside the Play shows every affected
  consumer. Offer “duplicate for this Play” before changing a broadly shared
  definition.
- Deleting or renaming Play-owned geometry from Map plan is reference-aware and
  shows the affected slot/projection before committing.

All multi-projection edits participate in the editor's existing undo/redo and
local-draft recovery as one transaction.

## Diagnostics and transparency

Compiler diagnostics must map back to both the generated Orders source path and
the owning Play control. A bad facing anchor should highlight the anchor and
the affected slot, not merely report a hidden generated leaf.

Before save, a Play summary should show:

- all participating roles and the linked task each receives;
- exact/pooled slots, required versus optional slots, readiness ranges, and the
  all-slots-filled expression when the recipe uses them;
- every map object and capacity it consumes;
- assembly, arming, engagement, latch, release, timeout, cooldown, and urgency
  in plain language;
- insertion neighbors and fallthrough for every role;
- any higher-priority order that commonly pre-empts it;
- unfilled posts or surplus eligible bodies; and
- whether a helper definition is Play-only or shared elsewhere.

An advanced **Show generated Orders** view is always available and copyable.
It is an explanation of ordinary source, not a second editable representation.

## Implementation sequence

### Phase 0 — real persisted editor vertical slice

Build the first production-shaped slice rather than a separate interaction
mock-up:

- add optional versioned workshop metadata to sheet persistence and the sheet
  API without changing gameplay identity;
- implement a pure Play-to-Orders projection transform with stable ids;
- add the fourth **Plays** workspace to the current editor and connect it to
  that real persistence path;
- reuse the current map, role, condition, capacity, lifecycle, priority, and
  Practice controls, labelled with the exact current Orders vocabulary; and
- save, server-compile, reload, and inspect an initial linked projection end to
  end.

Use this real editor to create both the North corridor ambush and dual Relay
chain through visible controls at desktop and 390×844 touch widths. No JSON
shortcut counts as authoring proof. Resolve the generic role/Orders/projection
model first, then the Ambush map multi-selection, required slot ranges and 5/5
condition, and the Relay paired roles, logical corridor/derived hop routes,
carrier/receiver Orders, and Toss inspection.

Confirm that both materialize under current Orders v2 without changing the
compiler. If role-qualified readiness or alternating Relay movement exposes a
real current-contract gap, document that exact gap and stop for an owner ruling;
do not conceal it in editor state. A sidecar model proven only against the
Ambush recipe does not pass Phase 0. Both behavior proofs may use the idle
practice opponent. Only a traced Relay failure attributable to the current
one-tick Toss tell opens the separate timing candidate and presentation
verification above.

### Phase 1 — complete ownership and safe linked editing

- Implement fingerprints, divergence detection, detach, delete, and atomic
  undo/redo.
- Render linked projections read-only in the existing Orders tree with exact
  priority neighbors and an Edit Play deep-link.
- Prove that imported or externally changed generated Orders are never silently
  overwritten and that detach preserves the exact materialized gameplay.

Persistence, deterministic projection, and non-destructive ownership are the
foundation of the richer generation that follows; none may be deferred to a
mock-up-only phase.

### Phase 2 — additive map-first Plays workspace

- Add the fourth workspace without changing the current Map plan or Orders
  paths.
- Reuse the existing map renderer/editor and west/east preview.
- Add Play creation, semantic geometry naming, map multi-select/group move,
  required/optional slot staffing, readiness ranges, facing/deployment targets,
  and two-way selection.
- Put capacity next to slots and priority/fallthrough next to each role.
- Complete the North corridor ambush and the dual Relay chain end to end
  without raw JSON.

### Phase 3 — recipes and iterative practice

- Harden Ambush and Relay chain first, then add Veteran escort, safe
  return/handoff, and lane closure as editor recipes that produce current
  primitives.
- Preserve Play context through save → Practice → return.
- Overlay selected participants' actual tracks against authored anchors/routes.
- Add impact-aware edits for shared conditions, capacities, and geometry.

Do not start with a broad recipe library. One excellent formation Play and one
excellent moving handoff chain are more valuable than ten one-shot generators.

### Phase 4 — accessibility, resilience, and authoring proof

- Complete keyboard/touch flows, focus restoration, local draft recovery, and
  phone layout.
- Test import/export with and without workshop metadata.
- Test old sheets, detached Plays, stale clients, concurrent revision
  conflicts, and intentionally drifted projections.
- Test 4/5 versus 5/5 arming, range-boundary occupancy, an out-of-range body,
  an unrelated friendly crossing a slot, same-pool range overlap rejection,
  pre-engagement displacement, and post-engagement lifecycle latching.
- Run the dual Relay focused proof against `idle-field-v1`. If Toss timing
  changes, add engine chronology and replay-presentation tests plus real-speed,
  frame-step, seek, phone Canvas, and WebGL review of tell/launch/flight/catch.
- Extend the existing editor-only browser smoke to create, save, practice,
  return, modify, rerun, detach, and recompile both acceptance Plays through
  visible UI controls only.

## Acceptance criteria

The feature is ready when all of the following are true:

1. Starting from the current template, an unfamiliar but game-literate author
   can build the complete five-required-slot North corridor ambush in ten
   minutes without raw JSON or manually duplicating lifecycle fields across
   roles.
2. The same author can relocate it, change one participant or readiness range,
   and change its commitment in under two minutes, with no generated-leaf
   editing.
3. Orders displays every projection at its real local priority, its exact
   behavior, and its continuation. There is no global or implicit Play
   priority.
4. Capacity visibly limits pooled participation using today's deterministic,
   target-scoped semantics; the engagement demonstrably cannot enter at 4/5
   readiness and can enter at 5/5 when its hostile trigger is also true.
5. A linked Play may project into one role, multiple roles, or multiple bodies
   in one role. The dual Relay recipe edits both partner projections together
   and produces at least two accepted alternating Tosses followed by continued
   homeward progress in its focused Idle Field Practice proof. The empty Relay
   follows its derived bounded hop route, holds at the immediately next
   receiver post, and never shortcuts or paths toward a later one before
   receiving the Core.
6. No generic Play field assumes assembly, formation readiness, engagement, or
   hostile contact; those concepts belong only to recipes that need them.
7. Save/reload, another device, sheet duplication, undo/redo, and Practice
   return preserve the linkage and selected context.
8. Detaching yields an ordinary valid source pair that is fully editable by the
   existing workflow and compiles to the same gameplay.
9. Exporting only Orders plus map plan loses authoring convenience but never
   loses or changes behavior.
10. An equivalent hand-authored source pair and the Play-materialized source
   pair normalize to equivalent ATL2 gameplay content.
11. Existing sheets and the current Squad, Orders, Map plan, and raw JSON paths
   continue to work without creating a Play.
12. The complete create/change/practice flow works by touch at 390 px, with no
    hover-only control or horizontal overflow.

## UX questions the implementation prototype must answer

These are interaction choices, not invitations to change Orders:

- Is moving a whole read-only projection from Orders clearer than requiring
  all priority placement in Plays? Test both; never permit child edits.
- Does staffing read more clearly as body tokens placed on anchors, or as a
  roster strip plus slot cards? Capacity and multi-body roles must remain
  honest in either representation.
- Does a mirrored pair of carrier/receiver role cards make the Relay chain's
  shared values and local differences clearer than drawing connections between
  two projections on the map? Test it without importing Ambush terminology.
- Is required-slot readiness most legible as map rings, a `4/5 ready` roster,
  or both? The author must be able to identify the missing/out-of-range slot at
  a glance.
- On phone, should the Play panel use a persistent compact summary plus an
  expandable bottom sheet, or a step-by-step drawer? The map and selected
  anchors must stay visible during edits.
- How should a group move distinguish moving tactical anchors from moving the
  watched zone or facing target? Accidental whole-Play displacement must be
  undoable in one action.

Answer these with both the North corridor ambush and dual Relay creation/edit
tests, not with a generic component demo. A choice that works only for static
formation slots has not passed the Relay test; a choice that works only for a
paired route has not passed the Ambush test.

## Why this direction

The strategic skill should be choosing participants, spatial relationships,
triggers, capacity, class synergies, commitment, priority, and continuation. In
an Ambush that means assembly timing, required-slot geometry, readiness ranges,
and release. In a Relay chain it means a corridor, partner timing, Toss
opportunities, and safe fallback. It should not mean copying either design into
several trees and later finding every copy again.

Linked Plays remove that clerical work while preserving the valuable part of
Orders: each body still follows one readable positional-priority tree, the
enemy still creates chaos, and continuation still emerges from ordinary
fallthrough. The additive map workspace makes spatial ideas tangible without
taking the precise current editor away from authors who prefer it.
