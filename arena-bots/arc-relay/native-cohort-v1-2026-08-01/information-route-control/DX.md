# Developer experience

## Boundary and scaffold

Authoring used only the frozen Arc Relay population packet, the experimental
rules document, a fresh `generic-mind` scaffold, public `BotArena.Sdk` source
and XML comments, `experiment arc-relay --print-contract`, and public CLI help.
The author-packet SHA-256 was verified as
`066234060191889353164ef0374d7fe2a7e8d769304fdcb714a18220120b7084`.
No engine, CLI implementation, stock mind, other entrant, cohort result, or
cross-entrant replay was inspected.

The mind surface made whole-army collision claims and carrier/support
assignment straightforward. The main friction was that Arc Relay carries the
Core in mode state rather than in the older resource-shaped body fields, so a
body's carrier status must be joined through `VisibleCores.CarrierActorId`.
The action catalog also makes movement's typed argument shape contract-specific;
the navigation helper consequently supports both public direction and
projectile-heading constraints instead of assuming one scaffold-era shape.

## Contract-driven choices

Map walls, dimensions, Wells, reactors, signatures, movement coupling, attack
range, action IDs/codes, parameter values, collision policy, and participant
forward direction are all read from the frozen contract or observation. The
doctrine recognizes the declared Arc Relay signature `kind` values it intends
to use, but takes each matching action ID, cooldown envelope, radius/range, and
legal target set from the public contract and per-tick legality masks.

All own starting tiles stay reserved for the whole joint movement step because
the contract forbids following a vacated actor. Visible actor positions,
hardlight blocks, hostile nodes, projectiles and their imminent substeps, and
every visible spawn reservation are excluded from movement. Route costs price
visible hostile proximity and hostile fields upward and friendly flare/smoke
coverage downward, allowing live geometry or information to switch the chosen
pickup or return path.

## Equal-budget record

- Implementation pass: one, from the fresh `generic-mind` scaffold.
- Authored doctrine revision: `revision-00-authored` (archived before compile).
- Mechanical compile/fault repairs: recorded below; no outcome-driven strategy
  edits.
- Self-play: one written in-process mirror replay at seed `314159`. Both team
  IDs are present in `Result.EligibleTeamIds`; the zero-fault contract therefore
  completed with both sides eligible and no participant runtime fault.
- Abort-only diagnostic reruns: wrote no replay and measured no cell, as the
  public CLI's exit-4 contract states. They exposed no result or score.
- Strategy changes based on self-play: prohibited and not performed.
- Controlled WASM builds after source freeze: exactly one, successful. Artifact
  SHA-256: `8c89636b0ba971429be7c4a33516191bf49f2c6caeaf3f1c664c76bf00b0384f`.

## Mechanical repair history

This section records only compilation, malformed-action, or runtime-fault
repairs. No repair may change target priorities, route costs, composition, role
assignment, or signature doctrine in response to match outcome.

- `revision-00-authored`: initial single-pass source, before compilation.
- `revision-01-archive-exclusion`: mechanical project repair after the first
  compile included archived `.cs` revisions through the SDK's default recursive
  source glob. The project now excludes `repairs/**/*.cs`. The same repair
  corrects the public `PublicUnitSlot` namespace qualification; neither change
  alters strategy.
- `revision-02-nullability`: mechanical compile repair accepting the public
  nullable `PriorityQueue.TryDequeue` element annotation and guarding the
  impossible null element. Route ordering and costs are unchanged.
- `revision-03-abort-tracing`: temporary own-source stderr tracing of public
  Arc mode state and signature submissions. Added only because the seed cell
  aborted before writing a replay; it is diagnostic instrumentation, not a
  doctrine change, and will be removed before source freeze.
- `revision-04-terminal-node-suppression`: mechanical replay-boundary repair.
  The trace reached tick 599 and identified symmetric Trip Node submissions as
  the final mode-changing operations immediately before the host's pre-state
  mismatch abort. Trip Node is now withheld only on the last contract tick,
  where no subsequent observation can expose its first public state. Ticks 0
  through 598 and all target/route logic are unchanged.
- `revision-05-node-branch-containment`: the narrow terminal-tick suppression
  still aborted because nodes placed on earlier ticks persist in the tick-599
  mode state. The implicated Trip Node submission branch is therefore fully
  withheld as conservative mechanical containment. Its authored targeting code
  remains archived and in-place but unreachable; no replacement tactic or
  strategy improvement was introduced.
- `revision-06-trace-removal`: removes temporary stderr instrumentation after
  the seed cell wrote a replay. Commands and gameplay decisions are identical
  to revision 05.

Archived revision sources use `.cs.txt` / `.csproj.txt` suffixes so the
controlled toolchain cannot treat historical revisions as active compilation
inputs. Their contents are unchanged from the named revisions.

## Source freeze

Source was frozen on 2026-08-01 after the written seed-314159 mechanical smoke
and before the controlled WASM build. The aggregate active-source SHA-256 is
`84a65deeac93a568c359f6607880b1809f0848de21645cb591b4c56a5a7881ae`.
It hashes, in ordinal filename order, each filename, a NUL byte, decimal byte
length, a NUL byte, and exact file bytes for `InformationRouteControl.cs`,
`InformationRouteControl.csproj`, and `Navigation.cs`. Cohort outcome data has
not been disclosed or inspected.
