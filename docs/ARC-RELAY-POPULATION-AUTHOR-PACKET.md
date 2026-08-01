# Arc Relay native cohort v1 — author packet

This packet freezes the authoring boundary for Gate 3. Four independent agents
each write one Arc Relay-native participant-scoped mind. The purpose is
strategy-space coverage and developer-experience evidence, not a ranked bot
contest and not a claim that the leading doctrine is best.

## Allowed information boundary

Authors may inspect only:

- this packet;
- `docs/EXPERIMENTAL-ARC-RELAY.md`;
- `templates/botarena-generic-mind/`;
- the public source and XML comments in `src/BotArena.Sdk/`;
- public CLI help and `nilbots experiment arc-relay --print-contract`;
- their own project, self-play logs, and self-play replay summary.

Authors must not inspect Engine, CLI implementation, the stock mind, another
entrant, another entrant's doctrine brief or DX report, cohort results, or any
cross-entrant replay before source freeze. Do not search the repository outside
the allowed paths. Ask the coordinator if the public boundary is missing a
fact.

## Equal authoring budget

Every author receives the same budget:

1. one implementation pass from a fresh `generic-mind` scaffold;
2. one in-process self-play mechanical smoke at seed `314159`;
3. mechanical compile, malformed-action, or runtime-fault repairs only;
4. no strategy improvement pass and no opponent-result feedback;
5. one controlled WASM build after source freeze;
6. `DX.md` completed before any cohort outcome is disclosed.

A mechanical repair may make the declared doctrine execute without faults; it
may not react to whether the self-play winner, score, or tactics looked good.
Archive every repaired revision and explain it in `DX.md`.

## Shared minimum capability boundary

The entrant must:

- implement `IGenericMindBot` natively and command all live bodies through one
  participant-scoped runtime;
- read map, mode, body class, action IDs/codes, and typed legality constraints
  from the public contract/observation rather than importing engine facts;
- stay valid for either participant assignment and mirror its spatial logic;
- use an eight-class composition under the two-copy cap;
- publish stable, legible role tags;
- implement collision-aware movement and respect visible respawn reservations;
- attempt the doctrine's Core acquisition, carrying/delivery, and denial work;
- use signatures selectively enough for the doctrine to be identifiable;
- complete self-play with both teams eligible and zero runtime faults.

There is no Arc-native tier label yet. Passing these checks is the provisional
Gate 3 cohort admission boundary, not T4/T5 equivalence and not numeric balance
eligibility outside this experiment.

## Coverage cells

Each cell is a starting question, not a forced solution. The author owns the
composition, roles, routes, target selection, and tactical details.

### A — split-control

Hold meaningful pressure in at least two theaters, rotate a reserve on the
public Well cadence, and keep one failed route from collapsing the whole army.
Explore whether distributed pickups can survive concentrated denial.

### B — convoy

Commit a carrier, screens, and an explicit handoff/catch chain to one route,
while making a deliberate choice about what minimal force contests the other
Wells. Explore whether formation and survival repay conceded breadth.

### C — interception

Prioritize identifying, cutting off, displacing, or destroying enemy carriers
and recovering the resulting loose Core. Explore the line between useful
denial and passive reactor camping; camping alone is not a doctrine.

### D — information/route-control

Use vision, smoke/suppression, placed hazards/constructs, and route switching
to create safer pickups and returns. Explore whether information and geometry
control produces delivery opportunities rather than decorative signatures.

## Required archive

Write only inside the assigned directory under
`arena-bots/arc-relay/native-cohort-v1-2026-08-01/<entrant-id>/`:

- all `.cs` source and `.csproj`;
- `botarena.json` with SDK `0.10.11`;
- `sheet.json` containing at least `schema`, `sheetId`, `mapId`, and
  `composition` (the runner hashes it; entrant code may keep its authored
  doctrine in source);
- `README.md` with the doctrine brief and declared composition;
- `DX.md` before results;
- `bot.wasm` after source freeze;
- `manifest.json` with source SHA-256, sheet SHA-256, artifact SHA-256,
  doctrine cell, SDK version, and repair history.

The authoring `sheet.json` uses `arc-relay-evaluation-sheet-v0`. It is a
provisional evaluation record, not the player-facing product schema.

## Commands

From the repository root, use the already preflighted debug CLI:

```bash
CLI=src/BotArena.Cli/bin/Debug/net10.0/botarena.dll

# Create in a scratch directory, then move the authored files into only your
# assigned archive directory.
dotnet "$CLI" new <EntrantName> --profile generic-mind

dotnet "$CLI" experiment arc-relay \
  --bot <your-project> --opponent <your-project> \
  --sheet0 <your-sheet> --sheet1 <your-sheet> \
  --runtime in-process --seed 314159 --out <scratch-output>

dotnet "$CLI" replay <scratch-output>/replay.json.gz --summary
dotnet "$CLI" build <your-project> --no-cache
```

The coordinator, not the author, runs WASM admission, freezes all hashes, and
opens results. Canonical evaluation replays stay gzip scratch and never enter
the archive or gallery.

## Frozen preflight

Before authoring opened, the coordinator proved:

- fresh `generic-mind` scaffold: exact H0 in-process completion, both teams
  eligible, no fault;
- stock mind controlled WASM artifact:
  `5aedf602ef1810c124ca57c801b0963280da2389a8c43feaf22d1c10d9f2c78a`;
- H0 rules fingerprint:
  `f6d3ee9b1bb17d7bd8d0981941fd00a6a96f0e7ef834497d11924c06087174eb`;
- Threefold fingerprint:
  `f4649d6d17e80c02cd4b09fec849de5729435855cf411592a5e2c73cd889bbbe`;
- exact WASM seed-42 canonical hash reproduced from the match record alone:
  `fb92bb681fe0c2407c5bb446f90ba1e22592bb08f2001a5035258c5877e2400a`;
- durable record `1,933 B` plus broadcast `160,894 B`, total `162,827 B`,
  under the hard `304 KiB` per-game ceiling.

No cohort outcome existed when these facts were frozen.
