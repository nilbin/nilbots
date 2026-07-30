# Aegis Tortoise Shell raw-provider audit

The durable `51,000,780`-byte GLB
`8a0a0129cc57afdf12a0c0e70159f2acd547b426501941330e4712d06b78c5ec`
was imported directly into Blender 5.2.0. Axis probes prove standard source
`+Y` up and source `-X` facing; a fixed 180-degree yaw places the physical
guard on target `+X`. No reflection, non-uniform warp, or provider preview
entered the verdict.

- Exact top IoU to the runtime Shell SVG: **84.81%**.
- Top IoU to the approved corrected concept-derived input: **90.56%**.
- Side IoU to the approved clean side: **83.90%**; height-to-length is
  **17.1% too thin**.
- Geometry: 414,315 vertices and 788,278 triangles in one mesh/primitive.
- Connectivity: 346 indexed seam islands weld to one complete watertight
  manifold, with zero boundary and non-manifold edges.
- Rig data: no skin, animation, or morph target.
- Materials: one double-sided PBR material with base-color, emission,
  metallic/roughness, and normal maps, but no tangent accessor and no semantic
  `team-accent` material.

Verdict: **accepted high-detail Shell form/material reference requiring an
authored retopo and rig**. It is not directly segmentable. The deterministic
next build must:

1. Return the blockout to the exact East-facing SVG planform: restore the
   north/south rear lobes, trim provider-only west-pod and shield-shoulder
   protrusions, and preserve the hard ±45-degree guard edges, open other three
   quadrants, and no muzzle.
2. Preserve the approved planted profile while restoring the missing side
   height locally; do not globally scale or flatten the provider mesh.
3. Rebuild a named root, directional guard hierarchy, and planted-support
   hierarchy. The welded manifold provides no legal rigid parts to parent.
4. Retopologize and rebake the accepted top armor and mechanics with tangents.
5. Make only the two guard bars and west-centre panel neutral semantic team
   paint; keep the central reactor as fixed class energy.

`contact-sheet.png` contains the approved concept, exact SVG and corrected
input comparisons, strict top/side/oblique/gameplay renders, and the
single-component diagnostic. `audit-report.json`, `orientation-audit.json`,
and `../geometry-report.json` are the machine-readable records.
