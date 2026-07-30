# Rebound Diamond raw-provider audit

The raw `37,580,164`-byte master
`fad945e58c6010f72205787c1570c895395f4846a05e4984e232c06858f1964d`
was imported into Blender 5.2.0. Its outer long nose identifies source
`-X` facing, so a fixed 180-degree yaw places it on runtime `+X`; no reflection
or non-uniform warp is used.

- Exact top IoU to the runtime SVG: **87.68%**.
- The tunnel stays genuinely open, but its transparent aperture is rounded and
  too small: 14.76% of model silhouette area versus 27.59% in the authority.
- Side-depth drift from the approved clean side: **+9.3%**.
- Geometry: 95,000 vertices, 180,580 triangles, one mesh/primitive.
- Connectivity: 76 indexed seam islands weld to one watertight component;
  zero boundary or non-manifold edges.
- Materials: one double-sided PBR material with base, normal,
  metallic/roughness, and neutral emission maps; no
  `projectile-mask` semantic material and no tangent accessor.
- Normal gameplay span: about 20 px at the comparison's explicit
  50 px/tile assumption.

Verdict: **coherent reconstruction master requiring a focused aperture-contour
correction**. Keep the outer nose on target `+X`, rebuild the open tunnel to
the exact elongated-hex SVG contour, then retopologize/rebake with tangents and
author semantic neutral projectile-mask materials. The rounded tunnel is not
fixed by rotating the whole master.

`contact-sheet.png` contains the approved concept, SVG authority, deterministic
Blender top/side/oblique/gameplay views, and overlays. `audit-report.json` and
`../geometry-report.json` are the machine-readable record.
