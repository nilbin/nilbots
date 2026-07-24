#!/usr/bin/env bash
# Runs every test suite. The input-stamped guest build is instant when current,
# and prevents contract tests from silently exercising a stale tracked artifact.
set -euo pipefail
cd "$(dirname "$0")/.."
bash scripts/build-wasm-guest.sh
dotnet test BotArena.sln -v q "$@"
