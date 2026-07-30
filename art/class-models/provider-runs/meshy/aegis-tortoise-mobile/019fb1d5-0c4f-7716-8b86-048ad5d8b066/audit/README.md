# Aegis Tortoise raw-provider audit

White is rigidly aligned overlap, red is provider-only silhouette, and cyan is
reference-only silhouette in `overlay-clean.png` and `overlay-concept.png`.
No rotation other than the measured fixed 180-degree facing correction, no
reflection, and no non-uniform warp were allowed.

The raw model is a 97.06% top-silhouette match to the rich 2D authority. Its
side is not accepted: it is approximately 41% too thick relative to length,
the four low piston feet have collapsed into the lower slab, and the cannon
does not project cleanly in side silhouette. It is therefore an excellent
high-detail armor/top master, not a walkable or runtime Bulwark.

The component images color loose indexed islands for diagnosis. Exact-position
welding proves those 268 islands are UV/normal seams within one watertight
manifold, not usable logical leg parts. Blender must cut/reconstruct and rig
four rigid leg hierarchies rather than parenting provider fragments.

See `../geometry-report.json` for the measured record.
