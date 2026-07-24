# WASM development

This repository compiles C# bots to a WASI p1 core module with the pinned
NativeAOT-LLVM toolchain. The resulting `.wasm` runs everywhere Wasmtime does;
only the **compiler host** has a platform restriction.

## Supported build hosts

| Developer host | Build backend | Requirements |
| --- | --- | --- |
| Linux x86_64 | Native by default | .NET 10 and wasi-sdk 29 |
| macOS Apple Silicon | Docker `linux/amd64` | .NET 10, Docker, Node 22+ |
| macOS Intel | Docker `linux/amd64` | .NET 10, Docker, Node 22+ |
| Linux arm64 | Docker `linux/amd64` | .NET 10, Docker, Node 22+ |

The pinned `runtime.linux-x64.Microsoft.DotNet.ILCompiler.LLVM` package contains
the compiler executable. There is no corresponding macOS or Linux arm64 host
package at the pinned version. Installing an arm64 wasi-sdk does not change that:
wasi-sdk supplies the linker/sysroot, not the NativeAOT compiler host.

`scripts/run-wasm-publish.sh` owns this distinction. Callers should not add
platform-specific MSBuild conditions or install Rosetta-hosted .NET directly.

## Fresh checkout

```bash
git clone <repository>
cd nilbots
bash scripts/setup.sh
```

`setup.sh` is idempotent. It installs .NET 10 for the current session when
missing, restores/builds managed code, creates the current built-in guest,
installs locked web dependencies with `npm ci`, and builds the viewer.
`global.json` selects the .NET 10 feature band and `.nvmrc` selects Node 22 for
version managers.

On Linux x64, it offers to install the synthetic wasi-sdk under
`/opt/botarena/wasi-sdk-29.0`. On other platforms it prepares a small dedicated
Docker builder on the first WASM build. This is separate from the much larger
application image—PostgreSQL, Node, the web build, and the server are not part
of the inner-loop compiler image.

Check what the machine will use:

```bash
export PATH="$PWD/scripts:$PATH"
botarena doctor
```

The checkout wrapper executes the CLI assembly built by `setup.sh` directly,
avoiding a redundant MSBuild evaluation on every play, set, or replay command.
Run `dotnet build BotArena.sln` after changing CLI/framework code; ordinary bot
source edits are compiled by `botarena play` and need no CLI rebuild.

## Fast development loop

For bot strategy work, use the managed diagnostic runtime:

```bash
botarena play --bot . --opponent hunter --runtime in-process
```

Not every code edit pays the NativeAOT cost:

| Change | Inner-loop rebuild |
| --- | --- |
| Player `.cs` strategy | in-process assembly, normally under a second after warm-up |
| Repeat of identical player WASM source | content-cache hit; no compile |
| Engine, CLI, docs, or web only | managed/web build; built-in WASM stamp stays valid |
| SDK, Guest, BuiltIn bot, or WasmGuest | one input-stamped shared guest publish |
| Player source before final verification | one WASM publish for that bot |

On the Apple Silicon reference machine, the programmed-shot change measured
18.4 seconds for the shared guest and 16.0 seconds for the one arc-aware player
artifact. The subsequent six-game all-WASM set took under one second because
both artifacts were cached. These are verification costs, not the per-edit
strategy loop.

It is incremental and does not invoke NativeAOT. Before sharing or submitting,
verify the canonical sandbox:

```bash
botarena build .
botarena play --bot . --opponent hunter --runtime wasm
```

Player builds use two caches:

- `~/.botarena/cache`: content-addressed final bot artifacts;
- `~/.cache/nilbots-wasm`: Docker/NuGet state on non-native hosts.

The second identical `botarena build` should be a cache hit and perform no
container compilation.

Framework developers rebuild the built-in guest with:

```bash
bash scripts/build-wasm-guest.sh
```

Its input stamp covers the SDK, guest adapter, built-in bots, WASM entry point,
project files, shared build properties, and NuGet configuration. If none changed,
the command exits immediately. The stamp is committed beside the tracked guest,
so a fresh checkout does not rebuild it needlessly. Generated `bin/` and `obj/`
sources are excluded: an ordinary managed build cannot invalidate the guest.
Relevant changes trigger one NativeAOT publish:
normally several seconds on native Linux x64 and tens of seconds under Apple
Silicon x64 emulation—not a full application-image rebuild.

