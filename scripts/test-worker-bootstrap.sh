#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

inventory="$test_root/workers.tsv"
key_one="AAAAC3NzaC1lZDI1NTE5AAAAIBbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
key_two="AAAAC3NzaC1lZDI1NTE5AAAAICcccccccccccccccccccccccccccccccccccccc"

bash deploy/worker-inventory.sh upsert \
  "$inventory" \
  worker-2 \
  206.168.215.230 \
  nilbots \
  /srv/nilbots/deployment \
  10.201.128.11 \
  ssh-ed25519 \
  "$key_one"
bash deploy/worker-inventory.sh upsert \
  "$inventory" \
  worker-3 \
  worker-3.example.test \
  nilbots \
  /srv/nilbots/deployment \
  10.201.128.12 \
  ssh-ed25519 \
  "$key_two"
bash deploy/worker-inventory.sh validate "$inventory"

[[ "$(
  bash deploy/worker-inventory.sh upstreams "$inventory"
)" == "web:8080 10.201.128.11:8080 10.201.128.12:8080" ]]
[[ "$(
  bash deploy/worker-inventory.sh targets "$inventory"
)" == $'worker-2\t206.168.215.230\tnilbots\t/srv/nilbots/deployment\nworker-3\tworker-3.example.test\tnilbots\t/srv/nilbots/deployment' ]]
[[ "$(
  bash deploy/worker-inventory.sh known-hosts "$inventory"
)" == "206.168.215.230 ssh-ed25519 $key_one
worker-3.example.test ssh-ed25519 $key_two" ]]
[[ "$(
  bash deploy/worker-inventory.sh record "$inventory" worker-3
)" == $'worker-3\tworker-3.example.test\tnilbots\t/srv/nilbots/deployment\t10.201.128.12\tssh-ed25519\t'"$key_two" ]]
[[ -z "$(
  bash deploy/worker-inventory.sh record "$inventory" worker-missing
)" ]]

# Re-registering one stable name updates that node without duplicating it.
bash deploy/worker-inventory.sh upsert \
  "$inventory" \
  worker-3 \
  worker-3-new.example.test \
  nilbots \
  /srv/nilbots/deployment \
  10.201.128.13 \
  ssh-ed25519 \
  "$key_two"
[[ "$(bash deploy/worker-inventory.sh targets "$inventory" | wc -l | tr -d ' ')" == "2" ]]
grep -q $'^worker-3\tworker-3-new.example.test\t' "$inventory"

if bash deploy/worker-inventory.sh upsert \
  "$inventory" \
  worker-4 \
  206.168.215.230 \
  nilbots \
  /srv/nilbots/deployment \
  10.201.128.14 \
  ssh-ed25519 \
  "$key_two" 2>/dev/null; then
  echo "duplicate worker host unexpectedly passed inventory validation" >&2
  exit 1
fi
if bash deploy/worker-inventory.sh upsert \
  "$inventory" \
  public-worker \
  public.example.test \
  nilbots \
  /srv/nilbots/deployment \
  45.67.15.126 \
  ssh-ed25519 \
  "$key_two" 2>/dev/null; then
  echo "public worker application address unexpectedly passed validation" >&2
  exit 1
fi

bash deploy/worker-inventory.sh remove "$inventory" worker-3
[[ "$(bash deploy/worker-inventory.sh targets "$inventory" | wc -l | tr -d ' ')" == "1" ]]
grep -q $'^worker-2\t' "$inventory"

primary_env="$test_root/primary.env"
cat >"$primary_env" <<'EOF'
BOTARENA_DOMAIN=nilbots.test
POSTGRES_PASSWORD=database-secret
BOTARENA_OPENIDDICT_CERT_PASSWORD=certificate-secret
BOTARENA_OBJECT_STORE=s3
BOTARENA_S3_ALLOW_HTTP=true
BOTARENA_S3_REGION=garage
BOTARENA_S3_BUCKET=nilbots
BOTARENA_S3_ACCESS_KEY=GKTEST
BOTARENA_S3_SECRET_KEY=s3-secret
BOTARENA_FRONTLINE_LABS_ENABLED=true
BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY=7
BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE=2
BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE=13
GARAGE_RPC_SECRET=must-not-copy
GARAGE_ADMIN_TOKEN=must-not-copy
GARAGE_METRICS_TOKEN=must-not-copy
EOF

standard_env="$test_root/worker-standard.env"
xs_env="$test_root/worker-xs.env"
bash deploy/render-worker-env.sh \
  "$primary_env" \
  10.201.128.10 \
  10.201.128.12 \
  compile-worker-3 \
  standard >"$standard_env"
bash deploy/render-worker-env.sh \
  "$primary_env" \
  10.201.128.10 \
  10.201.128.13 \
  compile-xs-smoke \
  xs-smoke >"$xs_env"

grep -qx 'BOTARENA_DB_HOST=10.201.128.10' "$standard_env"
grep -qx 'BOTARENA_PGBOUNCER_HOST=10.201.128.10' "$standard_env"
grep -qx 'BOTARENA_DB_PORT=6432' "$standard_env"
grep -qx 'BOTARENA_DB_NAME=botarena' "$standard_env"
grep -qx 'BOTARENA_NOTIFICATION_DB_NAME=botarena_session' "$standard_env"
grep -qx 'BOTARENA_S3_ENDPOINT=http://10.201.128.10:3900' "$standard_env"
grep -qx 'BOTARENA_WEB_BIND_ADDRESS=10.201.128.12' "$standard_env"
grep -qx 'BOTARENA_WEB_INSTANCE_ID=web-compile-worker-3' "$standard_env"
grep -qx 'BOTARENA_COMPILE_CPUS=1.25' "$standard_env"
grep -qx 'BOTARENA_NETWORK_HASH_KEY=certificate-secret' "$standard_env"
grep -qx 'BOTARENA_COMPILE_MEMORY=1g' "$xs_env"
for rendered_env in "$standard_env" "$xs_env"; do
  grep -qx 'BOTARENA_FRONTLINE_LABS_ENABLED=true' "$rendered_env"
  grep -qx 'BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY=7' "$rendered_env"
  grep -qx 'BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE=2' "$rendered_env"
  grep -qx 'BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE=13' "$rendered_env"
done
compile_worker_environment="$test_root/compile-worker-environment.yml"
awk '
  /^  compile-worker:/ { in_service = 1 }
  in_service && /^    environment:/ { in_environment = 1; next }
  in_environment && /^    [a-zA-Z0-9_-]+:/ { exit }
  in_environment { print }
' deploy/compose.production.yml >"$compile_worker_environment"
for setting in \
  BOTARENA_FRONTLINE_LABS_ENABLED \
  BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY \
  BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE \
  BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE; do
  grep -Eq "^[[:space:]]+${setting}:" "$compile_worker_environment"
done
if grep -Eq '^GARAGE_(RPC_SECRET|ADMIN_TOKEN|METRICS_TOKEN)=' \
  "$standard_env" "$xs_env"; then
  echo "Garage administration secret leaked into worker environment" >&2
  exit 1
fi

if bash deploy/render-worker-env.sh \
  "$primary_env" \
  45.67.15.126 \
  10.201.128.12 \
  compile-invalid \
  standard >/dev/null 2>&1; then
  echo "public primary application address unexpectedly passed validation" >&2
  exit 1
fi

hash_file() {
  if command -v sha256sum >/dev/null; then
    sha256sum "$1" | awk '{ print $1 }'
  else
    shasum -a 256 "$1" | awk '{ print $1 }'
  fi
}

feature_worker_env="$test_root/feature-worker.env"
printf '%s\n' \
  'POSTGRES_PASSWORD=keep-database-secret' \
  'BOTARENA_S3_SECRET_KEY=keep-s3-secret' \
  'BOTARENA_WEB_MEMORY=keep-resource-setting' \
  'SENTINEL=keep-this-line' \
  'BOTARENA_FRONTLINE_LABS_ENABLED=false' \
  'BOTARENA_FRONTLINE_LABS_ENABLED=false' \
  'BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY=99' \
  'BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE=9' \
  'BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE=99' \
  >"$feature_worker_env"
chmod 600 "$feature_worker_env"
feature_arguments=()
while IFS= read -r line; do
  feature_arguments+=("$line")
done < <(
  bash deploy/sync-worker-feature-env.sh render "$primary_env"
)
bash deploy/sync-worker-feature-env.sh apply \
  "$feature_worker_env" \
  "${feature_arguments[@]}"
grep -qx 'POSTGRES_PASSWORD=keep-database-secret' "$feature_worker_env"
grep -qx 'BOTARENA_S3_SECRET_KEY=keep-s3-secret' "$feature_worker_env"
grep -qx 'BOTARENA_WEB_MEMORY=keep-resource-setting' "$feature_worker_env"
grep -qx 'SENTINEL=keep-this-line' "$feature_worker_env"
for expected in \
  'BOTARENA_FRONTLINE_LABS_ENABLED=true' \
  'BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY=7' \
  'BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE=2' \
  'BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE=13'; do
  [[ "$(grep -c "^${expected%%=*}=" "$feature_worker_env")" == "1" ]]
  grep -qx "$expected" "$feature_worker_env"
done
feature_mode="$(
  stat -c '%a' "$feature_worker_env" 2>/dev/null ||
    stat -f '%Lp' "$feature_worker_env"
)"
[[ "$feature_mode" == "600" ]]

default_primary_env="$test_root/default-primary.env"
printf 'BOTARENA_DOMAIN=nilbots.test\n' >"$default_primary_env"
default_arguments=()
while IFS= read -r line; do
  default_arguments+=("$line")
done < <(
  bash deploy/sync-worker-feature-env.sh render \
    "$default_primary_env"
)
bash deploy/sync-worker-feature-env.sh apply \
  "$feature_worker_env" \
  "${default_arguments[@]}"
grep -qx 'BOTARENA_FRONTLINE_LABS_ENABLED=false' "$feature_worker_env"
grep -qx 'BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY=10' "$feature_worker_env"
grep -qx 'BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE=1' "$feature_worker_env"
grep -qx 'BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE=4' "$feature_worker_env"

duplicate_feature_source="$test_root/duplicate-feature-source.env"
printf '%s\n' \
  'BOTARENA_FRONTLINE_LABS_ENABLED=true' \
  'BOTARENA_FRONTLINE_LABS_ENABLED=false' \
  >"$duplicate_feature_source"
if bash deploy/sync-worker-feature-env.sh render \
  "$duplicate_feature_source" >/dev/null 2>&1; then
  echo "duplicate Labs source setting unexpectedly rendered" >&2
  exit 1
fi
invalid_feature_source="$test_root/invalid-feature-source.env"
printf 'BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY=0\n' \
  >"$invalid_feature_source"
if bash deploy/sync-worker-feature-env.sh render \
  "$invalid_feature_source" >/dev/null 2>&1; then
  echo "invalid Labs source setting unexpectedly rendered" >&2
  exit 1
fi
empty_feature_source="$test_root/empty-feature-source.env"
printf 'BOTARENA_FRONTLINE_LABS_ENABLED=\n' \
  >"$empty_feature_source"
if bash deploy/sync-worker-feature-env.sh render \
  "$empty_feature_source" >/dev/null 2>&1; then
  echo "empty Labs source setting unexpectedly rendered" >&2
  exit 1
fi

feature_hash="$(hash_file "$feature_worker_env")"
if bash deploy/sync-worker-feature-env.sh apply \
  "$feature_worker_env" \
  "${default_arguments[@]:0:3}" >/dev/null 2>&1; then
  echo "incomplete worker feature update unexpectedly applied" >&2
  exit 1
fi
[[ "$(hash_file "$feature_worker_env")" == "$feature_hash" ]]
if bash deploy/sync-worker-feature-env.sh apply \
  "$feature_worker_env" \
  "${default_arguments[0]}" \
  "${default_arguments[0]}" \
  "${default_arguments[1]}" \
  "${default_arguments[2]}" >/dev/null 2>&1; then
  echo "duplicate worker feature update unexpectedly applied" >&2
  exit 1
fi
[[ "$(hash_file "$feature_worker_env")" == "$feature_hash" ]]
if bash deploy/sync-worker-feature-env.sh apply \
  "$feature_worker_env" \
  "${default_arguments[0]}" \
  "${default_arguments[1]}" \
  "${default_arguments[2]}" \
  'GARAGE_ADMIN_TOKEN=must-not-copy' >/dev/null 2>&1; then
  echo "extra worker feature update unexpectedly applied" >&2
  exit 1
fi
[[ "$(hash_file "$feature_worker_env")" == "$feature_hash" ]]

legacy_primary_env="$test_root/legacy-primary.env"
printf '%s\n' \
  'POSTGRES_PASSWORD=test' \
  'BOTARENA_DB_HOST=db' \
  'BOTARENA_POSTGRES_BIND_ADDRESS=10.201.128.10' \
  >"$legacy_primary_env"
chmod 600 "$legacy_primary_env"
bash deploy/configure-database-env.sh primary "$legacy_primary_env" >/dev/null
grep -qx 'BOTARENA_DB_HOST=db' "$legacy_primary_env"
grep -qx 'BOTARENA_PGBOUNCER_HOST=pgbouncer' "$legacy_primary_env"
grep -qx 'BOTARENA_PGBOUNCER_BIND_ADDRESS=10.201.128.10' "$legacy_primary_env"
grep -qx 'BOTARENA_DB_PORT=6432' "$legacy_primary_env"
grep -qx 'BOTARENA_NOTIFICATION_DB_NAME=botarena_session' "$legacy_primary_env"

legacy_worker_env="$test_root/legacy-worker.env"
printf '%s\n' \
  'POSTGRES_PASSWORD=test' \
  'BOTARENA_DB_HOST=10.201.128.10' \
  >"$legacy_worker_env"
chmod 600 "$legacy_worker_env"
bash deploy/configure-database-env.sh worker "$legacy_worker_env" >/dev/null
grep -qx 'BOTARENA_DB_HOST=10.201.128.10' "$legacy_worker_env"
grep -qx 'BOTARENA_PGBOUNCER_HOST=10.201.128.10' "$legacy_worker_env"
grep -qx 'BOTARENA_DB_PORT=6432' "$legacy_worker_env"

for script in \
  deploy/bootstrap-hostup-worker.sh \
  deploy/configure-primary-worker-access.sh \
  deploy/deploy-current-worker.sh \
  deploy/hostup-vps.sh \
  deploy/refresh-primary-upstreams.sh \
  deploy/sync-worker-feature-env.sh \
  deploy/stop-worker.sh \
  deploy/unregister-worker.sh; do
  bash -n "$script"
done

echo "Worker inventory, deployment targets, secret filtering, and size profiles passed"
