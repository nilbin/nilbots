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

```bash
bash scripts/setup.sh        # .NET 10, wasi toolchain, web deps, all builds
bash scripts/play.sh --bot hunter --opponent coward --seed 7
# → prints the result + replay hash, writes out/replay.json and out/viewer.html
```

Open `out/viewer.html` in a browser: play/pause, step ticks, scrub the
timeline, click a bot to see its field of view, decisions, and debug output.

Same seed, same bots, same map ⇒ same match, always:

```bash
bash scripts/play.sh --seed 42   # run it twice — identical replay hash
dotnet run --project src/BotArena.Cli -- verify out/replay.json
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

## Development

```bash
bash scripts/test.sh          # all 62 tests (builds the WASM guest if needed)
bash scripts/build-wasm-guest.sh   # recompile bots to WASM
bash scripts/dev-viewer.sh    # hot-reload viewer against a fresh replay
bash scripts/e2e.sh           # full pipeline check
```

The C#→WASM compiler comes from the `dotnet-experimental` NuGet feed
(`nuget.config`). Environments that cannot reach GitHub releases get an
equivalent of wasi-sdk assembled from Ubuntu packages via
`scripts/setup-wasi-sdk.sh` (details in `docs/DECISIONS.md`).
