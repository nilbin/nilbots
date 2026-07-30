# Authored class projectile models

This package builds the genuine 3D heads for Trident Spark, Rebound Diamond,
and Lattice Rivet. The audited Meshy masters are verified by hash and used only
as human material/form reference; no provider vertex, UV, texture, or topology
enters the runtime assets.

Run with the pinned Blender 5.2 toolchain:

```sh
BLENDER=/path/to/nilbots/var/toolchains/Blender-5.2.0.app/Contents/MacOS/Blender
ARTIFACTS=/path/to/nilbots/var/model-artifacts/meshy

"$BLENDER" --background --factory-startup --python \
  art/class-models/class-projectiles/build_class_projectiles.py -- \
  --repo-root "$PWD" \
  --artifacts-root "$ARTIFACTS"
```

The deterministic recipe writes editable `.blend` sources and compact texture
sources under `source/`, fixed-camera evidence under `review/`, runtime GLBs
beside the three existing class-projectile manifests, and `build-report.json`.
Run it twice and compare the reported GLB hashes before accepting a generator
change.

Run the independent SVG-planform gate after the final rebuild:

```sh
node scripts/class-models/compare-class-projectile-runtime.mjs "$PWD"
```

It writes per-look top overlays plus `silhouette-report.json` and fails below
the pinned IoU/aperture floor. Red pixels are model-only, cyan pixels are
SVG-only, and white pixels agree.

The fixed-camera boards are art gates, not final gameplay approval. Final
approval belongs to a combined integration branch with a fresh hash-verified
native Frontline replay carrying explicit V4 presentation. Pin the replay
hash, tick, viewport, camera, renderer, and map presentation. Its combined
board must show the genuine head together with renderer-owned trail/glow and
authored spin, both teams, exact hit/miss/deflection terminals, and the Pulse
fallback control. Isolated renders in `review/` deliberately do not claim that
approval.
