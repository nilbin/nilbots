# Arc Relay play-awareness implementation — 2026-08-03

## Outcome

The web spectator now exposes coordinated execution without inventing a second
game state. Published operation roles become restrained in-world brackets and a
shared play sigil; a closed-by-default tactics lens explains the causal trace;
the timeline carries Core and play bookmarks; selecting a claimed body
highlights its current playmates; and the director may frame a coordinated play
only after an authoritative combat/Core contact makes it relevant.

This is presentation-only. No rule, map, class, doctrine, balance, replay wire,
canonical serializer, or fog calculation changed.

## Truth boundary

The implementation reads the bounded role-tag grammar already published by an
entrant:

`g-<operation-code>-<phase>-<task>`

- `p`, `c`, and `r` become preparing, committed, and recovery.
- Participants are grouped only while their current published role tags claim
  the same team and operation code.
- A release exists only when that group disappears from a later published tick.
  Match end is not presented as a release.
- Contact is derived only from current causal damage, destruction, relocation,
  Core pickup/drop/handoff, and bank events involving a claimed actor.
- Expanded cards are truncated at the playhead. Future participants, tasks,
  contact, recovery, and release from a completed replay remain hidden.
- Rear Hook (`rh`) and Lantern Sweep (`ls`) are the only operation codes with
  semantic names in the retained stock trace. The older generic `op` code is
  labelled **Unlabelled coordination**. Entrant names or movement patterns are
  never used to guess a play.
- Arc Relay no longer floats raw operation or baseline protocol tags above
  bodies. Its compact body summary uses the translated play role when one is
  active, then the current action/status; non-Arc modes keep their existing role
  presentation.

In team-vision mode, the lens shows the selected team's trace and only currently
observed opposing participants. Opponent rows cannot be expanded, and opponent
operation bookmarks are removed from the timeline. The arena brackets and
sigils use the same authoritative visibility test as the bodies. This consumes
fog truth; it does not change fog truth.

## Shipped presentation

### On the field

- preparing: a quiet broken bracket and restrained pulse;
- committed: a stronger locked bracket;
- recovery: an outward-drifting fade;
- one shared diamond sigil at the visible participant centroid;
- a neutral contact ring only at an authoritative play contact;
- selected bodies and their current playmates share the highlight;
- both Canvas2D and WebGL implement the same language.

### Viewer surfaces

- `◇ Tactics` is closed by default and shows only active published
  coordination at the current tick.
- Its expanded card lists the observed preparation, commit, first contact,
  recovery, baseline release, and currently known tasks with seek links.
- `Show team vision` returns directly to a claimed body's team perspective.
- Body cards translate operation roles into `play · task · phase` rather than
  exposing protocol tokens.
- The transport has causal Core bookmarks and play phase/contact bookmarks.
  Selecting a play narrows the play bookmarks; team vision narrows them to the
  selected team.
- The auto-director does not chase quiet preparation. A play becomes a camera
  candidate only when it intersects an authoritative hostile/Core contact.

### Core storytelling

The obsolete Core text banner was removed. Birth, pickup, steal, drop, bank,
and Pulse are carried by the field and audio. Pickup and drop gained distinct
deterministic Obsidian Foundry cues (`8,681 B` and `9,559 B`; `18,240 B`
combined), with corresponding Canvas2D/WebGL effects and timeline anchors.

## Retained-operation proof

The outcome-visible gallery was rebuilt from all ten retained operation matches
under `arena-bots/arc-relay/operation-counterplay-v1-2026-08-03/retained/`.
The broadcasts remain `1,364,354 B` total, `164,315 B` maximum: the viewer
change adds no replay bytes.

The production-browser smoke:

- opened all ten pages in WebGL;
- advanced every replay and found the score bug;
- sought to each retained operation's success tick;
- opened and selected a real current execution trace in all ten;
- rejected any card that exposed the later retained release tick;
- verified the old Core pickup text banner is absent;
- verified the Core pickup timeline anchor and captured its in-world effect;
- produced no page, console, or failed-request errors.

Evidence:

- `art/reviews/arc-relay-play-awareness/smoke.json`
- `art/reviews/arc-relay-play-awareness/first-operation-webgl.png`
- `art/reviews/arc-relay-play-awareness/core-pickup-webgl.png`
- `art/reviews/arc-relay-play-awareness/three-theater-overview-webgl.png`
- `art/reviews/arc-relay-play-awareness/rear-hook-canvas2d.png`
- `art/reviews/arc-relay-play-awareness/index.png`

Local gallery build:

`sandbox/arc-relay-play-awareness-review-v4/`

## Validation

- `cd web && npm test` — `389/389` web tests pass, including bounded parsing,
  phase/release grouping, current-playmate selection, Core cue coverage, asset
  coverage, and contact semantics.
- `cd web && npm run build` — production and all four scoped CLI viewers build.
- `git diff --check` — clean.
- Ten-match production WebGL smoke — all ten pass with zero browser errors.
- Forced-WebGL-failure Canvas2D smoke — Rear Hook trace and field layer pass.

The production entry bundle is `1,272,207 B` uncompressed and the WebGL chunk
is `793,318 B` uncompressed in this build. The only new binary runtime payload
is the `18,240 B` pair of compressed event cues.

## Honest remaining seam

The retained trace does not publish a trigger/branch-acceptance vocabulary, and
eight of the ten stock operations still use the generic `op` code. Therefore
this pass can truthfully show preparation, participation, tasks, commit,
contact, recovery, and release, but it cannot name those eight operations or
explain a chosen branch without inference. The right follow-up is a bounded,
presentation-safe operation/branch trace emitted by the entrant. It is not a
reason to infer tactics from movement, entrant names, or outcomes in the web
renderer.

## Integration notes

The branch is deliberately isolated. The likely conflict surfaces with the
directional-combat track are `Viewer.tsx`, `drawArena.ts`, `arenaCamera.ts`,
`ArenaCanvas3D.tsx`, `arenaActors.ts`, and `arenaOverlays.ts`. Merge the two
features semantically: retain directional facing/fire visuals while preserving
the awareness props, causal contact camera gate, role-caption suppression, and
fog-filtered bracket/sigil layers. No engine or replay artifact should be
resolved in favor of this branch because this branch changes none of them.
