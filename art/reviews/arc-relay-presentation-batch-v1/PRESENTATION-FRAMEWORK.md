# Arc Relay class, signature, and presentation framework — approval proposal

Status: proposed as part of the Arc Relay presentation batch. This document is
the review record. After approval, its durable contracts move into the owning
repository documentation and tests; this artifact remains the visual decision
record.

## Answer on concept fidelity

Yes: every storyboard owned by this batch can become recognizably the same
effect at the normal Tapline gameplay camera. The target is not a vague
inspiration. A paused runtime frame must retain the board's dominant shape,
spatial hierarchy, three-beat story, matte material language, and restrained
colour/emission balance.

That is a gameplay-camera fidelity promise, not a promise to reproduce an
offline illustration pixel for pixel. The generated boards use cinematic
lighting, very soft shadows, dense surface detail, and in places the visual
equivalent of ray-traced volumetrics. Three.js can match the important read with
pooled geometry, procedural materials, ribbons, panels, floor fields, trails,
and short particles. It should not spend runtime or visual noise on detail that
disappears when a tile is 40–70 pixels wide.

No skeletal mesh animation is required for these boards. Their action comes
from public body movement, whole-body settle/recoil, and renderer-owned effects
around the existing meshes. The Trip Node and Sentinel Seed models remain the
other 3D agent's assets; this batch owns their tell, activation, capacity, and
fire presentation only after those models are integrated.

### Fidelity by treatment

| Treatment | Gameplay-camera target | Deliberate runtime difference |
| --- | --- | --- |
| Tractor Hook | High: narrowing line, travelling head, taut dark cable, authored pull | Cable uses a bounded segmented curve; no simulated rope physics. |
| Falling Star | High after correction: cardinal tell, descending capsule, low five-tile impact | No generated sky column, permanent crater, or floor deformation. |
| Rail Line | High after correction: notched wind-up, broad pressure slab, short afterimage | Resolve is substantially dimmer and less saturated than the board for comfort in stacked fights. |
| Hardlight Block | High: complete one-tile frame, rising chunky slabs, matte settled obstacle | The collision-readable full envelope appears immediately; slab settling is cosmetic. |
| Repair Beam | High: three braided bands, travelling packets, one seam-closing repair beat | Bands are pooled segmented ribbons, not spline geometry rebuilt every frame. |
| Survey Flare | High: real flight, calibration ring, exact reveal footprint | Sparse floor ticks replace tiny board detail that would alias. |
| Null Field | High after correction: contracting boundary and shallow dark floor lens | No dome, screen distortion, or full-scene post-process. |
| Smoke Canister | Medium-high: real canister flight and low rolling matte lobes across the exact footprint | Stylized shader/mesh volumes replace true volumetric smoke; this is the largest approximation in the set. |
| Vector Dash | High: floor promise, one real moving body, short pressure wake | No duplicate/ghost bodies and no future-pose anticipation. |
| Prism Wall | High after correction: contact posts, three rising panes, capacity loss along an edge | Panes are rough and low-specular rather than the board's glassier result. |
| Arc Toss | High: destination tell, the one real Core in flight, honest landing state | No decorative duplicate Core or invented pickup. |
| Exchange | High: paired endpoints, folding ribbons, exact body relocations | Ribbons communicate association only; they do not imply collision or a curved path. |
| Target Paint | High: blunt acquisition brackets and three persistent tally bars | No thin high-frequency reticle lines or bright lock-on ping. |
| Kinetic Burst | High: contracting local ring, low torus, wakes only on relocated bodies | No camera shake or expanding full-screen distortion. |
| Level Up | High after correction: brass ring, three converging facets, persistent chevron | No full-height column or white flash. |
| Sixteen attack heads | High silhouette fidelity at 16–28 pixels | SVG-derived 3D relief retains the shapes, not the board's close-up microtexture. |

### Fidelity acceptance gate

For every owned treatment, the implementation produces a fixed-camera
start/action/resolve capture from a real Tapline replay beside the corresponding
storyboard row. It passes only when all of these are true:

1. The dominant silhouette and direction still match at 25% thumbnail scale.
2. The same three-beat story is recognizable without reading a label.
3. Every position, target, body, Core, capacity change, and outcome is supported
   by replay evidence available at that playhead time.
4. The effect stays readable without becoming the brightest object in an 8v8
   stacked fight.
5. Seeking directly to any phase produces the same frame as continuous play.
6. Desktop, phone landscape, and reduced-motion profiles keep the mechanical
   tell, active footprint, and outcome readable inside their budgets.

## What exists now

The repository already owns several good extension contracts:

