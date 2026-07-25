#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: build-release-bundle.sh GIT_SHA /absolute/output.tar.gz" >&2
  exit 2
fi

release_sha="$1"
output="$2"
if [[ ! "$release_sha" =~ ^[0-9a-f]{40}$ ]]; then
  echo "release Git SHA must be 40 lowercase hexadecimal characters" >&2
  exit 2
fi
if [[ "$output" != /* ]]; then
  echo "release bundle output path must be absolute" >&2
  exit 2
fi

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"
resolved_sha="$(git rev-parse --verify "${release_sha}^{commit}")"
if [[ "$resolved_sha" != "$release_sha" ]]; then
  echo "release Git SHA does not resolve to the requested commit" >&2
  exit 1
fi

mkdir -p "$(dirname "$output")"
temporary="$(mktemp "${output}.XXXXXX")"
trap 'rm -f "$temporary"' EXIT

# Archive only tracked deployment control files from the exact release commit.
# Secrets, images, application source, and Git metadata are never in the bundle.
git archive --format=tar "$release_sha" deploy | gzip -n >"$temporary"

required=(
  deploy/Caddyfile
  deploy/compose.production.yml
  deploy/deploy.sh
  deploy/init-garage.sh
  deploy/install-release.sh
)
contents="$(tar -tzf "$temporary")"
for path in "${required[@]}"; do
  if [[ "$contents" != *"$path"* ]]; then
    echo "release bundle is missing $path" >&2
    exit 1
  fi
done

mv "$temporary" "$output"
trap - EXIT
sha256sum "$output"
