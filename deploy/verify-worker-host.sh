#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "usage: verify-worker-host.sh DEPLOY_ROOT PRIMARY_PRIVATE_IP WORKER_PRIVATE_IP OPERATOR" >&2
  exit 2
fi

deploy_root="$1"
primary_private_ip="$2"
worker_private_ip="$3"
operator="$4"
shared="$deploy_root/shared"

fail() {
  echo "worker verification failed: $*" >&2
  exit 1
}

[[ "$(id -u)" -eq 0 ]] || fail "run as root"
[[ -f /etc/os-release ]] || fail "missing OS metadata"
. /etc/os-release
[[ "${ID:-}" == "ubuntu" && "${VERSION_ID:-}" == "26.04" ]] ||
  fail "expected Ubuntu 26.04"
[[ "$(dpkg --print-architecture)" == "amd64" ]] ||
  fail "expected amd64"
id "$operator" >/dev/null 2>&1 || fail "operator is missing"
operator_groups="$(id -nG "$operator")"
[[ " $operator_groups " == *" docker "* ]] ||
  fail "operator is not in the Docker group"
[[ " $operator_groups " == *" sudo "* ]] ||
  fail "operator is not in the sudo group"
sudoers_file="/etc/sudoers.d/90-$operator"
[[ -f "$sudoers_file" ]] || fail "operator sudo policy is missing"
visudo -cf "$sudoers_file" >/dev/null ||
  fail "operator sudo policy is invalid"
systemctl is-active --quiet docker || fail "Docker is not active"
systemctl is-enabled --quiet unattended-upgrades ||
  fail "unattended upgrades are not enabled"
systemctl is-active --quiet nilbots-worker-firewall ||
  fail "Docker ingress firewall is not active"
ufw_status="$(ufw status)"
grep -q '^Status: active$' <<<"$ufw_status" || fail "ufw is not active"
if grep -Eq '(^|[[:space:]])(80|443)/(tcp|udp)([[:space:]]|$)' \
  <<<"$ufw_status"; then
  fail "worker ufw exposes public ingress ports"
fi
[[ -f /etc/docker/daemon.json ]] || fail "Docker daemon policy is missing"
grep -q '"live-restore":[[:space:]]*true' /etc/docker/daemon.json ||
  fail "Docker live-restore is not enabled"
grep -q '"max-size":[[:space:]]*"10m"' /etc/docker/daemon.json ||
  fail "Docker log size is not bounded"
grep -q '"max-file":[[:space:]]*"3"' /etc/docker/daemon.json ||
  fail "Docker log retention is not bounded"
effective_sshd="$(sshd -T)"
grep -qx 'permitrootlogin no' <<<"$effective_sshd" ||
  fail "root SSH is not disabled"
grep -qx 'passwordauthentication no' <<<"$effective_sshd" ||
  fail "password SSH authentication is enabled"
ip -o -4 address show | awk '{ sub(/\/.*/, "", $4); print $4 }' |
  grep -Fqx "$worker_private_ip" ||
  fail "private address is not assigned"

[[ -f "$shared/.env" ]] || fail "shared worker environment is missing"
[[ "$(
  stat -c '%a:%u:%g' "$shared/.env"
)" == "600:$(id -u "$operator"):$(id -g "$operator")" ]] ||
  fail "shared worker environment has incorrect mode or ownership"
if [[ -f "$shared/role" && "$(<"$shared/role")" != "worker" ]]; then
  fail "deployment role is not worker"
fi
for key in \
  BOTARENA_DOMAIN \
  POSTGRES_PASSWORD \
  BOTARENA_DB_HOST \
  BOTARENA_PGBOUNCER_HOST \
  BOTARENA_DB_PORT \
  BOTARENA_DB_NAME \
  BOTARENA_NOTIFICATION_DB_NAME \
  BOTARENA_OPENIDDICT_CERT_PASSWORD \
  BOTARENA_NETWORK_HASH_KEY \
  BOTARENA_S3_ENDPOINT \
  BOTARENA_S3_ACCESS_KEY \
  BOTARENA_S3_SECRET_KEY \
  BOTARENA_WEB_BIND_ADDRESS \
  BOTARENA_COMPILE_INSTANCE_ID; do
  [[ "$(grep -c "^${key}=" "$shared/.env")" -eq 1 ]] ||
    fail "environment must contain exactly one $key"
done
if grep -Eq '^(GARAGE_RPC_SECRET|GARAGE_ADMIN_TOKEN|GARAGE_METRICS_TOKEN)=' \
  "$shared/.env"; then
  fail "worker received Garage administration credentials"
fi
grep -qx "BOTARENA_DB_HOST=$primary_private_ip" "$shared/.env" ||
  fail "legacy database rollback does not use the primary private address"
grep -qx "BOTARENA_PGBOUNCER_HOST=$primary_private_ip" "$shared/.env" ||
  fail "PgBouncer does not use the primary private address"
grep -qx "BOTARENA_DB_PORT=6432" "$shared/.env" ||
  fail "database does not use PgBouncer"
grep -qx "BOTARENA_DB_NAME=botarena" "$shared/.env" ||
  fail "database transaction-pool alias is incorrect"
grep -qx "BOTARENA_NOTIFICATION_DB_NAME=botarena_session" "$shared/.env" ||
  fail "database notification-pool alias is incorrect"
grep -qx "BOTARENA_S3_ENDPOINT=http://$primary_private_ip:3900" "$shared/.env" ||
  fail "S3 does not use the primary private address"
grep -qx "BOTARENA_WEB_BIND_ADDRESS=$worker_private_ip" "$shared/.env" ||
  fail "web does not bind the worker private address"

for certificate in openiddict-signing.pfx openiddict-encryption.pfx; do
  path="$shared/secrets/$certificate"
  [[ -s "$path" ]] || fail "$certificate is missing"
  [[ "$(stat -c '%a:%u:%g' "$path")" == "660:1654:$(id -g "$operator")" ]] ||
    fail "$certificate has incorrect mode or ownership"
done

iptables -w -C NILBOTS-WORKER \
  -s "$primary_private_ip" -p tcp --dport 8080 -j RETURN ||
  fail "primary web-ingress allow rule is missing"
iptables -w -C NILBOTS-WORKER -p tcp --dport 8080 -j DROP ||
  fail "worker web-ingress deny rule is missing"

timeout 5 bash -c "</dev/tcp/$primary_private_ip/6432" 2>/dev/null ||
  fail "PgBouncer is unreachable over the private network"
postgres_password="$(
  awk -F= '$1 == "POSTGRES_PASSWORD" {
    sub(/^[^=]*=/, "")
    print
  }' "$shared/.env"
)"
[[ -n "$postgres_password" ]] || fail "database password is empty"
PGPASSWORD="$postgres_password" \
  psql \
    "host=$primary_private_ip port=6432 dbname=botarena user=botarena connect_timeout=5" \
    -Atqc 'select 1' |
  grep -qx 1 ||
  fail "PgBouncer authentication/query failed over the private network"
timeout 5 bash -c "</dev/tcp/$primary_private_ip/3900" 2>/dev/null ||
  fail "Garage S3 is unreachable over the private network"

echo "worker host, secrets, private network, SSH, Docker, and firewalls verified"
