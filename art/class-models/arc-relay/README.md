# Arc Relay 3D fleet sources

The production WebGL companions preserve the geometry of the sixteen owner-approved
Meshy T2 `smart-topology` candidates recorded in
`art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json`.
That audit—not the older temporary review build—is the orientation and artifact
authority. Mason uses its audited pilot file, Lantern and Mortar use their
identity-normalized files, and the other thirteen use their lay-flat-X normalized
files. A fixed-camera nose audit additionally applies baked 180° facing corrections
to Kestrel and Mortar. Kestrel's nose was reversed. Mortar uses the explicitly
selected reroll task `019fc80a-8b00-783e-af88-a60d3dd45773`, whose raised tube is
substantially closer to the canonical silhouette and projects forward-right after
the correction.

Prepare an explicit provider task without relying on directory enumeration:

```sh
node scripts/class-models/prepare-meshy-candidate.mjs \
  --look arc-mortar \
  --task 019fc80a-8b00-783e-af88-a60d3dd45773 \
  --orientation identity \
  --facing-yaw 180 \
  --target-span 0.99 \
  --texture-size 1024
```

Build the deterministic runtime texture tier with glTF-Transform 4.4.2 and the
official KTX-Software 4.4.2 `toktx`, then promote or verify it from the repository
root:

```sh
node scripts/class-models/build-arc-runtime-texture-tier.mjs \
  --toktx /path/to/ktx-tools-4.4.2/bin/toktx \
  --gltf-transform /path/to/gltf-transform-4.4.2
node scripts/class-models/build-arc-runtime-texture-tier.mjs --check
node scripts/class-models/promote-meshy-arc-fleet.mjs
node scripts/class-models/promote-meshy-arc-fleet.mjs --check
```

Neither command calls Meshy. The tier builder retains the approved candidate's exact
accessor values, topology, normalization, and baked facing correction, replacing only
its embedded images from the normalized provider masters. The selective mipmapped
contract is 512 ETC1S base color, 256 UASTC normal, 256 UASTC metallic/roughness,
and 128 ETC1S emissive. Its complete audit lives at
`art/class-models/runtime-tiers/arc-relay/ktx2-selective-v1/audit.json`.

The promotion command verifies provider approval, geometry fingerprint, texture
contract, memory ledger, orientation, facing, scale, axes, and floor normalization
before copying the audited derivative beside each runtime look. It regenerates every
`model3d.json` and this directory's `ledger.json` without timestamps. Both `--check`
modes need no authoring tools and prove the checked-in runtime tier is current.

The approved provider models are monolithic: one mesh material, four embedded
textures, no named mechanical nodes, and no model-owned team paint. Runtime images
use `KHR_texture_basisu`; the original WebP candidates remain the appearance and
orientation authority, not the shipping payload. This is
intentional. Renderer-owned root lean, bank, pitch, follow-through, wake, exhaust,
and cooldown vents remain active; only per-node hardware, wheel, idle-part, and
model-emissive animation is unavailable. Team ownership remains renderer UI/effect
language and must not be manufactured by repainting or splitting these assets.

`fleet.json`, `sources/`, and `scripts/build-arc-relay-models.mjs` retain the older
provider-free named-group vector fleet as reproducible fallback and historical
evidence. They are no longer the production runtime source. Do not run that legacy
generator as a promotion step, and do not hand-edit production GLBs, manifests, or
the promoted fleet ledger.
