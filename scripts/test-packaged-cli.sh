#!/usr/bin/env bash
# Install the packed global tool into an isolated directory and prove it works
# without falling back to files from the source checkout.
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_dir="${1:-$repo_dir/artifacts/nuget}"
package_dir="$(cd "$package_dir" && pwd)"
version="$(dotnet msbuild "$repo_dir/src/BotArena.Cli/BotArena.Cli.csproj" -getProperty:Version -nologo)"
nilbots_tool_dir="$(mktemp -d)"
nilbots_work_dir="$(mktemp -d)"

cleanup() {
  rm -rf -- "$nilbots_tool_dir" "$nilbots_work_dir"
}
trap cleanup EXIT

dotnet tool install Nilbots \
  --tool-path "$nilbots_tool_dir" \
  --add-source "$package_dir" \
  --version "$version" \
  --ignore-failed-sources

cd "$nilbots_work_dir"
"$nilbots_tool_dir/nilbots" new PackageSmoke
"$nilbots_tool_dir/nilbots" maps
"$nilbots_tool_dir/nilbots" play \
  --runtime in-process \
  --bot PackageSmoke \
  --opponent hunter \
  --seed 42 \
  --out "$nilbots_work_dir/replay"

test -f "$nilbots_work_dir/PackageSmoke/lib/BotArena.Sdk.dll"
test -f "$nilbots_work_dir/replay/replay.json"
test -f "$nilbots_work_dir/replay/viewer.html"
echo "Packaged CLI smoke test passed."
