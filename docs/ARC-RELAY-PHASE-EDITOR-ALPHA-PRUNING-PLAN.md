# Arc Relay Phase Editor Alpha Pruning Plan

Status: owner-approved implementation handoff, 2026-08-20.

## Start condition

Do not begin this work until the currently running Scan / verified-clear /
zone-bounded-not-fighting / Hold-facing implementation has finished, committed,
and left the shared checkout clean. Re-read the resulting implementation before
changing it. This plan consumes that work; it must not independently recreate or
overwrite it.

Follow `AGENTS.md`, `CLAUDE.md`, `web/CLAUDE.md`, and
`.claude/skills/sheet-tuning/SKILL.md`. Work on `main` unless there is a real
need for isolation. Do not touch immutable historical playlists or tracked WASM
artifacts for this editor iteration.

## Goal

Make a useful phase straightforward to author from blank without exposing the
full Tactics DSL. Keep the strategic depth in the compiler and executor, but
reduce the normal phase editor to a small, truthful vocabulary.

The player should mostly answer four questions:

1. Where should this group move or stand?
2. How should it behave there and on the way?
3. How should it recover?
4. What observable event completes this phase?

This is a product-surface pruning pass plus the small semantic fixes required
for the remaining controls to tell the truth. It is not a new executor, a
second priority system, a balance pass, or a replacement for the existing
Tactics coordinator and ordinary generated Instructions.

## Locked product decisions

### 1. Visible phase job vocabulary

Offer only these four choices when authoring a new phase job:

- **Take up posts** — assigned positions, one post per bot.
- **Converge on a spot** — one shared destination.
- **Occupy an area** — flexible positions inside a drawn area. Rename the
  current `Hold an area`; geometry must not also imply posture.
- **Patrol a loop** — repeatedly follow a drawn loop.

Do not offer Line, Approach, Advance, Return, Escort, or Carry on as new job
choices.

Carry-forward is still necessary, but it is implicit. When a new phase does not
replace a group's ground, it inherits the prior effective job and geometry.
The UI may explain that it is continuing the previous assignment, but the
player must not select a fifth `Carry on` mode. It is acceptable to retain
`none` as an internal encoding if that remains the cleanest materialization.

Keep legacy job parsing/materialization while it is cheap and harmless. A
loaded draft that actively contains a hidden job must not silently lose it:
show its current value as legacy content or normalize it deliberately with a
clear explanation. Do not let built-in recipes create an apparently blank or
uneditable job. Update recipes to the visible vocabulary where practical,
while preserving the intent of Demolition March.

#### Destructive mode switching

Changing job type when compatible geometry already exists must show a
confirmation before clearing anything. The message must name what will be
removed, such as eight posts and their facings, an area, a route, or dependent
spatial skill marks.

- Default action: Cancel.
- Confirm: clear only geometry that is incompatible with the new mode.
- Offer Undo through the editor's normal document history/toast mechanism.
- Do not warn when there is nothing material to lose.
- Merely changing posture, travel behavior, recovery, or conditions must never
  clear geometry.

### 2. Visible destination posture

Offer only:

- **Observe** — stay on the authored task and do not engage.
- **Hold** — fight for the authored ground, turn toward threats from any
  direction, and do not meaningfully chase.
- **Aggressive** — pursue and finish nearby enemies within the authored leash.

Fold Wait into Observe for normal authoring. Do not promise that Observe
"breaks contact" unless evasive movement truly exists; the minimum honest
behavior is no voluntary Fight instruction, continued movement toward/along
the job, and retention or resumption of its ground.

Steal and Escort are Core-custody policies, not general destination postures.
Hide them from this control. Existing internal values may remain where needed
for old documents or generated custody behavior.

### 3. Visible on-the-way behavior

Offer only:

- **Just march** — do not fight en route; the destination posture takes over
  on arrival.
