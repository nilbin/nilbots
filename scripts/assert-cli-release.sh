#!/usr/bin/env bash
# Guards the ordering between publishing the CLI and deploying a server.
#
# `nilbots submit` refuses to build against a server whose SDK or build-pipeline
# version it cannot match (DECISIONS #85), and it tells the player to run
# `dotnet tool update -g Nilbots`. That advice only works if the published tool
# for THIS revision exists — so:
#
#   unpublished <sha>   (before publish-cli) the version must NOT be on NuGet yet,
#                       which forces a CliVersion bump per release. Without it
#                       `--skip-duplicate` would silently no-op and the tag below
#                       would point at a commit whose CLI never shipped.
#   published <sha>     (before publish-and-deploy) the version must be on NuGet
#                       AND its cli-v<version> tag must point at the revision
#                       being deployed. Checking NuGet alone is not enough: an
#                       untouched CliVersion is always "published", by an older
#                       commit carrying a different SDK.
set -euo pipefail
cd "$(dirname "$0")/.."

mode=${1:?usage: assert-cli-release.sh <published|unpublished> [revision]}
revision=${2:-}

version=$(sed -n 's/.*CliVersion = "\([^"]*\)".*/\1/p' src/BotArena.Toolchain/BotProject.cs | head -1)
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

Every release needs its own version, or the deploy guard cannot tell which
commit the published tool came from. Bump ToolchainInfo.CliVersion and
src/BotArena.Cli/BotArena.Cli.csproj <Version>, then re-run.
EOF
      exit 1
    fi
    echo "Nilbots $version is unpublished — safe to publish from this revision."
    ;;

  published)
    [ -n "$revision" ] || { echo "publish mode needs the revision to verify" >&2; exit 1; }
    if [ "$published" != true ]; then
      cat >&2 <<EOF
Nilbots $version is NOT published on NuGet.org.

Deploying this revision would leave every player unable to match the server's
toolchain, with no working upgrade path. Run this workflow with
operation=publish-cli on this same commit first, then deploy.
EOF
      exit 1
    fi
    git fetch --no-tags --depth=1 origin "refs/tags/$tag:refs/tags/$tag" 2>/dev/null || true
    tagged=$(git rev-parse -q --verify "refs/tags/$tag^{commit}" || true)
    if [ -z "$tagged" ]; then
      cat >&2 <<EOF
Nilbots $version is on NuGet.org, but there is no $tag tag to say which commit
published it. That tag is written by the publish-cli job, so either this version
predates the guard or the publish run did not complete. Bump CliVersion and
publish this revision's CLI before deploying it.
EOF
      exit 1
    fi
    if [ "$tagged" != "$revision" ]; then
      cat >&2 <<EOF
Nilbots $version was published from $tagged, but you are deploying $revision.

The published tool therefore bundles a different BotArena.Sdk/Guest than this
server will build with, so `nilbots submit` would refuse for every player with
no way to fix it. Bump CliVersion, run publish-cli on $revision, then deploy.
EOF
      exit 1
    fi
    echo "Nilbots $version was published from this exact revision — safe to deploy."
    ;;

  *)
    echo "unknown mode '$mode' (expected published or unpublished)" >&2
    exit 1
    ;;
esac
