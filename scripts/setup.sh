#!/usr/bin/env bash
# One-shot environment setup for a fresh container/machine. Idempotent.
# Installs: .NET 10 SDK, web dependencies, the synthetic wasi-sdk, and builds
# the WASM guest artifact + web viewer so `botarena play` works end to end.
set -euo pipefail
cd "$(dirname "$0")/.."

if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -q "^10\."; then
  echo "Installing .NET 10 SDK..."
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /opt/dotnet
  ln -sf /opt/dotnet/dotnet /usr/local/bin/dotnet
fi
dotnet --version

bash scripts/setup-wasi-sdk.sh

echo "Restoring and building solution..."
dotnet build BotArena.sln -v q

echo "Building WASM guest artifact..."
bash scripts/build-wasm-guest.sh

echo "Building web viewer..."
(cd web && npm install --silent && npm run build --silent)

echo
echo "Setup complete. Try:  bash scripts/play.sh --bot hunter --opponent coward --seed 7"