- **Respond to contact** — engage a nearby enemy with the short Hold leash,
  then resume the same route. This replaces the misleading label `Answer
  attacks`: the current mechanism reacts to nearby contact, not specifically
  to having received an attack.
- **Brawl through** — use the Aggressive fight policy while travelling, then
  resume the route and hand over to the destination posture on arrival.

Hide Hang back. Its current rear-side destination bias is not enemy avoidance
or safe-route planning, so do not rename it `Avoid enemies`. It can return in
the future only with a separately approved, truthful mechanic.

Travel behavior is not a second order or priority layer. Continue compiling it
as phase-scoped ordinary Instructions gated by whether the bot has arrived.

### 4. Visible recovery behavior

Offer only:

- **Fight on**.
- **Break for the heal tile**.
- **Heal up fully**.

Hide Patch up close. Explain the actual health trigger and completion threshold
in the option copy/readback; do not make a player infer them from the label.
Recovery must still pre-empt the ordinary phase floor according to the existing
Instruction priority and then return the bot to the same active phase.

## Formation readiness

Rename `Everyone in position` to **Formation is ready**. Dead bots remain
excused, but readiness needs one simple forgiveness control:

- **Everyone** — zero late bots.
- **All but one** — one late bot allowed.
- **All but two** — two late bots allowed.

Default to Everyone for every job, preserving today's strict body count until
the player deliberately allows late bots. Encode the setting explicitly, for
example by allowing the current `groupsSet: true` to mean zero slack and adding
an object form with `allowedLate: 0|1|2`. The exact wire shape is an
implementation choice, but it must validate in both the TypeScript and C#
models and survive a save/load/compile round trip.

Slack is team-wide body slack, not extra spatial radius. Do not change any
job's current spatial tolerance and do not add a distance-tolerance control in
this pass. In particular, an assigned Post currently counts as reached on its
own tile or an adjacent tile (Chebyshev distance at most one); preserve that
exact rule. Evaluate the allowed-late count only after applying the unchanged
spatial checks.

### Stable rejoin semantics

Freeze the readiness cohort when the phase begins:

- A living bot at phase entry is required until it reaches its ground, dies,
  or the phase advances.
- A bot dead at phase entry is excused for that phase.
- Once a required life dies, its later respawn must not newly block readiness.
- A respawned bot still receives and performs the active phase job.
- A respawn may help occupy ground, but it must not increase the number of
  bodies the phase is waiting for.

Do not expose a rejoiner checkbox. This fixed rule prevents readiness from
flapping because a respawn wave occurred while the team was assembling.

### Exact Posts correctness

Posts retain stronger meaning than a bounding box:

- Each squad slot is checked against its assigned post.
- Use the existing per-post Chebyshev-distance-one tolerance unchanged.
- One bot cannot satisfy two required posts, even when readiness ranges
  overlap.
- Slack means an authored number of assigned posts may remain unfilled; it
  does not turn the formation into an undifferentiated area.
- Apply this to large groups too. Remove the current shortcut where groups
  larger than three are accepted merely because every body is somewhere in
  the combined posts footprint. This applies the existing assigned-post check
  consistently; it must not tighten or expand that check's spatial radius.

If the ordinary condition-zone budget cannot express this honestly, perform
the squad-level readiness calculation in the existing Tactics coordinator.
Do not introduce another scheduler or reservation system.

## Visible phase-completion conditions

The normal phase picker should contain only the following rows.

### Our team

- **Formation is ready**, including the slack control above.
- **Everyone is healed up**.

Hide losses and losses-within from the normal phase surface.

### Enemies

- **Any enemy contact**.
- **At least N enemies were recently seen here**. Rename `Enemies gather
  somewhere`. It uses bounded team knowledge, not omniscience. Stale knowledge
  must not satisfy it. Use one canonical fixed freshness window and state that
  window in helper/readback text rather than adding another ordinary control.
