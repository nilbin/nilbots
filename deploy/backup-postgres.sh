#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"
if [[ $# -ne 1 || "$1" != /* ]]; then
  echo "usage: $0 /absolute/local/backup/directory" >&2
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
touch "$backup_dir/latest-success"
chmod 600 "$backup_dir/latest-success"

configured_keep="$(
  awk -F= '$1 == "BOTARENA_LOCAL_BACKUPS" {
    sub(/^[^=]*=/, "")
    print
    exit
  }' "$deploy_dir/.env"
)"
keep="${BOTARENA_LOCAL_BACKUPS:-${configured_keep:-32}}"
[[ "$keep" =~ ^[1-9][0-9]{0,3}$ ]] ||
  { echo "BOTARENA_LOCAL_BACKUPS must be between 1 and 9999" >&2; exit 1; }
backups=()
while IFS= read -r backup_name; do
  [[ -z "$backup_name" ]] || backups+=("$backup_name")
done < <(
  find "$backup_dir" -maxdepth 1 -type f \
    -name 'botarena-????????T??????Z.dump' -printf '%f\n' |
    sort -r
)
for ((index = keep; index < ${#backups[@]}; index++)); do
  rm -- "$backup_dir/${backups[$index]}"
done

echo "$backup"
