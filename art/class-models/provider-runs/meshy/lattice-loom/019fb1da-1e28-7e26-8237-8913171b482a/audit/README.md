# Lattice Loom raw-provider audit

White is rigidly aligned overlap, red is provider-only silhouette, and cyan is
reference-only silhouette in `overlay-clean.png` and `overlay-concept.png`.
Only the measured fixed 180-degree facing correction, uniform scale, and
translation were allowed.

The raw model preserves the low-hover/no-feet class, open diamond truss,
honeycomb core, four coils, paired fork, and exactly four valid cyan
shoulder/fork-root panels. It reaches 93.62% outer IoU to the clean top and
90.67% to the rich 2D authority. The forward span is short, the body is
slightly too tall/narrow, and the internal fork/truss geometry drifts; those
are targeted retopology/rebuild corrections, not permission to globally warp
the mesh.

The 732 loose indexed islands in the component images are UV/normal seam
splits. Exact-position welding yields one watertight manifold, so they are not
logical fork/truss parts. Root-level hover motion needs no skin, but semantic
materials, optimization, fork/truss correction, and underside hover emitters
still require authored runtime work.

See `../geometry-report.json` for the measured record.
