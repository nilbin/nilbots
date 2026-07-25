#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"

if [[ $# -lt 2 || $# -gt 4 ]]; then
  echo "usage: unregister-worker.sh PRIMARY_USER@HOST WORKER_NAME [DEPLOY_ROOT] [SSH_IDENTITY]" >&2
  exit 2
fi

primary_target="$1"
worker_name="$2"
deploy_root="${3:-/srv/nilbots/deployment}"
identity="${4:-${BOTARENA_SSH_IDENTITY:-}}"

[[ "$primary_target" =~ ^[a-z_][a-z0-9_-]*@[A-Za-z0-9][A-Za-z0-9.-]{0,252}$ ]] ||
  { echo "invalid primary SSH target" >&2; exit 2; }
[[ "$worker_name" =~ ^[a-z0-9][a-z0-9-]{0,62}$ ]] ||
  { echo "invalid worker name" >&2; exit 2; }
[[ "$deploy_root" =~ ^/[A-Za-z0-9._/-]+$ &&
   "$deploy_root" != "/" &&
   "$deploy_root" != *"//"* &&
   "$deploy_root" != *"/./"* &&
   "$deploy_root" != *"/../"* ]] ||
  { echo "invalid deployment root" >&2; exit 2; }
if [[ -n "$identity" && ! -f "$identity" ]]; then
  echo "SSH identity does not exist: $identity" >&2
  exit 2
fi

ssh_options=(-o BatchMode=yes -o ConnectTimeout=10)
if [[ -n "$identity" ]]; then
  ssh_options+=(-i "$identity" -o IdentitiesOnly=yes)
fi

inventory="$deploy_root/shared/workers.tsv"
ssh "${ssh_options[@]}" "$primary_target" \
  "bash -s -- remove '$inventory' '$worker_name'" \
  <"$deploy_dir/worker-inventory.sh"

echo "Worker '$worker_name' removed from future releases and Caddy upstreams."
echo "Its existing containers and VPS were not stopped or deleted."
