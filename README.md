# nilbots

nilbots is the home of **Arc Relay**, a deterministic team-strategy game about
authoring entrants and watching their plans play out. An entrant is either a tactical
sheet run by the frozen stock mind, or a custom C# mind built to WebAssembly through the
controlled toolchain. Both field eight classed bodies and share the same Season surface:
name, crest, composition, revisions, match history, and rating.

```
C# source ──▶ NativeAOT-LLVM ──▶ WASI core module ──▶ Wasmtime (fuel + memory
limits) ──▶ deterministic match engine ──▶ replay + hash ──▶ visual viewer
```

The current local and hosted line is `broadloom-4` on the 37x27 Broadloom map.
Hosted entrant playlist v36 pins that game, its tactics sheets, and the frozen
stock mind; v35 and earlier remain exact history. Historical Duel and Frontline
code remains for exact replay compatibility and the
labelled read-only archive; neither describes the product front door. See
[`docs/ARC-RELAY-GLOSSARY.md`](docs/ARC-RELAY-GLOSSARY.md) for current vocabulary,
[`docs/SHEET-AUTHORING.md`](docs/SHEET-AUTHORING.md) for the sheet contract, and
[`docs/DECISIONS.md`](docs/DECISIONS.md) for product and compatibility rulings.

## Quickstart

Prerequisites: Git, curl, and Node.js 22+. The setup script installs .NET 10
when needed. Docker is additionally required for WASM compilation on macOS
(including Apple Silicon) and Linux arm64; Linux x64 can compile natively.

```bash
bash scripts/setup.sh        # platform-aware, idempotent bootstrap
nilbots arc-relay \
  --bot arena-bots/arc-relay/tactical-playbook-mind-v1 \
  --opponent arena-bots/arc-relay/tactical-playbook-mind-v1 \
  --sheet0 arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/hunter-tapline-v1.json \
  --sheet1 arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/wellwright-tapline-v1.json \
  --seed 42
```

The command writes a canonical gzip replay and run receipt. Compile or inspect a sheet
through the same shared compiler used by the hosted editor:

```bash
nilbots experiment arc-relay-playbook \
  --playbook arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/hunter-tapline-v1.json
```

For a custom mind, start from `templates/botarena-generic-mind` and use it as the
`--bot` argument. The hosted site accepts custom mind sources through the controlled
build path and runs them only in the sandboxed WASM runtime. Builds are cached
deterministically (`nilbots cache status|clear`).

The WASM compiler backend is selected automatically: native on Linux x64 with
wasi-sdk, otherwise a focused cached `linux/amd64` Docker builder. See
[WASM development](docs/WASM-DEVELOPMENT.md) for the architecture, Apple
Silicon setup, fast inner loop, cache locations, and troubleshooting.

Open the produced viewer to play/pause, step ticks, scrub the timeline, and select a body
to inspect its field of view, order, action, destination, and debug reason.

Same versions, artifacts, sheets, map and seed ⇒ the same match, always:

```bash
nilbots verify <printed replay.json.gz path>
```

## Layout

| Path | What |
| --- | --- |
| `src/BotArena.Engine` | Pure deterministic simulation (no web/DB/WASM deps) |
| `src/BotArena.Sdk` | Developer-facing body and mind API |
| `src/BotArena.Bots.BuiltIn` | Built-in opponents (SDK-only, run anywhere) |
| `src/BotArena.Runtime` | In-process runtime (diagnostics/tests only) |
| `src/BotArena.Runtime.Wasm` | Canonical WASM runtime (Wasmtime, fuel, limits) |
| `src/BotArena.WasmGuest` | Guest program compiled to `artifacts/wasm/builtin-bots.wasm` |
| `src/BotArena.Cli` | `nilbots arc-relay`, compiler, replay, and verification commands |
| `web/` | React site, sheet workshop, and shared replay viewer |
| `maps/` | Versioned arena maps (JSON) |
| `tests/` | Engine, determinism, and WASM contract tests |
| `scripts/` | Setup and dev-loop tooling |

## Run the hosted app

The web app is an ASP.NET Core modular monolith backed by PostgreSQL. Players author and
trial tactical sheets, submit custom minds through the official toolchain, field up to
three entrants in Season 1, and watch shareable replay pages. Legacy Duel records remain
available under the archive and its admission path is retired.

```bash
docker compose up -d --build     # app on :8080 + postgres, volumes for data
```

Or natively: run PostgreSQL, then

```bash
ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet run --project src/BotArena.App
```

Configuration: `BOTARENA_DB` (connection string), `BOTARENA_DATA` (durable
object volume), `BOTARENA_ROOT` (toolchain checkout root), and
`BOTARENA_ROLE` (`all`, `web`, `compile-worker`, `compiler-runner`,
`match-worker`, or `migrate`). Put a TLS proxy in front before exposing it
publicly. Worker lane counts default to one and can be tuned with
`BOTARENA_COMPILE_WORKERS` and `BOTARENA_MATCH_WORKERS`.

For an internet-facing single-VPS installation, see the
[production deployment runbook](deploy/README.md). For the path from one
hobby VPS to multiple hosts, including security gates and promotion triggers,
see the [deployment and scaling plan](docs/DEPLOYMENT-SCALING-PLAN.md).

## Development

```bash
bash scripts/test.sh               # current guest + all test suites
bash scripts/build-wasm-guest.sh   # input-stamped; instant when unchanged
bash scripts/dev-viewer.sh         # hot-reload viewer against a fresh replay
bash scripts/e2e.sh           # full pipeline check
```

The C#→WASM compiler comes from the `dotnet-experimental` NuGet feed
(`nuget.config`). Environments that cannot reach GitHub releases get an
equivalent of wasi-sdk assembled from Ubuntu packages via
`scripts/setup-wasi-sdk.sh`. The compiler host itself is Linux x64, so macOS
and arm64 development uses `docker/wasm-builder.Dockerfile`; the output remains
portable WebAssembly.
