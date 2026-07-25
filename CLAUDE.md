# CLAUDE.md

This is the canonical shared agent guide for both Claude Code and Codex.
Claude Code reads it directly; Codex is directed here by `AGENTS.md`. The
workflow playbooks under `.claude/skills/` are shared in the same way.

nilbots: a programming game — C# bots compiled to WebAssembly fight in a
deterministic tile arena. `docs/PLAN-SUMMARY.md` holds the roadmap and phase
status; `docs/DECISIONS.md` is the numbered log of every decision made where
the plan left choices open (cite/extend it when making new ones).

## Environment bootstrap

Fresh environments are scripted. Linux x64 uses NativeAOT-LLVM natively when
wasi-sdk is installed; macOS/Apple Silicon and Linux arm64 automatically use
the focused cached linux/amd64 Docker builder:

```bash
bash scripts/setup.sh          # platform-aware .NET/WASM/web bootstrap
service postgresql start       # required by BotArena.App only
```

If the `botarena` DB role is missing (first boot):
`su postgres -c "psql -c \"CREATE ROLE botarena LOGIN PASSWORD 'botarena';\"" && su postgres -c "createdb -O botarena botarena"`.

The C#→WASM toolchain is NativeAOT-LLVM from the `dotnet-experimental` NuGet
feed (`nuget.config`), pinned in `ToolchainInfo`. Its compiler host package is
Linux x64 only. `scripts/run-wasm-publish.sh` is the platform boundary; do not
invent per-project platform conditions. On Ubuntu,
`scripts/setup-wasi-sdk.sh` synthesizes wasi-sdk at
`/opt/botarena/wasi-sdk-29.0`. Other hosts use
`docker/wasm-builder.Dockerfile`. The `wasm-component-ld` shim strips WASI-p2
componentization so we ship p1 core modules. Full details and troubleshooting:
`docs/WASM-DEVELOPMENT.md`.

## Commands

