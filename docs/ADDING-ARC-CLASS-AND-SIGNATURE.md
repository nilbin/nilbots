# Adding an Arc Relay class and signature

This is the canonical end-to-end extension contract for Arc Relay. A class is
not complete when its mechanic works in isolation: it must remain discoverable
from rules through replay, product catalog, WebGL, Canvas, audio, and review
evidence. Exact-set tests are the guardrail against a partially wired class.

## The two version axes

Keep these separate:

- **Signature grammar version is roster-wide.** Every signature in a ruleset
  publishes the same grammar generation. Tapline-2 therefore places all sixteen
  launch signatures in grammar 3, including `initialPhase`, `maturityPhase`,
  and `cooldownAnchor` for every entry.
- **Concrete definition versions are mechanic-specific.** Add a new C# record
  variant only when behavior, chronology, or canonical fields change. Tapline-2
  has four such changes. The other twelve signatures retain their established
  concrete definitions while participating fully in grammar 3.

An empty `Foo2` alias does not improve coverage. It creates new compatibility
surface while saying nothing new. Grammar coverage comes from the ruleset,
canonical metadata, handlers, replay evidence, presentation definitions, and
exact-set tests.

## Authoritative path

```text
launch class + concrete signature definition
                  |
                  v
typed lifecycle handler + canonical grammar metadata
                  |
                  v
public operation state and discrete replay facts
                  |
                  v
replay-owned form/projectile/signature package IDs
                  |
          +-------+-------+
          v               v
typed WebGL painter   truthful Canvas marker
          +-------+-------+
                  v
fact-bound audio + compatibility fallback + proof
```

Presentation may explain authoritative state. It must not anticipate a phase,
invent a target, duplicate a body or Core, or imply collision, range, damage,
or timing that the replay does not contain.

## 1. Mechanics and canonical rules

1. Add the stable lowercase kebab-case class ID to
   `ArcRelayLaunchClassIds.All` in roster order.
2. Add the signature's concrete record and `SignatureKind` in
   `ArcRelaySignatureDefinition.cs`. Put every runtime number on that typed
   record; do not hide mechanic values in presentation code.
3. Add its category, argument kind, engagement range, and grammar-3 lifecycle
   timing to `MetadataFor` and `TimingFor`.
4. Add the class form, action, signature definition, and allowed action in
   `ArcRelayH0Definition.cs`.
5. Register exactly one typed lifecycle handler in
   `ArcRelaySignatureBehaviors.cs`. The runtime coordinates chronology and
   dispatch; the handler owns its mechanic's start, advance, resolve, contact,
   and cancellation hooks.
6. Extend the canonical writer and SDK reader only when the concrete definition
   adds real fields. Preserve the property order and presence semantics of
   historical grammar bytes.
7. Add exact boundary tests for legal start, public tell or flight, response
   window, maturity, cancellation, owner/target destruction, suppression,
   cooldown anchor, and terminal reason.

If the signature cannot be represented truthfully by the generic operation
envelope—phase, positions, target, capacity, suppression, and reason—add and
round-trip an explicit replay fact. Never repair a missing fact with a renderer
inference.

## 2. Product catalog and sheet authoring

Add the human name, signature label, fantasy, and starter/unlock policy to
`ArcRelayClassCatalog.cs`. The catalog obtains `SignatureId` and `ActionId`
from the current typed engine definition; those IDs are not a second
handwritten mapping.

Both `/api/arc-relay/catalog` and `/api/sheets/catalog` expose the stable IDs.
The Orders editor uses `signatureId` to determine body-local ownership. After a
contract change, run `./scripts/generate-api-clients.sh` and commit all generated
surfaces. `ArcRelayClassCatalogTests` must still prove exact equality between
the launch roster, product catalog, engine signatures, and actions.

Target selectors remain an authoring-policy decision. Add a narrow selector
only when the generic target vocabulary cannot honestly express the new
signature; do not infer ownership from its display name.

## 3. Replay-owned presentation

`ArcRelayH0ReplayPresentation` snapshots three kinds of immutable presentation
identity:

- `arc-<class-id>` for the form look;
- `arc-shot-<class-id>` for the class's regular-attack projectile head;
- `arc-relay-signatures-vN` for the complete signature presentation package.

The signature package ID is present only on a ruleset whose grammar it was made
to represent. A later visual or audio replacement mints a new package ID rather
than silently restyling recorded matches. Presentation changes the replay hash,
but not rules, map, or match fingerprints. Absence selects the frozen legacy
treatment; an unknown ID or signature renders a generic public phase/footprint
marker instead of disappearing or borrowing today's treatment.

