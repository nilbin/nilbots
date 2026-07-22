# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Bot Arena: a programming game — C# bots compiled to WebAssembly fight in a
deterministic tile arena. `docs/PLAN-SUMMARY.md` holds the roadmap and phase
status; `docs/DECISIONS.md` is the numbered log of every decision made where
the plan left choices open (cite/extend it when making new ones).

## Environment bootstrap

Fresh containers have **no .NET SDK** and cannot download from GitHub releases
(network policy). Everything is scripted:

```bash
bash scripts/setup.sh          # .NET 10, synthetic wasi-sdk, npm deps, all builds
service postgresql start       # required by BotArena.App only
```

If the `botarena` DB role is missing (first boot):
`su postgres -c "psql -c \"CREATE ROLE botarena LOGIN PASSWORD 'botarena';\"" && su postgres -c "createdb -O botarena botarena"`.

The C#→WASM toolchain is NativeAOT-LLVM from the `dotnet-experimental` NuGet
feed (`nuget.config`), pinned in `ToolchainInfo`. The official wasi-sdk is
unreachable here, so `scripts/setup-wasi-sdk.sh` synthesizes one at
`/opt/botarena/wasi-sdk-29.0` from Ubuntu clang-18 packages (fake
`wasm-component-ld` strips WASI-p2 componentization → we ship p1 core modules).

## Commands

```bash
dotnet build BotArena.sln                  # all managed code
bash scripts/test.sh                       # all test suites (builds WASM guest first)
dotnet test tests/BotArena.Engine.Tests --filter "FullyQualifiedName~MovementTests"
bash scripts/build-wasm-guest.sh           # rebuild artifacts/wasm/builtin-bots.wasm
(cd web && npm run build)                  # SPA → web/dist/index.html (single file)
bash scripts/e2e.sh                        # full pipeline incl. player-bot build + cache assert
dotnet run --project src/BotArena.Cli -- play --bot hunter --opponent coward --seed 7
dotnet run --project src/BotArena.Cli -- doctor        # toolchain status
```

Run the web app (after postgres + web build):

```bash
WASI_SDK_PATH=/opt/botarena/wasi-sdk-29.0 ASPNETCORE_URLS=http://127.0.0.1:8080 \
  BOTARENA_DATA=$PWD/var dotnet run --project src/BotArena.App
```

EF migrations (dotnet-ef needs the runtime root exported):

```bash
export DOTNET_ROOT=/opt/dotnet PATH="$PATH:/root/.dotnet/tools"
cd src/BotArena.App && dotnet ef migrations add <Name>
```

Deployment for pilots: `docker compose up -d --build` (app + postgres).

## Architecture

Execution path (the product's core invariant — local and server identical):

```
C# source → controlled build project (BotArena.Toolchain) → NativeAOT-LLVM
→ WASI p1 core module → WasmBotRuntime (wasmtime-dotnet, fuel + memory limits,
deterministic WASI shims) → MatchEngine → canonical replay + SHA-256 hash
```

Project boundaries that must not be violated:

- **BotArena.Engine** is a pure simulation library: no ASP.NET/EF/WASM/SDK
  references. It defines `IBotRuntime`; runtimes plug in from outside.
- **BotArena.Sdk** (developer-facing API) must not reference the Engine; the
  two have deliberately duplicated types, mapped by adapters in
  BotArena.Runtime (in-process, diagnostic only) and BotArena.Guest (the
  guest-side tick loop compiled into every bot artifact).
- **BotArena.Toolchain** is the ONE controlled-build path: the CLI's
  `botarena build` and the server's submission pipeline call the same
  `BotBuilder.BuildFromSources`. Player csproj files are never trusted; the
  generated workspace is self-contained (references prebuilt Sdk/Guest DLLs)
  so `BuildIsolation` can run it as the unprivileged `botbuild` user.
- **BotArena.App** is a modular monolith (feature folders: Accounts, Bots,
  Matches, Jobs). Modules talk in-process; durable work goes through the
  `BackgroundJobs` table consumed by the single `JobWorker` (claims via
  `FOR UPDATE SKIP LOCKED`; set finalization is race-free only because there
  is one worker). No message broker, no microservices — plan §2 forbids them.

Runtime protocol 0.1 is a line-oriented text protocol over two wasm imports
(`botarena::next_observation` / `post_decision`). Host and guest halves live in
`Runtime.Wasm/WasmProtocol.cs` and `Guest/GuestProtocol.cs` — **they must be
edited in lockstep**.

Web (`web/`) is one Vite/React build with two modes chosen at runtime in
`main.tsx`: the site (router) or the standalone replay viewer (when
`window.__BOTARENA_REPLAY__` is injected by the CLI via the
`<!--BOTARENA_REPLAY-->` marker, or on `file:` URLs). The App serves
`web/dist/index.html` directly — no copy step.

## Invariants and their tests

- **Determinism is the product.** Same versions + artifacts + map + seed ⇒
  identical replay hash. The WASM host shims `clock_time_get`/`random_get`, so
  even bots calling `DateTime.UtcNow`/`Random.Shared` replay identically.
  Guarded by `BotArena.Determinism.Tests` and the WASM contract tests
  (in-process vs WASM hash equality).
- Gameplay-affecting values live only in `GameRules` and are pinned by the
  rules version; the PRNG (SplitMix64) and seed derivation are pinned by
  golden-value tests — if `RandomTests.GoldenValues_PinTheAlgorithm` fails you
  changed the game, which is a version bump, not a test update.
- Broadcast secrecy: API endpoints must never reveal winners/outcomes/ratings
  before `Match.BroadcastComplete(now)` — new endpoints must follow this.
- Version axes (SDK / runtime protocol / runtime config / game rules) are in
  `BotArenaVersions` + `ToolchainInfo`; all of them feed the build-cache key.

## Footguns

- WASM contract tests run against `artifacts/wasm/builtin-bots.wasm` — rebuild
  it (`scripts/build-wasm-guest.sh`) after touching Sdk/Guest/Bots.BuiltIn/
  WasmGuest, or the tests exercise a stale artifact (they *skip* if it is
  missing entirely).
- The build cache hashes **player sources only**; framework changes need a
  `Toolchain.GuestAdapterVersion` bump (or `--no-cache` while iterating).
- `dotnet run --no-build` after adding a migration runs the pre-migration
  binary — rebuild first.
- EF cannot compose LINQ over `FromSqlRaw` with `UPDATE...RETURNING`; use
  `ToListAsync()` then filter client-side (see `JobWorker`).
- Never `pkill -f BotArena.App` from a shell whose own command line contains
  that string — pkill kills the calling shell (exit 144) and can leave the
  server alive holding port 8080. Kill by PID from `ps` and verify.
- `TreatWarningsAsErrors` is on solution-wide (`Directory.Build.props`).
- The infinite-loop test bot (`HogBot`) needs observable side effects per
  iteration or LLVM elides the loop entirely — keep that in mind when writing
  hostile-bot tests.
