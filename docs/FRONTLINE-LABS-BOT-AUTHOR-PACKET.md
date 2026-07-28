# Frontline Labs bot-author packet

Status: frozen common input for the first independently authored
`frontline-labs-1` calibration cohort.

## Purpose

Create one deterministic, contract-driven `IGenericActorBot` that expresses
the assigned doctrine. This is a one-pass authorship exercise, not an
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
- public types and XML comments in `src/BotArena.Sdk`.

Do not inspect:

- `src/BotArena.Engine`, App execution code, or private replay projection;
- built-in Frontline policies or another cohort entrant;
- engine/runtime tests that reveal private fixtures or expected tactics;
- tournament outputs, replay evidence, standings, or balance reports.

The restriction is methodological, not a security boundary. It keeps each
strategy independently derived from the same player-facing information.

## Common implementation requirements

- Implement `IGenericActorBot`.
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
- Remember that each active body life has a separate bot instance and private
  memory. A form change preserves that instance; destruction, return,
  fabrication, and replication create fresh instances. Current allied body
  state and allied sensor union are shared through observations, not through
  hidden cross-instance memory.
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
  mechanical repair.

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