Current Tapline-12 grammar-3 replays use `arc-relay-signatures-v4`. V4 adds the
owner-attached Prism Screen presentation. V3 retains the instant Arc Toss plus
historical static Prism Wall; V2 remains the recognized one-tick Toss package,
and V1 keeps its original audio bindings. A future visual change still mints a
new package rather than mutating any historical read.

## 4. Visual assets and painters

Add one internal class look package and one `arc-shot-<class-id>` white-alpha
SVG package under `web/src/assets`. The projectile is cosmetic: it must not
claim different width, range, speed, collision, or damage. A GLB companion is
optional and stays lazy to WebGL; Canvas and loading fallbacks remain complete.

Add one typed signature definition under
`web/src/render/signatures/definitions`. It declares:

- stable signature/package IDs and public phases;
- topology and the truthful Canvas marker;
- its equally named WebGL painter;
- audio bindings or explicit silence;
- reduced-motion behavior;
- maximum segments, panels, particles, markers, transparent layers, and props;
- the approved fidelity reference and dominant silhouette.

Add the painter under `web/src/render3d/signatures/definitions`. Per-frame paint
is a pure function of replay state and playhead time and may only transform
pooled primitives. It must not allocate geometry or material, retain narrative
state, or depend on having rendered earlier frames. Use the shared primitive
kit before adding a new primitive family.

Canvas is intentionally compact, but not optional. It must preserve the tell,
target or footprint, capacity/suppression state, and result with no Three.js
dependency.

## 5. Audio

Put signature bindings on the same typed presentation definition that owns the
visual treatment. A binding names an authoritative trigger and either a runtime
cue intent or `silent`; omission is not a silence decision. The browser-
independent audio registry imports those same leaf definitions so replay tests
can run without Vite or WebGL.

Use `priority` only to choose which two distinct signature moments survive one
crowded tick, and `tickOffset` only to align the cue inside the already-public
visual tick. Set `replacesGenericWindup` or `replacesGenericRelease` when a
signature owns that part of the attack sentence; otherwise the scheduler may
truthfully layer the shared attack voice. Persistent props may name stable
`projectileAudioProfileIds` so an authoritative `attack-launch` traversal gets
one prop-specific shot without retriggering on later advances. Bind repair and
damage outcomes to their explicit facts. Do not infer them from animation,
health deltas, or a future tick.

Every roster signature needs one distinct primary identity. A second cue is
justified only by a separately useful public beat such as Trip Node triggering,
Rail Line releasing, or Sentinel firing. Ordinary armor impact, healing-tile,
and Core outcomes remain shared vocabulary. Cancellation, maintenance,
suppression, replacement, and expiry are explicitly silent unless they gain a
clear player-useful outcome.

New audio must follow `docs/AUDIO-DESIGN.md`: private provider provenance,
hash-pinned deterministic delivery processing, decoded-runtime comfort gates,
category voice limits, crowded-mix proof, and human listening at normal volume.
Signature cues are subordinate to Core scoring and frequent attack outcomes.
Seeking stays silent, and audio never runs ahead of the presentation cursor.

## 6. Exact coverage and review gates

A complete addition leaves these sets equal:

- launch classes = product catalog classes = class forms = form look packages
  = regular-attack projectile packages;
- concrete `SignatureKind` values = typed handlers = metadata/timing cases =
  canonical writer/reader cases;
- ruleset signatures = presentation definitions = WebGL painters = audio
  definitions.

It also proves:

- historical rules fingerprints and fixtures remain byte-exact;
- the new ruleset mints new bytes only for real contract changes;
- direct seek and continuous playback agree at tell, active, channel,
  in-flight, resolve, cancelled, suppressed, and expired boundaries;
- desktop, phone landscape, and reduced motion retain the mechanic's truth;
- the sixteen-body scene stays within pooled primitive, transparency,
  sustained-render, and audio crowd budgets;
- fixed-camera real-replay captures preserve the approved dominant silhouette
  and three-beat story at gameplay scale.

Minimum verification for a change in this path:

```bash
dotnet test tests/BotArena.Engine.Tests/BotArena.Engine.Tests.csproj
dotnet test tests/BotArena.App.Tests/BotArena.App.Tests.csproj
./scripts/generate-api-clients.sh
cd web
npm test
npx tsc -b
npm run build
```

Run the model, audio, real-replay capture, and mobile profiling gates whenever
the addition touches those surfaces. Generated clients and deterministic audio
exports must be clean on a second run.

The first complete implementation of this contract is retained in
`art/reviews/arc-relay-presentation-batch-v1/runtime-proof`. Its two real
Tapline WASM matches cover all sixteen grammar-3 signatures, all sixteen
projectile identities, browser seek/reduced-motion/device evidence, and the
mobile sustained-render budget.
