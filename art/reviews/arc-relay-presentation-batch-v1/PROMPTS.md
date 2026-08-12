# Concept-board generation record

## Shared reference

All boards used this local class-roster sheet as a visual reference for class
identity, materials, scale, camera, and the dark Tapline floor only:

`art/class-look-concepts/arc-relay/meshy7-hover-roster-v1/concept-sheet-v7.png`

SHA-256:
`3d60322ef767283da55dc3710af9ef097ed989bdd40cc597883925989af486d5`

The reference did not define game mechanics. Prompts explicitly requested
three sequential frames per effect, matte industrial sci-fi surfaces, a fixed
three-quarter arena camera, restrained cyan/violet/brass energy, readable
floor contact, no text, no UI chrome, and no scenery outside the play surface.

Generation tool: built-in `image_gen.imagegen`. The original tool outputs are
preserved outside the repository under the Codex generated-image store; exact
copied output hashes are in `manifest.json`.

## Board 01 — high-impact signatures

Prompt specification:

> Create one square 4-row by 3-column sequential concept sheet on the dark
> isometric arena floor. Each row is start, action, resolve. Row 1: Towline
> Tractor Hook, showing frozen line, traveling grapple, then taut cable. Row 2:
> Mortar Falling Star, showing cardinal reticle, descending dark capsule, and
> floor impact. Row 3: Longshot Rail Line, showing narrow charge lane, broad
> low pressure discharge, and afterimage. Row 4: Mason Hardlight Block, showing
> floor frame, stacked slabs rising, and a solid one-tile block. Matte,
> restrained orbital machinery; no labels, HUD, explosions, or glossy toy
> finish.

Production overrides the generated Falling Star light column, bright Rail, and
Hardlight gloss as documented in `CONCEPT-SPEC.md`.

## Board 02 — support and fields

Prompt specification:

> Create one wide 4-row by 3-column sequential concept sheet. Row 1: Patchbay
> Repair Beam from link formation to braided quiet channel to repaired seam
> ring. Row 2: Lantern Survey Flare from launch to in-flight flare to broad
> calibrated reveal ring. Row 3: Hush Null Field from inward boundary tell to
> dark suppression lens to fading boundary. Row 4: Veil Smoke from deployment
> to field growth to stable coverage. Use class materials from the reference,
> sparse low effects, no bright lasers, no text, no UI, no fantasy magic.

Production flattens Null, lowers Smoke, and removes Smoke's generated travel
arc because the replay has no smoke in-flight phase.

## Board 03 — motion and objective

Prompt specification:

> Create one wide 3-row by 3-column sequential concept sheet. Row 1: Kestrel
> Vector Dash with floor arrow, one real moving body and short wake, then a
> pressure resolve. Row 2: Palisade Prism Wall with exact contact lines, three
> rising panels, then stable wall. Row 3: Relay Arc Toss with one neutral Core,
> landing tell, visible arc, and settled Core. Never duplicate bodies or the
> Core. Matte industrial sci-fi, fixed arena camera, no labels or HUD.

## Board 04 — control and level-up

Prompt specification:

> Create one square 4-row by 3-column sequential concept sheet. Row 1:
> Switchback Exchange with paired endpoint brackets, complementary transfer
> ribbons, and settled swapped bodies. Row 2: Sunder Target Paint with closing
> brackets and three persistent tally bars. Row 3: Repulsor Kinetic Burst with
> contracting floor ring, low expanding torus, and directional body wakes. Row
> 4: level up with brass ring, three rising facets, and a new persistent brass
> chevron. Restrained, matte, no text, HUD, bloom blast, or mesh deformation.

Production omits the board's full-height level-up glow.

## Board 05 — class projectile heads, accepted direction

Prompt specification:

> Create a square 4-by-4 catalog of sixteen compact regular-attack projectile
> heads in exact roster order: Kestrel, Palisade, Towline, Patchbay; Lantern,
> Mortar, Minesmith, Hush; Relay, Switchback, Longshot, Mason; Sunder, Repulsor,
> Veil, Nest. Each must be a small east-facing munition head or energy carrier,
> not a miniature bot: one dominant silhouette, limited relief, no wheels,
> cockpit, antenna, or full chassis. Use the class material language only as
> identity reference. Dark neutral cells, no labels, trails, glow bloom, or HUD.

Runtime uses the silhouettes as SVG-derived 3D forms and recolours them with
team accent.

## Rejected projectile iteration

The first projectile prompt asked for too much direct class-hardware lineage.
Its output read as sixteen miniature bots. It is preserved at
`rejected/projectile-mini-bots-v1.png` for provenance and explicitly excluded
from approval.
