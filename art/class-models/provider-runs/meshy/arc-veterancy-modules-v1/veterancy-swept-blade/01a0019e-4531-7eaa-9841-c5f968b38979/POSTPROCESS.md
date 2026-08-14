# Swept-blade pilot post-processing

The provider master and texture downloads remain local/ignored under this run
directory according to `art/class-models/provider-runs/.gitignore`. Their hashes
and measurements are committed in `artifacts.json`, `raw-structure-audit.json`,
and the static review audit.

## Rich normalization

```sh
node scripts/class-models/normalize-provider-glb.mjs \
  art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/veterancy-swept-blade/01a0019e-4531-7eaa-9841-c5f968b38979/raw-model.glb \
  art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/veterancy-swept-blade/01a0019e-4531-7eaa-9841-c5f968b38979/model-normalized-rich-provider-facing-review.glb \
  0.36 identity --strip-authored-tangents
```

This produced +Y-up, floor-Y-zero bounds with a `0.36`-tile maximum planform
span. No geometry, UV, material, or texture optimization was performed.

## Facing correction

The first fit board showed the broad luminous cap pointing forward and reading
like a muzzle. The concept's narrow clipped end is the intended `+X`/front end,
so the accepted review candidate applies a deterministic world-Y correction:

```sh
node scripts/class-models/correct-provider-facing.mjs \
  art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/veterancy-swept-blade/01a0019e-4531-7eaa-9841-c5f968b38979/model-normalized-rich-provider-facing-review.glb \
  art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/veterancy-swept-blade/01a0019e-4531-7eaa-9841-c5f968b38979/model-normalized-rich-review.glb \
  180
```

Both normalized GLBs are ignored provider-review artifacts, not application
assets. `normalization.json` and `facing-correction.json` retain their byte and
hash evidence.

## Structure and static review

```sh
node scripts/class-models/audit-provider-glb-structure.mjs \
  art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/veterancy-swept-blade/01a0019e-4531-7eaa-9841-c5f968b38979/raw-model.glb \
  art/reviews/arc-relay-veterancy-modules-meshy7-v1/pilot/raw-structure-audit.json

node scripts/class-models/capture-veterancy-pilot-review.mjs \
  http://127.0.0.1:9201
```

The review command creates the deterministic six-view sheet, mirrored-pair
test, Kestrel and Longshot static fits, unsharpened 96-pixel readability samples,
and `pilot-audit.json`. It does not load the game, edit the runtime viewer, or run
the replay harness.
