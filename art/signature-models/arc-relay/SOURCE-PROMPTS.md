# Arc Relay signature-object source prompts

Generated with the built-in image-generation workflow on 2026-08-03. The
Minesmith and Nest runtime sprites were supplied only as visual references for
their established material language. Background removal used the standard
border-sampled soft-matte/despill helper; the untouched chroma sources remain
beside the alpha derivatives.

## Trip Node — initial source

```text
Use case: stylized-concept
Asset type: clean single-object source image for Meshy T2 image-to-3D, Arc Relay Trip Node proximity mine
Input image: visual reference only for Minesmith's compact hard-surface material language and chunky readable paneling; do not reproduce the whole vehicle
Primary request: create one compact low-profile sci-fi proximity mine, a dark graphite armored puck with three short folding prongs/spikes arranged around the rim, a clearly readable central neutral-white sensor lens, and one small forward notch/wedge pointing East/right so orientation is unambiguous
Style/medium: polished stylized 3D game-asset concept render, believable hard-surface depth, broad shapes that survive a small gameplay camera
Composition/framing: exactly one object, centered, generous padding, square image, mild orthographic top-down oblique view consistent with the reference, forward direction points right
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background, uniform edge to edge
Lighting/mood: soft diffuse studio illumination on the object only; no baked spotlight or dramatic shadow
Color palette: low-chroma charcoal, gunmetal, restrained weathered bronze hardware, neutral white sensor; no cyan team paint and no large amber/orange team-colored areas
Constraints: low enough to read as a floor mine; compact footprint; clean silhouette; no cast shadow, no contact shadow, no floor plane, no reflection, no glow halo, no range ring, no smoke, no explosion, no text, no logo, no watermark; do not use #ff00ff anywhere on the object
Avoid: vehicle chassis, wheels, tracks, legs, turret barrel, multiple mines, loose parts, scenery, high thin antennae, oversized spikes that obscure tile occupancy
```

The initial result was rejected because its raised white central dome repeated
the Core's spherical language. It is not retained in this project.

## Trip Node — revised centre candidate

```text
Use case: precise-object-edit
Asset type: revised clean single-object source image for Meshy T2, Arc Relay Trip Node mine
Primary request: remove the central white spherical/domed lens completely. Replace it with a deep recessed flat triangular sensor aperture whose point faces East/right, containing only one narrow horizontal neutral-white light slit. Flatten and armor the surrounding centre so there is no ball, orb, pearl, bubble, or hemispherical form anywhere on the mine.
Preserve: exactly the same single mine, graphite and restrained bronze materials, three-prong silhouette, right-facing orientation, mild top-down oblique camera, scale, framing, and perfectly flat solid #ff00ff background.
Constraints: the sensor must read as an inset cutout/rune rather than a raised object; no cyan or amber team color; no cast/contact shadow, floor, range ring, smoke, explosion, text, logo, watermark; do not use #ff00ff on the object.
Avoid: every spherical or domed component, glowing ball, Core-like silhouette, vehicle parts, multiple objects.
```

## Sentinel Seed — source candidate

```text
Use case: stylized-concept
Asset type: clean single-object source image for Meshy T2 image-to-3D, Arc Relay Sentinel Seed stationary sentry
Input image: visual reference only for Nest's taupe layered armor, graphite mechanisms, compact hard-surface detail, and wedge-like directionality; do not reproduce the whole vehicle or its cluster of round cargo pods
Primary request: create exactly one compact planted sci-fi sentry that reads as an angular folded seed-pod opened into a gun. Use a broad low hexagonal/wedge base, two sharply faceted folded side leaves, a flat rectangular sensor aperture, and one short unmistakable barrel pointing East/right. The silhouette must be angular and directional, visibly different from a circular mine and from a mobile bot.
Style/medium: polished stylized 3D game-asset concept render, believable hard-surface depth, broad readable forms for a small gameplay camera
Composition/framing: exactly one object, centered, generous padding, square image, mild orthographic top-down oblique view consistent with the reference, barrel and forward direction point right
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background, uniform edge to edge
Lighting/mood: soft diffuse studio illumination on the object only; no baked spotlight or dramatic shadow
Color palette: low-chroma taupe ceramic armor, charcoal and gunmetal mechanisms, restrained weathered bronze fasteners, one narrow neutral-white sensor slit; no cyan team paint and no large amber/orange team-colored areas
Constraints: stationary planted hardware, compact within one tile, low enough not to obscure neighboring occupancy, clean silhouette; no cast shadow, contact shadow, floor plane, reflection, glow halo, range ring, projectile, smoke, text, logo, or watermark; do not use #ff00ff anywhere on the object
Avoid: all spherical parts, round balls, clustered pods, circular mine/puck silhouette, wheels, tracks, hover jets, walking legs, full vehicle chassis, long artillery barrel, multiple sentries, loose parts, scenery
```

This v1 result was superseded because its two tall armor leaves visually
trapped the barrel in a forward-only firing lane. That contradicted the
Sentinel's omnidirectional target behavior, so it is retained only as rejected
provenance and is not the proposed provider input.

## Sentinel Seed — omnidirectional revision candidate

```text
Use case: precise-object-edit
Asset type: revised clean single-object source image for Meshy T2, Arc Relay Sentinel Seed stationary sentry

Primary correction: make the sentry truthfully read as capable of firing in every horizontal direction. Remove or drastically shorten the two tall side armor leaves that currently fence the barrel into a forward-only lane. Rebuild the upper assembly as one compact freely rotating gun head mounted on a clearly visible circular 360-degree yaw bearing above the planted base. The short barrel points East/right only as its canonical resting pose, not as a fixed firing arc. Give the barrel and rotating head visibly unobstructed clearance around the entire horizon: no rear stop, no tall side walls, no armored tunnel, no fixed casemate. Replace the tall leaves with low symmetric shoulder guards below the barrel sweep, or very small folded fins attached to the rotating head that cannot block its rotation.

Preserve: exactly one stationary sentry; the broad angular planted base; taupe ceramic armor, charcoal/gunmetal mechanisms and restrained weathered bronze fasteners; the narrow flat neutral-white sensor slit; the mild orthographic top-down oblique camera; the same overall scale, center framing, generous padding, and perfectly flat solid #ff00ff chroma-key background.

Gameplay read: compact automated low-hull short-range guard, planted and destructible, angular rather than circular; the circular bearing communicates yaw motion but the overall footprint must not resemble a mine.

Constraints: compact within one tile; low enough not to obscure neighboring occupancy; clean broad silhouette readable at small gameplay scale; no baked team cyan or amber; no cast shadow, contact shadow, floor plane, reflection, glow halo, range ring, target line, projectile, muzzle flash, smoke, text, logo, or watermark; do not use #ff00ff anywhere on the object.

Avoid: fixed forward firing arc, tall enclosing armor leaves, rear wall behind the gun, all spherical parts, round balls, clustered cargo pods, circular mine/puck silhouette, wheels, tracks, hover jets, walking legs, full vehicle chassis, long artillery barrel, multiple objects, loose parts, scenery.
```
