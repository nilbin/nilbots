# Frontline modular 3D concept prompts

The built-in image-generation tool produced both owner-review concepts. Each
generation used four inputs:

1. `review/current/frontline-current-arena.png` — edit target and camera/layout
   reference;
2. `art/themes/ember-forge/floor/source.png` — floor material reference;
3. `art/themes/ember-forge/walls/perimeter/source.png` — perimeter material
   reference;
4. `art/themes/ember-forge/walls/cover/source.png` — cover material reference.

The source map and current renderer remain authoritative. Generated concepts
are intentionally not used as topology input.

## V1 — conservative modular kit

```text
Use case: precise-object-edit
Asset type: approval concept for a reusable modular 3D game-arena kit
Input images: Image 1 is the edit target and authoritative camera/layout/topology; Image 2 is the exact Ember Forge floor material language; Image 3 is the exact heavy perimeter material language; Image 4 is the exact lower interior-cover material language.
Primary request: upgrade only the arena architecture in Image 1 from boxy extrusions into a premium genuine-3D modular kit while preserving the exact visible blocked/open topology, camera, framing, bot positions, objective overlay, and every wall footprint. This is a visual direction frame, not a new map layout.
Floor: keep one continuous blackened forged-steel field from Image 2; give plate seams, hammered surfaces, restrained copper joinery, shallow grates and heat wear real physically lit depth without raising obstacles on walkable tiles.
Perimeter: make the outer boundary visibly heavier and taller than cover, using broad forged armor faces, thick rounded/chamfered caps, curved buttress-like shoulders, recessed copper clamps and sparse ember seams from Image 3. It must still occupy exactly the existing boundary-wall footprint.
Interior cover: replace the square-crate feeling with lower modular armored housings shaped as softened wedges, clipped corners, curved end caps and shallow sloped shoulders, using the graphite composite, copper cooling channels, vents and worn ochre ceramic from Image 4. Preserve every existing cover footprint and navigable gap exactly.
Style/medium: high-end stylized hard-surface 3D game environment render, physically based materials, strong readable forms at the existing gameplay camera distance, cohesive with the two bots.
Lighting/mood: preserve the current dark arena lighting direction; restrained warm forge bounce in seams, no lava or open flame.
Constraints: one deterministic repeatable kit language; no whole-map sculpture; no hand-placed decorative prop; no new wall, obstacle, pillar, rail, cable, rubble or collision-looking object; no removed wall; no changed corridor width; no changed camera; no changed UI/tick badge; no text; no logo; no watermark. Keep floor, perimeter and interior cover unmistakably separate semantic families.
Avoid: rows of plain rectangular crates, perfectly square wall corners everywhere, toy-block proportions, glossy plastic, dense greebles, noisy high-frequency detail, fantasy masonry, map-scale machinery, terrain deformation.
```

## V2 — cast foundry bastion

```text
Use case: precise-object-edit
Asset type: second approval concept for a deterministic modular 3D game-arena kit
Input images: Image 1 is the edit target and authoritative camera/layout/topology; Image 2 is the exact Ember Forge continuous floor material; Image 3 is the exact fortified perimeter material; Image 4 is the exact low-cover material.
Primary request: show a more assertively sculpted but still practical modular-kit alternative for the arena architecture in Image 1. Preserve the exact visible blocked/open topology, wall footprints, walkable gaps, camera, framing, bots and objective overlay. Upgrade architecture only.
Design direction: "cast foundry bastion." The perimeter is a continuous heavy blackened-iron bastion assembled from repeatable straight and corner modules, with thick radiused shoulders, broad curved cap plates, occasional recessed copper tension bands and shallow buttress ribs. It must clearly remain inside the existing one-tile boundary footprint. Interior cover is substantially lower and visually lighter: flattened cast-metal housings with convex curved end caps, chamfered waistlines, low sloped top plates, restrained vents, copper cooling channels and small worn ochre ceramic service panels. Straight runs connect cleanly without becoming rows of cubes.
Floor: retain a single continuous blackened forged-steel material field from Image 2, with physically lit plate seams, shallow hammered relief, sparse inset grates and copper joinery; no raised obstacle or prop on walkable floor.
Style/medium: premium stylized hard-surface 3D environment render for a top-down combat game, production-feasible modular geometry, PBR materials, readable at gameplay scale.
Lighting/mood: preserve the current dark directional arena lighting, with only restrained warm reflection from copper/heat-worn seams.
Constraints: this image describes reusable straight/end/corner modules selected from map topology; it is not a one-piece map mesh. No new obstacle, wall, rail, column, rubble, cable, machinery or prop. No removed wall. No changed corridor width. No layout/camera/UI changes. No text/logo/watermark. Perimeter and cover must be instantly distinguishable by height, mass and profile.
Avoid: square crates, uniformly rectangular boxes, toy blocks, cylindrical pipes as whole walls, excessive sci-fi greebles, glowing lava, open flame, fantasy stone, glossy plastic, terrain deformation, bespoke map sculpture.
```

## V3 — living bastion

V3 uses V2 as its edit target, the current gameplay render as topology/camera
reference, and the Ember Forge floor and cover sources as material references.

