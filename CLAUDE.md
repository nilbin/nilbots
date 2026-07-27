# CLAUDE.md

This is the canonical shared agent guide for both Claude Code and Codex.
Claude Code reads it directly; Codex is directed here by `AGENTS.md`. The
workflow playbooks under `.claude/skills/` are shared in the same way.

**Two directories have their own scoped guide**, and working in either without
reading it means re-deciding things that are already settled:

- [`web/CLAUDE.md`](web/CLAUDE.md) — the Vite/React build: which of the three
  outputs a change lands in, the viewer/site folder boundary that
  `structure.test.ts` enforces, TanStack Query for every read *and* write, and
  why full screen is an orientation rather than a button.
- [`mobile/CLAUDE.md`](mobile/CLAUDE.md) — the Expo app: routes stay thin, data
  access goes through `hooks/`, the single long-lived arena WebView, push
  registration following the session, and the `app.json` orientation footgun.

This file wins wherever the two overlap. Claude Code loads a scoped guide
automatically when it reads a file in that directory; **Codex does not**, so it
has to open them deliberately — which is why `AGENTS.md` names them too.

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
`su postgres -c "psql -c \"CREATE ROLE botarena LOGIN PASSWORD 'botarena' CREATEDB;\"" && su postgres -c "createdb -O botarena botarena"`.
CREATEDB is what lets the opt-in PostgreSQL suite build a throwaway database per
test; without it those tests fail loudly telling you so.

The App tests' PostgreSQL half is opt-in and skipped unless you ask for it:

```bash
BOTARENA_TEST_DB="Host=127.0.0.1;Database=botarena_test;Username=botarena;Password=botarena" \
  dotnet test tests/BotArena.App.Tests
```