The stamp guarantees reuse of the checked-in artifact; it does not claim that
two forced NativeAOT publishes are byte-for-byte reproducible. The pinned
toolchain can emit a different module hash from the same stamped inputs, even
when both modules pass the deterministic replay contract. Do not commit a
forced-rebuild artifact when no stamped input changed. Artifact-hash parity
between independent build environments remains a separate release check.

Useful overrides:

```bash
bash scripts/build-wasm-guest.sh --force
bash scripts/build-wasm-guest.sh --docker
bash scripts/build-wasm-guest.sh --docker --rebuild-builder
BOTARENA_WASM_BUILD_MODE=docker botarena build .
```

`--native` deliberately fails outside Linux x64. `--docker` is also useful on a
Linux workstation whose system toolchain differs from the deployment builder.

## What is pinned

- .NET target: `net10.0`
- NativeAOT-LLVM: `10.0.0-rc.1.26306.1`
- compiler invocation: `ToolchainInfo.BuildPipelineVersion`
- wasi-sdk contract: 29
- output: WASI p1 core module
- SDK/guest versions: `ToolchainInfo`

The experimental NativeAOT package targets `wasm32-unknown-wasip2`. The
repository's `wasm-component-ld` shim intentionally produces a p1 core module
because the Wasmtime host needs fuel metering, deterministic host functions,
and memory limits. See decisions 3–5 in `DECISIONS.md`.

The official wasi-sdk 29 works on unrestricted Linux x64 systems. CI/deployment
uses `scripts/setup-wasi-sdk.sh`, which assembles the compatible sysroot from
Ubuntu 24.04 packages. Different sysroots can produce different artifact hashes;
the server/deployment build remains canonical.

## Tests and release checks

```bash
bash scripts/test.sh
bash scripts/e2e.sh
```

`test.sh` always calls the input-stamped guest build first, so WASM contract
tests cannot accidentally exercise an old tracked artifact. Contract tests
compare in-process and WASM replay hashes, including active control and
speed-two projectile observations. SDK/guest 0.8 also pins an arc-program
round trip: trailing action limits, exact current eight-way headings, and a
private programmed `Shoot` must produce the same replay through both runtimes.

`e2e.sh` additionally scaffolds a player bot, performs a cold build and cache
hit, runs it in Wasmtime, verifies the replay, and builds the viewer.

When changing `BotArena.Sdk`, `BotArena.Guest`, `BotArena.Bots.BuiltIn`, or
`BotArena.WasmGuest`:

1. bump `ToolchainInfo.GuestAdapterVersion` when player artifacts must rebuild;
2. rebuild `artifacts/wasm/builtin-bots.wasm`;
3. run `scripts/test.sh`;
4. commit the updated tracked artifact with the source change.

## Troubleshooting

### Docker is installed but unreachable

Start Docker and confirm `docker info` succeeds. Bot iteration can continue with
`--runtime in-process` while Docker is unavailable.

### Apple Silicon reports an amd64 warning

That platform is intentional: the compiler host is Linux x64. Ensure the Docker
installation has x86_64 emulation enabled. The emitted module itself is
architecture-neutral WebAssembly.

### First build is slow

The first Docker build downloads Ubuntu packages, .NET, and compiler NuGet
packages. Later builds reuse the builder image and
`~/.cache/nilbots-wasm`. Do not use the full application `Dockerfile` for the
guest inner loop.

### NuGet restore appears stuck

Verify access to both sources in `nuget.config`. Docker networking must reach
`api.nuget.org` and `pkgs.dev.azure.com`. The build log path printed by
`botarena build` streams restore/compiler output while the CLI waits.

### Linux native build cannot find wasi-sdk

On Ubuntu 24.04:

```bash
sudo bash scripts/setup-wasi-sdk.sh
```

Or set `WASI_SDK_PATH` to an official wasi-sdk 29 installation. If neither is
available, use `BOTARENA_WASM_BUILD_MODE=docker`.

### Clean rebuild

Use `--force` for the built-in guest or `botarena build --no-cache` for a player
bot. Rebuild the Docker toolchain image only when diagnosing its prerequisites;
ordinary source changes do not require it.
