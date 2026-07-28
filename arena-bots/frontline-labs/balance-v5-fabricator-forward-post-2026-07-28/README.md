# Frontline Labs balance v5: Fabricator forward post

This is a post-reveal, single-variable population-balance arm.

- Fabricator assigns exactly one surplus mobile body to the current
  objective's perimeter tile nearest the next contract-defined objective, but
  only after an uncontested own claim reaches half threshold and another body
  remains on point.
- Pressure, Bastion, and Adapter are copied byte-for-byte from baseline v2 and
  remain controls. Rejected v3/v4 candidates remain archived.
- Rules, map, fabrication values, formation behavior for every other body,
  playlist, and contract fingerprints remain frozen.
- The sprint uses seed `104729` with mirrored assignments: 12 matches.

The arm tests whether Fabricator's near-breach timeouts come from local
formation success that fails to stage the next push.

Acceptance criteria:

- all 12 matches verify with zero faults;
- six non-Fabricator games retain their baseline replay hashes;
- the branch activates only under its registered half-threshold/controller
  conditions and selects exactly one forward perimeter body;
- no more than one game in each Fabricator/Adapter and Fabricator/Bastion
  mirrored pair reaches MaxTicks;
- Fabricator/Adapter combined terminal progress improves on baseline `-56`;
- Fabricator/Bastion combined terminal progress is positive rather than `0`;
- Fabricator retains at least three points across its six games.

If both relevant mirrored pairs still produce four MaxTicks games, forward
staging is not the cause of Fabricator's conversion problem.
