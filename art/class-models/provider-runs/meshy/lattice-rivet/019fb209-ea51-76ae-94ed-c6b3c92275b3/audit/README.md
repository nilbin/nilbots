# Lattice Rivet raw-provider audit

The raw `40,265,884`-byte master
`f62409fd1d8bf3cafaaa92a29cd8e3eb9cfe072d51368c7ce1a63c253f030774`
was imported into Blender 5.2.0 and normalized from inspected source `-X`
facing to runtime `+X`, with a fixed yaw, uniform scale, and translation only.

- Exact top IoU to the runtime SVG: **95.69%**.
- The diamond aperture remains open and the top/material identity is strong.
- Side-depth drift from the approved clean side: **+88.9%**.
- Geometry: 185,463 vertices, 356,538 triangles, one mesh/primitive.
- Materials: one double-sided PBR material with base, normal,
  metallic/roughness, and neutral emission maps; no
  `projectile-mask` semantic material and no tangent accessor.
- Normal gameplay span: about 19 px at the comparison's explicit
  50 px/tile assumption.

The apparent depth is not one thick body. Exact-position welding proves two
separate complete watertight projectile bodies:

| Component | Welded vertices | Triangles | Source physical-depth range | Planform coverage |
| --- | ---: | ---: | --- | --- |
| Positive-depth / visible top | 87,161 | 174,338 | +0.156148 to +0.287284 | 100.000% × 99.993% |
| Negative-depth duplicate | 91,088 | 182,200 | -0.285500 to -0.144590 | 99.988% × 100.000% |

There are zero boundary or non-manifold edges. Both components cover the whole
planform; they are not UV islands or two halves of one shell.

Verdict: **failed depth master; top/material reference only**. The narrow
deterministic correction is to separate the two welded components, retain the
positive-depth body whose visible top supplied the audited planform, and delete
the negative-depth duplicate without moving, flattening, averaging, or
bridging either. Re-run the side gate afterward. The retained body is only
about 0.069 authored tiles deep versus the 0.160 clean-side reference, so
rebuild its underside if that reference remains the approved depth. Then
retopologize/rebake with tangents and author semantic neutral
projectile-mask materials.

`contact-sheet.png` makes the stacked duplication explicit.
`audit-report.json` and `../geometry-report.json` contain the measured record.
