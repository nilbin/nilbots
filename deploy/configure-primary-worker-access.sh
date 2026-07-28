#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
usage:
  configure-primary-worker-access.sh add|remove WORKER_PRIVATE_IP
  configure-primary-worker-access.sh sync WORKER_INVENTORY

Run as root on the primary VPS. The exact worker addresses are applied both to
PostgreSQL's compatibility HBA rules and a persistent Docker ingress firewall
covering PostgreSQL (5432) and PgBouncer (6432).
EOF
  exit 2
}

[[ "$(id -u)" -eq 0 ]] || { echo "run as root on the primary VPS" >&2; exit 1; }
[[ $# -eq 2 ]] || usage
operation="$1"
argument="$2"
[[ "$operation" == "add" ||
   "$operation" == "remove" ||
   "$operation" == "sync" ]] || usage

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

state_dir=/etc/nilbots
worker_addresses="$state_dir/pgbouncer-workers"
firewall_script=/usr/local/sbin/nilbots-primary-database-firewall
install -d -m 755 "$state_dir"
touch "$worker_addresses"
chmod 600 "$worker_addresses"

next_addresses="$(mktemp "$state_dir/pgbouncer-workers.XXXXXX")"
cleanup() {
  rm -f -- "$next_addresses"
}
trap cleanup EXIT

case "$operation" in
  add|remove)
    is_private_ipv4 "$argument" ||
      { echo "worker address must be private IPv4" >&2; exit 2; }
    awk -v address="$argument" '$0 != address && NF { print }' \
      "$worker_addresses" >"$next_addresses"
    if [[ "$operation" == "add" ]]; then
      printf '%s\n' "$argument" >>"$next_addresses"
    fi
    ;;
  sync)
    [[ -f "$argument" ]] ||
      { echo "worker inventory does not exist: $argument" >&2; exit 1; }
    awk -F '\t' 'NF && $1 !~ /^#/ { print $5 }' "$argument" >"$next_addresses"
    ;;
esac

while IFS= read -r worker_private_ip; do
  [[ -z "$worker_private_ip" ]] ||
    is_private_ipv4 "$worker_private_ip" ||
    { echo "invalid private worker address: $worker_private_ip" >&2; exit 1; }
done <"$next_addresses"
sort -u -o "$next_addresses" "$next_addresses"
chmod 600 "$next_addresses"
mv -f -- "$next_addresses" "$worker_addresses"
trap - EXIT
mapfile -t allowed_addresses <"$worker_addresses"

cat >"$firewall_script" <<'FIREWALL'
#!/usr/bin/env bash
set -euo pipefail
addresses=/etc/nilbots/pgbouncer-workers
chain=NILBOTS-DATABASE

iptables -w -N "$chain" 2>/dev/null || true
iptables -w -F "$chain"
iptables -w -A "$chain" \
  -m conntrack --ctstate RELATED,ESTABLISHED -j RETURN
# DOCKER-USER also sees traffic originating on Docker bridges. Keep local
# container-to-container database traffic out of the external worker allowlist.
iptables -w -A "$chain" -i 'br+' -j RETURN
iptables -w -A "$chain" -i docker0 -j RETURN
while IFS= read -r address; do
  [[ -z "$address" ]] || iptables -w -A "$chain" \
    -s "$address" -p tcp -m multiport --dports 5432,6432 -j RETURN
done <"$addresses"
iptables -w -A "$chain" \
  -p tcp -m multiport --dports 5432,6432 -j DROP
iptables -w -A "$chain" -j RETURN
iptables -w -C DOCKER-USER -j "$chain" 2>/dev/null ||
  iptables -w -I DOCKER-USER 1 -j "$chain"
FIREWALL
chmod 755 "$firewall_script"

cat >/etc/systemd/system/nilbots-primary-database-firewall.service <<EOF
[Unit]
Description=nilbots exact-address database container ingress
Requires=docker.service
PartOf=docker.service
After=docker.service

[Service]
Type=oneshot
ExecStart=$firewall_script
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable --now nilbots-primary-database-firewall.service >/dev/null
"$firewall_script"
for address in "${allowed_addresses[@]:-}"; do
  [[ -z "$address" ]] || iptables -w -C NILBOTS-DATABASE \
    -s "$address" -p tcp -m multiport --dports 5432,6432 -j RETURN
done
iptables -w -C NILBOTS-DATABASE \
  -p tcp -m multiport --dports 5432,6432 -j DROP

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
  bash -s -- "$hba_file" "${allowed_addresses[@]}" <<'POSTGRES_SCRIPT'
set -euo pipefail
hba_file="$1"
shift
temporary="${hba_file}.nilbots.$$"
cleanup() {
  rm -f -- "$temporary"
}
trap cleanup EXIT
awk '
  !($0 ~ /# nilbots-worker$/)
' "$hba_file" >"$temporary"
for address in "$@"; do
  printf 'host all all %s/32 scram-sha-256 # nilbots-worker\n' \
    "$address" >>"$temporary"
done
chmod --reference="$hba_file" "$temporary"
chown --reference="$hba_file" "$temporary"
mv "$temporary" "$hba_file"
trap - EXIT
POSTGRES_SCRIPT

docker exec "$database_container" \
  psql -U botarena -d botarena -v ON_ERROR_STOP=1 \
  -Atqc 'select pg_reload_conf()' >/dev/null

expected_count="${#allowed_addresses[@]}"
for address in "${allowed_addresses[@]}"; do
  rule_count="$(
    docker exec "$database_container" \
      psql -U botarena -d botarena -Atqc \
      "select count(*) from pg_hba_file_rules
       where type = 'host'
         and database = '{all}'
         and user_name = '{all}'
         and address = '$address'
         and netmask = '255.255.255.255'
         and auth_method = 'scram-sha-256'
         and error is null"
  )"
  [[ "$rule_count" == "1" ]] ||
    { echo "PostgreSQL rule is not exact for $address/32" >&2; exit 1; }
done

echo "Primary database access synchronized for $expected_count worker(s)"
