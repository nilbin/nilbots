# GeometricDodger authoring DX

This report was written after the strategy source was frozen and after only
compile and in-process self-play mechanical checks. No opponent source,
retained entrant, replay body, standings archive, or tournament result was
inspected. The self-play outcome did not trigger a strategy edit.

## Frozen source

- `GeometricDodger.cs` SHA-256:
  `447b5eadf73b238618e0eb0bf259804693a2f177adf4cfd1ae9d24a312eda21e`
- `ArenaBasics.cs` SHA-256:
  `9f7f91df9630d0187cefc37b3c956226981a9dcb1fa562954a68a67d66ae509c`
- `README.md` SHA-256:
  `b787581c76761c11ad9364eb77adec4dfe6046f5758c1494705968ce1dcaa634`
- `GeometricDodger.csproj` SHA-256:
  `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573`
- `botarena.json` SHA-256:
  `4a325a8c8b4951a2ecf0c233701ba7a3e8bdf5918476f4f0bb0f9994b18e0ecb`

## Mechanical checks

- `dotnet build GeometricDodger.csproj` succeeded with zero warnings and
  zero errors.
- One same-project, seed-`104729`, in-process Frontline Labs self-play
  completed normally and emitted a complete replay.
- `nilbots verify` accepted that replay's canonical v3 contract, content, and
  stored hash.
- No WASM build was run; the cohort owner retains the single canonical build.

## Player-facing friction

- `nilbots new GeometricDodger --profile generic-actor` creates a
  `GeometricDodger/` directory. Retaining entrants under kebab-case cohort
  paths therefore requires a directory rename.
- The generated dodge helper clearly demonstrates projectile cadence, but it
  only leaves a path that reaches the body on the next advance. Building the
  requested public-geometry baseline required extending the public current
  heading through `RemainingTiles`.
- The public SDK documentation is explicit that `Heading` is current evidence,
  not a promise about a private committed bend. That made the intended
  information boundary implementable without engine knowledge.
- The current public `experiment frontline-labs` help exposes hosted Labs and
  its named local mechanics arms, but not the micro-screen's one-private-bend
  candidate selector. The authoring smoke therefore used hosted Labs v1;
  candidate-arm execution remains with the cohort owner.
