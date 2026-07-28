#!/usr/bin/env bash
set -euo pipefail

if [[ "$(id -u)" -ne 0 || $# -ne 1 ]]; then
  echo "usage: sudo install-primary-maintenance.sh /absolute/deployment/root" >&2
  exit 2
fi
deploy_root="$1"
if [[ "$deploy_root" != /* ||
      "$deploy_root" == "/" ||
      "$deploy_root" == *"//"* ||
      "$deploy_root" == *"/../"* ||
      ! -f "$deploy_root/shared/.env" ]]; then
  echo "invalid deployment root" >&2
  exit 2
fi
operator="$(stat -c '%U' "$deploy_root/shared/.env")"
[[ "$operator" =~ ^[a-z_][a-z0-9_-]*$ ]] ||
  { echo "could not resolve deployment operator" >&2; exit 1; }

cat >/etc/systemd/system/nilbots-postgres-backup.service <<EOF
[Unit]
Description=nilbots local PostgreSQL backup
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
User=$operator
ExecStart=/bin/bash $deploy_root/current/deploy/backup-postgres.sh $deploy_root/shared/backups
EOF

cat >/etc/systemd/system/nilbots-postgres-backup.timer <<'EOF'
[Unit]
Description=Nightly nilbots local PostgreSQL backup

[Timer]
OnCalendar=*-*-* 03:17:00 UTC
RandomizedDelaySec=15m
Persistent=true

[Install]
WantedBy=timers.target
EOF

cat >/etc/systemd/system/nilbots-postgres-restore-rehearsal.service <<EOF
[Unit]
Description=nilbots disposable PostgreSQL restore rehearsal
Requires=docker.service
After=docker.service nilbots-postgres-backup.service

[Service]
Type=oneshot
User=$operator
ExecStart=/bin/bash $deploy_root/current/deploy/restore-postgres-backup.sh --latest $deploy_root/shared/backups
EOF

cat >/etc/systemd/system/nilbots-postgres-restore-rehearsal.timer <<'EOF'
[Unit]
Description=Weekly nilbots PostgreSQL restore rehearsal

[Timer]
OnCalendar=Sun *-*-* 04:17:00 UTC
RandomizedDelaySec=15m
Persistent=true

[Install]
WantedBy=timers.target
EOF

systemctl daemon-reload
systemctl enable --now \
  nilbots-postgres-backup.timer \
  nilbots-postgres-restore-rehearsal.timer >/dev/null
echo "Nightly local backups and weekly disposable restore rehearsals installed"
