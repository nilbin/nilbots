#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$deploy_dir/.." && pwd)"
if [[ ! -f "$deploy_dir/.env" ]]; then
  echo "missing $deploy_dir/.env; copy .env.example and fill every secret first" >&2
  exit 1
fi
if [[ ! -f "$deploy_dir/secrets/openiddict-signing.pfx" ||
      ! -f "$deploy_dir/secrets/openiddict-encryption.pfx" ]]; then
  echo "missing OpenIddict certificates under $deploy_dir/secrets" >&2
  exit 1
fi

cd "$repo_root"
export BOTARENA_IMAGE_TAG
BOTARENA_IMAGE_TAG="$(git rev-parse --short=12 HEAD)"
compose=(
  docker compose
  --env-file "$deploy_dir/.env"
  -f "$deploy_dir/compose.production.yml"
)

"${compose[@]}" config --quiet
"${compose[@]}" build
"${compose[@]}" up -d --wait db
"${compose[@]}" stop compile-worker match-worker >/dev/null 2>&1 || true
"${compose[@]}" run --rm migrate
"${compose[@]}" up -d --no-deps --wait web match-worker compile-worker
"${compose[@]}" up -d --no-deps caddy
"${compose[@]}" ps

echo "deployed Git $(git rev-parse --short=12 HEAD) as image tag $BOTARENA_IMAGE_TAG"