```bash
dotnet build BotArena.sln                  # all managed code
bash scripts/test.sh                       # all test suites (builds WASM guest first)
dotnet test tests/BotArena.Engine.Tests --filter "FullyQualifiedName~MovementTests"
bash scripts/build-wasm-guest.sh           # cross-platform, input-stamped guest build
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

Deployment for local pilots: `docker compose up -d --build` (app + postgres).
Internet-facing deployment: follow `deploy/README.md`; production uses
explicit roles, a one-shot migration container, and Caddy.

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
  `BackgroundJobs` table claimed via `FOR UPDATE SKIP LOCKED`. Deployment
  roles are selected with `BOTARENA_ROLE`: `web`, `compile-worker`,
  `compiler-runner`, `match-worker`, `migrate`, or local-only `all`.
  Production's networked `compile-worker` coordinates database jobs with a
  filesystem queue; the `compiler-runner` compiles one request at a time with
  no container network or application secrets. Production runs exactly one
  match worker by default; ranked-set and shared-bot row locks now make
  concurrent match consumers safe, and `BOTARENA_MATCH_WORKERS` may raise the
  in-process lane count when measurements justify it. Compilation capacity may
  scale independently. There is still no message broker or microservice boundary.
  Incremental application-layer maintenance follows
  `docs/BACKEND-MAINTAINABILITY-PLAN.md`: endpoints and workers delegate
  repeated business invariants to explicit use cases, while direct EF Core
  inside those use cases remains preferred over generic repositories, a
  mediator, or an event bus.
- Durable artifacts and replays are addressed by stable object keys through
  `IObjectStore`; database rows must never regain machine-local paths. The
  first-VPS backend is a local persistent volume. Add an S3-compatible backend
  before workers span hosts.
- Production web processes never migrate or seed. `BOTARENA_ROLE=migrate`
  performs the one-shot deployment bootstrap. ASP.NET Data Protection keys
  live in PostgreSQL, and production OpenIddict certificates are provisioned
  shared secrets rather than generated independently by each process.
- Production releases are manual-only GitHub Actions runs. They publish the
  runtime and compiler images to GHCR with immutable SHA tags, digest-pinned
  deployment references, SBOMs, and provenance attestations.

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
- **A server may not ship ahead of its CLI compatibility surface.** `/api/meta` advertises
  `sdkVersion` + `buildPipelineVersion`, and `nilbots submit` refuses to build
  against a server it cannot byte-match (DECISIONS #93). So changing
  the CLI, SDK, engine/runtime, controlled compiler inputs, maps, packaged bot
  inputs, or replay viewer also means bumping `CliVersion` (and the CLI csproj
  `<Version>`) and running the release workflow's `publish-cli` BEFORE
  `publish-and-deploy`. Enforced by `scripts/assert-cli-release.sh`:
  `publish-cli` tags the commit `cli-v<version>`, and the deploy refuses unless
  that tag names the revision being deployed or the enumerated compatibility
  surface is byte-identical. Server/auth/site-only revisions may reuse the
  already-published compatible CLI.

## Conventions

- **One top-level class per file**, named for it — tests included (one test
  class per file). Small records/DTOs tightly coupled to a main type may
  colocate (the `Replay.cs` pattern). Legacy grouped CLI files
  (`ToolchainCommands.cs`, `OtherCommands.cs`) are grandfathered — split them
  when you next touch them.
- **`sandbox/` is gitignored scratch** — agent workdirs, throwaway experiments.
  Anything worth keeping goes in `scripts/` (tracked, generalized — no
  session-specific paths) or `.claude/skills/`. If a scratch script proved
  useful once, promote it before the container dies.
- Rule changes follow the balance-harness skill and
  `docs/EVALUATION-METHODOLOGY.md`: implement behind `GameRules` flags; use
  same-cohort A/B for mechanic causality; evaluate substantial redesigns with
  rules-native bot generations, replay dynamics, and outcome-blind viewing.
- **Rules-change surfaces.** A rules/gameplay change is not done until every
  derived surface agrees: engine (`GameRules` + `MatchSession`), SDK
  doc-comments (describe when a field/action is *inert* — never name a rules
  version, they rot when ship decisions change), site DocsPage + template
  README (shipped rules only), agent-arena experiment briefs (experiment
  rules), CLI help, `web/src/types.ts` (replay mirror), `replay --summary`.
  `DocDriftTests` pins the mechanical ones (enum/property mirror, version
  stamps, rules-name lists); prose accuracy is on the author.
- Keep each active substantial experiment's player contract in one concise
  `docs/EXPERIMENTAL-*.md` brief. Agent-arena authors receive that brief plus
  shipped docs/SDK/CLI; they should not have to reconstruct timing or scoring
  from design history, and official site/template docs must not imply an
  unshipped arm has shipped.

## Footguns

- WASM contract tests run against `artifacts/wasm/builtin-bots.wasm` — rebuild
  it (`scripts/build-wasm-guest.sh`) after touching Sdk/Guest/Bots.BuiltIn/
  WasmGuest. `scripts/test.sh` calls the input-stamped build unconditionally,
  preventing a stale tracked artifact.
- The NativeAOT compiler executable is Linux x64 only. On macOS/arm64, let
  `run-wasm-publish.sh` use Docker. The portable part is the emitted WASI
  module, not the compiler process.
- The build cache key covers player sources, the pinned versions, **and the
  SHA-256 of the staged `BotArena.Sdk`/`BotArena.Guest` DLLs** (DECISIONS #84),
  so a framework edit invalidates it without a version bump. Those two
  assemblies are compiled into every player artifact: they import
  `src/ToolchainAssembly.props` so their bytes are configuration- and
  directory-independent, and every host that compiles bots must ship both
  beside itself. Change either project and expect a full rebuild.
- `BOTARENA_BUILD_ISOLATION=off` forces submission builds to run as the
  current user (isolation needs root + setpriv + a `botbuild` account);
  `botarena doctor` shows which mode is active.
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
