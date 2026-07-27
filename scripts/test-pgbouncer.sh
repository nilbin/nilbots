#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ $# -gt 1 ||
      ($# -eq 1 && "$1" != "--application-tests") ]]; then
  echo "usage: test-pgbouncer.sh [--application-tests]" >&2
  exit 2
fi
run_application_tests=0
[[ $# -eq 0 ]] || run_application_tests=1

mkdir -p sandbox
# Docker Desktop/Colima share the checkout, while macOS's default temporary
# directory is outside the VM's bind-mount allowlist.
test_root="$(mktemp -d "$PWD/sandbox/pgbouncer-test.XXXXXX")"
suffix="$(printf '%s' "$test_root" | sha256sum | cut -c1-12)"
network="nilbots-pgbouncer-test-$suffix"
postgres_container="nilbots-postgres-test-$suffix"
pgbouncer_container="nilbots-pgbouncer-test-$suffix"
pgbouncer_image="botarena-pgbouncer:test-$suffix"
password="pgbouncer-integration-secret"

cleanup() {
  status=$?
  if [[ "$status" -ne 0 ]]; then
    docker logs "$pgbouncer_container" >&2 2>/dev/null || true
    docker logs "$postgres_container" >&2 2>/dev/null || true
  fi
  docker rm -f "$pgbouncer_container" "$postgres_container" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
  rm -rf -- "$test_root"
  return "$status"
}
trap cleanup EXIT

docker build --target pgbouncer -t "$pgbouncer_image" .
docker network create "$network" >/dev/null
docker run -d \
  --name "$postgres_container" \
  --network "$network" \
  --network-alias db \
  -e POSTGRES_USER=botarena \
  -e POSTGRES_PASSWORD="$password" \
  -e POSTGRES_DB=botarena \
  postgres:16 \
  -c shared_preload_libraries=pg_stat_statements \
  -c compute_query_id=on >/dev/null

for _ in {1..40}; do
  if docker exec "$postgres_container" \
    pg_isready -U botarena -d botarena >/dev/null 2>&1; then
    break
  fi
  sleep 0.5
done
docker exec "$postgres_container" \
  pg_isready -U botarena -d botarena >/dev/null
docker exec "$postgres_container" \
  psql -U botarena -d botarena -v ON_ERROR_STOP=1 -Atqc \
  'create extension if not exists pg_stat_statements'

scram_verifier="$(
  docker exec "$postgres_container" \
    psql -U botarena -d botarena -Atqc \
    "select rolpassword from pg_authid where rolname = 'botarena'"
)"
[[ "$scram_verifier" =~ ^SCRAM-SHA-256\$[0-9]+: ]]
printf '"botarena" "%s"\n' "$scram_verifier" \
  >"$test_root/pgbouncer-userlist.txt"
chmod 644 "$test_root/pgbouncer-userlist.txt"
# The application fixture creates disposable databases. Production deliberately
# exposes only two fixed aliases; this test-only wildcard lets the same fixture
# exercise those databases through PgBouncer without weakening production.
awk '
  /^\[databases\]$/ {
    print
    print "* = host=db port=5432 pool_mode=transaction pool_size=30 reserve_pool_size=5"
    next
  }
  { print }
' deploy/pgbouncer/pgbouncer.ini >"$test_root/pgbouncer.ini"
chmod 644 "$test_root/pgbouncer.ini"

docker run -d \
  --name "$pgbouncer_container" \
  --network "$network" \
  --network-alias pgbouncer \
  -p 127.0.0.1::6432 \
  -v "$test_root/pgbouncer.ini:/etc/pgbouncer/pgbouncer.ini:ro" \
  -v "$test_root/pgbouncer-userlist.txt:/run/botarena-secrets/pgbouncer-userlist.txt:ro" \
  "$pgbouncer_image" >/dev/null

psql_through_pool() {
  local database="$1"
  shift
  docker run --rm \
    --network "$network" \
    -e PGPASSWORD="$password" \
    postgres:16 \
    psql -h pgbouncer -p 6432 -U botarena -d "$database" "$@"
}

for _ in {1..40}; do
  if [[ "$(psql_through_pool botarena -Atqc 'select 1' 2>/dev/null)" == "1" ]]; then
    break
  fi
  sleep 0.5
done
[[ "$(psql_through_pool botarena -Atqc 'select 1')" == "1" ]]
[[ "$(psql_through_pool botarena -Atqc \
  "select count(*) > 0 from pg_extension where extname = 'pg_stat_statements'")" == "t" ]]

database_stats="$(psql_through_pool pgbouncer -Atqc 'show databases')"
grep -Eq '^botarena\|.*\|transaction\|' <<<"$database_stats"
grep -Eq '^botarena_session\|.*\|session\|' <<<"$database_stats"

[[ "$(psql_through_pool botarena_session -Atqc 'select 1')" == "1" ]]
docker restart "$pgbouncer_container" >/dev/null
for _ in {1..40}; do
  if [[ "$(psql_through_pool botarena -Atqc 'select 1' 2>/dev/null)" == "1" ]]; then
    break
  fi
  sleep 0.5
done
[[ "$(psql_through_pool botarena -Atqc 'select 1')" == "1" ]]

if [[ "$run_application_tests" == "1" ]]; then
  published_address="$(docker port "$pgbouncer_container" 6432/tcp)"
  published_port="${published_address##*:}"
  [[ "$published_port" =~ ^[0-9]+$ ]]
  dotnet build BotArena.sln --configuration Release
  BOTARENA_TEST_DB="Host=127.0.0.1;Port=$published_port;Database=botarena;Username=botarena;Password=$password;Maximum Pool Size=20" \
  BOTARENA_POSTGRES_REQUIRED=true \
    dotnet test tests/BotArena.App.Tests/BotArena.App.Tests.csproj \
      --configuration Release \
      --no-build \
      --logger "console;verbosity=normal"
fi

docker exec "$postgres_container" \
  psql -U botarena -d botarena -v ON_ERROR_STOP=1 -Atqc \
  'create table restore_smoke (id integer primary key); insert into restore_smoke values (1)'
docker exec "$postgres_container" \
  pg_dump -U botarena -d botarena --format custom >"$test_root/restore-smoke.dump"
BOTARENA_POSTGRES_CONTAINER="$postgres_container" \
  bash deploy/restore-postgres-backup.sh "$test_root/restore-smoke.dump"

echo "PgBouncer transaction pool, session alias, SCRAM, and pg_stat_statements passed"
