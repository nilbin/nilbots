# Arc Relay presentation batch — production concept

## Read of the game

Arc Relay is a readable autonomous arena sport, not a conventional shooter.
The important drama is declared intent, formation change, route control, Core
custody, and the exact moment a public commitment resolves. The effects should
therefore feel like restrained field machinery in a dark orbital facility:
spacey and ambient in texture, but short, causal, matte, and legible at overview
scale.

The implementation target is the WebGL 3D renderer. It does not animate bones
or infer movable mesh parts. It animates reusable renderer primitives, material
parameters, and whole bodies only where the replay already changes their pose.

## Signature storyboards

Every row below is a start/action/resolve story. `visibleSignatures` supplies
the authoritative phase, tiles, target, capacity, suppression state, and
lifespan. `signature-changed`, `body-relocated`, `signature-damage`,
`signature-repair`, and Core facts supply discrete beats. Rendering at time `t`
must be deterministic and must not depend on having played prior frames.

| Class / signature | Production story | Replay truth | Sound |
| --- | --- | --- | --- |
| Kestrel — Vector Dash | A broken floor arrow contracts under the hull; the real body traverses its authored poses with one short wake; a low forward pressure fan clears. No ghost bodies. | Published `tell`, then ordinary body poses and terminal `completed`. | Quiet `signature-motion` at completion. |
| Palisade — Prism Wall | Exact contact posts light first; three low-specular panes rise from those contacts and settle into a stable matte field wall. Capacity loss removes energy from one edge at a time. | Active positions, `remainingCapacity`, and projectile-contact reasons. No pre-roll. | `signature-construct` once at start. |
| Towline — Tractor Hook | The frozen declared line narrows; a compact grapple head travels down it; on contact a dark cable becomes taut while the target follows its authored relocation. A miss does not invent a latch. | `pendingStrikes`, launched terminal fact, and optional `body-relocated`. | Existing wind-up, then `signature-tether` instead of the generic release. |
| Patchbay — Repair Beam | Three quiet braided bands join the units; small packets move toward the target; each real repair closes one warm-green seam ring. Interruption simply drains the bands. | Channel phase plus `signature-repair`; the effect ends with the published operation. | Existing `healing` on each repair fact; no loop. |
| Lantern — Survey Flare | One compact flare follows the published in-flight interval; landing opens a thin horizon ring and sparse calibration ticks across the exact reveal footprint. | `in-flight`, `landed`, active positions, and end tick. | `signature-survey` on `landed`. |
| Mortar — Falling Star | The two-tick floor reticle fills its four cardinal arms; a dark capsule descends only during the resolve beat; a low dust-and-pressure cross spreads across the five affected tiles. No sky beam. | Tell timing and `signature-damage` positions. | Existing `impact`, coalesced once at resolve. |
| Hush — Null Field | Boundary ticks contract during the one-tick tell; a shallow dark-violet lens then flattens across the exact footprint; suppressed constructs visibly mute and carry the existing crossed state. No dome. | Tell/active phases, active positions, suppression, and end tick. | `signature-null` on `activated`; silence while maintained. |
| Relay — Arc Toss | The destination ring appears during the one-tick tell; the one real neutral Core leaves custody and follows a clean arc; the landing becomes either the existing levitation pickup or neutral settle. No duplicate orb. | Core in-flight state plus signature and Core relocation/pickup/drop facts. | Existing Core-family cues only. |
| Switchback — Exchange | Both endpoints receive paired brackets; complementary low ribbons fold inward; each real body moves to its authored destination and keeps its identity. The ribbons vanish as positions settle. | Tell phase, exact `body-relocated` facts, and terminal completion. | `signature-exchange` once on completion. |
| Longshot — Rail Line | A thin, low-saturation lane with two charge notches persists for the exact wind-up; resolve is a broad, dim pressure slab along the frozen path; a faint floor afterimage clears quickly. | `pendingStrikes`, visible signature path, completion/cancel reason, and damage facts. | Existing wind-up, projectile release, and one coalesced impact. |
| Mason — Hardlight Block | A floor frame appears on activation; four chunky matte slabs rise and lock into one honest one-tile block; contact erodes its edge energy before capacity expires. No glass gloss. | Active position, capacity, contact/destruction reason, and end tick. No pre-roll. | `signature-construct` once at start. |
| Sunder — Target Paint | Four blunt brackets close around the exact target; three coral/brass tally bars persist; each `segment-consumed` removes one bar, and the last one collapses the brackets. | Target actor, remaining capacity, segment-consumed/consumed, and end tick. | `signature-target` once at start. |
| Repulsor — Kinetic Burst | The exact one-tick ring contracts toward the caster; resolve sends one low chunky torus outward; short wakes appear only behind bodies that actually relocate. | Tell, terminal completion, and body relocation facts. | `signature-kinetic` once at completion. |
| Veil — Smoke Canister | A muted canister cap appears at the target as the field becomes active; a few low matte volumes roll across only the published footprint; opacity breathes slowly without making a dome. | Immediate active state, exact positions, suppression/reveal presentation, and end tick. No invented flight. | `signature-smoke` once at start. |

