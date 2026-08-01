# ArcConvoy authoring DX

This report was completed after the entrant's own permitted mechanical smoke
and before any cohort result or cross-entrant information was disclosed.

## Boundary and budget

Authoring used only the Gate 3 author packet, the public Arc Relay experiment
brief, the fresh generic-mind scaffold, public SDK source/XML comments, public
CLI help/`--print-contract`, and ArcConvoy's own compile/smoke output. No Engine
or CLI implementation, stock mind, other entrant, cohort result, Git history,
or cross-entrant replay was inspected.

The doctrine was implemented in one pass. Two attempted smoke invocations
stopped during compilation and never executed a match; both led only to the
mechanical repairs below. Exactly one self-play match executed, in-process at
seed `314159`. After it completed, no strategy source, project configuration,
`botarena.json`, or `sheet.json` was changed.

## What worked well

- The participant-scoped surface makes an explicit handoff chain small and
  legible. One plan can name a carrier and receiver, command the source to
  handoff, and make the target wait without distributed agreement machinery.
- `ParticipantRegionAssignments` plus the Arc Relay binding were sufficient to
  resolve the correct reactor and mirror the formation from either participant
  assignment. No spawn-side constant was needed.
- The legality masks were a strong authoring boundary. Movement headings,
  handoff receivers, repair targets, flare tiles, Prism directions, hook
  headings, and Arc Toss landings could all use exact typed values paired with
  the observed action code.
- Public Well capacity plus visibility-filtered Core state gave persistent
  memory a clean validity rule: retain a last-seen Core only while its Well
  still advertises the same outstanding identity.
- Free-vocabulary role tags made the doctrine auditable without adding a
  separate diagnostics protocol.

## Friction and ambiguities

- The generic-mind scaffold is Frontline-shaped. Its stock movement helper
  expects a `DirectionConstraint`, whereas Arc Relay's `move-eight-way` uses a
  `ProjectileHeadingConstraint`. An Arc-native template or a mode-neutral
  movement adapter would prevent a fresh author from silently writing a mind
  that cannot move.
- The Arc binding publishes an ordered region-ID array while the mode publishes
  an ordered Well schedule array. Joining them by index is workable but less
  explicit than a typed `wellId`/`regionId` pair.
- A mind body has no direct `CarriedCore` property. The necessary join from
  `VisibleCores[].CarrierActorId` to `MindBody.ActorId` is sound, but a helper or
  XML example would make the most important mode state easier to discover.
- The evaluation sheet is correctly labeled provisional, but the author packet
  specifies only its minimum fields. A single canonical example would remove
  uncertainty about the intended `composition` representation. The straightforward
  class-ID array used here was accepted by the runner.
- Archive folders remain inside the project source tree. Ordinary SDK project
  compilation recursively included `.cs` repair snapshots and reported
  duplicate types. The snapshots had to retain their contents with `.cs.txt`
  names. A documented archive convention would make the repair requirement and
  source discovery rule fit together cleanly.
- The successful command prints the winner but not participant fault totals.
  `run.json` records both eligible teams; with the public zero-fault allowance,
  eligibility proves zero runtime faults. Printing the counts directly would
  make the mechanical check self-contained.

## Mechanical evidence

- Executed smoke: `ArcConvoy` versus itself, in-process, seed `314159`.
- Completion: max-tick completion at tick 599 (600 ticks).
- Eligibility: teams 0 and 1 both eligible; therefore zero runtime faults.
- Replay SHA-256: `23bbe3491e18f8594028d8b62afed8aafe55b9b687b78308558e17f8a4a9885d`.
- Sheet SHA-256 reported by the run:
  `065ca2325afaafa1a129ccba23e9bae4daa29a426271be7d1351608cffa79c17`.
- The permitted replay summary recorded Core pickups, two banks, 226 handoffs,
  and completed uses of Arc Toss, Null Field, Repair Beam, and Tractor Hook.
  These facts are recorded only as capability/mechanical evidence; they were
  not used to improve the strategy.

## Repair history

1. `ArenaBasics.TryRepair` submitted through an undefined local name `body`.
   The exact pre-repair project is retained under
   `repairs/repair-01-before/` with source snapshots named `*.cs.txt`. The only
   doctrine-source correction was `body.Command(...)` to
   `repairer.Command(...)` in that helper.
2. The first archive form retained `.cs` extensions, so recursive compilation
   treated the snapshots as live duplicate types. Snapshot contents were not
   changed; each archived filename received a `.txt` suffix. This was archive
   packaging only and changed no executable source.

No malformed-action, runtime-fault, winner-driven, score-driven, or
tactics-driven repair was made.
