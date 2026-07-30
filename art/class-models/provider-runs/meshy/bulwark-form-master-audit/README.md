# Bulwark Shell and whole-Turret master audit

This audit measures the downloaded durable GLBs themselves in Blender 5.2.0.
Provider previews are provenance only. The source files and lossless maps were
hashed against their recorded downloads and never modified; no provider call
or credit was used.

| Form | Exact SVG top | Corrected concept top | Side-height drift | Exact-weld result | Verdict |
| --- | ---: | ---: | ---: | --- | --- |
| Aegis Tortoise Shell | 84.81% | 90.56% | -17.1% | 1 watertight manifold | Strong Shell reference; authored retopo/rig required |
| Aegis Tortoise whole Turret | 83.98% | 95.62% | -8.6% | 1 watertight manifold | Strong radial top reference; authored fourfold rebuild required |

`bulwark-form-comparison.png` is the concise fixed-gameplay/oblique comparison.
Each provider run's `audit/contact-sheet.png` contains the complete authority,
overlay, side, connectivity, and gameplay evidence.

## Segmentation and rigging verdict

Neither raw master has deterministic provider-component cleanup available.
Shell's 346 indexed islands and Turret's 275 indexed islands are UV/normal
seams: exact-position welding yields one complete watertight manifold in each
case. Parenting those islands would invent part boundaries. No corrected
candidate is produced by this audit.

Both masters remain valuable for authored retopology:

- Shell retains the intended directional guard, three open quadrants,
  detailed top armor, and planted-machine material language. Rebuild named
  guard and support parts while correcting the exact SVG planform and the
  17.1% thin side profile.
- Turret preserves the submitted whole radial object, 99.84% fourfold
  symmetry, the singular hub, and four equal arms. Rebuild one canonical
  arm/support hierarchy and rotate copies by exact 90-degree increments,
  keeping one hub and mounting the completed whole Turret once.

## Team material verdict

Both GLBs have one fused material with baked cyan in base-color and emission
maps, no `extras.nilbotsRole = "team-accent"`, and no tangents. They are
neither team-neutral nor directly tintable.

- Shell authoring must isolate exactly the two shield-edge bars and the
  west-centre panel; the central reactor remains class energy.
- Turret authoring must isolate exactly four equal top bars, retain the
  reactor as class energy, and remove four unintended cyan under-arm surfaces
  exposed by the oblique/side audit.

## Reproduction

```sh
BLENDER=/path/to/Blender-5.2.0.app/Contents/MacOS/Blender
ARTIFACTS=/path/to/nilbots/var/model-artifacts/meshy

"$BLENDER" --background --factory-startup --python \
  scripts/class-models/audit-bulwark-form-masters.py -- \
  --repo-root "$PWD" --artifacts-root "$ARTIFACTS" --phase probe

node scripts/class-models/compare-bulwark-form-master-audit.mjs \
  --repo-root "$PWD" --artifacts-root "$ARTIFACTS" --phase probe

"$BLENDER" --background --factory-startup --python \
  scripts/class-models/audit-bulwark-form-masters.py -- \
  --repo-root "$PWD" --artifacts-root "$ARTIFACTS" --phase render \
  --source-up aegis-tortoise-shell=+y \
  --source-facing aegis-tortoise-shell=-x \
  --source-up aegis-tortoise-turret=+y \
  --source-facing aegis-tortoise-turret=+x

node scripts/class-models/compare-bulwark-form-master-audit.mjs \
  --repo-root "$PWD" --artifacts-root "$ARTIFACTS" --phase final
```

The Node comparison uses the repository's pinned `web` development dependency
on Sharp. Normalization permits fixed source-axis correction, uniform scale,
and translation only.
