#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "usage: restore-postgres-backup.sh BACKUP.dump | --latest BACKUP_DIRECTORY" >&2
  exit 2
}

if [[ $# -eq 2 && "$1" == "--latest" ]]; then
  backup_dir="$2"
  [[ "$backup_dir" == /* && -d "$backup_dir" ]] || usage
  backup="$(
    find "$backup_dir" -maxdepth 1 -type f \
      -name 'botarena-????????T??????Z.dump' -printf '%T@ %p\n' |
      sort -nr |
      awk 'NR == 1 { sub(/^[^ ]+ /, ""); print; exit }'
  )"
elif [[ $# -eq 1 ]]; then
  backup="$1"
else
  usage
fi
[[ "$backup" == /* && -s "$backup" && ! -L "$backup" ]] ||
  { echo "backup must be a non-empty regular file at an absolute path" >&2; exit 1; }

database_containers=()
if [[ -n "${BOTARENA_POSTGRES_CONTAINER:-}" ]]; then
  [[ "$(docker inspect --format '{{.State.Running}}' \
    "$BOTARENA_POSTGRES_CONTAINER" 2>/dev/null)" == "true" ]] ||
    { echo "BOTARENA_POSTGRES_CONTAINER is not running" >&2; exit 1; }
  database_containers=("$BOTARENA_POSTGRES_CONTAINER")
else
  while IFS= read -r container_id; do
    [[ -z "$container_id" ]] || database_containers+=("$container_id")
  done < <(
    docker ps \
      --filter label=com.docker.compose.project=botarena \
      --filter label=com.docker.compose.service=db \
      --format '{{.ID}}'
  )
fi
[[ "${#database_containers[@]}" -eq 1 ]] ||
  { echo "expected exactly one running botarena PostgreSQL container" >&2; exit 1; }
postgres_image="$(
  docker inspect --format '{{.Config.Image}}' "${database_containers[0]}"
)"
[[ -n "$postgres_image" ]] ||
  { echo "could not resolve the production PostgreSQL image" >&2; exit 1; }

container="nilbots-postgres-restore-$$"
password="$(openssl rand -hex 32)"
cleanup() {
  docker rm -f -v "$container" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker run -d \
  --name "$container" \
  -e POSTGRES_USER=botarena \
  -e POSTGRES_PASSWORD="$password" \
  -e POSTGRES_DB=botarena_restore \
  "$postgres_image" >/dev/null
for _ in {1..60}; do
  if docker exec "$container" \
    pg_isready -U botarena -d botarena_restore >/dev/null 2>&1; then
    break
  fi
  sleep 0.5
done
docker exec "$container" \
  pg_isready -U botarena -d botarena_restore >/dev/null
docker exec -i "$container" \
  pg_restore \
    --username botarena \
    --dbname botarena_restore \
    --exit-on-error <"$backup"

table_count="$(
  docker exec "$container" \
    psql -U botarena -d botarena_restore -Atqc \
    "select count(*) from pg_class c
     join pg_namespace n on n.oid = c.relnamespace
     where c.relkind = 'r'
       and n.nspname not in ('pg_catalog', 'information_schema')"
)"
[[ "$table_count" =~ ^[1-9][0-9]*$ ]] ||
  { echo "restored database contains no application tables" >&2; exit 1; }

echo "Restored $(basename "$backup") into a disposable PostgreSQL container ($table_count tables)"
