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

# Reproducibility (DECISIONS #72): identical sources must produce identical bytes no
# matter WHERE they are compiled. Two different cache roots mean two different
# workspace paths — the exact difference that made local and server artifacts diverge
# and broke the bit-identical-parity promise. Compare hashes from a cold build in each.
hash_from() {  # $1 = cache root
  BOTARENA_HOME="$1" dotnet run --project src/BotArena.Cli -- build sandbox/E2EBot \
    | sed -n 's/^Artifact hash: *//p' | tr -d '[:space:]'
}
REPRO_ROOT="$(mktemp -d)"
REPRO_A="$(hash_from "$REPRO_ROOT/a")"
REPRO_B="$(hash_from "$REPRO_ROOT/b")"
rm -rf "$REPRO_ROOT"
[ -n "$REPRO_A" ] && [ "$REPRO_A" = "$REPRO_B" ] || {
  echo "build is NOT reproducible across workspace paths: '$REPRO_A' != '$REPRO_B'" >&2
  echo "(this is what makes local and server artifacts differ — see DECISIONS #72)" >&2
  exit 1
}
echo "Reproducible across build roots: $REPRO_A"
dotnet run --project src/BotArena.Cli -- play --bot sandbox/E2EBot --opponent hunter --seed 42 --out "$OUT"
dotnet run --project src/BotArena.Cli -- doctor
echo
echo "E2E OK — open $OUT/viewer.html in a browser to watch the match."
