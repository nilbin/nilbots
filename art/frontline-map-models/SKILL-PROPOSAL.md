# Proposed visual-assets skill addition

Root owns the shared skill edit. The following subsection records the process
actually exercised by this pilot and is ready to integrate:

```markdown
### 3D arena-kit pilot

Keep map JSON and `WallLayout` authoritative. Never generate, model, or ship a
whole map mesh. Start with an owner-approved whole-arena concept at the real
gameplay camera, but treat it only as art direction. The implementation is a
reusable kit resolved deterministically from presentation family plus
neighbour topology: obstacle, end, straight, corner, T-junction, and cross,
with exact rotations and a small stable variant set. Preserve the continuous
world-space floor material; floor relief and wear stay visibly flat and never
imply collision.

Use a gated two-pass concept process. First approve the whole-arena 2D art
direction in the real camera. Then infer and approve isolated 3D-readable
module turnarounds; do not submit the whole-arena concept as geometry
authority. Reject a turnaround before any paid call if its views disagree, its
ends cannot repeat, or a nominal one-cell module reads as a multi-cell
barricade.

For a provider pilot, submit one isolated module per task—perimeter and cover
separately—using consistent, plain-background views at least 1040 px:
front/gameplay-oblique, strict side, rear, and top when footprint matters.
Approve the exact input files and hashes before spending credits. Text prompts
guide texture, not geometry; show footprint, height, mating planes, and rear
construction in the images. Start Meshy 6 with PBR, 4K texture, no remesh,
lighting removal on, and GLB only. Omit deprecated symmetry controls. Start
image enhancement **off** for controlled high-resolution AI turnarounds; the
Striker A/B showed that enhancement can soften hard-surface landmarks. Turn it
on only as a targeted A/B for a diagnosed input defect.

Record a hard call/credit ledger before submission. After every task, retain
the raw master locally outside runtime, commit the immutable inputs, hashes,
compact preview, exact request parameters, sanitized result, geometry
inventory, review board, and accept/reject verdict. Remove API keys, signed
URLs, and expiring provider URLs from Git. A successful provider task is
evidence, not promotion; stop spending as soon as the route is clearly a
donor rather than a shippable module.

Compare provider and deterministic Blender/procedural modules against the same
concept. Inspect neutral front/side/rear/top, repeated straight/corner/junction
arrangements, bounds/axes/triangles/materials/size, and the exact 58-degree
gameplay camera. A provider mesh may supply shape and surface reference, but
Blender owns connector planes, bounds, retopology, semantic materials, LODs,
and export. Reject visible seams, ambiguous blocked footprints, family drift,
or a result that competes with bots.

Keep the world hierarchy explicit: environment and bots may share graphite,
aged bronze, amber machinery, layered armor, vents, and swept/chamfered forms,
but environment art is rougher, more matte, coarser, lower-contrast, and
lower-saturation. Bots stay cleaner, finer, sharper, and more emissive. Team
cyan/red never belongs in environment materials.

When approved bot visuals exceed one tile, audit the real map before changing
gameplay scale. Compute centre and legal-movement clearance against wall-cell
bounds, including one-tile corridors, spawns, and objectives. Prefer a
topology-aware renderer-only setback on wall faces bordering open floor;
retain connected wall edges so family seams do not open. Use
`max(0, visualRadius + safety - 0.5)` as the required open-edge inset. Keep map
JSON, collision, tile centres, actor/projectile transforms, and camera world
scale unchanged.

Treat normal action follow and explicit whole-arena Fit as separate camera
promises. If model readability needs a zoom-out ceiling, express it as a
world-space span shared by both renderers, never as a maximum DOM viewport.
Do not blindly cap an all-actor midpoint; first choose a semantic action
cluster, and retain Fit as the tactical overview.
```
