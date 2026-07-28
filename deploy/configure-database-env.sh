#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 || ("$1" != "primary" && "$1" != "worker") ]]; then
  echo "usage: configure-database-env.sh primary|worker ENV_FILE" >&2
  exit 2
fi
role="$1"
requested_env="$2"
[[ -f "$requested_env" ]] ||
  { echo "environment file does not exist: $requested_env" >&2; exit 1; }
env_file="$(realpath "$requested_env")"

value_for() {
  local key="$1"
  awk -F= -v key="$key" '
    $1 == key {
      count += 1
      sub(/^[^=]*=/, "")
      value = $0
    }
    END {
      if (count == 1) print value
      else if (count > 1) exit 2
    }
  ' "$env_file"
}

is_private_or_loopback_ipv4() {
  local ip="$1"
  local a b c d
  IFS=. read -r a b c d <<<"$ip"
  for octet in "$a" "$b" "$c" "$d"; do
    [[ "$octet" =~ ^[0-9]{1,3}$ ]] || return 1
    ((10#$octet <= 255)) || return 1
  done
  ((10#$a == 127)) ||
    ((10#$a == 10)) ||
    ((10#$a == 172 && 10#$b >= 16 && 10#$b <= 31)) ||
    ((10#$a == 192 && 10#$b == 168))
}

set_value() {
  local key="$1"
  local value="$2"
  local temporary
  temporary="$(mktemp "${env_file}.XXXXXX")"
  awk -F= -v key="$key" '$1 != key { print }' "$env_file" >"$temporary"
  printf '%s=%s\n' "$key" "$value" >>"$temporary"
  mode="$(stat -c '%a' "$env_file" 2>/dev/null ||
    stat -f '%Lp' "$env_file")"
  chmod "$mode" "$temporary"
  mv -f -- "$temporary" "$env_file"
}

case "$role" in
  primary)
    bind_address="$(value_for BOTARENA_PGBOUNCER_BIND_ADDRESS)" ||
      { echo "duplicate PgBouncer bind setting" >&2; exit 1; }
    if [[ -z "$bind_address" ]]; then
      bind_address="$(value_for BOTARENA_POSTGRES_BIND_ADDRESS)" ||
        { echo "duplicate PostgreSQL bind setting" >&2; exit 1; }
    fi
    bind_address="${bind_address:-127.0.0.1}"
    is_private_or_loopback_ipv4 "$bind_address" ||
      { echo "primary database bind must be loopback or private IPv4" >&2; exit 1; }
    set_value BOTARENA_PGBOUNCER_HOST pgbouncer
    set_value BOTARENA_PGBOUNCER_BIND_ADDRESS "$bind_address"
    ;;
  worker)
    database_host="$(value_for BOTARENA_DB_HOST)" ||
      { echo "duplicate database host setting" >&2; exit 1; }
    is_private_or_loopback_ipv4 "$database_host" ||
      { echo "worker database host must be private IPv4" >&2; exit 1; }
    [[ "$database_host" != 127.* ]] ||
      { echo "worker database host cannot be loopback" >&2; exit 1; }
    set_value BOTARENA_PGBOUNCER_HOST "$database_host"
    ;;
esac

set_value BOTARENA_DB_PORT 6432
set_value BOTARENA_DB_NAME botarena
set_value BOTARENA_NOTIFICATION_DB_NAME botarena_session
set_value BOTARENA_DB_MAX_POOL_SIZE 20
set_value BOTARENA_NOTIFICATION_DB_MAX_POOL_SIZE 2

echo "$role database environment uses PgBouncer transaction/session aliases"
