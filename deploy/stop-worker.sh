#!/usr/bin/env bash
set -euo pipefail

if [[ $# -gt 1 ]]; then
  echo "usage: stop-worker.sh [DEPLOY_ROOT]" >&2
  exit 2
fi

deploy_root="${1:-/srv/nilbots/deployment}"
[[ "$deploy_root" =~ ^/[A-Za-z0-9._/-]+$ &&
   "$deploy_root" != "/" &&
   "$deploy_root" != *"//"* &&
   "$deploy_root" != *"/./"* &&
   "$deploy_root" != *"/../"* ]] ||
  { echo "invalid deployment root" >&2; exit 2; }

if [[ ! -L "$deploy_root/current" ]]; then
  echo "Worker has no active release; no containers to stop."
  exit 0
fi
[[ "$(<"$deploy_root/shared/role")" == "worker" ]] ||
  { echo "deployment role is not worker" >&2; exit 1; }
release_target="$(readlink "$deploy_root/current")"
[[ "$release_target" =~ ^releases/[0-9a-f]{40}$ ]] ||
  { echo "worker current release link is invalid" >&2; exit 1; }
deploy_dir="$deploy_root/$release_target/deploy"

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
  --profile compile
)

# Compose interpolates inactive stateful services too; workers deliberately do
# not receive these Garage administration values.
export GARAGE_RPC_SECRET="${GARAGE_RPC_SECRET:-unused-on-worker}"
export GARAGE_ADMIN_TOKEN="${GARAGE_ADMIN_TOKEN:-unused-on-worker}"
export GARAGE_METRICS_TOKEN="${GARAGE_METRICS_TOKEN:-unused-on-worker}"

"${compose[@]}" config --quiet
"${compose[@]}" stop web compile-worker compiler-runner
echo "Worker application containers stopped."
