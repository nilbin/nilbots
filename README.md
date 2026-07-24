# Bot Arena

A programming game: write an autonomous bot in C#, compile it to WebAssembly,
and watch it fight in a deterministic top-down arena — locally first, on a
competitive platform later.

```
C# source ──▶ NativeAOT-LLVM ──▶ WASI core module ──▶ Wasmtime (fuel + memory
limits) ──▶ deterministic match engine ──▶ replay + hash ──▶ visual viewer
```

This repository currently implements the engine proof (Phase 0A), the WASM
runtime proof (Phase 0C), a React replay viewer (Phase 0B-lite), and a CLI that
ties them together. See `docs/PLAN-SUMMARY.md` for the roadmap and
`docs/DECISIONS.md` for the decisions log.

## Quickstart

Prerequisites: Git, curl, and Node.js 22+. The setup script installs .NET 10
when needed. Docker is additionally required for WASM compilation on macOS
(including Apple Silicon) and Linux arm64; Linux x64 can compile natively.

```bash
bash scripts/setup.sh        # platform-aware, idempotent bootstrap
bash scripts/play.sh --bot hunter --opponent coward --seed 7
# → prints the result + replay hash, writes replay.json + viewer.html under
#   out/<bot>-vs-<opponent>-<map>-s<seed>/ (the paths are printed; --out overrides)
```

Write your own bot:

```bash
export PATH="$PWD/scripts:$PATH"       # source-checkout `botarena` command
botarena new MyBot && cd MyBot     # scaffolded project: MyBot.cs + botarena.json
botarena play --bot . --opponent hunter --seed 42   # compiles YOUR bot to WASM
botarena watch . --opponent hunter --seed 42        # rebuild + replay on save
botarena doctor                                      # toolchain status
```

Builds are cached deterministically (`botarena cache status|clear`); only your
source changes trigger recompilation. The CLI and local server safely share
that cache: simultaneous identical requests perform one compile and reuse its
artifact.

The WASM compiler backend is selected automatically: native on Linux x64 with
wasi-sdk, otherwise a focused cached `linux/amd64` Docker builder. See
[WASM development](docs/WASM-DEVELOPMENT.md) for the architecture, Apple
Silicon setup, fast inner loop, cache locations, and troubleshooting.

Open the printed `viewer.html` in a browser: play/pause, step ticks, scrub the
timeline, click a bot to see its field of view, decisions, and debug output.

Same seed, same bots, same map ⇒ same match, always:

```bash
bash scripts/play.sh --seed 42   # run it twice — identical replay hash
dotnet run --project src/BotArena.Cli -- verify <printed replay.json path>
```

## Layout

| Path | What |
| --- | --- |
| `src/BotArena.Engine` | Pure deterministic simulation (no web/DB/WASM deps) |
| `src/BotArena.Sdk` | Developer-facing bot API |
| `src/BotArena.Bots.BuiltIn` | Built-in opponents (SDK-only, run anywhere) |
| `src/BotArena.Runtime` | In-process runtime (diagnostics/tests only) |
| `src/BotArena.Runtime.Wasm` | Canonical WASM runtime (Wasmtime, fuel, limits) |
| `src/BotArena.WasmGuest` | Guest program compiled to `artifacts/wasm/builtin-bots.wasm` |
| `src/BotArena.Cli` | `botarena play / replay / verify / bots / maps` |
| `web/` | React + Tailwind replay viewer (single-file build) |
| `maps/` | Versioned arena maps (JSON) |
| `tests/` | Engine, determinism, and WASM contract tests |
| `scripts/` | Setup and dev-loop tooling |

## Run the platform (pilot)

The web app is an ASP.NET Core modular monolith (accounts, bots, submissions,
matches) backed by PostgreSQL. Friends register in the browser, paste their
bot's C# source, the server compiles it to WASM with the official toolchain,
and challenges play out server-side with shareable replay pages.

```bash
docker compose up -d --build     # app on :8080 + postgres, volumes for data
```

Or natively: run PostgreSQL, then

```bash
ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet run --project src/BotArena.App
```

Configuration: `BOTARENA_DB` (connection string), `BOTARENA_DATA` (artifact and
replay volume), `BOTARENA_ROOT` (toolchain checkout root). Put a TLS proxy in
front before exposing it publicly.

## Development

```bash
bash scripts/test.sh               # current guest + all test suites
bash scripts/build-wasm-guest.sh   # input-stamped; instant when unchanged
bash scripts/dev-viewer.sh    # hot-reload viewer against a fresh replay
bash scripts/e2e.sh           # full pipeline check
```

The C#→WASM compiler comes from the `dotnet-experimental` NuGet feed
(`nuget.config`). Environments that cannot reach GitHub releases get an
equivalent of wasi-sdk assembled from Ubuntu packages via
`scripts/setup-wasi-sdk.sh`. The compiler host itself is Linux x64, so macOS
and arm64 development uses `docker/wasm-builder.Dockerfile`; the output remains
portable WebAssembly.
