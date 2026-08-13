# Arc Relay presentation batch — concept approval v1

This directory began as the approval artifact for the Tapline presentation
pass. The approved implementation and its retained real-game evidence now live
in `runtime-proof/`; the concept boards remain the visual contract.

Generated on 2026-08-12 from the `main` worktree at baseline `4aaf80cc`.

## Implemented batch

- 16 renderer-authored signature treatments, each driven only by
  replay-authored phases, positions, and facts. Trip Node and Sentinel Seed use
  their separately promoted Meshy props, while their deployment motion and
  presentation grammar live in this renderer batch.
- A typed mechanics-to-presentation extension framework, exact coverage tests,
  replay presentation versioning, and a canonical guide for adding another
  class/signature without rediscovering every integration surface.
- 16 class-specific regular-attack projectile heads, implemented as SVG masks
  that the WebGL renderer extrudes into compact 3D forms. No provider-made
  projectile models are required.
- One fact-driven level-up animation around the already persistent brass
  chevron.
- A full timing audit of all 16 signatures. The audit's possible mechanics
  changes remain recommendations only; this remediation changes presentation,
  not gameplay truth.
- The existing restrained SFX language remains in place. No ambience is added
  and music is unchanged.
- Tapline is the primary review map; the implementation remains map-agnostic.

Trip Node and Sentinel Seed model source, materials, and manifests remain owned
by the 3D-model handoff. Their real-game deployment animation, presentation
registration, tests, and proof are part of this batch.

## Non-negotiable implementation rules

- Targeting, collision, damage, healing, Core custody, signature timing, and
  replay truth do not change in this presentation remediation. The possible
  Tapline vNext package in `SIGNATURE-TIMING-AUDIT.md` is documentation, not an
  applied rules change. Historical `tapline-1` rules, fingerprints, stock
  artifacts, ratings, and hosted playlists remain immutable.
- A visual tell never begins before the replay publishes it. A visual resolve
  never survives past its authoritative end.
- No skeletal or part animation is assumed. Effects use pooled renderer
  geometry, material parameters, and already-authoritative whole-body poses.
- Every current and future class/signature has one explicit mechanics,
  replay-evidence, WebGL, Canvas fallback, audio, and asset registration path.
  Missing coverage fails tests rather than silently falling back for a known
  shipping class.
- The WebGL 3D renderer is the quality target. Canvas keeps compact truthful
  parity and does not need the same ornament.
- Regular attacks remain cosmetic projectile travel over the existing instant
  resolution. Fizzle/miss uses the same release voice; voluntary cancel stays
  silent.
- No ambient loop is added.

## Production corrections to the boards

The boards are direction, not literal frame captures. Production follows these
corrections:

- Falling Star has **no vertical light column**. The final beat is a low,
  floor-bound cardinal impact.
- Rail Line is a broad, low-luminance pressure slab, not a brilliant laser.
- Hardlight and Prism surfaces are matte/rough with narrow emissive edges.
- Null Field is a shallow floor lens, never a dome or bubble.
- Smoke uses fewer, lower abstract volumes. It never becomes a cotton cloud.
- Smoke travel is implemented only with the proposed grammar-3 `in-flight`
  phase. The renderer never invents it while reading a grammar-2 replay.
- Projectile colours in the sheet identify their class lineage. Runtime keeps
  the existing team-accent energy treatment and takes identity from silhouette
  and relief.

The fidelity target is nevertheless concrete: at the normal Tapline gameplay
camera, each owned treatment must retain the board's dominant silhouette,
spatial hierarchy, three-beat story, matte material language, and restrained
emission. `PRESENTATION-FRAMEWORK.md` records the effect-by-effect confidence,
deliberate approximations, implementation seam, and capture acceptance gate.

## Files

- `index.html` — mobile-friendly approval gallery.
- `CONCEPT-SPEC.md` — exact signature, projectile, and level-up treatment.
- `PRESENTATION-FRAMEWORK.md` — fidelity contract, typed extension architecture,
  conformance gates, and the future class/signature checklist.
- `AUDIO-CUE-PROPOSAL.md` — event mapping, six-call provider cap, and comfort
  gates.
- `SIGNATURE-TIMING-AUDIT.md` — current timing reality, actionable-window
  analysis, and the exact four-change Tapline vNext recommendation.
- `PROMPTS.md` — image-generation prompt and reference provenance.
- `manifest.json` — dimensions, hashes, and status of every board.
- `rejected/projectile-mini-bots-v1.png` — preserved rejected iteration; it
  read as miniature bots rather than attack heads and is not presented as a
  candidate.
- `runtime-proof/README.md` — implemented grammar coverage, WASM provenance,
  all-16 directed and strategic-overview motion, phone/reduced-motion/seek
  proof, and performance results.
- `runtime-proof/index.html` — mobile-friendly gallery for every retained
  post-implementation frame, plus the live replay entry point.

## Local review

```sh
python3 scripts/serve-gallery.py 9093 --bind 127.0.0.1 \
  --directory .
```

Then open
`/art/reviews/arc-relay-presentation-batch-v1/runtime-proof/` for the retained
frame gallery or `/web/dist-review/?standalone` for the two live replays.

The approved scope authorizes the framework, canonical documentation, renderer
presentation work, and retained review proof. It does not authorize the timing
audit's optional mechanics changes, automatic SFX rerolls, or deployment.
