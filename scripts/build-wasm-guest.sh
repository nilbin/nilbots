#!/usr/bin/env bash
# Builds the canonical built-in bot guest.
#
# Linux x64 compiles natively. macOS and Linux arm64
# automatically use the cached host-CPU-matched Docker builder. An input stamp makes
# unchanged invocations effectively instant instead of rerunning NativeAOT.
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

force=0
backend_args=()
publish_args=()
while [ "$#" -gt 0 ]; do
  case "$1" in
    --force)
      force=1
      shift
      ;;
    --native|--docker|--rebuild-builder)
      backend_args+=("$1")
      shift
      ;;
    --)
      shift
      publish_args+=("$@")
      break
      ;;
    *)
      # Preserve the old script's ability to forward MSBuild/dotnet arguments.
      publish_args+=("$1")
      shift
      ;;
  esac
done

artifact="artifacts/wasm/builtin-bots.wasm"
stamp="artifacts/wasm/builtin-bots.inputs"

input_hash="$(
  {
    printf '%s\n' "builtin-wasm-inputs-v5"
    {
      find \
        src/BotArena.Sdk \
        src/BotArena.Guest \
        src/BotArena.Bots.BuiltIn \
        src/BotArena.WasmGuest \
        -type f \
        ! -path '*/bin/*' \
        ! -path '*/obj/*' \
        \( -name '*.cs' -o -name '*.csproj' \) -print
      printf '%s\n' \
        Directory.Build.props \
        docker/wasm-builder.Dockerfile \
        global.json \
        nuget.config \
        scripts/build-wasm-guest.sh \
        scripts/run-wasm-publish.sh \
        scripts/setup-wasi-sdk.sh
    } |
      LC_ALL=C sort |
      while IFS= read -r file; do
        printf '%s ' "$file"
        git hash-object "$file"
      done
  } | git hash-object --stdin
)"

if [ "$force" -eq 0 ] && [ -f "$artifact" ] && [ -f "$stamp" ] \
    && [ "$(sed -n '1p' "$stamp")" = "$input_hash" ]; then
  echo "WASM guest is up to date ($artifact)."
  exit 0
fi

publish_command=(
  bash scripts/run-wasm-publish.sh
  --root "$repo_root"
  --project src/BotArena.WasmGuest/BotArena.WasmGuest.csproj
)
if [ "${#backend_args[@]}" -gt 0 ]; then
  publish_command+=("${backend_args[@]}")
fi
publish_command+=(-- -c Release)
if [ "${#publish_args[@]}" -gt 0 ]; then
  publish_command+=("${publish_args[@]}")
fi
"${publish_command[@]}"

produced="src/BotArena.WasmGuest/bin/Release/net10.0/wasi-wasm/native/BotArena.WasmGuest.wasm"
[ -f "$produced" ] || {
  echo "Build succeeded but did not produce $produced" >&2
  exit 1
}
mkdir -p "$(dirname "$artifact")"
cp "$produced" "$artifact"
printf '%s\n' "$input_hash" >"$stamp"

if command -v sha256sum >/dev/null; then
  sha256sum "$artifact"
else
  shasum -a 256 "$artifact"
fi
