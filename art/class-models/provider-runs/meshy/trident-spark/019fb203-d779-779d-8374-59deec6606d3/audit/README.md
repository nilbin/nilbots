# Trident Spark raw-provider audit

The raw `34,210,560`-byte master
`b48adbf2931a101663918e207af12794f5ee3cc3a480bf35e98ede979be0736d`
was imported into Blender 5.2.0 and normalized from inspected source `+X`
facing to runtime `+X`, with the fixed source-up correction, uniform scale,
and translation only.

- Exact top IoU to the runtime SVG: **92.98%**.
- Side-depth drift from the approved clean side: **-33.7%**; the underside is
  visibly pinched and too thin.
- Geometry: 56,037 vertices, 103,874 triangles, one mesh/primitive.
- Connectivity: 79 indexed seam islands weld to one watertight component;
  zero boundary or non-manifold edges.
- Materials: one double-sided PBR material with base, normal,
  metallic/roughness, and neutral emission maps; no
  `projectile-mask` semantic material and no tangent accessor.
- Normal gameplay span: about 21 px at the comparison's explicit
  50 px/tile assumption.

Verdict: **coherent reconstruction master, not runtime-ready**. Retain its
identity and material evidence, restore the exact SVG wing/fork planform,
rebuild the lower side profile without warping the top, retopologize/rebake
with tangents, and author semantic neutral projectile-mask materials before
both-team replay review.

`contact-sheet.png` contains the approved concept, SVG authority, deterministic
Blender top/side/oblique/gameplay views, and overlays. `audit-report.json` and
`../geometry-report.json` are the machine-readable record.
