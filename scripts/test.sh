#!/usr/bin/env bash
# Runs every test suite. The WASM contract tests skip themselves when the guest
# artifact is missing; build it first so they actually run.
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f artifacts/wasm/builtin-bots.wasm ] || bash scripts/build-wasm-guest.sh
dotnet test BotArena.sln -v q "$@"
