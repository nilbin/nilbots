#!/usr/bin/env bash
# Runs `dotnet publish` with the pinned NativeAOT-LLVM host.
#
# Default backend:
#   Linux x86_64             native (fastest)
#   macOS / Linux arm64      cached linux/amd64 Docker builder
#
# Usage:
#   run-wasm-publish.sh --root <mount-root> --project <relative.csproj> [options] [-- <dotnet args>]
#
# Options:
#   --native                 require the native Linux x64 backend
#   --docker                 require the Docker backend
#   --rebuild-builder        rebuild the cached Docker toolchain image
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
publish_root=""
project=""
mode="${BOTARENA_WASM_BUILD_MODE:-auto}"
rebuild_builder=0
dotnet_args=()

while [ "$#" -gt 0 ]; do
  case "$1" in
    --root)
      publish_root="${2:?--root requires a directory}"
      shift 2
      ;;
    --project)
      project="${2:?--project requires a relative project path}"
      shift 2
      ;;
    --native)
      mode="native"
      shift
      ;;
    --docker)
      mode="docker"
      shift
      ;;
    --rebuild-builder)
      rebuild_builder=1
      shift
      ;;
    --)
      shift
      dotnet_args=("$@")
      break
      ;;
    *)
      echo "Unknown option: $1" >&2
      exit 2
      ;;
  esac
done

[ -n "$publish_root" ] || { echo "--root is required" >&2; exit 2; }
[ -n "$project" ] || { echo "--project is required" >&2; exit 2; }
publish_root="$(cd "$publish_root" && pwd)"
[ -f "$publish_root/$project" ] || {
  echo "Project not found: $publish_root/$project" >&2
  exit 2
}

host_os="$(uname -s)"
host_arch="$(uname -m)"
native_supported=0
if [ "$host_os" = "Linux" ] && { [ "$host_arch" = "x86_64" ] || [ "$host_arch" = "amd64" ]; }; then
  native_supported=1
fi

if [ "$mode" = "auto" ]; then
  native_sdk="${WASI_SDK_PATH:-}"
  if [ -z "$native_sdk" ] && [ -x /opt/botarena/wasi-sdk-29.0/bin/clang ]; then
    native_sdk=/opt/botarena/wasi-sdk-29.0
  fi
  if [ -z "$native_sdk" ]; then
    user_home="${HOME:?HOME is required}"
    native_sdk="$user_home/.wasi-sdk/wasi-sdk-29.0"
  fi
  if [ "$native_supported" -eq 1 ] && [ -x "$native_sdk/bin/clang" ]; then
    mode="native"
  else
    mode="docker"
  fi
fi

if [ "$mode" = "native" ]; then
  [ "$native_supported" -eq 1 ] || {
    echo "NativeAOT-LLVM's pinned host package requires Linux x86_64." >&2
    echo "Use the automatic Docker backend (omit --native) on $host_os/$host_arch." >&2
    exit 1
  }
  command -v dotnet >/dev/null || {
    echo ".NET 10 is required. Run scripts/setup.sh or install the .NET 10 SDK." >&2
    exit 1
  }
  wasi_sdk="${WASI_SDK_PATH:-}"
  if [ -z "$wasi_sdk" ]; then
    if [ -x /opt/botarena/wasi-sdk-29.0/bin/clang ]; then
      wasi_sdk=/opt/botarena/wasi-sdk-29.0
    else
      user_home="${HOME:?HOME is required}"
      wasi_sdk="$user_home/.wasi-sdk/wasi-sdk-29.0"
    fi
  fi
  [ -x "$wasi_sdk/bin/clang" ] || {
    echo "wasi-sdk not found at $wasi_sdk." >&2
    echo "On Ubuntu 24.04 run: sudo bash scripts/setup-wasi-sdk.sh" >&2
    exit 1
  }
  export WASI_SDK_PATH="$wasi_sdk"
  native_command=(
    dotnet publish "$publish_root/$project"
    "-p:PathMap=$publish_root=/src"
    -p:ContinuousIntegrationBuild=true
  )
  if [ "${#dotnet_args[@]}" -gt 0 ]; then
    native_command+=("${dotnet_args[@]}")
  fi
  exec "${native_command[@]}"