- `ArcRelaySignatureDefinition` is a closed typed mechanics catalog. Grammar 2
  publishes category, argument kind, and engagement range for every signature.
- `ArcRelaySignatureState` publishes a generic operation envelope: stable ID,
  signature ID/kind, owner/team, phase, timing, exact positions, optional target,
  remaining capacity, and suppression.
- Replay facts publish discrete outcomes such as signature changes, relocation,
  damage, repair, projectile contact, and Core custody.
- Replay presentation snapshots exact form look and projectile look IDs without
  putting artwork in gameplay fingerprints.
- Chassis, projectile, and optional GLB assets are discovered from manifests.

The current weakness is repeated orchestration. Adding a class or signature can
touch the engine runtime's type switches, the canonical writer, the App catalog,
sheet authoring, a hard-coded WebGL style map, Canvas branches, a class fallback
map, and a central audio switch. Some omissions fail loudly, but no one test yet
proves the complete mechanics-to-presentation chain.

## Proposed architecture

```text
typed launch class + typed signature handler
                    │
                    ▼
        canonical rules metadata
  id · category · arguments · range · timing
                    │
                    ▼
          public replay evidence
 operation phase · positions · target · facts
 form look · projectile look · presentation pack
                    │
                    ▼
      typed presentation definition
 topology · phase painter · audio bindings
 reduced motion · primitive budget · fallback
                    │
          ┌─────────┴─────────┐
          ▼                   ▼
 pooled WebGL primitives   truthful Canvas marker
          │                   │
          └─────────┬─────────┘
                    ▼
   coverage · seek · crowd · comfort proof
```

The rule is one authoritative fact moving downward. Presentation may explain a
fact; it may not manufacture one.

### 1. Typed mechanics handlers

Unique skills remain explicit C# code. A universal JSON effect language would
hide important chronology and make unusual mechanics harder to review.

`ArcRelaySignatureRuntime` becomes a coordinator for operation IDs, cooldowns,
canonical event ordering, visibility, and handler dispatch. One registered
handler per `SignatureKind` owns only the hooks it needs from the small public
chronology vocabulary:

- validate/select targets;
- create/start an operation;
- advance at tick start;
- resolve after movement;
- participate in combat/projectile contact;
- react to tile entry or body destruction;
- end/cancel and expose its public state.

The extraction happens first with no gameplay change. Historical fingerprints,
goldens, and chronology tests must remain identical. The four approved Tapline
vNext changes are then implemented in their own handlers rather than adding more
conditions to a central switch.

A registry-conformance test enumerates every `SignatureKind` and requires
exactly one handler, one metadata record, one canonical writer/reader case, and
one action-argument contract. Duplicate or missing registration fails the test
that introduced the new kind.

### 2. Versioned replay presentation

The generic replay operation envelope remains the common language. A new
signature should fit its phases, positions, target, capacity, and discrete
facts. If a mechanic cannot be represented truthfully, its implementation must
add and round-trip an explicit fact instead of teaching a renderer to infer it.

New replays additionally snapshot one immutable `signaturePresentationId`
beside their existing map/form presentation. It changes replay identity but not
rules, map, or match fingerprints. The selected presentation package owns the
visual definition IDs and audio-pack/binding version. Historical replays with no
field use the current legacy effect treatment; an unknown package falls back to
a generic truthful phase/footprint marker instead of blanking the event.

This lets a later art pass mint `v2` without silently restyling old captures.

### 3. Discoverable WebGL presentation definitions

The current `ARC_SIGNATURE_STYLES` table and large conditional painter become
discoverable typed presentation modules. Each definition declares:

- the stable signature ID and presentation version;
- supported public phases and positional topology;
- a deterministic WebGL painter driven by replay time and evidence;
- audio bindings by public fact/reason, including an explicit `silent` choice;
- reduced-motion treatment;
- maximum pooled meshes, transparent layers, and particles;
- the art-side fidelity brief and reference row used for approval.

Modules share a small primitive kit: floor ring/lens, line and lane, panel/slab,
segmented ribbon/cable, travelling head, low pressure torus, sparse particles,
and stylized volume lobes. A unique signature composes these primitives in its
own module; it does not add another global renderer switch.

The shared signature-presentation descriptor and evidence derivation live under
`web/src/render` and import no Three.js. The lazy WebGL factories and primitive
implementation live under `web/src/render3d`. This keeps Three.js and its assets
out of the self-contained Canvas/CLI viewer while both renderers consume the
same phase, topology, reduced-motion, and audio intent.

Every painter is a pure function of the replay and playhead time. Pools retain
allocated objects, not narrative state, so pause, backwards seek, and direct
seek remain deterministic.

