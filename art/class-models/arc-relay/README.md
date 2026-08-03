# Arc Relay 3D fleet sources

The production WebGL companions are the sixteen owner-approved Meshy T2
`smart-topology` candidates recorded in
`art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json`.
That audit—not the older temporary review build—is the orientation and artifact
authority. Mason uses its audited pilot file, Lantern and Mortar use their
identity-normalized files, and the other thirteen use their lay-flat-X normalized
files. A fixed-camera nose audit additionally applies baked facing corrections:
180° for Kestrel and 90° for Mortar. Kestrel's nose was reversed, while Mortar's
cannon pointed away from the replay camera instead of along authoritative `+X`.

Promote or verify the exact approved bytes from the repository root:

```sh
node scripts/class-models/promote-meshy-arc-fleet.mjs
node scripts/class-models/promote-meshy-arc-fleet.mjs --check
```

The command never calls Meshy and never regenerates provider geometry. It verifies each
candidate's audited SHA-256, byte count, topology/material counts, orientation,
facing correction, scale, axes, and floor normalization before copying it beside the runtime look.
It also regenerates each `model3d.json` and this directory's `ledger.json` without
timestamps. The check mode proves all runtime GLBs are byte-identical to those
approved, facing-corrected candidates and that the manifests and fleet ledger are current.

The approved provider models are monolithic: one mesh material, four embedded
WebP textures, no named mechanical nodes, and no model-owned team paint. This is
intentional. Renderer-owned root lean, bank, pitch, follow-through, wake, exhaust,
and cooldown vents remain active; only per-node hardware, wheel, idle-part, and
model-emissive animation is unavailable. Team ownership remains renderer UI/effect
language and must not be manufactured by repainting or splitting these assets.

`fleet.json`, `sources/`, and `scripts/build-arc-relay-models.mjs` retain the older
provider-free named-group vector fleet as reproducible fallback and historical
evidence. They are no longer the production runtime source. Do not run that legacy
generator as a promotion step, and do not hand-edit production GLBs, manifests, or
the promoted fleet ledger.
