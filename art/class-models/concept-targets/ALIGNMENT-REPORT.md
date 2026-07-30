# Class concept top-view alignment audit

Date: 2026-07-30

This is the retained result of the rigid top-view audit used to gate the
approved concepts. No provider task was submitted and no provider credits were
spent.

## Method

- Crop only the strict top panel from each approval sheet.
- Segment the connected neutral studio background and visually verify the
  resulting masks and overlays.
- Keep directional assets facing East/right; keep Turret fourfold.
- Optimize only one uniform scale and rigid translation. Do not reflect,
  anisotropically scale, deform, or non-uniformly warp.
- Measure on a 512×512 analysis canvas. Centroid residuals after rigid
  alignment were 0.04–0.13 px.
- Treat outer-envelope IoU as the more stable planform number for the open
  trussed Lattice.

## Gate

| Concept | Reference | IoU | Outer IoU | Verdict |
| --- | --- | ---: | ---: | --- |
| Aegis mobile V2 | high-resolution 2D concept | 95.46% | 96.19% | **Pass** |
| Aegis mobile V2 | runtime SVG | 72.63% | 73.36% | Authority conflict; do not reshape the approved concept |
| Lattice levitating V2 | high-resolution 2D concept | 88.48% | 94.44% | **Pass outer planform**; clean truss gaps and fork tips |
| Lattice levitating V2 | runtime SVG | 81.34% | 82.77% | Authority conflict; do not squeeze the approved concept |
| Aegis Shell V1 | runtime SVG | 82.37% | 83.21% | Targeted top-footprint correction |
| Aegis whole Turret V1 | runtime SVG | 80.16% | 80.57% | Targeted top-footprint correction |

The Aegis high-resolution concept and runtime SVG have only 75.35% rigid IoU.
The Lattice high-resolution concept and runtime SVG have only 76.13%. No single
rigid model can match both pairs exactly. The rich approved 2D concept is the
visual/top-planform authority for the mobile Aegis and Lattice; their SVGs
remain Canvas/site fallbacks and semantic team-accent authorities. Shell and
whole Turret use their SVGs as the exact top authority because no richer
separate 2D form exists.

## Required corrections before provider submission

### Aegis mobile V2

- Preserve the current top planform; it is already a high-resolution 2D pass.
- Keep the split eastern armour, central reactor, cannon slot, and four walking
  supports.
- The two long forward shield slits may remain authored cyan class energy, but
  are not team-owned. Restore the SVG's west-centre semantic team inlay.
- Keep the cyan core as authored class energy rather than team colour.

### Lattice levitating V2/V3

- Preserve the central honeycomb core, four open truss quadrants, two forward
  forks, and low-hover gap.
- Match each triangular truss opening and extend the fork tips to the rich 2D
  endpoints.
- Remove V3's unsupported north/south team strips. Enlarge/reshape the four
  valid team regions to the SVG's west-shoulder and east-fork-root polygons.
- Keep the amber core and underside hover emitters as authored class energy.

### Aegis Shell V1

- Use the runtime SVG raster as the strict provider top.
- Preserve the East-facing 90-degree shield, circular reactor, and exposed
  rear support identity.
- Remove extra north/south protrusions and restore the slimmer west/rear SVG
  lobes.
- Keep the two shield-edge team bars. Remove team ownership from the two rear
  corner-pod bars and restore the required west-centre team inlay.
- Keep the core as authored class energy.

### Aegis whole Turret V1

- Use the runtime SVG raster as the strict provider top.
- Preserve fourfold symmetry, one central reactor, and four equal cardinal
  armour pods.
- Fill the missing diagonal shoulder webs and slightly trim the four terminal
  pod tips. The concept is 12.17% under the SVG area while 95.15% of its own
  area already lies inside the SVG; this is a targeted additive correction,
  not a redesign.
- Preserve the four cardinal semantic team inlays. Keep the core as authored
  class energy.

## Reconstruction rule

Do not warp an approval sheet. A clean Meshy package uses the exact designated
2D top on a neutral background and the approved concept side/oblique views for
depth and materials. Blender/authored cleanup owns final outlines, negative
spaces, semantic team materials, animation pivots, and rigging.
