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

At the normal Tapline camera, every owned row is expected to read as the same
effect shown here: the same dominant silhouette, spatial hierarchy, three-beat
story, matte material family, and controlled emission. Offline-rendered
volumetrics and micro-surface detail are not the target. Smoke is the largest
approximation; it becomes a small set of stylized low volumes rather than a
ray-traced cloud. The complete effect-by-effect fidelity and capture gate lives
in `PRESENTATION-FRAMEWORK.md`.

The timing audit in `SIGNATURE-TIMING-AUDIT.md` was completed before this
presentation package. Four signatures received fingerprinted Tapline-2 timing;
historical grammar-2 replays still render their original semantics. This
remediation changes only WebGL presentation, capture tooling, and documentation,
not those mechanics.

## Signature storyboards

Every row below is a start/action/resolve story. `visibleSignatures` supplies
the authoritative phase, tiles, target, capacity, suppression state, and
lifespan. `signature-changed`, `body-relocated`, `signature-damage`,
`signature-repair`, and Core facts supply discrete beats. Rendering at time `t`
must be deterministic and must not depend on having played prior frames.

| Class / signature | Production story | Replay truth | Sound |
| --- | --- | --- | --- |
| Kestrel — Vector Dash | A compact destination socket contracts; the real body turns into its authored relocation and traverses it with one short wake. No forward guide line or ghost bodies. | Published `tell`, then ordinary body poses and terminal `completed`. | Quiet `signature-motion` at completion. |
| Palisade — Prism Wall | Exact contact posts light first; three low-specular panes rise from those contacts and settle into a stable matte field wall. Capacity loss removes energy from one edge at a time. | Active positions, `remainingCapacity`, and projectile-contact reasons. No pre-roll. | `signature-construct` once at start. |
| Towline — Tractor Hook | One narrow team-tinted cable extends toward the locked target; on contact that strand becomes taut while the target follows its authored relocation. The bulky procedural grapple head and generic red strike ray are suppressed for now, and a miss does not invent a latch. | `pendingStrikes`, launched terminal fact, and optional `body-relocated`. | Existing wind-up, then `signature-tether` instead of the generic release. |
| Patchbay — Repair Beam | Three quiet braided bands join the units; small packets move toward the target; each real repair closes one warm-green seam ring. Interruption simply drains the bands. | Channel phase plus `signature-repair`; the effect ends with the published operation. | Existing `healing` on each repair fact; no loop. |
| Lantern — Survey Flare | One compact flare follows the published in-flight interval; landing opens a thin horizon ring and sparse calibration ticks across the exact reveal footprint. | `in-flight`, `landed`, active positions, and end tick. | `signature-survey` on `landed`. |
| Mortar — Falling Star | The two-tick floor reticle fills its four cardinal arms; a translucent faceted warm mass descends only during resolve; a low dust-and-pressure cross spreads across the five affected tiles. No opaque sphere or sky beam. | Tell timing and `signature-damage` positions. | Existing `impact`, coalesced once at resolve. |
| Minesmith — Trip Node | Three short floor rails draw inward; the deliberately small approved matte mine emerges from below and becomes the only central silhouette; a trigger collapses into one low pressure torus. | Active position, capacity, suppression, and `signature-damage`. No invented pre-placement tell. | `signature-construct` on placement; existing impact on a real trigger. |
| Hush — Null Field | Boundary ticks contract during the one-tick tell; a shallow dark-violet floor lens then occupies the exact footprint; suppressed constructs visibly mute and carry the existing crossed state. No central hub or dome. | Grammar 3 matures after response movement. Grammar 2 retains its tick-start activation. | `signature-null` on `activated`; silence while maintained. |
| Relay — Arc Toss | The destination ring appears during the one-tick tell; the one real neutral Core leaves custody and follows its authored arc; the landing becomes either the existing levitation pickup or neutral settle. No duplicate orb or decorative trajectory ribbon. | Core in-flight state plus signature and Core relocation/pickup/drop facts. | Existing Core-family cues only. |
| Switchback — Exchange | Both endpoints receive low rings and a restrained ground association; each real body moves to its authored destination and keeps its identity. Only the endpoints remain as positions settle. No arrows, shuttles, or renderer-authored push. | Tell phase, exact `body-relocated` facts, and terminal completion. | `signature-exchange` once on completion. |
| Longshot — Rail Line | A very thin, low-saturation lane persists for the exact wind-up; resolve is a narrow dim pressure pass along the frozen path; one faint floor afterimage clears quickly. | `pendingStrikes`, visible signature path, completion/cancel reason, and damage facts. | Existing wind-up, projectile release, and one coalesced impact. |
| Mason — Hardlight Block | A compact floor frame appears on activation; four grid-aligned, low-specular hardlight faces and a top pane rise into one honest one-tile block. Slim projector rails share Prism Wall's construction grammar, while the closed footprint distinguishes this movement-blocking barrier from Palisade's thin wall. Three restrained cap bars show its remaining hull. No borrowed map masonry, glass gloss, or oversized posts. | Active position, capacity, contact/destruction reason, and end tick. No pre-roll. | `signature-construct` once at start. |
| Sunder — Target Paint | Four blunt brackets acquire the tracked target during its new one-tick window; on successful resolve a compact overhead coral/brass tally persists and loses one mark per `segment-consumed`. No lateral scan line. | Grammar-3 acquisition, target tracking, completion/cancel reason, capacity, and end tick. Grammar 2 retains immediate paint. | `signature-target` only on successful acquisition. |
| Repulsor — Kinetic Burst | The exact one-tick boundary contracts toward the rooted caster; after response movement, resolve sends one low torus outward; faint wakes appear only behind bodies that actually relocate. No opaque central machine. | Grammar-3 post-movement completion/cancel and body relocation facts. Grammar 2 retains tick-start resolve. | `signature-kinetic` once at completion. |
| Veil — Smoke Canister | A muted canister follows its public ballistic phase; landing leaves one low, squashed transparent field volume across the published footprint. It breathes softly without opaque smoke grapes or a solid dome. | Grammar-3 `in-flight`/`landed` phases and exact active positions. Grammar 2 retains immediate active smoke with no invented travel. | `signature-smoke` once on landing. |
| Nest — Sentinel Seed | Nest presses forward without pitching its long nose through the floor while the compact destination socket/seed treatment announces deployment; once active, the deliberately small approved matte sentry grows from floor contact and becomes the persistent silhouette; its split head yaws only toward an observed real shot. | Tell, active position, capacity, suppression, and `signature-damage`. | `signature-construct` on deployment; the shared projectile and impact cues communicate real shots. |

