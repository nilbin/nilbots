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
grep -qx 'BOTARENA_S3_ENDPOINT=http://10.201.128.10:3900' "$standard_env"
grep -qx 'BOTARENA_WEB_BIND_ADDRESS=10.201.128.12' "$standard_env"
grep -qx 'BOTARENA_COMPILE_CPUS=1.25' "$standard_env"
grep -qx 'BOTARENA_NETWORK_HASH_KEY=certificate-secret' "$standard_env"
grep -qx 'BOTARENA_COMPILE_MEMORY=1g' "$xs_env"
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

for script in \
  deploy/bootstrap-hostup-worker.sh \
  deploy/configure-primary-worker-access.sh \
  deploy/deploy-current-worker.sh \
  deploy/hostup-vps.sh \
  deploy/refresh-primary-upstreams.sh \
  deploy/stop-worker.sh \
  deploy/unregister-worker.sh; do
  bash -n "$script"
done

echo "Worker inventory, deployment targets, secret filtering, and size profiles passed"
