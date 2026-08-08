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

## Final forward-combat operation proof

The outcome-visible gallery was rebuilt again after integration, this time from
the ten final `arc-relay-forward-combat-01` live proofs under
`arena-bots/arc-relay/forward-combat-operation-proof-v1-2026-08-03/`.
The compact broadcasts total `1,276,527 B`; the largest is Relay Catch at
`160,235 B`. The awareness layer adds no replay or broadcast bytes.

Every card says what is being watched: operation, side, opponent, winner,
terminal reason, and the verified prepare/commit/success/release window. The
index is deliberately outcome-visible.

The production-browser smoke:

- opened all ten final forward-combat pages in WebGL;
- advanced every replay and found the score bug;
- sought to each operation's verified success tick;
- opened and selected a real current execution trace in all ten;
- rejected any card that exposed its later release tick;
- verified the old Core pickup text banner is absent;
- verified the Core pickup timeline anchor and captured its in-world effect;
- verified every model visible in this gallery comes from the promoted Meshy
  runtime assets rather than the older procedural fleet;
- produced no page, console, or failed-request errors.

A second production smoke deliberately denied WebGL context creation. The
viewer fell back to Canvas2D, advanced the same current replay, exposed the
same score and tactics trace, and produced no unexpected browser error.

Evidence:

- `art/reviews/arc-relay-play-awareness/smoke.json`
- `art/reviews/arc-relay-play-awareness/smoke-canvas2d.json`
- `art/reviews/arc-relay-play-awareness/first-operation-webgl.png`
- `art/reviews/arc-relay-play-awareness/first-operation-canvas2d.png`
- `art/reviews/arc-relay-play-awareness/core-pickup-webgl.png`
- `art/reviews/arc-relay-play-awareness/three-theater-overview-webgl.png`
- `art/reviews/arc-relay-play-awareness/index.png`

The earlier procedural-model comparison screenshot is invalid evidence and is
excluded. The retained replacement pair uses the same completed replay, tick
26, camera, map, and approved Meshy fleet on both sides:

- before awareness: `core-pickup-before-awareness-webgl.png`, SHA-256
  `5d9846c8428eea6fc90af208eb8629f1d53b6e956d38e99e5a1bcbbd8107a771`;
- after awareness: `core-pickup-after-awareness-same-replay-webgl.png`,
  SHA-256
  `23c7f43e162cc9213818c9530b83fa562128758e16a9a231939c96ba8124c7b1`.

That A/B changes only presentation: it removes the obsolete pickup banner,
keeps the existing diegetic Core cue, and adds the closed Tactics entry point.
The current forward-combat screenshot is kept separately as
`core-pickup-webgl.png`.

Mortar does not appear in this gallery. Its separate facing review was still in
progress when this awareness evidence was captured, so this report makes no
claim about that model's final orientation.

Local gallery build:

`sandbox/arc-relay-forward-awareness-review-v1/`

## Validation

- `cd web && npm test` — `390/390` web tests pass, including bounded parsing,
  phase/release grouping, current-playmate selection, Core cue coverage, asset
  coverage, contact semantics, the forward-facing replay contract, and exact
  runtime-to-audited Meshy asset hashes at the captured revision.
- `cd web && npm run build` — production and all four scoped CLI viewers build.
- `git diff --check` — clean.
- Ten-match production WebGL smoke — all ten pass with zero browser errors.
- Forced-WebGL-failure Canvas2D smoke — current Escort Counterpunch trace and
  field layer pass.

The production entry bundle is `1,274,257 B` uncompressed and the WebGL chunk
is `793,373 B` uncompressed in this build. The only new binary runtime payload
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

## Integration result

The awareness pass is integrated after the versioned forward-combat contract.
Directional facing and fire visuals coexist with the awareness props, causal
contact camera gate, role-caption suppression, and fog-filtered bracket/sigil
layers. The final browser evidence above comes from that combined production
build, not from the former isolated awareness branch.