fi

[ "$mode" = "docker" ] || {
  echo "BOTARENA_WASM_BUILD_MODE must be auto, native, or docker (got '$mode')." >&2
  exit 2
}
command -v docker >/dev/null || {
  echo "Docker is required for WASM compilation on $host_os/$host_arch." >&2
  echo "Install/start Docker, or iterate with --runtime in-process." >&2
  exit 1
}
docker info >/dev/null 2>&1 || {
  echo "Docker is installed but its daemon is not reachable." >&2
  echo "Start Docker, or iterate with --runtime in-process." >&2
  exit 1
}

# The pinned NativeAOT-LLVM compiler host ships for both linux-x64 and
# linux-arm64, and both emit byte-identical modules (DECISIONS #147). Matching
# the container platform to the host CPU keeps the compiler native: an
# emulated (Rosetta/qemu) builder is slower and can deadlock, so emulation is
# reserved for an explicit BOTARENA_WASM_DOCKER_PLATFORM override.
case "$host_arch" in
  arm64|aarch64) host_docker_arch=arm64 ;;
  *) host_docker_arch=amd64 ;;
esac
container_platform="${BOTARENA_WASM_DOCKER_PLATFORM:-linux/$host_docker_arch}"
container_arch="${container_platform#linux/}"
emulated=0
[ "$container_arch" = "$host_docker_arch" ] || emulated=1

builder_file="$repo_root/docker/wasm-builder.Dockerfile"
builder_key="$(
  {
    git hash-object "$builder_file"
    git hash-object "$repo_root/scripts/setup-wasi-sdk.sh"
  } |
    git hash-object --stdin |
    cut -c1-12
)"
builder_image="${BOTARENA_WASM_BUILDER_IMAGE:-nilbots-wasm-builder:$builder_key-$container_arch}"

if [ "$rebuild_builder" -eq 1 ] || ! docker image inspect "$builder_image" >/dev/null 2>&1; then
  echo "Preparing cached WASM builder $builder_image (first run only)..."
  docker build \
    --platform "$container_platform" \
    --file "$builder_file" \
    --tag "$builder_image" \
    "$repo_root"
fi

user_home="${HOME:?HOME is required}"
cache_root="${BOTARENA_WASM_DOCKER_CACHE:-$user_home/.cache/nilbots-wasm}"
mkdir -p "$cache_root/dotnet" "$cache_root/nuget"

docker_user_args=()
if command -v id >/dev/null; then
  docker_user_args=(--user "$(id -u):$(id -g)")
fi

docker_command=(docker run --rm --platform "$container_platform")
if [ -n "${BOTARENA_WASM_CONTAINER_NAME:-}" ]; then
  docker_command+=(--name "$BOTARENA_WASM_CONTAINER_NAME")
fi
if [ "${#docker_user_args[@]}" -gt 0 ]; then
  docker_command+=("${docker_user_args[@]}")
fi
if [ "$emulated" -eq 1 ]; then
  # Under CPU emulation (Rosetta/qemu), MSBuild's multi-node fan-out
  # intermittently deadlocks at 0% CPU: the entry node blocks in
  # rt_mutex_schedule while a freshly spawned worker never completes its
  # handshake (DECISIONS #147). Single-node, in-process compilation removes
  # every cross-process handshake; ilc keeps its internal parallelism and the
  # emitted artifact bytes are unchanged. Native platform-matched builds must
  # not inherit these flags — they are slower and pointless there.
  docker_command+=(--env DOTNET_EnableWriteXorExecute=0)
fi
docker_command+=(
  --mount "type=bind,src=$publish_root,dst=/workspace"
  --mount "type=bind,src=$cache_root,dst=/cache"
  --workdir /workspace
  "$builder_image"
  dotnet publish "$project"
  -p:PathMap=/workspace=/src
  -p:ContinuousIntegrationBuild=true
)
if [ "$emulated" -eq 1 ]; then
  docker_command+=(
    -p:UseSharedCompilation=false
    -maxcpucount:1
    -nodeReuse:false
  )
fi
if [ "${#dotnet_args[@]}" -gt 0 ]; then
  docker_command+=("${dotnet_args[@]}")
fi
exec "${docker_command[@]}"
