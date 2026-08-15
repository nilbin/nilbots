# Meshy 7 complete veterancy split-diadem tiers

Status: **runtime-promoted for owner gameplay review**. These five static models
replace the rejected reusable-feather renderer composition, but remain on the
veterancy worktree until the owner reviews the gameplay gallery.

Each rank is one complete two-part Meshy 7 ornament generated from four coherent
views. Post-processing is deterministic:

- translate the disconnected halves outward, without scaling them, until the
  full-height centre gap is at least 34% of the final lateral span;
- normalize upright `+Y`, rotate the provider width into the Nilbots `+X`
  facing contract, and preserve progressively larger rank spans and heights;
- floor authored roughness at 0.78, quantize geometry, and encode four embedded
  PBR maps as WebP;
- build a rich 512 px review tier and a promoted lean 256 px tier per rank.

The five lean GLBs total 1,504,868 transfer bytes. Individual ranks range from
166,080 to 457,932 bytes and 6,238 to 19,832 triangles. Runtime requests only
the current rank; it does not eagerly load the complete set. The renderer
selects one whole model, follows chassis yaw, and leaves the Core pickup,
handoff, drop, and Arc Toss animation untouched.

Rebuild and verify:

```sh
node scripts/class-models/build-veterancy-whole-diadem-runtime-tiers.mjs
node scripts/class-models/build-veterancy-whole-diadem-runtime-tiers.mjs --check --no-build
node scripts/class-models/promote-veterancy-whole-diadem-v1.mjs --check
```

Exact provider task IDs, transforms, budgets, memory estimates, and hashes are
recorded in [audit.json](audit.json); exact runtime copies are recorded in
[promotion.json](promotion.json).
