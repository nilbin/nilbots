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

## class-first-class handoff: consumed

`codex/class-first-class` has been integrated. Its content landed, its
packaging did not: the branch minted a `generic-actor-match-3` profile, and the
merge folded the same facts additively onto `generic-actor-match-2` instead.

- Typed class identity and spawn-reservation observability shipped. Class IDs
  reach the canonical contract topology (emitted only when a ruleset declares
  classes, per the #156 additive-canonical pattern) and the observation (self,
  allies, visible enemies, participant status). A visible tile publishes the
  automatic-return, fabrication, or replication claim that reserves it.
- The duplicate hold and projectile encodings did not ship. The already-landed
  `holdOwnerTeamId`/`holdEndsAtTick` pair and `TicksPerAdvance`/`DamagePerHit`
  are the single encoding of those facts; the branch's `holdRemainingTicks` and
  `damage` spellings were dropped and their mirrors retargeted.
- No second contract generation, no second registered hosted playlist version.
  `generic-actor-match-2` remains the one generic lineage, the pinned
  `frontline-labs-1` match fingerprint
  `cf10fe4929d8cd11cace95e62b07d9732fbd1549dc2e9fe096f78605028ca837` is
  unchanged, and the frozen phase-1 artifact population keeps playing every
  class-free contract it could play before.
- The branch's document-layer verification was kept and strengthened: the
  replay-v3 verifier now re-derives each visible spawn reservation and each
  visible projectile from the authoritative pre-state and the embedded attack
  profile, and cross-checks observed class identity against the contract.
- Which contract generation the phase-2 arms declare is unchanged by this
  merge: they declare classes, so they are new content-identified rulesets on
  profile 2. No DECISIONS number was minted here.
