#!/usr/bin/env bash
# Viewer development loop: generate a fresh replay into web/public/ and start
# the Vite dev server so edits to the viewer hot-reload against real data.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p web/public
dotnet run --project src/BotArena.Cli -- play "$@" --out web/public
mv web/public/viewer.html /tmp/ 2>/dev/null || true
cd web && npm run dev