Setting that variable commits to running them — an unreachable server or a role
without CREATEDB then fails rather than skips (DECISIONS #101). CI additionally
sets `BOTARENA_POSTGRES_REQUIRED=true`, which makes a MISSING variable an error.

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
(cd web && npm run build)                  # hashed dist/ + theme-scoped dist-cli viewers
bash scripts/generate-api-clients.sh       # OpenAPI document + web/mobile/CLI clients
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
- The Engine also owns the immutable public match contract:
  `PublicRulesManifestFactory` projects `GameRules`, `ArenaMap`, and exact
  scoring-team/submitted-participant/stable-unit-slot/initial-life topology
  into explicitly ordered canonical rules, map, and aggregate fingerprints.
  Shipped protocol 0.1 and replay v1 do not deliver or embed it; the internal
  Frontline replay v2 embeds it for the experimental runtime and viewer.
- Frontline is an unshipped experiment implemented internally through
  Package 6. Official rules 0.1–0.5 leave `GameRules.Frontline` null and
  continue through legacy `MatchSession`, runtime protocol 0.1, replay v1, and
  map format 1. Experimental map format 2, exact rules/map/topology
  fingerprints, `FrontlineMatchSession`, independently instantiated per-life
  `ActorRuntime`s, canonical team observations, replication/fabrication,
  per-life Anchor/turret forms, and internal replay v2 exist. Web/mobile can
  present that v2 through their version-neutral replay model. The shipped
  SDK/guest, protocol, CLI/App match path, canonical WASM runner, server
  admission, and ladders do not expose Frontline yet. Format-v2 assets live
  under `maps/experimental/`; current App and CLI catalogs/package inputs
  enumerate only top-level format-v1 maps, and legacy `MatchEngine` still
  rejects a Frontline definition defensively.
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
- Production releases are manual-only GitHub Actions runs. Image publication
  and deployment require both the release E2E verifier and the reusable CI
  workflow's contract-drift and mandatory PostgreSQL jobs. Successful runs
  publish the runtime and compiler images to GHCR with immutable SHA tags,
  digest-pinned deployment references, SBOMs, and provenance attestations.
- The primary's persistent `shared/workers.tsv` is the non-secret worker fleet
  authority. `deploy/bootstrap-worker.sh` is the only normal path for adding a
  node: it provisions and verifies private binding, filtered secrets, shared
  OpenIddict certificates, SSH host identity, and firewall policy. Caddy and
  the manual release workflow both derive workers from that validated
  inventory; do not reintroduce per-worker GitHub variables.

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
- Public contract collections never use array position as identity. Scoring
  team, submitted participant, stable team-local unit slot, and runtime life
  are distinct; consumers resolve them by their explicit IDs. Canonical
  collection order is a serialization/fingerprint rule, not an identity rule.
- The Frontline call boundary is
  `PrepareTick()` → exact keyed `StepActors(...)` (with `Step(...)` retained as
  the historical `BotDecision` adapter). Preparation applies due lifecycle
  once, is idempotent until a successful step, and returns canonical
  `(teamId, unitId, lifeId)` actors. The decision dictionary must contain
  exactly those keys (empty when no lives are active); missing, extra, or stale
  lives fail atomically. A destruction on tick `D` respawns at
  `D + 1 + PrimeRespawnTicks`, and the new life may act immediately.
- Each active Frontline life owns an independent runtime instance of its
  submitted participant's artifact. Form changes preserve that exact life,
  runtime, and private memory; destruction disposes it, and respawn or
  refabrication creates a fresh `lifeId` and runtime. Stable slots retain an
  immutable default form while the active life carries its effective form and
  pending transition.
- Frontline protected pads block opposing ground entry only: they grant no
  damage immunity and do not stop projectiles. The authored `PrimeSpawn` is
  permanently reserved against own children; fabrication selects another free
  pad tile after movement. Old-life projectiles persist with their exact
  firing-life owner. Damage events/ledgers count
  `min(DamagePerHit, remainingHealth)`, objective presence uses post-damage
  active lives, and a final-tick breach precedes max-tick completion. The
  objective-only max-tick score is
  `(activePositionIndex - positionCount/2) * captureThreshold + signedCaptureProgress`
  in the public team-advance direction (zero draws).
- Anchor is a life-scoped `child-mobile` → `turret` transition. It starts after
  movement/fabrication, remains the source form and Wait-only through combat
  and objective, completes after objective on
  `startedTick + windupTicks - 1`, and emits explicit start/change/cancel
  events. Death emits Destroyed then cancellation; a future-due terminal
  transition stays pending. Turrets are stationary/non-rotating,
  objective-weight zero, see and fire omnidirectionally, and use the separate
  absolute-eight-way `shoot-direction` action without changing body facing.
- Internal replay v2 snapshots the exact canonical observation passed to each
  life runtime, its accepted decision and masks, lifecycle/form causality,
  authoritative post-state, and terminal stable-unit result. Its Engine and
  TypeScript validators intentionally reject self-consistent impossible
  histories. Replay-v1 verification and hashes remain byte-exact.
- Rules/map aliases and presentation are outside component content hashes;
  ordered gameplay sequences stay ordered, true sets are canonicalized, and
  the aggregate match fingerprint includes the exact topology and public
  provenance. Historical official rules/map/match goldens must remain exact.
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
- **API contract surfaces.** The HTTP contract has one source of truth: the
  response types in `BotArena.App`. `dotnet build` emits
  `contracts/BotArena.App.json` from them, and every client is generated from
  that document — `web/src/api/schema.d.ts`, `mobile/src/api/schema.d.ts`, and
  `src/BotArena.Cli/Generated/ApiContracts.cs`. **Never hand-edit a generated
  file or the document**; change the endpoint and run
  `bash scripts/generate-api-clients.sh`, committing the regenerated output with
  your change. CI's `contract-drift` job regenerates and fails on any diff, so a
  forgotten regeneration is a red build rather than a client that silently
  disagrees with the server. Two consequences worth knowing: a handler returning
  an anonymous type produces no response schema (return a named record), and
  `Results.Ok(...)` is untyped to ASP.NET, so each endpoint needs
  `.Produces<T>()` for its shape to reach the document. Note `web/src/types.ts`
  is a different contract — the replay mirror — and stays hand-maintained.
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
