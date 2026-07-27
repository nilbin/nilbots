#!/usr/bin/env bash
# Runs every test suite. The input-stamped guest build is instant when current,
# and prevents contract tests from silently exercising a stale tracked artifact.
set -euo pipefail
cd "$(dirname "$0")/.."
bash scripts/build-wasm-guest.sh
dotnet build BotArena.sln -v q
dotnet test BotArena.sln --no-build -v q "$@"
python3 scripts/test-frontline-replay-eval.py
python3 scripts/test-replay-review-sample.py
bash scripts/test-init-garage.sh
bash scripts/test-release-installer.sh
bash scripts/test-worker-bootstrap.sh
