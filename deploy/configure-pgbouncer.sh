#!/usr/bin/env bash
set -euo pipefail

if [[ $# -gt 1 ]]; then
  echo "usage: configure-pgbouncer.sh [DEPLOY_DIRECTORY]" >&2
  exit 2
fi

deploy_dir="${1:-$(cd "$(dirname "$0")" && pwd)}"
secret_dir="$deploy_dir/secrets"
target="$secret_dir/pgbouncer-userlist.txt"

[[ -d "$secret_dir" ]] ||
  { echo "missing deployment secrets directory: $secret_dir" >&2; exit 1; }

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

docker exec "$database_container" \
  psql -U botarena -d botarena -v ON_ERROR_STOP=1 -Atqc \
  'create extension if not exists pg_stat_statements' >/dev/null

shared_preloads="$(
  docker exec "$database_container" \
    psql -U botarena -d botarena -Atqc 'show shared_preload_libraries'
)"
if [[ ",$shared_preloads," != *",pg_stat_statements,"* ]]; then
  echo "PostgreSQL did not preload pg_stat_statements" >&2
  exit 1
fi

scram_verifier="$(
  docker exec "$database_container" \
    psql -U botarena -d botarena -Atqc \
    "select rolpassword from pg_authid where rolname = 'botarena'"
)"
if [[ ! "$scram_verifier" =~ ^SCRAM-SHA-256\$[0-9]+: ]]; then
  echo "botarena does not have a SCRAM-SHA-256 PostgreSQL verifier" >&2
  exit 1
fi

temporary="$(mktemp)"
cleanup() {
  rm -f -- "$temporary"
}
trap cleanup EXIT
printf '"botarena" "%s"\n' "$scram_verifier" >"$temporary"
chmod 600 "$temporary"

target_group="$(stat -c '%g' "$secret_dir")"
if [[ "$(id -u)" -eq 0 ]]; then
  install -m 600 "$temporary" "${target}.new"
  chown "1654:$target_group" "${target}.new"
  mv -f -- "${target}.new" "$target"
else
  sudo -n install -m 600 "$temporary" "${target}.new"
  sudo -n chown "1654:$target_group" "${target}.new"
  sudo -n mv -f -- "${target}.new" "$target"
fi

echo "PgBouncer authentication and pg_stat_statements are configured"