```text
Use case: precise-object-edit
Asset type: third owner-approval concept for a deterministic modular 3D arena kit
Input images: Image 1 is the preferred V2 art direction to revise; Image 2 is the authoritative gameplay camera and visible map topology; Image 3 is the exact Ember Forge floor material language; Image 4 is the exact cover material language.
Primary request: keep the strong Ember Forge material quality and exact visible blocked/open topology, but make the arena substantially less square and more alive than Image 1. Change only architectural profiles, surface rhythm, and flat floor wear. Preserve camera, framing, every wall/cover footprint, every navigable gap, bots, objective overlay, and the immediate truth that each occupied tile is blocked.
Perimeter: build a continuous heavy foundry bastion from repeatable topology modules, not rectangular blocks. Use alternating shallow convex and concave cap profiles, broad radiused shoulders, tapered buttress lobes contained inside the boundary footprint, occasional angled/scalloped corner transitions, asymmetrical forged plate breaks, recessed service channels, copper tension clamps, vents, and restrained ember heat wear. Allow subtle deterministic height/profile variation between compatible straight modules, but keep a clearly continuous collision boundary.
Interior cover: replace softened rectangles with a family of low armored foundry housings that have tapered waists, convex bowed sides, faceted/rounded nose-like end caps, clipped asymmetric shoulders, shallow saddle or spine top profiles, and occasional paired shell forms. Each unit must stay wholly inside the existing blocked-tile footprint and connect in deterministic straight/end/corner/junction arrangements. Use asymmetrical panel seams, sparse vents, copper cooling channels and small worn ochre ceramic service panels; maintain a lower silhouette than perimeter.
Floor: remain one continuous blackened forged-steel material field. Add only flat localized life: traffic-polished arcs through open lanes, soot gradients near cover, quenched blue/brown heat bloom, restrained copper repair seams, shallow inset drains or grates, and subtle material transitions that follow architectural zones without tracing a gameplay grid. Nothing on walkable floor may read as raised collision.
Style/medium: premium stylized hard-surface 3D game environment, physically based materials, authored modular environment art, readable at the existing gameplay distance.
Lighting/mood: preserve the current dark directional light; restrained warm bounce from worn copper and hot seams, no lava or flame.
Constraints: visual proof of a reusable instanced kit resolved from map JSON neighbour topology and family tags; not a whole-map mesh or hand-placed scene. No new obstacle, wall, prop, rail, cable, rubble, pillar or machinery. No removed wall. No changed corridor width. No changed map/camera/UI. No text, logo, watermark. Keep perimeter versus cover unmistakable by height and mass.
Avoid: rectangles with merely beveled corners, rows of crates, uniform module repetition, toy blocks, perfectly mirrored panel layouts, excessive greebles, random silhouette changes that obscure blocked tiles, terrain deformation, glossy plastic, fantasy stone.
```

## V4 — matte living bastion

V4 is a controlled material-only edit of V3. The Ember Forge floor, perimeter,
and cover sources are the remaining material references.

```text
Use case: precise-object-edit
Asset type: controlled material-correction approval concept for a modular 3D game arena
Input images: Image 1 is the exact edit target and provisionally approved geometry/layout/lighting composition; Image 2 is the desired dry forged floor material response; Image 3 is the desired rough perimeter material response; Image 4 is the desired matte cover material response.
Primary request: change only the material response and surface finish in Image 1. Preserve its exact less-square living-bastion silhouettes, curved/tapered wall profiles, asymmetric panel breaks, module positions, map layout, camera, framing, bots, objective overlay, lighting direction, and every navigable gap. Do not redesign or move geometry.
Materials: make the architecture predominantly matte charcoal and dry blackened iron with high roughness, micro-pitted forged surfaces, rough industrial coating, subtle soot/dust accumulation, and varied but restrained roughness between panels. Add worn exposed gunmetal only on high edges and contact points. Copper clamps and cooling channels are aged, oxidized, and mostly satin rather than polished. Ochre ceramic service panels are dry, chipped, and non-metallic. Allow localized satin metal on a few maintenance plates only.
Floor: keep a continuous dry blackened forged-steel field with dusty traffic polish, restrained quenched blue/brown heat bloom, soot gradients, rubbed edge wear and shallow grime in seams. It may have localized soft sheen from wear, never broad oily reflections or a wet look.
Emission: confine dim ember-orange emission to recessed vents, deep seams, and a few cooling slots. No broad glowing panels and no bloom washing over surfaces.
Lighting/mood: retain the same dark directional scene lighting and readable contrast, but make highlights broad, dim and rough instead of sharp/specular. Preserve natural shadow depth.
Constraints: material-only A/B; exact geometry/layout/camera invariants from Image 1. No new or removed module, obstacle, prop, wall, panel silhouette, decal object, rail, cable, rubble, pillar, machinery, text, logo or watermark.
Avoid: glossy plastic, wet metal, oily floor, mirrorlike black surfaces, broad white highlights, clearcoat, chrome, excessive bloom, neon outlines, lava, open flame, polished sci-fi showroom finish.
```
