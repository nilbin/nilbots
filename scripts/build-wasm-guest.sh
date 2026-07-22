#!/usr/bin/env bash
# Compiles the WASM guest (built-in bots) to artifacts/wasm/builtin-bots.wasm.
# Requires a wasi-sdk; run scripts/setup-wasi-sdk.sh first in restricted containers.
set -euo pipefail
cd "$(dirname "$0")/.."

export WASI_SDK_PATH="${WASI_SDK_PATH:-$HOME/.wasi-sdk/wasi-sdk-29.0}"
if [ ! -x "$WASI_SDK_PATH/bin/clang" ]; then
  echo "wasi-sdk not found at $WASI_SDK_PATH — run scripts/setup-wasi-sdk.sh" >&2
  exit 1
fi

dotnet publish src/BotArena.WasmGuest -c Release "$@"
mkdir -p artifacts/wasm
cp src/BotArena.WasmGuest/bin/Release/net10.0/wasi-wasm/native/BotArena.WasmGuest.wasm \
   artifacts/wasm/builtin-bots.wasm
sha256sum artifacts/wasm/builtin-bots.wasm
