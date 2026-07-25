#!/usr/bin/env bash
set -euo pipefail

if [[ $# -gt 1 ]]; then
  echo "usage: refresh-primary-upstreams.sh [DEPLOY_ROOT]" >&2
  exit 2
fi

deploy_root="${1:-/srv/nilbots/deployment}"
[[ "$deploy_root" =~ ^/[A-Za-z0-9._/-]+$ &&
   "$deploy_root" != "/" &&
   "$deploy_root" != *"//"* &&
   "$deploy_root" != *"/./"* &&
   "$deploy_root" != *"/../"* ]] ||
  { echo "invalid deployment root" >&2; exit 2; }
[[ -L "$deploy_root/current" ]] ||
  { echo "primary deployment has no active release" >&2; exit 1; }
[[ "$(<"$deploy_root/shared/role")" == "primary" ]] ||
  { echo "deployment role is not primary" >&2; exit 1; }

release_target="$(readlink "$deploy_root/current")"
[[ "$release_target" =~ ^releases/[0-9a-f]{40}$ ]] ||
  { echo "primary current release link is invalid" >&2; exit 1; }
deploy_dir="$deploy_root/$release_target/deploy"
inventory="$deploy_root/shared/workers.tsv"

export BOTARENA_WEB_UPSTREAMS
BOTARENA_WEB_UPSTREAMS="$(
  bash "$deploy_dir/worker-inventory.sh" upstreams "$inventory"
)"

compose=(
  docker compose
  --env-file "$deploy_dir/.env"
)
if [[ -f "$deploy_dir/release.env" ]]; then
  compose+=(--env-file "$deploy_dir/release.env")
fi
compose+=(
  -f "$deploy_dir/compose.production.yml"
  --profile web
  --profile ingress
)

"${compose[@]}" config --quiet
"${compose[@]}" up -d --no-deps --force-recreate caddy
"${compose[@]}" ps caddy
echo "Caddy upstreams refreshed: $BOTARENA_WEB_UPSTREAMS"
