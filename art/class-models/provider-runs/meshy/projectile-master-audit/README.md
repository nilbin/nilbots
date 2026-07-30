# Projectile master audit

This is a Blender 5.2.0 audit of the downloaded GLBs themselves. Provider
thumbnails are provenance only. The raw masters in durable artifact storage
were hashed and imported without modification; no provider request or paid
call was made for this audit.

| Head | Exact top IoU | Side-depth drift | Welded components | Verdict |
| --- | ---: | ---: | ---: | --- |
| Pulse Bolt procedural control | 99.70% | authored 0.155-tile maximum | 5 authored objects | Art-side control only |
| Trident Spark | 92.98% | -33.7% | 1 | Coherent reconstruction master; not runtime-ready |
| Rebound Diamond | 87.68% | +9.3% | 1 | Coherent reconstruction master; aperture contour correction needed |
| Lattice Rivet | 95.69% | +88.9% | 2 | Failed depth master; top/material reference only |

`gameplay-scale-comparison.png` shows every head at the renderer's 58-degree
camera and its current effective span at an explicit 50 px/tile assumption:
19 px for Pulse and Lattice, 20 px for Rebound, and 21 px for Trident. The
common cyan/red treatment is the same grayscale multiply proxy for all four;
it is comparison evidence, not a substitute for real replay review.

Pulse Bolt is the clearest generic control at normal size. Trident and Rebound
retain more class character at the same scale, but neither raw master may ship
without the recorded Blender corrections and semantic material work. Lattice's
duplicated body makes the current raw model unusable even though its top view
is excellent. This evidence deliberately does not decide whether the
mechanical heads remain defaults or become store skins.

## Lattice duplicate proof

Exact-position welding reduces 105 indexed seam islands to two independent
watertight components, with zero boundary or non-manifold edges:

| Component | Welded vertices | Triangles | Source physical-depth range | Planform coverage |
| --- | ---: | ---: | --- | --- |
| Positive-depth / visible top | 87,161 | 174,338 | +0.156148 to +0.287284 | 100.000% × 99.993% |
| Negative-depth duplicate | 91,088 | 182,200 | -0.285500 to -0.144590 | 99.988% × 100.000% |

Both components cover the complete projectile planform and are separated by a
real gap. The narrow deterministic cleanup is therefore to separate by welded
component, retain the positive-depth body, delete the negative-depth duplicate
without moving either, then re-run the side gate. Do not average, flatten, or
bridge the two bodies. The retained component is only about 0.069 authored
tiles deep after normalization versus the 0.160 clean-side reference, so its
underside still needs an authored depth rebuild if that measured gate remains
the target.

## Reproduction

```sh
BLENDER=/path/to/Blender-5.2.0.app/Contents/MacOS/Blender
ARTIFACTS=/path/to/nilbots/var/model-artifacts/meshy

"$BLENDER" --background --factory-startup --python \
  scripts/class-models/audit-projectile-masters.py -- \
  --repo-root "$PWD" --artifacts-root "$ARTIFACTS" --phase probe

node scripts/class-models/compare-projectile-master-audit.mjs \
  --repo-root "$PWD" --artifacts-root "$ARTIFACTS" --phase probe

"$BLENDER" --background --factory-startup --python \
  scripts/class-models/audit-projectile-masters.py -- \
  --repo-root "$PWD" --artifacts-root "$ARTIFACTS" --phase render \
  --source-facing trident-spark=+x \
  --source-facing rebound-diamond=-x \
  --source-facing lattice-rivet=-x

"$BLENDER" --background --factory-startup --python \
  art/class-models/controls/pulse-bolt-3d/build-pulse-bolt-control.py -- \
  --repo-root "$PWD"

node scripts/class-models/compare-projectile-master-audit.mjs \
  --repo-root "$PWD" --artifacts-root "$ARTIFACTS" --phase final
```

The Node comparison uses the repository's pinned `web` development dependency
on Sharp. It compares authored visible alpha, including Pulse Bolt's
low-opacity outer energy envelope.
