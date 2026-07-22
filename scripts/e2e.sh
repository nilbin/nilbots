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
echo
echo "E2E OK — open $OUT/viewer.html in a browser to watch the match."
