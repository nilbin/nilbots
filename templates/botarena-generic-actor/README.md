# BOTNAME

An experimental `IGenericActorBot` for Frontline Labs. The same programming
model is designed for variable team sizes, unit counts, match formats, maps,
and future action/form catalogs; read those values from `StartLife.Contract`
and the per-tick legality masks instead of assuming current counts or numeric
action codes.

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

The starter's `Choose` helper demonstrates the important authoring pattern:
look up the current action by stable ID, take its negotiated numeric code, and
only submit it when available. Typed constraints on that legality entry list
the currently legal directions, targets, forms, headings, or shot-program
payload. The host still resolves joint conflicts, so an individually available
move or transition can be blocked by another body's simultaneous choice.

Current Frontline Labs v1 includes `fabricate`, `split`, `transform`, and
`shoot-direction` in addition to movement, rotation, shooting, and waiting.
Do not assume all forms expose all actions. See
`docs/FRONTLINE-LABS-RULES.md` in a source checkout for the standalone Labs-v1
rule card and `docs/EXPERIMENTAL-FRONTLINE.md` for its product/authoring
boundary. The exact contract delivered to the bot and embedded in the replay
remains authoritative.
