#!/usr/bin/env bash
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  echo "run this script as root on the target Ubuntu host" >&2
  exit 1
fi

operator="${BOTARENA_OPERATOR:-nilbots}"
if [[ ! "$operator" =~ ^[a-z_][a-z0-9_-]*$ ]]; then
  echo "BOTARENA_OPERATOR must be a valid Linux user name" >&2
  exit 2
fi

public_ingress="${BOTARENA_PUBLIC_INGRESS:-1}"
if [[ "$public_ingress" != "0" && "$public_ingress" != "1" ]]; then
  echo "BOTARENA_PUBLIC_INGRESS must be 0 or 1" >&2
  exit 2
fi

. /etc/os-release
if [[ "${ID:-}" != "ubuntu" || "${VERSION_ID:-}" != "26.04" ]]; then
  echo "expected Ubuntu 26.04; found ${PRETTY_NAME:-unknown}" >&2
  exit 1
fi
if [[ "$(dpkg --print-architecture)" != "amd64" ]]; then
  echo "expected amd64; found $(dpkg --print-architecture)" >&2
  exit 1
fi
if [[ ! -s /root/.ssh/authorized_keys ]]; then
  echo "/root/.ssh/authorized_keys is missing; refusing to create an inaccessible operator" >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y ca-certificates curl git postgresql-client unattended-upgrades ufw

if ! id "$operator" >/dev/null 2>&1; then
  useradd --create-home --shell /bin/bash "$operator"
fi
usermod -aG sudo "$operator"
install -d -m 700 -o "$operator" -g "$operator" "/home/$operator/.ssh"
install -m 600 -o "$operator" -g "$operator" \
  /root/.ssh/authorized_keys "/home/$operator/.ssh/authorized_keys"
printf '%s ALL=(ALL:ALL) NOPASSWD: ALL\n' "$operator" \
  >"/etc/sudoers.d/90-$operator"
chmod 440 "/etc/sudoers.d/90-$operator"
visudo -cf "/etc/sudoers.d/90-$operator" >/dev/null

install -m 755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
cat >/etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: ${UBUNTU_CODENAME:-$VERSION_CODENAME}
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io \
  docker-buildx-plugin docker-compose-plugin
usermod -aG docker "$operator"

if [[ ! -e /etc/docker/daemon.json ]]; then
  cat >/etc/docker/daemon.json <<'EOF'
{
  "live-restore": true,
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
EOF
fi
systemctl enable --now docker
systemctl restart docker

cat >/etc/apt/apt.conf.d/20auto-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF
systemctl enable --now unattended-upgrades

cat >/etc/ssh/sshd_config.d/60-nilbots-hardening.conf <<'EOF'
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitEmptyPasswords no
PermitRootLogin prohibit-password
X11Forwarding no
MaxAuthTries 3
EOF
sshd -t
systemctl reload ssh

ufw default deny incoming
ufw default allow outgoing
ufw allow OpenSSH
if [[ "$public_ingress" == "1" ]]; then
  ufw allow 80/tcp
  ufw allow 443/tcp
  ufw allow 443/udp
fi
ufw --force enable

timedatectl set-timezone UTC
timedatectl set-ntp true
install -d -m 750 -o "$operator" -g "$operator" /srv/nilbots

echo "host provisioned (public ingress: $public_ingress); verify SSH as $operator before disabling root SSH"