- **At least N enemies are currently down**.
- **This area has been verified clear**, using the accepted rolling-observation
  and Scan semantics from the preceding implementation.
- **No friendly is currently fighting in this area**, using the accepted
  zone-bounded fight-state semantics from the preceding implementation.

Hide enemies destroyed this phase and enemies destroyed in a short window.

#### Multi-zone conditions

Every condition row that authors a zone must support between one and three
zones. In the current phase surface this applies to recent-enemy presence,
verified clear, and no-friendly-fighting; use the same reusable editor/model
for any other zone condition that remains elsewhere in Tactics.

Multiple zones form one possibly disconnected watched area. They do not create
another AND/OR editor:

- **Recent enemies:** count distinct recently known enemy lives across the
  union. An enemy inside overlapping zones counts once.
- **Verified clear:** every walkable tile in the union must satisfy the
  accepted rolling observation/no-unresolved-memory rule. Therefore every
  authored zone must be proved clear.
- **No friendly fighting:** no qualifying friendly fight may intersect any
  tile in the union.

This gives the player a simple way to describe several entrances, both sides
of a corridor, or disconnected pieces of tactically equivalent ground without
duplicating an entire condition case. Per-zone enemy thresholds and arbitrary
nested zone logic are out of scope.

The UI should show all zones in the condition's colour, distinguish them as
Area 1–3, allow each to be selected/redrawn/deleted independently, offer `Add
another area` until the cap is reached, and use an honest summary such as
`3+ enemies recently seen across these areas` or `all three areas verified
clear`.

Use a one-to-three `zones` collection in newly authored documents. Continue to
accept the existing singular `zone` shape as a one-element legacy form, and
normalize it on an intentional edit/save rather than silently dropping it.
Validate the cap and non-empty collection in both TypeScript and C#. Shared or
overlapping tiles and actors must be canonicalized before evaluation so overlap
never multiplies counts, scan work, or fight state.

### Cores

- **A Core is up for grabs**.
- **A Core spawns soon**.
- **A Core was taken this phase**.

Do not offer Core banked or Core lost as normal phase-completion conditions.
Core custody is a temporary, higher-priority branch inside whichever phase is
active:

1. A bot acquires a Core.
2. The active phase/tactic custody preset takes over: keep carrying, bank
   alone, or bank under escort.
3. The Core is banked or lost.
4. Custody ends and surviving bots resume the still-active phase.

`A Core was taken this phase` remains because an author may genuinely want the
wider tactic to change after acquisition. Ordinary pickup, return, banking,
loss, and resumption must not require a dedicated phase. Keep bank/loss facts
internally if the custody executor needs them; merely remove them from normal
phase authoring.

### Time and ground

Hide all current rows:

- We have been at this phase a while.
- The destination is reached.
- The ground has been watched for long enough.

`Destination reached` is not identical to Formation ready, but its current
majority/first-mover meaning is weaker and harder to understand. Formation
ready owns the common intent.

`Ground watched long enough` is not the inverse of clear. It currently means
continuous observation even when enemies are present. Verified-clear plus
Scan owns the common player intent. Keep the underlying fact only if it is
still consumed elsewhere.

### Hidden conditions and loaded drafts

Hide is not the same as silently deleting authored content. Continue parsing
hidden condition rows unless removal materially simplifies the system. If a
loaded draft actively uses one, show a compact `Existing advanced condition`
readback with a way to remove it. Do not make hidden state invisible while it
still changes execution.

Do not redesign the existing Required / Triggered / Unless and alternative-case
semantics in this pass.

## Strictly linear phase sequencing

Remove the next-phase picker and explicit authored phase jumps entirely.

- Completing a non-final phase always activates the following phase in array
  order.
- Completing the final phase finishes the tactic.
- A phase with no completion cases remains active indefinitely.
- Alternative completion cases all have the same destination.
- A phase whose completion condition is already true may naturally advance on
  its normal first evaluation tick; do not add explicit skip pointers.
