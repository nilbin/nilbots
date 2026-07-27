#!/usr/bin/env bash
# Guards the ordering between publishing the CLI and deploying a server.
#
# `nilbots submit` refuses to build against a server whose SDK or build-pipeline
# version it cannot match (DECISIONS #93), and it tells the player to run
# `dotnet tool update -g Nilbots`. That advice only works if a compatible
# published tool exists — so:
#
#   unpublished <sha>   (before publish-cli) the version must NOT be on NuGet yet,
#                       which forces a CliVersion bump whenever a new tool is
#                       published. Without it `--skip-duplicate` would silently
#                       no-op and the tag below could name bytes that never shipped.
#   published <sha>     (before publish-and-deploy) the version must be on NuGet
#                       AND its cli-v<version> tag must either point at the server
#                       revision or have an identical CLI compatibility surface.
#                       Server/auth/UI-only revisions may therefore deploy without
#                       minting a no-op NuGet version, while SDK/engine/compiler/
#                       replay-viewer changes still fail closed.
set -euo pipefail
cd "$(dirname "$0")/.."

mode=${1:?usage: assert-cli-release.sh <published|unpublished> [revision]}
revision=${2:-}

if [ "$mode" = published ]; then
  [ -n "$revision" ] || { echo "publish mode needs the revision to verify" >&2; exit 1; }
  revision=$(git rev-parse --verify "${revision}^{commit}")
  toolchain_source=$(git show "$revision:src/BotArena.Toolchain/BotProject.cs")
else
  toolchain_source=$(<src/BotArena.Toolchain/BotProject.cs)
fi
version=$(printf '%s\n' "$toolchain_source" |
  sed -n 's/.*CliVersion = "\([^"]*\)".*/\1/p' | head -1)
[ -n "$version" ] || { echo "could not read ToolchainInfo.CliVersion" >&2; exit 1; }
tag="cli-v$version"

index=$(curl -fsS --retry 3 --retry-delay 2 \
  "https://api.nuget.org/v3-flatcontainer/nilbots/index.json") || {
  echo "could not reach NuGet.org to check Nilbots $version" >&2
  exit 1
}
published=false
printf '%s' "$index" | grep -qi "\"$version\"" && published=true

case "$mode" in
  unpublished)
    if [ "$published" = true ]; then
      cat >&2 <<EOF
Nilbots $version is already published on NuGet.org.

This operation publishes new tool bytes, so it needs a new version. Bump
ToolchainInfo.CliVersion and src/BotArena.Cli/BotArena.Cli.csproj <Version>,
then re-run. Server-only releases should use publish-and-deploy directly.
EOF
      exit 1
    fi
    echo "Nilbots $version is unpublished — safe to publish from this revision."
    ;;

  published)
    if [ "$published" != true ]; then
      cat >&2 <<EOF
Nilbots $version is NOT published on NuGet.org.

Deploying this revision would leave every player unable to match the server's
toolchain, with no working upgrade path. If the CLI compatibility surface
changed, bump and publish the CLI first.
EOF
      exit 1
    fi
    git fetch --no-tags --depth=1 origin "refs/tags/$tag:refs/tags/$tag" 2>/dev/null || true
    tagged=$(git rev-parse -q --verify "refs/tags/$tag^{commit}" || true)
    if [ -z "$tagged" ]; then
      cat >&2 <<EOF
Nilbots $version is on NuGet.org, but there is no $tag tag to say which commit
published it. That tag is written by the publish-cli job, so either this version
predates the guard or the publish run did not complete. Publish a compatible
CLI before deploying this revision.
EOF
      exit 1
    fi

    if [ "$tagged" = "$revision" ]; then
      echo "Nilbots $version was published from this exact revision — safe to deploy."
      exit 0
    fi

    compatibility_paths=(
      Directory.Build.props
      global.json
      nuget.config
      src/BotArena.Cli
      src/BotArena.Toolchain
      src/BotArena.Engine
      src/BotArena.Runtime
      src/BotArena.Runtime.Wasm
      src/BotArena.Sdk
      src/BotArena.Guest
      src/BotArena.WasmGuest
      src/BotArena.Bots.BuiltIn
      artifacts/wasm/builtin-bots.wasm
      docker/wasm-builder.Dockerfile
      maps
      templates/botarena-bot
      docs/PLAYER-GUIDE.md
      scripts/run-wasm-publish.sh
      scripts/setup-wasi-sdk.sh
      web/index.html
      web/package.json
      web/package-lock.json
      web/vite.config.ts
      web/vite.cli.config.ts
      web/src/App.tsx
      web/src/components
      web/src/assets
      web/src/audio
      web/src/index.css
      web/src/main.tsx
      web/src/playback.ts
      web/src/render
      web/src/replayMetadata.ts
      web/src/types.ts
    )
    if ! git diff --quiet "$tagged" "$revision" -- "${compatibility_paths[@]}"; then
      changed=$(git diff --name-only "$tagged" "$revision" -- "${compatibility_paths[@]}")
      cat >&2 <<EOF
Nilbots $version was published from $tagged, but you are deploying $revision.

The CLI compatibility surface changed:
$changed

The published tool may therefore build, simulate, or replay different bytes.
Bump CliVersion, run publish-cli on the intended release revision, then deploy.
EOF
      exit 1
    fi
    echo "Nilbots $version is published and its compatibility surface is unchanged since $tagged — safe to deploy."
    ;;

  *)
    echo "unknown mode '$mode' (expected published or unpublished)" >&2
    exit 1
    ;;
esac
