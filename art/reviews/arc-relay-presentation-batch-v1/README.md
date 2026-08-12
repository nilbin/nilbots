# Arc Relay presentation batch — concept approval v1

This is an approval artifact, not a runtime asset drop. It sketches the next
Tapline presentation pass before implementation begins.

Generated on 2026-08-12 from the `main` worktree at baseline `4aaf80cc`.

## Proposed batch

- 14 renderer-authored signature effects, each driven only by replay-authored
  phases, positions, and facts.
- 16 class-specific regular-attack projectile heads, implemented as SVG masks
  that the WebGL renderer extrudes into compact 3D forms. No provider-made
  projectile models are required.
- One fact-driven level-up animation around the already persistent brass
  chevron.
- A restrained signature SFX language made from at most six private SFX Engine
  source generations. No ambience and no changes to music.
- Tapline is the primary review map; the implementation remains map-agnostic.

Trip Node and Sentinel Seed are deliberately outside this batch because the 3D
model agent has taken ownership of those two props. This batch will not edit
their source models, materials, manifests, or renderer integration.

## Non-negotiable presentation rules

- Game mechanics, simulation timing, targeting, collision, damage, healing,
  Core custody, and replay data do not change.
- A visual tell never begins before the replay publishes it. A visual resolve
  never survives past its authoritative end.
- No skeletal or part animation is assumed. Effects use pooled renderer
  geometry, material parameters, and already-authoritative whole-body poses.
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
- Smoke has no fake travel phase: Tapline publishes it as an immediate field,
  so production begins the field on the authoritative start frame.
- Projectile colours in the sheet identify their class lineage. Runtime keeps
  the existing team-accent energy treatment and takes identity from silhouette
  and relief.

## Files

- `index.html` — mobile-friendly approval gallery.
- `CONCEPT-SPEC.md` — exact signature, projectile, and level-up treatment.
- `AUDIO-CUE-PROPOSAL.md` — event mapping, six-call provider cap, and comfort
  gates.
- `PROMPTS.md` — image-generation prompt and reference provenance.
- `manifest.json` — dimensions, hashes, and status of every board.
- `rejected/projectile-mini-bots-v1.png` — preserved rejected iteration; it
  read as miniature bots rather than attack heads and is not presented as a
  candidate.

## Local review

```sh
python3 scripts/serve-gallery.py 9093 --bind 127.0.0.1 \
  --directory art/reviews/arc-relay-presentation-batch-v1
```

Approval of the whole batch authorizes the renderer work and exactly six
private SFX Engine generation jobs. It does not authorize automatic rerolls.
Any seventh provider call requires a separate approval.
