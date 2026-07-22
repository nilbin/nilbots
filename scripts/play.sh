#!/usr/bin/env bash
# Runs a match through the CLI: bash scripts/play.sh --bot hunter --opponent coward --seed 7
set -euo pipefail
cd "$(dirname "$0")/.."
exec dotnet run --project src/BotArena.Cli -- play "$@"
