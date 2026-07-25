#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 5 ]]; then
  echo "usage: render-worker-env.sh PRIMARY_ENV PRIMARY_PRIVATE_IP WORKER_PRIVATE_IP INSTANCE_ID SIZE" >&2
  exit 2
fi

source_env="$1"
primary_private_ip="$2"
worker_private_ip="$3"
instance_id="$4"
size="$5"

is_private_ipv4() {
  local ip="$1"
  local a b c d
  IFS=. read -r a b c d <<<"$ip"
  for octet in "$a" "$b" "$c" "$d"; do
    [[ "$octet" =~ ^[0-9]{1,3}$ ]] || return 1
    ((10#$octet <= 255)) || return 1
  done
  ((10#$a == 10)) ||
    ((10#$a == 172 && 10#$b >= 16 && 10#$b <= 31)) ||
    ((10#$a == 192 && 10#$b == 168))
}

is_private_ipv4 "$primary_private_ip" ||
  { echo "primary address must be private IPv4" >&2; exit 2; }
is_private_ipv4 "$worker_private_ip" ||
  { echo "worker address must be private IPv4" >&2; exit 2; }
[[ "$primary_private_ip" != "$worker_private_ip" ]] ||
  { echo "primary and worker private addresses must differ" >&2; exit 2; }
[[ "$instance_id" =~ ^[a-z0-9][a-z0-9-]{0,62}$ ]] ||
  { echo "invalid worker instance ID" >&2; exit 2; }
[[ -f "$source_env" ]] ||
  { echo "primary environment does not exist" >&2; exit 1; }

line_for() {
  local key="$1"
  awk -v key="$key" '
    index($0, key "=") == 1 {
      count += 1
      value = $0
    }
    END {
      if (count == 1) print value
      else if (count > 1) exit 2
    }
  ' "$source_env"
}

emit_required() {
  local key="$1"
  local line
  line="$(line_for "$key")" ||
    { echo "primary environment contains duplicate $key" >&2; exit 1; }
  [[ -n "$line" ]] ||
    { echo "primary environment is missing $key" >&2; exit 1; }
  printf '%s\n' "$line"
}

emit_optional() {
  local key="$1"
  local line
  line="$(line_for "$key")" ||
    { echo "primary environment contains duplicate $key" >&2; exit 1; }
  [[ -z "$line" ]] || printf '%s\n' "$line"
}

object_store_line="$(line_for BOTARENA_OBJECT_STORE)"
object_store="${object_store_line#*=}"
object_store="${object_store%\"}"
object_store="${object_store#\"}"
[[ "$object_store" == "s3" ]] ||
  { echo "primary object store must be s3" >&2; exit 1; }

for key in \
  BOTARENA_DOMAIN \
  POSTGRES_PASSWORD \
  BOTARENA_OPENIDDICT_CERT_PASSWORD \
  BOTARENA_OBJECT_STORE \
  BOTARENA_S3_ALLOW_HTTP \
  BOTARENA_S3_REGION \
  BOTARENA_S3_BUCKET \
  BOTARENA_S3_ACCESS_KEY \
  BOTARENA_S3_SECRET_KEY; do
  emit_required "$key"
done

network_hash="$(line_for BOTARENA_NETWORK_HASH_KEY)"
if [[ -n "$network_hash" ]]; then
  printf '%s\n' "$network_hash"
else
  cert_password="$(line_for BOTARENA_OPENIDDICT_CERT_PASSWORD)"
  printf 'BOTARENA_NETWORK_HASH_KEY=%s\n' "${cert_password#*=}"
fi

for key in \
  BOTARENA_COMPILE_ACCOUNT_10M \
  BOTARENA_COMPILE_ACCOUNT_DAILY \
  BOTARENA_COMPILE_NETWORK_10M \
  BOTARENA_COMPILE_NETWORK_DAILY \
  BOTARENA_COMPILE_ACCOUNT_QUEUED \
  BOTARENA_COMPILE_GLOBAL_QUEUED \
  BOTARENA_BUILD_TIMEOUT_SECONDS; do
  emit_optional "$key"
done

printf 'BOTARENA_DB_HOST=%s\n' "$primary_private_ip"
printf 'BOTARENA_S3_ENDPOINT=http://%s:3900\n' "$primary_private_ip"
printf 'BOTARENA_WEB_BIND_ADDRESS=%s\n' "$worker_private_ip"
printf 'BOTARENA_WEB_PORT=8080\n'
printf 'BOTARENA_COMPILE_INSTANCE_ID=%s\n' "$instance_id"
printf 'BOTARENA_COMPILE_WORKERS=1\n'

case "$size" in
  standard)
    printf '%s\n' \
      'BOTARENA_WEB_MEMORY=768m' \
      'BOTARENA_COMPILE_CPUS=1.25' \
      'BOTARENA_COMPILE_MEMORY=3g' \
      'BOTARENA_COMPILE_COORDINATOR_CPUS=0.25' \
      'BOTARENA_COMPILE_COORDINATOR_MEMORY=384m' \
      'BOTARENA_COMPILE_WORKSPACE_SIZE=1g'
    ;;
  xs-smoke)
    printf '%s\n' \
      'BOTARENA_WEB_MEMORY=384m' \
      'BOTARENA_COMPILE_CPUS=0.5' \
      'BOTARENA_COMPILE_MEMORY=1g' \
      'BOTARENA_COMPILE_COORDINATOR_CPUS=0.15' \
      'BOTARENA_COMPILE_COORDINATOR_MEMORY=192m' \
      'BOTARENA_COMPILE_WORKSPACE_SIZE=512m'
    ;;
  *)
    echo "worker size must be standard or xs-smoke" >&2
    exit 2
    ;;
esac
