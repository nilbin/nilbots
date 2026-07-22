#!/usr/bin/env bash
# Full end-to-end check: build everything, run every test suite, play a WASM
# match, verify the replay hash, and confirm the viewer was produced.
set -euo pipefail
cd "$(dirname "$0")/.."

bash scripts/build-wasm-guest.sh
(cd web && npm run build --silent)
dotnet test BotArena.sln -v q

OUT=$(mktemp -d)
dotnet run --project src/BotArena.Cli -- play --bot hunter --opponent wander --seed 42 --out "$OUT"
dotnet run --project src/BotArena.Cli -- verify "$OUT/replay.json"
[ -f "$OUT/viewer.html" ] || { echo "viewer.html missing" >&2; exit 1; }

# Player-bot loop: new -> build (controlled, cached) -> play own artifact vs built-in.
rm -rf sandbox/E2EBot
mkdir -p sandbox && (cd sandbox && dotnet run --project ../src/BotArena.Cli -- new E2EBot)
dotnet run --project src/BotArena.Cli -- build sandbox/E2EBot
dotnet run --project src/BotArena.Cli -- build sandbox/E2EBot | grep -q "Cache:            hit" \
  || { echo "expected a build cache hit on the second build" >&2; exit 1; }
dotnet run --project src/BotArena.Cli -- play --bot sandbox/E2EBot --opponent hunter --seed 42 --out "$OUT"
dotnet run --project src/BotArena.Cli -- doctor
echo
echo "E2E OK — open $OUT/viewer.html in a browser to watch the match."
