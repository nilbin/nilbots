# Bulwark Shell and whole-Turret provider-input audit

Date: 2026-07-30

This is an offline input audit. No provider task was submitted and no provider
credit was spent.

## Method

- Keep the owner-approved inferred-3D concept sheets immutable.
- Use the runtime Shell/Turret SVG only as strict-top geometry and semantic
  authority, never as a flat replacement for the inferred 3D top.
- Make targeted identity-preserving edits: slimmer Shell rear footprint and
  correct inlays; additive Turret diagonal webs and equally trimmed terminal
  pods.
- Normalize four separate views to 1254×1254 RGB, exact flat `#b8b8b8`
  background, 989–990 px foreground width, no clipping, and no cast/contact
  shadow.
- Align the clean top to its SVG with one uniform scale and centroid
  translation. Do not rotate, reflect, deform, or nonuniformly warp.
- Inspect all eight outputs at original resolution. Pink in retained overlays
  is SVG-only area; cyan is clean-top-only area.

## Geometry result

| Package | Previous IoU | Clean IoU | Clean outer IoU | Improvement | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Aegis Tortoise Shell | 82.37% | 88.57% | 88.58% | +6.20 pp | **Materially closer; pass input gate** |
| Aegis Tortoise whole Turret | 80.16% | 85.85% | 85.84% | +5.69 pp | **Materially closer; pass input gate** |

The remaining overlay differences are base-mesh cleanup work, not permission
to reinterpret the identity. Shell retains stronger exposed rear mechanisms
than the SVG lobes. Turret still needs exact final diagonal-web/pod contour
correction. Blender/technical art owns those final silhouettes after provider
reconstruction.

## Input integrity

- Every view is a 1254×1254 three-channel RGB PNG.
- Every 32 px canvas border is exactly `#b8b8b8`; zero non-flat border pixels
  were found.
- Every object occupies 989–990 px in width with at least 132 px horizontal
  padding.
- No crop contains the pale/white hardware corruption produced by the
  rejected shadow-as-alpha experiment.
- Each package contains one object. The whole Turret includes its one hub and
  four connected arms; it is not one arm repeated in the task.

## Semantic result

- Shell: the two shield-edge bars and west-centre panel are team inlays; the
  reactor is class energy; rear corner pods are not team-owned.
- Whole Turret: the four equal cardinal bars are team inlays; the centered
  reactor is class energy.
- Prompted cyan placement is only authoring evidence. The eventual corrected
  model still requires a separate texture-free material with
  `extras.nilbotsRole = "team-accent"`; the core remains hull/class energy.

Machine-readable crop and overlay evidence lives in
`clean-input-report.json` and `bulwark-form-input-audit.json`. Reproduce with:

```sh
node art/class-models/provider-inputs/prepare-clean-inputs.mjs
node art/class-models/provider-inputs/audit-bulwark-form-inputs.mjs
```
