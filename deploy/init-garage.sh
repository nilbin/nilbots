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

GARAGE_INIT_ATTEMPTS="${GARAGE_INIT_ATTEMPTS:-30}"
GARAGE_INIT_RETRY_DELAY_SECONDS="${GARAGE_INIT_RETRY_DELAY_SECONDS:-1}"
if [[ ! "$GARAGE_INIT_ATTEMPTS" =~ ^[1-9][0-9]*$ ]]; then
  echo "GARAGE_INIT_ATTEMPTS must be a positive integer" >&2
  exit 1
fi
if [[ ! "$GARAGE_INIT_RETRY_DELAY_SECONDS" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
  echo "GARAGE_INIT_RETRY_DELAY_SECONDS must be a non-negative number" >&2
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

is_transient_garage_error() {
  local output="$1"
  [[ "$output" == *"ServiceUnavailable (503)"* ||
     "$output" == *"Could not reach quorum"* ||
     "$output" == *"Network error: Not connected"* ||
     "$output" == *"Connection refused"* ||
     "$output" == *"unexpected end of file"* ||
     "$output" == *"timed out"* ]]
}

retry_delay() {
  local attempt="$1"
  if (( attempt < GARAGE_INIT_ATTEMPTS )); then
    sleep "$GARAGE_INIT_RETRY_DELAY_SECONDS"
  fi
}

healthy_nodes() {
  awk '
    /^==== HEALTHY NODES ====$/ {
      inside_healthy = 1
      next
    }
    /^==== / {
      if (inside_healthy) {
        exit
      }
    }
    inside_healthy {
      print
    }
  '
}

garage_capture_retry() {
  local description="$1"
  shift
  local attempt output=""
  for ((attempt = 1; attempt <= GARAGE_INIT_ATTEMPTS; attempt++)); do
    if output="$(garage "$@" 2>&1)"; then
      printf '%s\n' "$output"
      return 0
    fi
    if ! is_transient_garage_error "$output"; then
      echo "$output" >&2
      return 1
    fi
    retry_delay "$attempt"
  done
  echo "$description did not become available after $GARAGE_INIT_ATTEMPTS attempts" >&2
  echo "$output" >&2
  return 1
}

garage_mutation_retry() {
  local description="$1"
  shift
  local attempt output=""
  for ((attempt = 1; attempt <= GARAGE_INIT_ATTEMPTS; attempt++)); do
    if output="$(garage "$@" 2>&1)"; then
      return 0
    fi
    if ! is_transient_garage_error "$output"; then
      echo "$output" >&2
      return 1
    fi
    retry_delay "$attempt"
  done
  echo "$description did not complete after $GARAGE_INIT_ATTEMPTS attempts" >&2
  echo "$output" >&2
  return 1
}

storage_services=(garage-a garage-b garage-c)
storage_peers=()
storage_ids=()
for service in "${storage_services[@]}"; do
  peer="$(node_peer "$service")"
  storage_peers+=("$peer")
  storage_ids+=("${peer%%@*}")
done
gateway_peer="$(node_peer garage-gateway)"
gateway_id="${gateway_peer%%@*}"

# A repository-free release changes the bind-mount source paths and can
# recreate all Garage containers together. Docker may then reuse the old
# addresses for different containers. Garage persists those resolved
# addresses, so a failed node can still appear in `garage status` under the
# correct ID while its stale address now reaches the wrong peer. Reconnect
# every current ID@service address and count only the HEALTHY section.
gateway_status=""
connect_error=""
for ((attempt = 1; attempt <= GARAGE_INIT_ATTEMPTS; attempt++)); do
  for peer in "${storage_peers[@]}"; do
    if ! connect_output="$(garage node connect "$peer" 2>&1)"; then
      connect_error="$connect_output"
    fi
  done

  if gateway_status="$(garage status 2>&1)"; then
    healthy_status="$(healthy_nodes <<<"$gateway_status")"
    all_connected=true
    for node_id in "${storage_ids[@]}" "$gateway_id"; do
      if [[ "$healthy_status" != *"${node_id:0:16}"* ]]; then
        all_connected=false
      fi
    done
    if [[ "$all_connected" == true ]]; then
      break
    fi
  fi

  if (( attempt == GARAGE_INIT_ATTEMPTS )); then
    echo "Garage nodes did not become healthy after $GARAGE_INIT_ATTEMPTS attempts" >&2
    if [[ -n "$connect_error" ]]; then
      echo "$connect_error" >&2
    fi
    echo "$gateway_status" >&2
    exit 1
  fi
  retry_delay "$attempt"
done

layout="$(garage_capture_retry "Garage layout metadata" layout show)"
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
  layout="$(garage_capture_retry "Garage layout metadata" layout show)"
fi

for tag in garage-a garage-b garage-c gateway; do
  if [[ "$layout" != *"$tag"* ]]; then
    echo "Garage layout does not contain expected node tag '$tag'" >&2
    exit 1
  fi
done

ensure_bucket() {
  local attempt bucket_output="" create_output="" last_output=""
  for ((attempt = 1; attempt <= GARAGE_INIT_ATTEMPTS; attempt++)); do
    if bucket_output="$(garage bucket info "$BOTARENA_S3_BUCKET" 2>&1)"; then
      return 0
    fi
    last_output="$bucket_output"

    if [[ "$bucket_output" == *"NoSuchBucket (404)"* ]]; then
      if create_output="$(garage bucket create "$BOTARENA_S3_BUCKET" 2>&1)"; then
        last_output="$create_output"
      elif [[ "$create_output" == *"BucketAlreadyExists"* ||
              "$create_output" == *"BucketAlreadyOwnedByYou"* ]] ||
           is_transient_garage_error "$create_output"; then
        last_output="$create_output"
      else
        echo "$create_output" >&2
        return 1
      fi
    elif ! is_transient_garage_error "$bucket_output"; then
      echo "$bucket_output" >&2
      return 1
    fi

    retry_delay "$attempt"
  done
  echo "Garage bucket metadata did not converge after $GARAGE_INIT_ATTEMPTS attempts" >&2
  echo "$last_output" >&2
  return 1
}

ensure_key() {
  local attempt key_output="" import_output="" current_secret="" last_output=""
  for ((attempt = 1; attempt <= GARAGE_INIT_ATTEMPTS; attempt++)); do
    if key_output="$(garage key info --show-secret "$BOTARENA_S3_ACCESS_KEY" 2>&1)"; then
      current_secret="$(awk '/Secret key:/ { print $NF; exit }' <<<"$key_output")"
      if [[ -z "$current_secret" ]]; then
        echo "Garage key metadata omitted the secret key" >&2
        return 1
      fi
      if [[ "$current_secret" != "$BOTARENA_S3_SECRET_KEY" ]]; then
        echo "Garage access key exists with a different secret" >&2
        return 1
      fi
      return 0
    fi
    last_output="$key_output"

    if [[ "$key_output" == *"NoSuchAccessKey (404)"* ]]; then
      if import_output="$(
        garage key import --yes \
          -n nilbots-app \
          "$BOTARENA_S3_ACCESS_KEY" \
          "$BOTARENA_S3_SECRET_KEY" 2>&1
      )"; then
        last_output="$import_output"
      elif [[ "$import_output" == *"Already"* ||
              "$import_output" == *"already"* ]] ||
           is_transient_garage_error "$import_output"; then
        last_output="$import_output"
      else
        echo "$import_output" >&2
        return 1
      fi
    elif ! is_transient_garage_error "$key_output"; then
      echo "$key_output" >&2
      return 1
    fi

    retry_delay "$attempt"
  done
  echo "Garage key metadata did not converge after $GARAGE_INIT_ATTEMPTS attempts" >&2
  echo "$last_output" >&2
  return 1
}

ensure_bucket
ensure_key

garage_mutation_retry \
  "Garage bucket permission grant" \
  bucket allow \
  --read \
  --write \
  --key "$BOTARENA_S3_ACCESS_KEY" \
  "$BOTARENA_S3_BUCKET"

echo "Garage cluster, bucket, and application key are ready"