- Tactic-wide abort/restart behavior is unchanged.

The persisted player-facing Tactics document should no longer require a `to`
field on each transition. The compiler/coordinator may still generate or use
an internal next-phase index/id as execution machinery, but it is not authored
policy.

There are no meaningful saved explicit jumps that require a compatibility UI.
Remove the picker, named-forward-target validation, summary wording, recipes,
fixtures, and tests that exist solely to support arbitrary jumps. Do not build
a migration subsystem for unused jumps.

## Tactic completion and rearm

Remove the generated hidden 40-tick tactic rearm delay. It was inherited from
the old root-Order cooldown; the current squad coordinator requires only one
dark evaluation pass to clear per-body Instruction state between activations.
There is no separate documented gameplay reason for the remaining 40 ticks.

Use these semantics when a tactic ends on tick `T`:

- Its tags are dark for the completion evaluation, clearing the ended run's
  Instruction episodes.
- A different eligible tactic may still be selected immediately on tick `T`,
  following ordinary top-to-bottom tactic order.
- If no other tactic becomes active, the same tactic may be selected again on
  tick `T + 1` when its start conditions are still true.
- A newly selected tactic remains non-pre-emptible under the existing
  coordinator rules; the completed higher tactic does not reclaim the team
  merely because it becomes eligible again.

Generate `rearm: 0`, or internalize the mandatory one-tick boundary in the
coordinator if the generated field can be removed cleanly. Keeping the Orders
program field for contract simplicity is acceptable; emitting 40 is not. Do
not add a cooldown control to the simplified editor. If meaningful tactical
cooldowns are wanted later, they require a separate owner decision and must be
player-visible rather than hidden policy.

## UI and authoring requirements

- Keep the existing Nilbots visual language and map-first editor; this is not
  a mobile-first redesign.
- From a blank sheet, the four job modes and their drawing affordances must be
  immediately understandable without reading DSL documentation.
- The phase readback must use the same player-facing labels as the controls.
- Hidden vocabulary must not leak through summaries, tooltips, validation
  errors, or blank recipe defaults during normal authoring.
- Map geometry remains editable in place after the phase has been created.
- No raw JSON editing may be required for any acceptance flow below.

## Required tests

### Document, compiler, and coordinator

- Save/load/validate/compile each visible job, posture, travel behavior,
  recovery behavior, and condition.
- Prove strict sequential phase activation and final-phase completion without
  authored targets.
- Prove a phase with no completion condition holds indefinitely.
- Prove multiple alternative cases all advance to the same next phase.
- Prove tactic-wide abort behavior remains higher-level and unchanged.
- Prove a tactic ending on tick T cannot reselect itself on T, may reselect on
  T+1 when no other tactic owns the team, and begins with fresh Instruction
  state.
- Prove a different eligible tactic may begin on the completion tick and is
  not subsequently pre-empted by the completed tactic.
- Prove Core custody bank and loss both return control to the same still-active
  phase unless that phase independently completed.
- Prove `A Core was taken this phase` can still intentionally advance a phase.

### Formation readiness

- Zero, one, and two allowed-late settings.
- Dead at phase entry is excused.
- Death while assembling excuses that life.
- Respawn during the same phase performs the job but does not re-block it.
- Eight assigned Posts are checked individually.
- Overlapping post tolerances cannot let one body satisfy multiple posts.
- Area, spot, and patrol readiness use their honest job footprints.

### Posture, travel, and recovery behavior

Use focused deterministic scene tests, not only materialized JSON assertions:

- Observe reaches/retains its job and never emits a voluntary Fight action.
- Hold turns and fires against frontal, side, and rear contact, respects its
  short leash, and returns to its ground.
- Aggressive pursues within its leash and resumes its ground afterward.
- Just march ignores contact en route and hands over on arrival.
- Respond to contact fights with the short leash, resumes the same route, and
  does not keep the travel fight after arrival.
