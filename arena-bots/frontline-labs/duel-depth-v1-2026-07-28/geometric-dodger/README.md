# GeometricDodger

GeometricDodger is the retained public-geometry evasion baseline for the
Frontline Labs duel-depth micro-screen. It is intentionally the simple policy
the screen tries to falsify as universal.

For each hostile visible projectile, the bot extends only its currently
manifested heading through the public map for its remaining travel budget.
When its current tile lies on one of those paths, it chooses a legal adjacent
tile by:

1. avoiding an immediate advance;
2. minimizing the count of manifested paths crossing the destination;
3. staying on the active objective when already controlling it;
4. minimizing distance back to the active objective;
5. maximizing distance from visible projectile bodies.

Ordinary objective pathfinding also treats those manifested paths as blocked.
The bot does not infer a private future bend, remember an opponent, or model
opponent tendencies. It takes only clear straight shots: mobile fire follows
current facing with no shot program, while absolute-heading forms use their
public heading action. An available child may be fabricated, but every body
uses the same geometry policy and no body transforms or splits.

Frontline Labs has no built-in generic opponent, so point the command at two
generic bot projects or WASM artifacts:

```bash
nilbots experiment frontline-labs \
  --bot . \
  --opponent ../AnotherGenericBot \
  --runtime in-process \
  --seeds 104729,130363,155921

# Final parity check uses the same generic WASM runtime as hosted Labs:
nilbots build .
nilbots experiment frontline-labs \
  --bot out/bot.wasm \
  --opponent ../AnotherGenericBot/out/bot.wasm \
  --seed 104729
```

The command always selects the immutable `frontline-labs-1` definition and
writes canonical replay v3. It bypasses App authentication, queues, and pilot
quotas; it does not rank or submit either bot. `--swap` reverses participant
and team assignment. A batch `--out evidence` writes one replay under
`evidence/s<seed>/`.

Nilbots creates one independent instance of your class for every active life.
`StartLife` receives the exact static rules, map, topology, participant/team
counts, unit slots, forms, transitions, objective binding, lineage, and
private deterministic seed. `Tick` receives dynamic allies, visible enemies,
scores, objective state, and exact action legality for that body.

`ArenaBasics` selects actions through contract kinds and current legality,
uses negotiated numeric codes, and reads map/objective geometry from the
resolved contract. The host may still block an individually legal move during
joint resolution.
