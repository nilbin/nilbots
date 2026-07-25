#!/usr/bin/env bash
set -euo pipefail

if [[ "$(id -u)" -ne 0 || $# -ne 4 ]]; then
  echo "usage: finalize-worker-host.sh PRIMARY_PRIVATE_IP WORKER_PRIVATE_IP OPERATOR WEB_PORT" >&2
  echo "run as root on the worker after operator SSH has been verified" >&2
  exit 2
fi

primary_private_ip="$1"
worker_private_ip="$2"
operator="$3"
web_port="$4"

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

is_private_ipv4 "$primary_private_ip" ||
  { echo "primary address must be private IPv4" >&2; exit 2; }
is_private_ipv4 "$worker_private_ip" ||
  { echo "worker address must be private IPv4" >&2; exit 2; }
[[ "$primary_private_ip" != "$worker_private_ip" ]] ||
  { echo "primary and worker private addresses must differ" >&2; exit 2; }
[[ "$operator" =~ ^[a-z_][a-z0-9_-]*$ ]] ||
  { echo "invalid operator user" >&2; exit 2; }
[[ "$web_port" =~ ^[0-9]{2,5}$ ]] &&
  ((10#$web_port >= 1024 && 10#$web_port <= 65535)) ||
  { echo "invalid private web port" >&2; exit 2; }
id "$operator" >/dev/null 2>&1 ||
  { echo "operator '$operator' does not exist" >&2; exit 1; }
ip -o -4 address show | awk '{ sub(/\/.*/, "", $4); print $4 }' |
  grep -Fqx "$worker_private_ip" ||
  { echo "$worker_private_ip is not assigned to this host" >&2; exit 1; }

install -d -m 755 /usr/local/sbin
firewall_script=/usr/local/sbin/nilbots-worker-firewall
cat >"$firewall_script" <<EOF
#!/usr/bin/env bash
set -euo pipefail
chain=NILBOTS-WORKER
iptables -w -N "\$chain" 2>/dev/null || true
iptables -w -F "\$chain"
iptables -w -A "\$chain" -m conntrack --ctstate RELATED,ESTABLISHED -j RETURN
iptables -w -A "\$chain" -s "$primary_private_ip" -p tcp --dport "$web_port" -j RETURN
iptables -w -A "\$chain" -p tcp --dport "$web_port" -j DROP
iptables -w -A "\$chain" -j RETURN
iptables -w -C DOCKER-USER -j "\$chain" 2>/dev/null ||
  iptables -w -I DOCKER-USER 1 -j "\$chain"
EOF
chmod 755 "$firewall_script"

cat >/etc/systemd/system/nilbots-worker-firewall.service <<'EOF'
[Unit]
Description=nilbots worker Docker ingress policy
Requires=docker.service
After=docker.service
PartOf=docker.service

[Service]
Type=oneshot
ExecStart=/usr/local/sbin/nilbots-worker-firewall
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable nilbots-worker-firewall.service
systemctl restart nilbots-worker-firewall.service

# OpenSSH uses the first value it obtains. This file sorts before the
# provisioner's 60-nilbots-hardening.conf so `no` wins over its intentionally
# transitional `prohibit-password` value.
rm -f /etc/ssh/sshd_config.d/70-nilbots-worker.conf
cat >/etc/ssh/sshd_config.d/40-nilbots-worker.conf <<EOF
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
AllowUsers $operator
EOF
sshd -t
systemctl reload ssh

echo "worker firewall persisted and root SSH disabled"
