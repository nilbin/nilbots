# Perimeter straight v1 Meshy inputs

## Gate

These are four separate 1536×1536 views of one isolated, compact perimeter
straight module:

| View | SHA-256 |
| --- | --- |
| `front.png` | `43c0fa795f292ba319b41dbfd0378156fd9ed8874860e1703565f75b78753931` |
| `side.png` | `8734e2a9bb8c885f0340dbf33592d76286e26335e418fd95345ff8d7ce639098` |
| `rear.png` | `910765555a52107b08e62547e8328d78a83a0a68115d7b7662c9bca3369bc53a` |
| `top.png` | `c135ad06a50e32356a6ecfa9d518f7a67827e2109e31e5af89a5c2049312b42c` |

The source sheet was corrected before any paid provider call:

1. the first version's terminal pylons failed the repeatability gate;
2. the next version fixed the ends but described a long multi-tile barricade;
3. the selected source is a compact one-cell module with flat mating ends.

The two rejected-but-useful designs remain under `iterations/`. They are
concept evidence, not provider inputs.

The selected geometry is intentionally thinner than a full square cell in the
side/top views. It is a perimeter face module, not collision authority. The
topology-derived continuous substrate remains responsible for the
authoritative wall footprint, while this result is evaluated as a detailed
repeatable shell.

## Source generation prompts

### Initial four-view sheet

```text
Create a clean orthographic four-view asset design sheet for exactly ONE
reusable FRONTLINE PERIMETER STRAIGHT WALL MODULE, using the attached approved
arena concept only as the shape, material, and detailing reference. The same
identical object must appear in all four panels, at the same scale and under
identical neutral studio lighting. Arrange a precise 2x2 grid with generous
separation: upper-left = gameplay-side three-quarter oblique, upper-right =
strict left side orthographic, lower-left = exterior/rear orthographic,
lower-right = strict top orthographic. The module spans exactly one square
collision tile: length X 1.00, depth Z 0.88, height Y 0.72. Its cut ends at
X=-0.5 and X=+0.5 are flat, vertical, undecorated mating planes so repeated
instances connect seamlessly. Design: compact cast sci-fi foundry bastion;
subtly bowed/swept long silhouette rather than a box; broad grounded foot;
layered graphite-black armor; softly chamfered corners; a restrained
aged-bronze upper wear plate; recessed horizontal vent bank on the gameplay
face; tiny dim warm-amber maintenance recesses only; believable panel seams
and coarse wear; substantial actual depth, not a 2D extrusion. Rough matte PBR
appearance, low saturation and low contrast, less polished and less emissive
than a hero bot. No blue, cyan, red, purple, or team colors. No floor slab,
terrain, wall corner, doorway, turret, weapon, pipes extending beyond the
footprint, text, labels, dimension arrows, people, bots, projectiles, or extra
objects. Pale neutral gray seamless background in every panel. Maintain exact
geometry, proportions, panel placement, vent count, and wear-plate placement
across all four views. Asset-production reference sheet, not a cinematic
scene.
```

### Repeatable-end correction

```text
Controlled production correction of the supplied four-view turnaround sheet.
Keep the exact same single wall module, all four panel cameras, proportions,
graphite/aged-bronze matte materials, center vent bank, lighting, and pale gray
backgrounds. Change ONLY the two ends so this is a repeatable straight-run
module: REMOVE both large raised end towers / terminal pylons / flared end-cap
buttresses. The wall body and broad grounded foot must instead continue
cleanly to X=-0.5 and X=+0.5 and terminate at two simple flat vertical
rectangular mating planes with the same cross-section as the middle body. The
mating ends must have no cap, nose, protruding shoulder, oversized foot,
unique terminal ornament, or silhouette bulge; repeated copies placed
end-to-end should form one continuous wall with only a narrow recessed seam.
A small half-width bronze clamp at each edge is acceptable only if two
neighboring copies visually complete one clamp. Preserve the gently bowed
long faces, chamfered top shoulders, vent bank, recessed amber maintenance
slots, rough weathering, and actual depth. The lower-left remains strict
gameplay-front orthographic, upper-right strict side orthographic, lower-right
strict top orthographic, upper-left gameplay oblique. Same identical corrected
object in all panels. No redesign, no text, labels, floor, extra objects, or
team colors.
```

### One-cell proportion correction

```text
Production proportion correction only. Keep the exact four-view sheet, exact
same flat-ended repeatable straight-wall design, matte graphite/aged-bronze
materials, vent and maintenance-light language, lighting, and backgrounds.
SHORTEN the module along its long X axis so it visibly occupies ONE SQUARE
GAMEPLAY TILE: in the strict front view its overall width-to-height ratio must
be exactly about 1.40:1 (width 1.00 tile, height 0.72 tile), not a long
multi-tile wall; in strict top view its overall length-to-depth ratio must be
about 1.14:1 (length 1.00 tile, depth 0.88 tile). Preserve flat vertical mating
faces at both X ends and no terminal pylons or flared end caps. Recompose the
detailing for the shorter body: 5-7 central vent slots, one compact
aged-bronze wear plate, 3-5 tiny dim amber maintenance recesses, broad readable
panels rather than squeezing the old details. The identical shortened compact
module appears at the same scale in all four views: upper-left gameplay
oblique, upper-right strict side, lower-left strict front, lower-right strict
top. This must look like a substantial almost-square wall cell that repeats
seamlessly into a run, not a long barricade. No text, labels, dimensions,
floor, extra objects, team colors, redesign, or camera changes.
```

### Rear completion

```text
Render the exact same compact one-tile perimeter straight wall module from the
supplied corrected four-view sheet in one STRICT REAR / EXTERIOR ORTHOGRAPHIC
view. This is the opposite face, not the vented gameplay front. Lock the
reference silhouette and proportions exactly: overall width-to-height about
1.40:1, depth about 0.88 of width, flat vertical mating faces on both
left/right ends, no terminal pylons, no end caps, no flared feet. Preserve the
top bronze wear plate position visible in the top view, graphite matte
cast-metal body, panel seams, chamfered shoulders, and tiny dim amber edge
recesses. Rear face is simpler than front: three broad recessed armored
service panels, restrained horizontal ribs, no large vent bank. Center one
isolated object, straight-on, no perspective, same scale and neutral studio
lighting, pale neutral gray seamless background. No ground slab, corner piece,
text, labels, dimensions, extra objects, team colors, or redesign.
```
