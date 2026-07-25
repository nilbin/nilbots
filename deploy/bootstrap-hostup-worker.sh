#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"

usage() {
  cat >&2 <<'EOF'
usage: HOSTUP_API_KEY=... bootstrap-hostup-worker.sh \
  --primary SSH_USER@PUBLIC_HOST \
  --primary-private-ip PRIVATE_IP \
  --worker VPS_ID_OR_PUBLIC_IP \
  --network PRIVATE_NETWORK_NAME \
  --name WORKER_NAME \
  [--private-ip PRIVATE_IP|auto] \
  [--size standard|xs-smoke] \
  [--identity /path/to/private-key] \
  [--adopt]

This is the end-to-end HostUp path: attach/discover private networking,
provision or adopt the worker, register it, install the primary's current
immutable release, and refresh Caddy. `--adopt` is the resume path for a
previously bootstrapped worker.
EOF
  exit 2
}

primary_target=""
primary_private_ip=""
worker_reference=""
network_name=""
worker_name=""
private_ip="auto"
worker_size="standard"
identity="${BOTARENA_SSH_IDENTITY:-}"
adopt=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --primary) primary_target="${2:-}"; shift 2 ;;
    --primary-private-ip) primary_private_ip="${2:-}"; shift 2 ;;
    --worker) worker_reference="${2:-}"; shift 2 ;;
    --network) network_name="${2:-}"; shift 2 ;;
    --name) worker_name="${2:-}"; shift 2 ;;
    --private-ip) private_ip="${2:-}"; shift 2 ;;
    --size) worker_size="${2:-}"; shift 2 ;;
    --identity) identity="${2:-}"; shift 2 ;;
    --adopt) adopt=1; shift ;;
    *) usage ;;
  esac
done

[[ -n "$primary_target" &&
   -n "$primary_private_ip" &&
   -n "$worker_reference" &&
   -n "$network_name" &&
   -n "$worker_name" ]] || usage
[[ -n "${HOSTUP_API_KEY:-}" ]] ||
  { echo "HOSTUP_API_KEY is required" >&2; exit 2; }

echo "Ensuring HostUp private-network attachment..."
worker_private_ip="$(
  bash "$deploy_dir/hostup-vps.sh" \
    attach-private "$worker_reference" "$network_name" "$private_ip"
)"
worker_status="$(
  bash "$deploy_dir/hostup-vps.sh" status "$worker_reference"
)"
IFS=$'\t' read -r worker_vps_id worker_public_ip worker_power_state \
  <<<"$worker_status"
[[ "$worker_vps_id" == vps_* &&
   "$worker_public_ip" =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ &&
   "$worker_power_state" == "running" ]] ||
  { echo "HostUp worker is not running with a public IPv4" >&2; exit 1; }

echo "Waiting for SSH after HostUp network reconciliation..."
ssh_options=(-o BatchMode=yes -o ConnectTimeout=5 -o StrictHostKeyChecking=accept-new)
if [[ -n "$identity" ]]; then
  ssh_options+=(-i "$identity" -o IdentitiesOnly=yes)
fi
worker_admin_user="root"
bootstrap_extra=()
if [[ "$adopt" == "1" ]]; then
  worker_admin_user="nilbots"
  bootstrap_extra+=(--adopt)
fi
for _ in {1..40}; do
  if ssh "${ssh_options[@]}" "$worker_admin_user@$worker_public_ip" true \
    2>/dev/null; then
    break
  fi
  sleep 3
done
ssh "${ssh_options[@]}" "$worker_admin_user@$worker_public_ip" true

bootstrap_arguments=(
  --primary "$primary_target"
  --primary-private-ip "$primary_private_ip"
  --worker-admin "$worker_admin_user@$worker_public_ip"
  --worker-private-ip "$worker_private_ip"
  --name "$worker_name"
  --size "$worker_size"
)
if [[ -n "$identity" ]]; then
  bootstrap_arguments+=(--identity "$identity")
fi
bootstrap_arguments+=("${bootstrap_extra[@]}")
bash "$deploy_dir/bootstrap-worker.sh" "${bootstrap_arguments[@]}"

deploy_arguments=("$primary_target" "$worker_name")
if [[ -n "$identity" ]]; then
  deploy_arguments+=(/srv/nilbots/deployment "$identity")
fi
bash "$deploy_dir/deploy-current-worker.sh" "${deploy_arguments[@]}"

echo "HostUp worker '$worker_name' is fully online at $worker_private_ip."
echo "Stable HostUp reference for power actions: $worker_vps_id"
