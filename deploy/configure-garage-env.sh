#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"
env_file="$deploy_dir/.env"
if [[ ! -f "$env_file" ]]; then
  echo "missing $env_file; create the base deployment configuration first" >&2
  exit 1
fi
if ! command -v openssl >/dev/null 2>&1; then
  echo "openssl is required to generate storage credentials" >&2
  exit 1
fi

set_if_unconfigured() {
  local name="$1"
  local value="$2"
  local current=""
  if grep -q "^${name}=" "$env_file"; then
    current="$(awk -F= -v key="$name" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "$env_file")"
    if [[ -n "$current" && "$current" != CHANGE_ME* ]]; then
      return
    fi

    local temporary
    temporary="$(mktemp "$env_file.XXXXXX")"
    awk -v key="$name" -v value="$value" '
      index($0, key "=") == 1 { print key "=" value; next }
      { print }
    ' "$env_file" >"$temporary"
    chmod 600 "$temporary"
    mv "$temporary" "$env_file"
    return
  fi

  printf '\n%s=%s\n' "$name" "$value" >>"$env_file"
}

chmod 600 "$env_file"
set_if_unconfigured GARAGE_RPC_SECRET "$(openssl rand -hex 32)"
set_if_unconfigured GARAGE_ADMIN_TOKEN "$(openssl rand -hex 32)"
set_if_unconfigured GARAGE_METRICS_TOKEN "$(openssl rand -hex 32)"
set_if_unconfigured GARAGE_ZONE hostup-stockholm
set_if_unconfigured GARAGE_NODE_CAPACITY 10GB
set_if_unconfigured BOTARENA_OBJECT_STORE s3
set_if_unconfigured BOTARENA_S3_ENDPOINT http://garage-gateway:3900
set_if_unconfigured BOTARENA_S3_ALLOW_HTTP true
set_if_unconfigured BOTARENA_S3_REGION garage
set_if_unconfigured BOTARENA_S3_BUCKET nilbots
set_if_unconfigured BOTARENA_S3_ACCESS_KEY "GK$(openssl rand -hex 16)"
set_if_unconfigured BOTARENA_S3_SECRET_KEY "$(openssl rand -hex 32)"

echo "Garage and private S3 settings are configured in deploy/.env"
