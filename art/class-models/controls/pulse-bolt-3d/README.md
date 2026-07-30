# Pulse Bolt procedural 3D comparison control

Status: **art-side control only**. This does not add a runtime model, catalog
entry, or new look ID. The existing `pulse-bolt` SVG remains canonical.

The deterministic Blender generator asserts the exact canonical SVG hash
`21e821e5dda1c46ea666f46699c9a8311528c5db868f2a1d445ff08c43f0ef06`,
maps its East-facing paths into contained shallow volumes, and exports an
editable `.blend` plus a compact comparison GLB:

- 116 vertices and 212 triangles across five authored objects.
- One-tile authored planform and 0.155-tile maximum real depth.
- 99.70% top IoU to the authored visible SVG envelope.
- Two neutral materials; both GLB materials carry
  `extras.nilbotsRole = "projectile-mask"`.
- 16,032-byte GLB, no camera, light, skin, animation, trail, halo, floor glow,
  impact, motion, or hitbox.
- Effective live span 0.3888 tiles before any future reviewed renderer change.

`review/contact-sheet.png` shows top, side, oblique, 58-degree gameplay camera,
and both tint proofs. The team-color views change only semantic material
factors; the model contains no fixed team hue.

Rebuild with:

```sh
/path/to/Blender-5.2.0.app/Contents/MacOS/Blender \
  --background --factory-startup --python \
  art/class-models/controls/pulse-bolt-3d/build-pulse-bolt-control.py -- \
  --repo-root "$PWD"
```

`build-report.json` records the generated artifact hashes and
`audit-report.json` independently inspects the GLB structure and silhouette.
