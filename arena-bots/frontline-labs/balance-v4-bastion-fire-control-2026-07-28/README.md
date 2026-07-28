# Frontline Labs balance v4: Bastion fire control

This is a post-reveal, single-variable population-balance arm.

- Bastion changes only turret target validation and ordering: exact eight-way
  ray, contract-provided range, clear walls/corners, then objective controller,
  health, distance, and actor identity.
- Pressure, Fabricator, and Adapter are copied byte-for-byte from baseline v2
  and remain controls. The rejected v3 Pressure candidate remains archived but
  is not promoted into this arm.
- Rules, map, turret statistics, Anchor placement, mobile allocation, playlist,
  and all contract fingerprints remain frozen.
- The sprint uses seed `104729` with mirrored assignments: 12 matches.

The arm tests whether Bastion's repeated stalls are caused by approximate
turret aiming rather than weak turret numerics or map geometry.

Acceptance criteria:

- all 12 matches verify with zero faults;
- Anchor still completes in both Bastion assignments against every opponent;
- every submitted turret shot uses an exact, in-range, clear ray at submission;
- the six matches without Bastion retain their baseline replay hashes;
- the mirrored Bastion/Fabricator pair is no longer two 0-0 MaxTicks draws:
  at least one is decisive without Bastion losing the other;
- Bastion retains at least four points across its six games;
- no both-turret deadlock is introduced.

If exact targeting activates but the Bastion/Fabricator pair remains two 0-0
MaxTicks draws, fire-control correctness is not the cause of its conversion
problem.
