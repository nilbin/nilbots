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

remote_command() {
  local command=""
  printf -v command '%q ' "$@"
  printf '%s\n' "${command% }"
}

inventory="$deploy_root/shared/workers.tsv"
worker_record="$(
  ssh "${ssh_options[@]}" "$primary_target" \
    "$(remote_command bash -s -- record "$inventory" "$worker_name")" \
    <"$deploy_dir/worker-inventory.sh"
)"
[[ -n "$worker_record" ]] ||
  { echo "worker '$worker_name' is not registered" >&2; exit 1; }
IFS=$'\t' read -r \
  record_name worker_host worker_user worker_path worker_private_ip \
  host_key_type host_key_base64 extra <<<"$worker_record"
[[ "$record_name" == "$worker_name" && -z "$extra" ]] ||
  { echo "primary returned an invalid worker record" >&2; exit 1; }

echo "Removing '$worker_name' from the fleet inventory..."
ssh "${ssh_options[@]}" "$primary_target" \
  "$(remote_command bash -s -- remove "$inventory" "$worker_name")" \
  <"$deploy_dir/worker-inventory.sh"

inventory_removed=1
rollback_inventory() {
  if [[ "$inventory_removed" != "1" ]]; then
    return
  fi
  echo "Caddy refresh failed; restoring '$worker_name' to the inventory..." >&2
  ssh "${ssh_options[@]}" "$primary_target" \
    "$(remote_command \
      bash -s -- \
      upsert \
      "$inventory" \
      "$worker_name" \
      "$worker_host" \
      "$worker_user" \
      "$worker_path" \
      "$worker_private_ip" \
      "$host_key_type" \
      "$host_key_base64")" \
    <"$deploy_dir/worker-inventory.sh" || true
  ssh "${ssh_options[@]}" "$primary_target" \
    "$(remote_command bash -s -- "$deploy_root")" \
    <"$deploy_dir/refresh-primary-upstreams.sh" || true
}
trap rollback_inventory EXIT

echo "Refreshing Caddy before stopping the worker..."
ssh "${ssh_options[@]}" "$primary_target" \
  "$(remote_command bash -s -- "$deploy_root")" \
  <"$deploy_dir/refresh-primary-upstreams.sh"
inventory_removed=0
trap - EXIT

echo "Stopping worker application containers gracefully..."
worker_target="$worker_user@$worker_host"
worker_ssh_options=("${ssh_options[@]}")
temporary_known_hosts="$(mktemp)"
cleanup() {
  if [[ -f "$temporary_known_hosts" ]]; then
    rm -- "$temporary_known_hosts"
  fi
}
trap cleanup EXIT
printf '%s %s %s\n' \
  "$worker_host" "$host_key_type" "$host_key_base64" >"$temporary_known_hosts"
chmod 600 "$temporary_known_hosts"
worker_ssh_options+=(
  -o UserKnownHostsFile="$temporary_known_hosts"
  -o StrictHostKeyChecking=yes
)
if ! ssh "${worker_ssh_options[@]}" "$worker_target" \
  "$(remote_command bash -s -- "$worker_path")" \
  <"$deploy_dir/stop-worker.sh"; then
  echo "warning: worker was unreachable; continuing primary-side removal" >&2
fi

echo "Revoking the worker's exact PostgreSQL rule..."
ssh "${ssh_options[@]}" "$primary_target" \
  "$(remote_command sudo bash -s -- remove "$worker_private_ip")" \
  <"$deploy_dir/configure-primary-worker-access.sh"

echo "Worker '$worker_name' drained, stopped, and removed from future releases."
echo "The VPS and its data were preserved; provider shutdown or deletion is separate."
