#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"
if [[ $# -ne 1 || "$1" != /* ]]; then
  echo "usage: $0 /absolute/off-host-synced/backup/directory" >&2
  exit 2
fi
if [[ ! -f "$deploy_dir/.env" ]]; then
  echo "missing $deploy_dir/.env" >&2
  exit 1
fi

backup_dir="$1"
mkdir -p "$backup_dir"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup="$backup_dir/botarena-$timestamp.dump"
temporary="$backup.partial"

docker compose \
  --env-file "$deploy_dir/.env" \
  -f "$deploy_dir/compose.production.yml" \
  exec -T db pg_dump --username botarena --dbname botarena --format custom >"$temporary"
if [[ ! -s "$temporary" ]]; then
  echo "database backup was empty" >&2
  exit 1
fi
mv "$temporary" "$backup"
chmod 600 "$backup"
echo "$backup"
