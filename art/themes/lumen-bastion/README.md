# Lumen Bastion

Lumen Bastion is an optional bright Arc Relay visual experiment. Ember Forge
remains the default; a replay must explicitly name `lumen-bastion` in its
presentation header to use this package. Presentation changes replay identity
only: rules, fingerprints, map geometry, collision, vision, Wells, Reactors,
lanes, and team colours are unchanged.

## Direction

The arena is deliberately high key: warm-white ceramic composite carries the
large masses, light grey defines structure, and satin silver is limited to
reinforcement and service seams. Small graphite recesses exist only where a
light or vent needs contrast. Actor-owned teal and orange remain absent from
neutral architecture.

Sparse periwinkle-white lighting uses the existing declarative environment
hooks. Side vents, raised-module seams, occasional tower cores, and perimeter
light apertures are genuinely emissive in WebGL; the generated material fields
carry matching diffused inlays for Canvas2D. No light changes visibility or
marks walkable space.

The 3D scene retains Arc Relay's 12-degree yaw, 52-degree pitch, and 28-degree
lens. It reuses the topology-derived bastion grammar with a cleaner silhouette:
white shoulders and towers, pale service caps, no chimney cadence, and a
1.6-tile non-playable perimeter apron. All in-map geometry remains inside the
authoritative blocked footprint.

## Sources and production

The floor, perimeter, and cover are separate opaque material fields generated
with Codex's built-in image tool. Their exact accepted prompts are retained in
`art/themes/SOURCE-PROMPTS.md`. `art.json` owns the deterministic seamless
normalization, PBR derivation, topology atlases, runtime encodes, and 5 MB
theme budget.

Accepted source SHA-256 fingerprints:

- floor: `d1a4ebdfb350681de033db6300b7c4a590c9b1c691e903fe0b7b0d272676370c`
- perimeter: `14f794bf7695944101c48532b6c0e76ae95965f13f7440fef5095681a3e52625`
- cover: `5deb31ae947e76eac44841e1bc7d7ed7d21f07dae7c0dd0d2d62381a7521c874`

The visual-review replay was temporarily and explicitly bound to Lumen Bastion.
It is a native `tapline-3` match on
`arc-relay-tapline-01`, under ruleset `arc-relay-tapline-03`, seed `9001`.
Its canonical replay hash is
`a93fe7b0a08997ffce4b7954f33fd2834b2b817208e51ccd8e03b037eb636bef`.
`review-canvas-tapline-3.png` is the deterministic Canvas2D overview captured
from that replay at tick 175; WebGL geometry and emission are covered by the
theme scene test.