Trip Node and Sentinel Seed are listed in the full roster but intentionally have
no treatment here. Their replacement models and material review belong to the
other 3D model agent.

## Board index and corrections

| Board | Rows, top to bottom | Production correction |
| --- | --- | --- |
| `01-high-impact-signatures.png` | Tractor Hook, Falling Star, Rail Line, Hardlight Block | Remove Falling Star's generated light column; dim Rail; finish Hardlight matte. |
| `02-support-fields.png` | Repair Beam, Survey Flare, Null Field, Smoke Canister | Flatten Null; lower and simplify Smoke; omit the board's smoke travel arc. |
| `03-motion-objective.png` | Vector Dash, Prism Wall, Arc Toss | One real moving body/Core only; no ghosts or duplicate objective. |
| `04-control-level-up.png` | Exchange, Target Paint, Kinetic Burst, Level Up | Endpoint motion is fact-driven; no full-height level-up column. |
| `05-class-projectile-heads.png` | Sixteen classes in roster order | Shapes and relief carry identity; runtime recolours them with team accent. |

## Class-specific regular-attack heads

These are regular-attack presentation identities, not new projectile entities.
Each becomes a genuine east-facing SVG mask with a 0.42–0.58 tile scale. WebGL
uses the existing layered SVG extrusion path, so no Meshy round and no GLB are
required. The renderer owns travel, rotation, wake, hit flash, and team tint.

| Class | Look ID | Read at game scale |
| --- | --- | --- |
| Kestrel | `kestrel-dart` | split, tapered flight dart |
| Palisade | `palisade-wedge` | broad armored wedge |
| Towline | `towline-shackle` | compact open shackle |
| Patchbay | `patchbay-seal` | rounded service capsule |
| Lantern | `lantern-lens` | faceted annular lens |
| Mortar | `mortar-slug` | squat banded shell |
| Minesmith | `minesmith-clamp` | three-jaw rivet |
| Hush | `hush-rotor` | four-lobe dampener puck |
| Relay | `relay-cradle` | open horseshoe cradle |
| Switchback | `switchback-shuttle` | paired rail shuttle |
| Longshot | `longshot-needle` | long narrow flechette |
| Mason | `mason-block` | compact squared rivet |
| Sunder | `sunder-prism` | sharp hollow shard |
| Repulsor | `repulsor-cog` | radial pressure rotor |
| Veil | `veil-crescent` | asymmetric curved blade |
| Nest | `nest-cluster` | four-cell seed cluster |

All sixteen retain the accepted shared `projectile` SFX. That keeps a busy
four-shot tick coherent and preserves the rule that a miss/fizzle sounds like
the same thing leaving the weapon; the separate impact or deflect communicates
the outcome.

## Level-up beat

The existing brass chevron is the persistent truth. At an authoritative
`leveled-up` fact:

1. A thin brass floor ring expands for 0.18 visual ticks.
2. Three short matte facets rise around the body over 0.36 ticks.
3. The facets converge toward the UI row and the newly earned chevron appears.
4. All temporary geometry is gone by 0.9 ticks; the chevron remains.

There is no white flash, full-height column, camera shake, or mesh deformation.
The accepted `level-up` SFX remains unchanged.

## Implementation shape after approval

1. Split the current generic signature rig into shared pooled primitives and
   per-signature painters. Add a replay-fact index for discrete resolve beats.
2. Implement the fourteen treatments above and compact Canvas truth markers.
3. Add sixteen class-projectile look folders and map every Arc class away from
   the shared `arc-pulse` fallback.
4. Add the fact-driven level-up transient beside the existing chevron system.
5. Generate and integrate the approved six-source signature audio set, then
   apply sparse tick mixing and the strict comfort gate.
6. Exercise real Tapline replays at normal overview, phone landscape, pause,
   seek, and reduced-motion settings; update tests and capture proof.

The implementation will not modify engine projects, game rules, replay wire
types, map geometry, music, or the model agent's Trip Node/Sentinel Seed files.

## Verification gates

- Every one of the 14 owned signature IDs has an explicit renderer treatment
  and an authoritative trigger test.
- All 16 Arc classes resolve to unique projectile look IDs and valid SVG masks.
- Seek directly into tell, active, in-flight, resolved, suppressed, and expired
  moments produces the same frame as continuous playback.
- Cancelled line signatures disappear without a release or resolve effect.
- Smoke is active on its authored start frame; no cosmetic travel delays it.
- Projectiles, effects, and level-up transients add no hitbox or targeting
  claim and never duplicate an actor or Core.
- Pooled geometry/materials perform without per-frame asset creation in a
  sixteen-body Tapline fight.
- Reduced motion shortens wakes/arcs and removes decorative drift while keeping
  telegraphs, targets, capacity, and outcomes readable.
- The complete audio pack passes the comfort and crowded-mix checks in
  `AUDIO-CUE-PROPOSAL.md`.