## Board index and corrections

| Board | Rows, top to bottom | Production correction |
| --- | --- | --- |
| `01-high-impact-signatures.png` | Tractor Hook, Falling Star, Rail Line, Hardlight Block | Remove Falling Star's generated light column; dim Rail; finish Hardlight matte. |
| `02-support-fields.png` | Repair Beam, Survey Flare, Null Field, Smoke Canister | Remove Null's hub; reduce Smoke to one low transparent volume; use the travel arc only for grammar 3. |
| `03-motion-objective.png` | Vector Dash, Prism Wall, Arc Toss | One real moving body/Core only; no dash guide line, Core trajectory ribbon, ghosts, or duplicate objective. |
| `04-control-level-up.png` | Exchange, Target Paint, Kinetic Burst, Level Up | Endpoint motion is fact-driven; no Exchange arrows, Target Paint scan line, Kinetic hub, or full-height level-up column. |
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
4. All temporary geometry is gone by 1.8 presentation ticks; the chevron remains.

There is no white flash, full-height column, camera shake, or mesh deformation.
The accepted `level-up` SFX remains unchanged.

## Implemented shape

The repository now has the typed handler and presentation framework, the
Tapline-2 grammar package, all sixteen WebGL treatments and Canvas truth
markers, sixteen class projectile identities, fact-driven level-up, signature
audio, and canonical extension documentation. Trip Node and Sentinel Seed use
the separately approved Meshy assets without modifying their model files.

This remediation adds signature-specific whole-body acting and stronger
physical start/action/settle verbs around those existing facts. It retains map
geometry, music, mechanics, historical profiles, frozen hosted artifacts, and
the separately promoted Trip Node/Sentinel Seed model bytes.

## Verification gates

- Every one of the 16 signature IDs has an explicit renderer treatment
  and an authoritative trigger test.
- Every engine launch class has exactly one catalog record, signature, action,
  replay form presentation, class look, and regular-attack projectile.
- Every signature kind has exactly one typed mechanics handler, metadata entry,
  canonical writer/reader path, WebGL presentation definition, and audio
  binding or explicit silence.
- A deliberately unknown presentation package/signature produces a truthful
  generic phase/footprint marker and never crashes playback.
- Historical grammar-2 replays retain byte-compatible rules and render without
  invented vNext anticipation or travel.
- Null Field, Target Paint, and Kinetic Burst each expose a real response move;
  Smoke publishes its complete launch/flight/landing chronology.
- Every new cancel, move, resolve, cooldown, and duration boundary has an engine
  chronology test and replay round-trip proof.
- All 16 Arc classes resolve to unique projectile look IDs and valid SVG masks.
- Seek directly into tell, active, in-flight, resolved, suppressed, and expired
  moments produces the same frame as continuous playback.
- Cancelled line signatures disappear without a release or resolve effect.
- Smoke is active on its authored start frame in grammar 2; grammar 3 activates
  only on its authoritative landing.
- Projectiles, effects, and level-up transients add no hitbox or targeting
  claim and never duplicate an actor or Core.
- Pooled geometry/materials perform without per-frame asset creation in a
  sixteen-body Tapline fight.
- Reduced motion shortens wakes/arcs and removes decorative drift while keeping
  telegraphs, targets, capacity, and outcomes readable.
- Real-replay motion captures retain every treatment's dominant silhouette and
  three-beat story in both the ordinary fixed overview and an honestly labelled
  director view; neither is replaced by mesh-count or existence assertions.
- The complete audio pack passes the comfort and crowded-mix checks in
  `AUDIO-CUE-PROPOSAL.md`.
