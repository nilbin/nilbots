# Frontline Labs bot-author packet

Status: frozen common input for the first independently authored
`frontline-labs-1` calibration cohort.

## Purpose

Create one deterministic, contract-driven bot that expresses the assigned
doctrine: an `IGenericMindBot` on the mind profile (one program driving every
body the participant owns, for the whole match) or an `IGenericActorBot` on the
per-life profile (one independent instance per body life). Your assignment names
which. This is a one-pass authorship exercise, not an
iterative tournament optimization exercise.

Every cohort author receives this same packet and one doctrine sentence.
Authors do not receive another entrant's source, tournament results, selected
replays, balance analysis, or post-match coaching before artifacts are frozen.

## Permitted authoring material

Use only:

- this packet and the assigned doctrine sentence;
- the generated `nilbots new <Name> --profile generic-actor` project,
  including its README and starter helper;
- the complete Labs-v1 rule card in
  [`FRONTLINE-LABS-RULES.md`](FRONTLINE-LABS-RULES.md);
- when explicitly assigned an automatic-companion or duel-map experiment, the
  additional candidate contract in
  [`EXPERIMENTAL-FRONTLINE-DUEL-DEPTH.md`](EXPERIMENTAL-FRONTLINE-DUEL-DEPTH.md);
- the Labs-v1 authoring command and product boundary in
  [`EXPERIMENTAL-FRONTLINE.md`](EXPERIMENTAL-FRONTLINE.md);
- the generic runtime/toolchain material in
  [`WASM-DEVELOPMENT.md`](WASM-DEVELOPMENT.md);
- public types and XML comments in `src/BotArena.Sdk`;
- for a mind assignment, the `nilbots new <Name> --profile generic-mind`
  project and its README, plus the mind sections of
  [`RUNTIME-PROTOCOL.md`](RUNTIME-PROTOCOL.md) and
  [`FRONTLINE-LABS-RULES.md`](FRONTLINE-LABS-RULES.md).

Do not inspect:

- `src/BotArena.Engine`, App execution code, or private replay projection;
- built-in Frontline policies or another cohort entrant;
- engine/runtime tests that reveal private fixtures or expected tactics;
- tournament outputs, replay evidence, standings, or balance reports.

The restriction is methodological, not a security boundary. It keeps each
strategy independently derived from the same player-facing information.

## Common implementation requirements

- Implement `IGenericActorBot`, or `IGenericMindBot` on a mind assignment.
- Treat `StartLife.Contract` as authoritative. Read teams, participants, unit
  slots, forms, actions, transitions, map regions, and mode binding from it.
- Resolve the numeric action code from the current
  `GenericActorActionLegality`; do not copy action codes into strategy logic.
- Check `Available` and typed argument constraints before submitting an action.
- Do not assume two players, three unit slots, fixed form counts, fixed map
  dimensions, or that a future contract contains every Labs-v1 action.
- Use stable IDs only where the doctrine intentionally recognizes an optional
  semantic capability such as `fabricate`, `split`, `transform`, or
  `shoot-direction`. Fall back safely when it is absent.
- **Memory.** Per-life: each active body life has a separate bot instance and
  private memory; a form change preserves that instance, while destruction,
  return, fabrication and replication create fresh ones. **Under the mind: one
  instance for the whole match, and its fields are your memory.** Nothing is
  cleared when a body dies, there is no memory API to learn, and **a runtime
  fault forgets the match** — the Store and everything in it are discarded, and
  under this contract's zero allowance the fault also disqualifies you. Write
  for that.
- **The tick invariant (mind).** `Think` is called exactly once per tick,
  unconditionally, from tick 0 to the terminal tick — including ticks on which
  you own no live body. Ask `mind.Bodies.Length`, do not branch on being alive.
- **The default-`Wait` contract (mind).** Commands are written onto bodies, not
  returned. Every own live body you do not write to waits; forgetting one costs
  that body a tick in the replay, not the match. A command naming a body you do
  not own or that is not live is `Rejected` and recorded; two commands for one
  body is a fault.
- **Role tags (mind).** `body.SetRole("channeler")` publishes a free-vocabulary
  label of at most 24 UTF-8 bytes. It is non-authoritative, sticky until
  changed, and **published on your bodies that an enemy can see**. Use your own
  vocabulary; a deliberately wrong label is a legitimate move.
- **Composition.** `botarena.json` declares the army you play, and a bot is
  permanently classed: switching means a new bot, not an edited one.
- Treat lifecycle assignments and `StartLife.Origin` as data. A future or
  experimental slot may be Ready for explicit fabrication or may create its
  first fresh life automatically at a declared tick. Automatic activation
  inherits no Prime/parent memory and is distinct from both initial deployment
  and post-destruction automatic return.
- Use only deterministic observation, contract, and `context.Random` inputs.
  Do not use clocks, entropy APIs, files, network access, threads, reflection,
  native calls, or environment state.
- Return one bounded action promptly on every tick. Never deliberately fault.
- Keep gameplay logic in ordinary `.cs` source accepted by the controlled
  builder.
- Before freezing an entrant, run at least one exact `--runtime wasm` smoke.
  In-process success does not exercise the sandbox or embedded SDK/Guest.
  Local Labs reports the precise sandbox failure and peak completed-tick fuel
  when a WASM life faults; preserve that output in DX notes.

## Budget and repair policy

The author gets one implementation pass. Building the project and fixing a
compiler error is allowed. A contract misunderstanding, deterministic crash,
or action that immediately faults may be repaired without revealing strategic
results.

There is no ordinary watch-match-edit loop and no strategy improvement pass.
If a shared defect in the author packet or scaffold is discovered, every
author receives the same correction and equal repair opportunity. Every
submitted revision is retained.

## Deliverables

The assigned cohort directory must contain:

- bot source and project file;
- `botarena.json`;
- a short README explaining the doctrine in player-facing terms;
- a manifest recording the assigned doctrine and authoring/repair counts;
- `DX.md`, written after the source is frozen, recording time to first valid
  build, documentation gaps, terminology confusion, missing helpers, awkward
  action/contract APIs, useful diagnostics, hardcoding temptations, and every
  mechanical repair. On a mind assignment it must also record **what the profile
  made EASY** — which per-life machinery you did not have to write, and which
  bug classes never appeared. The point of the round is to measure the
  ergonomics claim, not to assert it.

After authorship closes, the orchestrator performs the controlled WASM build,
records the exact SHA-256 and source revision, archives the canonical artifact,
and only then starts the frozen match matrix.

DX feedback is not tournament feedback. Authors record player-facing
authoring friction before seeing standings, opponent source, or replays, and
do not use the report as an extra strategy-editing pass.

## Doctrine assignments

Each author receives exactly one of these sentences:

- **Pressure:** seek early territorial advantage and credible breach
  opportunities without assuming escalation will unlock.
- **Fabricator:** value additional active bodies and coordinated mobile
  crossfire when the contract makes fabrication worthwhile.
- **Bastion:** use Anchor for deliberate area denial while retaining enough
  mobile objective pressure to finish games.
- **Adapter:** change priorities from visible allied/enemy state, score, active
  objective, and available contract capabilities rather than committing to one
  opening.

The sentence is a direction, not a prescribed algorithm. Independent design
choices are the point of the cohort.
