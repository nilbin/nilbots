# Smart geometry audit

`audit_smart_geometry.py` is the offline acceptance-evidence wrapper for a
generated GLB. It makes no provider or network call and never edits the source
model. The output directory must be new and live below `sandbox/` or `var/`
with `audit` in a later path component.

Run it from the repository root:

```sh
python3 scripts/class-models/audit_smart_geometry.py \
  --input art/class-models/provider-runs/meshy/<look-id>/<task-id>/raw-model.glb \
  --audit-dir var/class-models/smart-audits/<task-id> \
  --canonical-top art/class-look-concepts/<class>/<look-id>/concept.png \
  --authored-span-tiles 1 \
  --runtime-scale 1 \
  --pixels-per-tile 64
```

If a strict-top provider result arrives as a vertical concept plane, add a
measured rigid quarter-turn such as `--rigid-rotation-x-degrees -90`. The
report records the exact matrix and unit determinant; the source GLB remains
unchanged. Choose the sign from the visible authored face, not bounds alone.
After that inspection, `--rigid-rotation-z-degrees 180` can normalize a
reversed chassis to Nilbots `+X` facing.

The wrapper resolves Blender from the shared main worktree's pinned
`var/toolchains/Blender-5.2.0...` package. An explicit absolute `--blender`
path is allowed for another host, but the Blender-side audit rejects anything
outside 5.2.x.

The audit emits:

- `audit.json`: full source-document, hierarchy, topology, component, material,
  image, animation, pivot, overlap, bounds, normalization, camera, and render
  evidence;
- `geometry-report.json`: compact counts, the eight largest geometric
  components, fixed-view hashes, composed live scale, and optional
  top-silhouette measurements;
- strict top, side, front, rear, front/rear three-quarter, and pinned
  58-degree gameplay renders;
- `top-silhouette.png`: an opaque material-override top mask on transparent
  background;
- four optional `top-silhouette-overlay-yaw-*.png` overlays.

The canonical top authority must be a non-interlaced 8-bit RGBA PNG with
meaningful transparency. Both masks are independently centered and uniformly
scaled to the same maximum planform span. The report measures IoU, canonical
coverage, candidate containment, area delta, and centroid delta at every
quarter-turn. It deliberately selects no semantic facing and computes no
acceptance verdict. The reviewer must identify real `+X` facing from the fixed
views, then judge named anchors, negative spaces, side/depth class, forbidden
cues, and mechanical part boundaries independently.

`authored-span-tiles × runtime-scale` is recorded as the live maximum planform
span. The pixel figure is a nominal gameplay-size sampling input, not a wall
clearance proof; test every legal yaw and renderer transform in a real replay
before approval.

Run the offline wrapper tests with:

```sh
python3 scripts/class-models/test_audit_smart_geometry.py
```
