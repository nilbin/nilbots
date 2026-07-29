# Class-skins integration notes

- The selected class defaults are internal form presentation, not account
  cosmetics: Trident Wasp + Trident Spark, Aegis Tortoise + Rebound Diamond,
  and Lattice Loom + Lattice Rivet. Rendering them never depends on ownership.
- The owner approved the other six concept pairs as live purchase packs. That
  approval supersedes the older “Aureate Warden is the only chassis manifest
  with a recommended projectile” presentation invariant. Integration should
  reconcile that prose in the numbered decision log; this branch deliberately
  does not mint a number.
- Alternate manifests expose presentation-only `classId`, but current account
  appearance persistence has no class-compatibility field or enforcement.
  Purchased looks therefore remain globally equipable until the first-class
  class work supplies an end-to-end policy. No schema was added on this branch.
