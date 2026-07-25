#!/usr/bin/env bash
set -euo pipefail

if [[ "$(id -u)" -ne 0 || $# -ne 2 ]]; then
  echo "usage: configure-primary-worker-access.sh add|remove WORKER_PRIVATE_IP" >&2
  echo "run as root on the primary VPS" >&2
  exit 2
fi

operation="$1"
worker_private_ip="$2"

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

[[ "$operation" == "add" || "$operation" == "remove" ]] ||
  { echo "operation must be add or remove" >&2; exit 2; }
is_private_ipv4 "$worker_private_ip" ||
  { echo "worker address must be private IPv4" >&2; exit 2; }

mapfile -t database_containers < <(
  docker ps \
    --filter label=com.docker.compose.project=botarena \
    --filter label=com.docker.compose.service=db \
    --format '{{.ID}}'
)
if [[ "${#database_containers[@]}" -ne 1 ]]; then
  echo "expected exactly one running botarena PostgreSQL container" >&2
  exit 1
fi
database_container="${database_containers[0]}"
hba_file="$(
  docker exec "$database_container" \
    psql -U botarena -d botarena -Atqc 'show hba_file'
)"
[[ "$hba_file" == /* ]] ||
  { echo "PostgreSQL returned an invalid hba_file path" >&2; exit 1; }

docker exec -i "$database_container" \
  bash -s -- "$operation" "$hba_file" "$worker_private_ip" <<'POSTGRES_SCRIPT'
set -euo pipefail

operation="$1"
hba_file="$2"
worker_private_ip="$3"
temporary="${hba_file}.nilbots.$$"
cleanup() {
  if [[ -f "$temporary" ]]; then
    rm -- "$temporary"
  fi
}
trap cleanup EXIT

awk -v address="$worker_private_ip/32" '
  !($1 == "host" && $2 == "all" && $3 == "all" && $4 == address)
' "$hba_file" >"$temporary"
if [[ "$operation" == "add" ]]; then
  printf 'host all all %s/32 scram-sha-256 # nilbots-worker\n' \
    "$worker_private_ip" >>"$temporary"
fi
chmod --reference="$hba_file" "$temporary"
chown --reference="$hba_file" "$temporary"
mv "$temporary" "$hba_file"
trap - EXIT
POSTGRES_SCRIPT

docker exec "$database_container" \
  psql -U botarena -d botarena -v ON_ERROR_STOP=1 \
  -Atqc 'select pg_reload_conf()' >/dev/null

rule_count="$(
  docker exec "$database_container" \
    psql -U botarena -d botarena -Atqc \
    "select count(*) from pg_hba_file_rules
     where type = 'host'
       and database = '{all}'
       and user_name = '{all}'
       and address = '$worker_private_ip'
       and netmask = '255.255.255.255'
       and auth_method = 'scram-sha-256'
       and error is null"
)"
case "$operation" in
  add)
    [[ "$rule_count" == "1" ]] ||
      { echo "PostgreSQL worker rule was not installed exactly once" >&2; exit 1; }
    ;;
  remove)
    [[ "$rule_count" == "0" ]] ||
      { echo "PostgreSQL worker rule was not removed" >&2; exit 1; }
    ;;
esac

echo "PostgreSQL worker access $operation complete for $worker_private_ip/32"