- Brawl through uses the aggressive leash, resumes the route, and hands over
  on arrival.
- Every recovery option pre-empts at its documented health threshold and then
  resumes the same phase/job.
- Every emitted executor action carries a non-empty `debugMessage` explaining
  the selected phase intent.

### Conditions and vision

- Recent-enemy presence succeeds from fresh bounded team knowledge and fails
  once that knowledge is stale.
- Every zone-bearing condition round-trips one, two, and three zones; zero and
  four are rejected in both validators.
- Recent-enemy presence counts distinct enemy lives across the zone union and
  never double-counts overlap.
- Verified clear cannot succeed from lack of vision; it requires the accepted
  rolling scan evidence and no unresolved enemy memory across every zone in
  the union.
- No-friendly-fighting is affected only by fights intersecting its authored
  zone union.
- Hidden active conditions in a loaded draft remain visible as legacy/advanced
  content until removed.

### Frontend

- Normal controls expose exactly the approved option sets.
- A destructive job-mode switch can be cancelled with no document mutation.
- Confirming the switch removes only incompatible geometry and can be undone.
- A non-destructive switch produces no warning.
- New phases carry prior ground implicitly.
- No next-phase target picker appears.
- Normal newly authored transitions serialize without arbitrary targets.
- Zone-bearing rows can add, select, redraw, and remove up to three independently
  visible areas, and their summaries state the union semantics.
- Built-in recipes open without invisible active jobs or conditions.

## Required acceptance flow

Starting from a blank sheet in the visible editor, author a three-phase tactic
without raw JSON:

1. Take up assigned Posts under Observe. Continue when Formation is ready with
   one allowed late bot and at least N enemies recently seen in a drawn area.
2. Occupy a drawn area under Hold, Respond to contact on the way, and Break for
   the heal tile. Continue when the drawn combat area is verified clear and no
   friendly is fighting there.
3. Patrol a drawn loop under Aggressive/Brawl through and remain there because
   the final phase has no completion condition.

Give the tactic an ordinary Core-custody preset. In deterministic proof, a Core
pickup must enter custody, bank or lose the Core, and return survivors to the
same phase without a Core-specific phase or a bank/loss transition row.

The saved/read-back tactic must be understandable from the map and phase
storyboard alone. Switching one populated job mode must visibly exercise the
warning, Cancel, Confirm, and Undo flow.

## Replay-visible acceptance

Because readiness, Hold, travel fighting, and custody affect runtime behavior,
finish with trace-first evidence under
`.claude/skills/sheet-tuning/SKILL.md`:

1. Trace the focused deterministic scenes for the semantics above.
2. Run focused compiler/coordinator/movement tests.
3. Author the combined acceptance sheet through the visible editor.
4. Run the standard six-cell Arc Relay exhibit with that exact sheet on the
   current Tapline profile. Do not run a balance battery and do not describe
   the six outcomes as balance evidence.
5. Inspect the manifest and all horizon outcomes, and publish the phone-readable
   exhibit through a Cloudflare tunnel.

This is an implementation/usability proof: the controls produce the behavior
they promise. It is not evidence that the tactic or reduced vocabulary is
balanced.

## Documentation and delivery

Update the relevant current Tactics specification and authoring documentation,
plus `docs/DECISIONS.md` if the implementation needs a durable numbered
decision. Remove stale UI terminology from current docs, but do not rewrite
historical experimental reports.

Deliver:

- a concise implementation report;
- the exact focused test commands and results;
- the authored acceptance sheet identity;
- deterministic replay/trace evidence and known limitations;
- the public six-cell exhibit URL;
- the final commit SHA.

Completion requires a clean working tree with all changes committed. No
playlist/rating-era, tracked WASM, CLI-version, or deployment work belongs in
this pass unless the owner separately authorizes a release.
