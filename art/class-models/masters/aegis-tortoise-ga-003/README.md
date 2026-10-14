# Aegis Tortoise GA-003

GA-003 is the padded UV-only successor to GA-002. It preserves GA-002's exact
levitating geometry, hierarchy, pivots, transforms, semantic team emitters,
and all 14 Mobile/Shell/Tower clips. Only texture-eligible body UVs changed.

The body atlas is jointly area/gameplay weighted. Main-hull effective linear
texel density is 12.79× higher than GA-002, strict overlap count is zero, and
the eight renderer-owned team-accent meshes are excluded from the provider
upload. The upload uses a non-gameplay Shell+Tower coverage pose solely to
expose texture surfaces.

The coverage upload bakes each evaluated part's world transform into a
temporary identity-root mesh. Its validation reimports the GLB and proves all
30 named body parts and 29,994 triangles remain present, with the hover pods,
shell plates, and tower vanes at distinct positions. Never upload an export
that relies on omitted parent pivots to place repeated parts.

Do not replace geometry or animation with provider output. Use the archived
standalone PNG PBR payload through transplant schema v3, then validate the
result in the production WebGL replay before runtime acceptance.