The WebGL renderer is the quality target. Canvas consumes the same derived
phase/topology contract and renders compact truthful markers; it does not ship
Three.js or duplicate the ornamental treatment.

### 4. Manifest-driven class and projectile presentation

The class-look manifests already carry `classId` and `defaultProjectile`. The
implementation builds the internal class presentation index from those
manifests and removes Arc Relay's duplicated TypeScript class union/fallback
table. Frontline's mobile/emplaced/stance variants gain explicit manifest roles
rather than being inferred from filename fragments.

Arc Relay's new regular-attack packages use the stable convention
`arc-shot-<class-id>`. `ArcRelayH0ReplayPresentation` can therefore snapshot the
exact projectile ID for every form without importing web assets or maintaining
a second class-to-projectile table. The human label and silhouette remain the
distinct concepts already approved (`Kestrel dart`, `Palisade wedge`, and so
on).

The sheet/catalog API exposes the class's stable signature/action ID. The web
authoring UI consumes that value instead of maintaining its own `CLASS_SKILLS`
map. Product name, fantasy, and unlock policy remain explicit App catalog data;
they are product choices, not renderer metadata.

### 5. Audio bindings live with presentation

Core game cues remain in the sound pack. Signature-specific bindings move from
the central replay-audio switch into each signature presentation definition.
The audio loader validates that every referenced cue exists, carries mix and
provenance metadata, and passes the established comfort metrics. Reuse and
intentional silence are valid declared bindings; an absent binding is not.

Tick coalescing, voice limits, projectile stacking, positional pan width, and
the master comfort limiter remain shared mixer policy. The six-call provider
cap in `AUDIO-CUE-PROPOSAL.md` is unchanged.

## One path for adding a class and signature

After the framework lands, a complete addition follows this checklist:

1. Add the launch class and its concrete signature definition/handler.
2. Declare category, argument shape, engagement range, lifecycle timing, and
   cooldown anchor; add chronology tests for every start/resolve/cancel edge.
3. Add the user-facing catalog entry and unlock policy. Coverage must exactly
   match the engine's launch-class set.
4. Add one class-look manifest and optional reviewed GLB companion, plus the
   `arc-shot-<class-id>` projectile package or an explicit approved reuse.
5. Add one signature presentation module with replay evidence, reduced-motion,
   resource budget, audio binding/silence, and fidelity brief.
6. Round-trip the replay presentation package and any genuinely new fact.
7. Pass the complete coverage gate and real-replay capture review.
8. Update the canonical class/signature extension guide and player-facing
   rules surface in the same change.

There is no claim that a new mechanic requires zero code. The promise is that
all required work has one explicit home, a discoverable path, and a failing
test when any layer is missing.

## Conformance and compatibility gates

The long-running goal is not complete until these gates exist and pass:

- Every engine launch class has exactly one class catalog record, signature,
  action, replay form presentation, class look, and regular-attack projectile.
- Every signature kind has exactly one typed mechanics handler and metadata
  record; canonical rules writer/reader coverage is exact.
- Every signature in the selected presentation package has exactly one WebGL
  definition plus an audio binding or explicit silence.
- Every referenced cue and asset resolves; IDs are unique, stable, lowercase
  kebab case, and manifest paths remain inside their package.
- A deliberately unknown signature/package renders the generic public
  phase/footprint fallback and never crashes playback.
- Historical Tapline rules and replays retain their exact gameplay bytes and
  use the legacy presentation fallback.
- New presentation metadata changes replay identity but not component gameplay
  fingerprints.
- Direct seek equals continuous playback for tell, active, channel, in-flight,
  resolve, suppressed, cancelled, and expired states.
- Sixteen-body crowded playback stays inside declared pooled-object,
  transparency, sustained-frame, and audio comfort budgets.
- Trip Node and Sentinel Seed model hashes/materials stay outside this batch;
  their public phase integration still passes the same presentation tests.

## Canonical documentation after approval

Implementation will promote the decisions above into their owning documents:

- `docs/ARENA-VISUALS.md` — presentation package, primitive, fidelity,
  reduced-motion, and capture gates.
- `docs/EXPERIMENTAL-ARC-RELAY.md` — exact vNext signature chronology and
  player-visible mechanics.
- `docs/AUDIO-DESIGN.md` — signature binding, pack versioning, mix, provenance,
  and comfort rules.
- a new concise `docs/ADDING-ARC-CLASS-AND-SIGNATURE.md` — the end-to-end
  extension checklist and conformance commands.
- replay/contract documentation beside the new optional presentation field.

Tests, not prose alone, enforce set coverage and compatibility. The documents
explain why the boundaries exist and how to extend them.
