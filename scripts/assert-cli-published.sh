#!/usr/bin/env bash
# Fails unless the CLI version this revision builds is already on NuGet.org.
#
# The server advertises its SDK and build-pipeline versions at /api/meta, and
# `nilbots submit` refuses to build against a server it cannot match
# (DECISIONS #85). That gate is only humane if the CLI it tells players to
# install actually exists — so a deploy of THIS revision requires that
# Nilbots <CliVersion> has been published from it. Run `publish-cli` first.
set -euo pipefail
cd "$(dirname "$0")/.."

version=$(sed -n 's/.*CliVersion = "\([^"]*\)".*/\1/p' src/BotArena.Toolchain/BotProject.cs | head -1)
[ -n "$version" ] || { echo "could not read ToolchainInfo.CliVersion" >&2; exit 1; }

index=$(curl -fsS --retry 3 --retry-delay 2 \
  "https://api.nuget.org/v3-flatcontainer/nilbots/index.json") || {
  echo "could not reach NuGet.org to verify Nilbots $version" >&2
  exit 1
}

if printf '%s' "$index" | grep -qi "\"$version\""; then
  echo "Nilbots $version is published — safe to deploy a server that requires it."
  exit 0
fi

cat >&2 <<EOF
Nilbots $version is NOT published on NuGet.org.

Deploying this revision would leave every player unable to match the server's
toolchain, with no working upgrade path. Run this workflow with
operation=publish-cli for this same revision first, then deploy.

Published versions: $(printf '%s' "$index" | tr -d ' \n')
EOF
exit 1
