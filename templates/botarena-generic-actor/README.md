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

# Cumulative WASM-only fundamentals qualification:
nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm \
  --suite frontline-qualification-3
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

The generated bot is a competent apprentice rather than a blank or solved
policy. Its short `Tick` priority list promptly activates an available
companion, leaves an imminent straight-projectile path, takes a clear direct
shot, and pathfinds around walls toward the active objective. Its dodge helper
projects a visible projectile's current heading across two advances, and each
life avoids its just-vacated dodge tile for one further tick so objective
pathing cannot immediately step back into the same shot. Those contract-driven
building blocks live in `ArenaBasics.cs`; keep or replace them as your policy
develops.

The immediate evasion is intentionally baseline boilerplate. The interesting
work starts with deciding when moving costs too much territory or firing
tempo, and with using multiple bodies and programmed trajectories to force
those choices on an opponent.

The starter deliberately does not assign body roles, choose transformations,
coordinate focus fire, program curved-shot traps, remember opponents, or adapt
its doctrine. `BOTNAME.cs` is the intended first editing surface: reorder its
priorities, add conditions, and use actor/unit identity plus shared
observations to make the independent lives cooperate.

The qualification command is local and unranked. Current suite 3 runs both
assignments in WASM and checks contract/count handling, deterministic replay
hashes, useful automatic children, objective movement/holding, direct fire,
straight-projectile evasion, and explicit Fabricate. A complete pass awards
T2 under the pinned duel-depth union profile. It remains authoring/fun-floor
evidence (`balanceEvidenceEligible: false`); numeric balance voting starts at
the separately qualified T4 floor.

After the starter passes suite 3, suite 4 is the next tactical target. It
automatically reruns that exact T2 prerequisite, then checks legal curved
intercepts, strict wall termination, declared projectile range/cadence,
cooldown tempo, and local transform safety. The generated starter is
intentionally not claimed to pass it.

`ArenaBasics` demonstrates the important authoring pattern: select actions by
their contract kind or stable ID, use the negotiated numeric code, and obey
the current typed legality constraints. The host still resolves joint
conflicts, so an individually available move or transition can be blocked by
another body's simultaneous choice.

Current Frontline Labs v1 includes `fabricate`, `split`, `transform`, and
`shoot-direction` in addition to movement, rotation, shooting, and waiting.
Do not assume all forms expose all actions. See
`docs/FRONTLINE-LABS-RULES.md` in a source checkout for the standalone Labs-v1
rule card and `docs/EXPERIMENTAL-FRONTLINE.md` for its product/authoring
boundary. The exact contract delivered to the bot and embedded in the replay
remains authoritative.
