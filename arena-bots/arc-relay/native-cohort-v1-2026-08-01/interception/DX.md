# CutlineMind authoring DX

## Boundary and budget

This entrant was authored from a fresh `generic-mind` scaffold using only the
Gate 3 author packet, the public Arc Relay brief, the scaffold, public SDK
source/XML comments, the public printed contract, and its own seed-314159
self-play evidence. No opponent, cohort, stock-mind, Engine, or CLI
implementation source informed the doctrine. The packet's later evidence-only
preflight correction did not change source or doctrine.

The strategy was written in one pass. After that pass, changes were restricted
to the mechanical repairs below. The successful smoke result was not used to
improve target selection, routes, composition, pacing, or signature frequency.

## What was clear

The participant-scoped API makes the central coordination step direct: the
mind sees all eight `MindBody` handles once, assigns roles once, maintains one
carrier sighting, and shares one destination-claim set. `MindBody.Action` and
the legality constraint union were sufficient to avoid copying action codes,
cooldowns, target ranges, or current target sets into source. Public
`ArcRelayCoreState.CarrierActorId` is the decisive interception fact; it lets
the doctrine distinguish a carrier from an ordinary visible body without
trusting role tags.

The public contract also exposed enough spatial semantics to keep the router
assignment-neutral: reactors have team IDs, participant region bindings name
the hostile home pad, movement profiles distinguish deliberate handling, and
visible tiles publish spawn reservations. The action catalog's Arc movement
uses `ProjectileHeadingConstraint`, while rotation uses
`DirectionConstraint`; the typed split was important and was discoverable from
the printed contract plus SDK types.

The provisional sheet was concise and accepted as authored. It should remain
described as evaluation/audit data rather than a player-facing roster schema.

## Friction

The fresh scaffold's `.csproj` reference was correct in its generated scratch
location but became one directory too shallow after the packet-required move
into the deeper cohort archive. That failure is mechanical and easy to repair,
but a relocation-safe scaffold reference or a CLI move/export command would
remove it.

`experiment arc-relay --help` did not render Arc Relay-specific help in this
preflighted CLI; it fell through to the broader experiment help. The packet's
exact command and `--print-contract` were enough to proceed, but a dedicated
usage block would make the sheet flags and output contract easier to discover.

The first running self-play aborted without a replay on an SDK health invariant
after the authored `rail-line` branch was active. With no replay or stack trace,
the only outcome-blind repair available at the player boundary was to disable
that one signature branch. The Longshot remains in the declared cutline and
uses its contract-declared long basic gun. A public abort diagnostic identifying
the failing tick/event or action would make this kind of repair substantially
less inferential.

## Mechanical smoke

The permitted in-process self-play used both sides of the same project and
sheet at seed `314159`. The completed replay hash is
`fd75a140b9eb5e7962e819fe9ec51b556f75f9c146bfba9407baee5e820d8cf8`.
It reached the declared 600-tick horizon with both team IDs eligible.

Mechanical replay queries found:

- 0 mind runtime faults;
- 7,270 accepted commands and no rejected or faulted commands;
- 7,093 successful action resolutions;
- 177 physically blocked resolutions;
- no malformed-action outcome.

The replay and run record remain gzip/plain scratch outputs and are not copied
into this archive.

## Repair history

1. `revision-00-smoke-build-failure.tar.gz` archives the authored revision that
   could not find `BotArena.Sdk` after relocation. SHA-256:
   `e63f9c3eb1cd17a9fef28f674bbb3c7ffa6cbf77b4f8dc136fb9e6969546723a`.
   Repair: changed only the project reference from `../../../src/...` to
   `../../../../src/...`.
2. `revision-01-post-path-fix-runtime-abort.tar.gz` archives the compiling
   revision whose actual match aborted with `ArgumentOutOfRangeException`
   parameter `health` and wrote no replay. SHA-256:
   `53e9203506c53c9e7faeeaf472127c6ee2352d1b41fe49506c386ca6201a540c`.
   Repair: disabled only the `rail-line` signature submission branch. No
   composition, routing, role assignment, focus selection, or result-driven
   tactical change was made.

Source was frozen after the second repair and successful mechanical smoke,
before the controlled WASM build.
