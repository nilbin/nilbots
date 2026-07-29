# Class-model concept targets

These images bridge the approved 2D class art and authored 3D geometry. They
are modeling references, not runtime assets and not independent redesigns.

Each model must satisfy both sources:

- the canonical SVG controls the exact top-view silhouette, negative spaces,
  rule-bearing hardware, and semantic team-accent regions;
- the oblique target controls side/underside volume, material character,
  recess depth, and the intended gameplay-camera read.

When the two disagree, the canonical SVG wins. A target may explain depth
outside the visible top plane, but it cannot change the bot's footprint or
promise mechanics the class does not have.

## Striker target v1

- Canonical input:
  `web/src/assets/class-looks/trident-wasp/sprite.svg`
- Raster reference:
  `references/trident-wasp-2d.png`
- Oblique modeling target:
  `striker-oblique-target-v1.png`
- Four-view approval sheet:
  `striker-model-sheet-v1.png`
- Approval-sheet SHA-256:
  `7209a6afc3e58bcc37b2b5c710da167c8ee563656251e6de8fbe74c20305f683`
- Status: approved as the Striker depth/material direction on 2026-07-29.
- Generated with the built-in image-generation workflow as a
  `sketch-to-render` reference.

The generated top view is illustrative and is not a geometry source. Where it
differs, the canonical SVG and measured overlay win. Once a sheet is approved,
the human-corrected editable model source owns every later mesh/material
decision; further image-generation inference is outside the production path.
No procedural Blender generator is approved for this look.

Prompt:

> Translate the canonical top-view design into a genuinely
> three-dimensional low-hover combat skimmer. Preserve its dart/wasp
> planform, central hexagonal core, three forward channels, clipped aft
> shoulders, material-region boundaries, and three semantic cyan team
> panels. Use shaped continuous hull volumes, tapered sidewalls, armor
> thickness, recessed channels, an inset core well, underbody field emitters,
> vents, and a shallow visible air gap. Materials are restrained stylized
> PBR: graphite metal, warm titanium/brass, amber energy, and clean
> repaintable team panels. Avoid tracks, wheels, landing gear, rotors,
> cockpit, conventional tank cues, stacked flat cutouts, marble/crystal
> texture, grid noise, toy plastic, and extra weapons.
