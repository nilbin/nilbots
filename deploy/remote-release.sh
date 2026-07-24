#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "usage: remote-release.sh GIT_SHA RUNTIME_IMAGE_REF COMPILER_IMAGE_REF REPO_PATH" >&2
  exit 2
fi

release_sha="$1"
runtime_image="$2"
compiler_image="$3"
repo_path="$4"

if [[ ! "$release_sha" =~ ^[0-9a-f]{40}$ ]]; then
  echo "release Git SHA must be 40 lowercase hexadecimal characters" >&2
  exit 2
fi
if [[ ! "$runtime_image" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$ ]]; then
  echo "invalid immutable runtime image reference" >&2
  exit 2
fi
if [[ ! "$compiler_image" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$ ]]; then
  echo "invalid immutable compiler image reference" >&2
  exit 2
fi
if [[ ! -d "$repo_path/.git" ]]; then
  echo "deployment repository not found at $repo_path" >&2
  exit 2
fi

cd "$repo_path"
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "deployment checkout has tracked changes; refusing to overwrite them" >&2
  exit 1
fi

git fetch --quiet origin "$release_sha"
if [[ "$(git rev-parse FETCH_HEAD)" != "$release_sha" ]]; then
  echo "fetched revision did not match requested release" >&2
  exit 1
fi

git checkout --quiet --detach "$release_sha"
exec bash deploy/deploy.sh --images "$runtime_image" "$compiler_image" "$release_sha"

