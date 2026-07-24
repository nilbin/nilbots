#!/usr/bin/env bash
# One-shot developer setup. Idempotent on macOS (including Apple Silicon) and
# Linux. Linux x64 uses a native WASM compiler when wasi-sdk is installed;
# other hosts use the cached linux/amd64 Docker builder automatically.
set -euo pipefail
cd "$(dirname "$0")/.."

if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -q "^10\."; then
  echo "Installing .NET 10 SDK..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  if [ "$(id -u)" -eq 0 ]; then
    dotnet_dir=/opt/dotnet
  else
    user_home="${HOME:?HOME is required}"
    dotnet_dir="$user_home/.dotnet"
  fi
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$dotnet_dir"
  export PATH="$dotnet_dir:$PATH"
  if [ "$(id -u)" -eq 0 ]; then
    ln -sf "$dotnet_dir/dotnet" /usr/local/bin/dotnet
  else
    echo "Add $dotnet_dir to PATH in your shell profile for future terminals."
  fi
  /usr/bin/env rm /tmp/dotnet-install.sh
fi
dotnet --version

host_os="$(uname -s)"
host_arch="$(uname -m)"
user_home="${HOME:?HOME is required}"
native_wasi="${WASI_SDK_PATH:-/opt/botarena/wasi-sdk-29.0}"
if [ ! -x "$native_wasi/bin/clang" ] \
    && [ -x "$user_home/.wasi-sdk/wasi-sdk-29.0/bin/clang" ]; then
  native_wasi="$user_home/.wasi-sdk/wasi-sdk-29.0"
fi
if [ "$host_os" = "Linux" ] \
    && { [ "$host_arch" = "x86_64" ] || [ "$host_arch" = "amd64" ]; } \
    && [ ! -x "$native_wasi/bin/clang" ]; then
  echo
  echo "Native Linux x64 WASM prerequisites are not installed."
  if [ "$(id -u)" -eq 0 ]; then
    bash scripts/setup-wasi-sdk.sh
  elif command -v sudo >/dev/null; then
    sudo bash scripts/setup-wasi-sdk.sh
  else
    echo "No sudo available; the Docker WASM backend will be used."
  fi
fi

command -v npm >/dev/null || {
  echo "Node.js 22+ and npm are required for the replay viewer." >&2
  exit 1
}

echo "Restoring and building solution..."
dotnet build BotArena.sln -v q

echo "Building WASM guest artifact..."
bash scripts/build-wasm-guest.sh

echo "Building web viewer..."
(cd web && npm ci --silent && npm run build --silent)

echo
echo "Setup complete."
echo "WASM backend: $(dotnet run --project src/BotArena.Cli --no-build -- doctor | sed -n 's/^WASM build backend:     //p')"
echo "Try: bash scripts/play.sh --bot hunter --opponent coward --seed 7"
