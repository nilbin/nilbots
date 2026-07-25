#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"
if [[ ! -f "$deploy_dir/.env" ]]; then
  echo "missing $deploy_dir/.env" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
source "$deploy_dir/.env"
set +a

required=(
  GARAGE_RPC_SECRET
  GARAGE_ADMIN_TOKEN
  GARAGE_METRICS_TOKEN
  GARAGE_ZONE
  GARAGE_NODE_CAPACITY
  BOTARENA_S3_BUCKET
  BOTARENA_S3_ACCESS_KEY
  BOTARENA_S3_SECRET_KEY
)
for name in "${required[@]}"; do
  if [[ -z "${!name:-}" || "${!name}" == CHANGE_ME* ]]; then
    echo "$name must be configured in deploy/.env" >&2
    exit 1
  fi
done

if [[ ! "$GARAGE_RPC_SECRET" =~ ^[0-9a-fA-F]{64}$ ]]; then
  echo "GARAGE_RPC_SECRET must be 32 bytes encoded as 64 hexadecimal characters" >&2
  exit 1
fi
if [[ ! "$BOTARENA_S3_ACCESS_KEY" =~ ^GK[0-9a-fA-F]{32}$ ]]; then
  echo "BOTARENA_S3_ACCESS_KEY must start with GK followed by 32 hexadecimal characters" >&2
  exit 1
fi
if [[ ! "$BOTARENA_S3_SECRET_KEY" =~ ^[0-9a-fA-F]{64}$ ]]; then
  echo "BOTARENA_S3_SECRET_KEY must be 32 bytes encoded as 64 hexadecimal characters" >&2
  exit 1
fi
if [[ ! "$GARAGE_NODE_CAPACITY" =~ ^[1-9][0-9]*(MB|GB|TB)$ ]]; then
  echo "GARAGE_NODE_CAPACITY must be an integer with an MB, GB, or TB suffix" >&2
  exit 1
fi

compose=(
  docker compose
  --env-file "$deploy_dir/.env"
  -f "$deploy_dir/compose.production.yml"
)

garage() {
  "${compose[@]}" exec -T -e RUST_LOG=garage=warn garage-gateway /garage "$@"
}

node_peer() {
  "${compose[@]}" exec -T -e RUST_LOG=garage=warn "$1" /garage node id --quiet
}

gateway_status="$(garage status)"
for service in garage-a garage-b garage-c; do
  peer="$(node_peer "$service")"
  node_id="${peer%%@*}"
  if [[ "$gateway_status" != *"${node_id:0:16}"* ]]; then
    garage node connect "$peer" >/dev/null
    gateway_status="$(garage status)"
  fi
done

gateway_peer="$(node_peer garage-gateway)"
gateway_id="${gateway_peer%%@*}"
for attempt in {1..30}; do
  gateway_status="$(garage status)"
  all_connected=true
  for service in garage-a garage-b garage-c; do
    peer="$(node_peer "$service")"
    node_id="${peer%%@*}"
    if [[ "$gateway_status" != *"${node_id:0:16}"* ]]; then
      all_connected=false
    fi
  done
  if [[ "$gateway_status" != *"${gateway_id:0:16}"* ]]; then
    all_connected=false
  fi
  if [[ "$all_connected" == true ]]; then
    break
  fi
  if [[ "$attempt" == 30 ]]; then
    echo "Garage nodes did not converge within 30 seconds" >&2
    exit 1
  fi
  sleep 1
done

layout="$(garage layout show)"
layout_version="$(awk '/Current cluster layout version:/ { print $NF; exit }' <<<"$layout")"
if [[ -z "$layout_version" ]]; then
  echo "could not determine Garage cluster layout version" >&2
  exit 1
fi

if [[ "$layout_version" == 0 ]]; then
  for service in garage-a garage-b garage-c; do
    peer="$(node_peer "$service")"
    node_id="${peer%%@*}"
    garage layout assign "$node_id" \
      --zone "$GARAGE_ZONE" \
      --capacity "$GARAGE_NODE_CAPACITY" \
      --tag "$service" >/dev/null
  done
  garage layout assign "$gateway_id" \
    --gateway \
    --zone "$GARAGE_ZONE" \
    --tag gateway >/dev/null
  garage layout apply --version 1 >/dev/null
  layout="$(garage layout show)"
fi

for tag in garage-a garage-b garage-c gateway; do
  if [[ "$layout" != *"$tag"* ]]; then
    echo "Garage layout does not contain expected node tag '$tag'" >&2
    exit 1
  fi
done

if ! garage bucket info "$BOTARENA_S3_BUCKET" >/dev/null 2>&1; then
  if create_error="$(garage bucket create "$BOTARENA_S3_BUCKET" 2>&1)"; then
    :
  elif [[ "$create_error" == *"BucketAlreadyExists"* ||
          "$create_error" == *"BucketAlreadyOwnedByYou"* ]]; then
    # A restarted gateway can pass its node healthcheck just before bucket
    # metadata has converged. The create then correctly reports the existing
    # bucket; wait until this node can read it instead of failing a redeploy.
    for attempt in {1..30}; do
      if garage bucket info "$BOTARENA_S3_BUCKET" >/dev/null 2>&1; then
        break
      fi
      if [[ "$attempt" == 30 ]]; then
        echo "Garage bucket metadata did not converge within 30 seconds" >&2
        exit 1
      fi
      sleep 1
    done
  else
    echo "$create_error" >&2
    exit 1
  fi
fi

if garage key info "$BOTARENA_S3_ACCESS_KEY" >/dev/null 2>&1; then
  current_secret="$(
    garage key info --show-secret "$BOTARENA_S3_ACCESS_KEY" |
      awk '/Secret key:/ { print $NF; exit }'
  )"
  if [[ "$current_secret" != "$BOTARENA_S3_SECRET_KEY" ]]; then
    echo "Garage access key exists with a different secret" >&2
    exit 1
  fi
else
  garage key import --yes \
    -n nilbots-app \
    "$BOTARENA_S3_ACCESS_KEY" \
    "$BOTARENA_S3_SECRET_KEY" >/dev/null
fi

garage bucket allow \
  --read \
  --write \
  --key "$BOTARENA_S3_ACCESS_KEY" \
  "$BOTARENA_S3_BUCKET" >/dev/null

echo "Garage cluster, bucket, and application key are ready"
