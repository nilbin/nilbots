# TerritoryHolder authoring notes

## Scope and freeze

The bot was generated once with:

```bash
scripts/botarena new TerritoryHolder --profile generic-actor
```

The generated `TerritoryHolder/` directory was renamed to the retained
`territory-holder/` path. One strategy pass then changed the starter into the
territorial-risk doctrine described in `README.md`. No opponent source,
engine source, test source, replay, result, standings, or prior entrant was
inspected, and no match outcome informed the implementation.

The C# source was frozen before this file was written. Its SHA-256 values are:

```text
233c754df23862f4d9790ed2af088d058322da0b4b9f6692f2a370c6f83d27c3  TerritoryHolder.cs
08ea2e5a40d39e4d610287d156adbf5a5b679794cc79ea0ffa1ee9152f938eb7  ArenaBasics.cs
```

The retained project/configuration hashes at the same point are:

```text
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  TerritoryHolder.csproj
30fec77b7caf45f8dab0b731d4bbf334921b809a1fa3ae8e1633ec1f8c498587  botarena.json
```

## Mechanical checks

- `dotnet build TerritoryHolder.csproj` restored the fresh scaffold and
  succeeded with 0 warnings and 0 errors.
- In-process self-play against the identical project completed for seeds
  `104729` and `130363` with exit code 0.
- Self-play standard output was suppressed. Generated temporary replay
  documents were not opened.
- No WASM build was performed.

## Developer experience

- The player-facing Frontline rule card and generic-actor template provided
  enough information to implement the doctrine without private engine
  knowledge.
- A fresh scaffold has no `obj/project.assets.json`, so
  `dotnet build --no-restore` fails with `NETSDK1004`; the ordinary documented
  `dotnet build` restores the project and succeeds.
- The scaffold command uses the class name as its directory name. A retained
  lowercase slug therefore requires a directory rename, while the generated
  class name, project name, and `botarena.json` entry type remain unchanged.
- The public observation exposes projectile cadence and path but not the
  source attack profile on each projectile. The risk rule therefore compares
  health against the maximum single-hit damage across declared attack
  profiles, which is conservative and contract-driven.
- The independent-life model makes a simple doctrine preferable here: every
  body can determine objective value from public form weights and allied
  positions without hidden shared memory.
