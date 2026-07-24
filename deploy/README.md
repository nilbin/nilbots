# Nilbots production deployment

This directory is the single-VPS production shape from
[`docs/DEPLOYMENT-SCALING-PLAN.md`](../docs/DEPLOYMENT-SCALING-PLAN.md):
Caddy, one web role, one match worker, one compile worker, PostgreSQL, and a
private local object volume. The roles are separate processes of the same
modular monolith.

## Host prerequisites

- Ubuntu Server 26.04 LTS on x86-64/AMD64
- Docker Engine and the Compose plugin from Docker's official Ubuntu repository
- a domain whose A/AAAA record points at the VPS
- inbound TCP 22, 80, and 443 plus UDP 443; no published PostgreSQL/app ports
- SSH-key access through a non-root operator account

Docker-published ports can bypass uncomplicated host firewall rules. This
Compose file publishes only Caddy, but still verify the effective
`iptables`/`DOCKER-USER` policy on the VPS.

On a fresh matching VPS, `deploy/provision-host.sh` installs Docker from its
official repository, creates the `nilbots` operator from root's authorized
keys, enables security updates and the firewall, and applies conservative SSH
and Docker log settings. Run it once as root, verify a separate operator SSH
session, and only then disable root SSH.

## First deployment

From the repository root:

```bash
cp deploy/.env.example deploy/.env
chmod 600 deploy/.env
```

Replace every `CHANGE_ME` and set the real domain. Long hexadecimal secrets
avoid connection-string and `.env` escaping surprises:

```bash
openssl rand -hex 32
openssl rand -hex 32
```

Generate the OpenIddict signing/encryption certificate pair. All future web
replicas must use this same pair:

```bash
set -a
source deploy/.env
set +a
bash scripts/generate-deployment-certificates.sh "$PWD/deploy/secrets"
```

Check that DNS is live, then deploy:

```bash
bash deploy/deploy.sh
```

The script tags images with the current Git commit, starts PostgreSQL, runs the
one-shot migration/seeding role, waits for web and worker readiness, and then
starts Caddy. Verify:

```bash
docker compose --env-file deploy/.env -f deploy/compose.production.yml ps
curl --fail "https://$(awk -F= '/^BOTARENA_DOMAIN=/{print $2}' deploy/.env)/health/ready"
```

## Updating

1. Fetch and check out the exact reviewed commit.
2. Take a database backup and ensure it has copied off the VPS.
3. Run `bash deploy/deploy.sh`.
4. Test login, `/api/meta`, a submission, one match, and replay playback.

The deploy script drains job workers before migration. The migration uses an
expand-first schema change where rollback compatibility matters. To roll back,
check out the previous commit and rerun the script; do not reverse a database
migration unless that release explicitly documents a safe downgrade.

## Backup

Create a PostgreSQL custom-format backup in an absolute directory:

```bash
bash deploy/backup-postgres.sh /absolute/path/synced-off-this-vps
```

The command only creates and validates the local dump. A scheduled job must
copy it to another provider/location and alert on failure. The `objectdata`
Docker volume also needs an off-host backup until an external S3-compatible
backend replaces it. Run a restore rehearsal into a disposable deployment at
least quarterly.

## Operational checks

```bash
docker compose --env-file deploy/.env -f deploy/compose.production.yml logs --tail=200 web
docker compose --env-file deploy/.env -f deploy/compose.production.yml logs --tail=200 match-worker
docker compose --env-file deploy/.env -f deploy/compose.production.yml logs --tail=200 compile-worker
docker compose --env-file deploy/.env -f deploy/compose.production.yml exec db \
  psql -U botarena -d botarena -c \
  'select "Type", "Status", count(*) from "BackgroundJobs" group by 1,2 order by 1,2;'
```

Alert at minimum on HTTPS downtime, failed/stale backups, disk usage, repeated
job failure, and the age of the oldest pending job.

## Security boundary

This layout is appropriate for a private/friends pilot. Do not accept
submissions from arbitrary strangers until the public-submission gate in the
deployment plan is complete: compiler inputs vendored, outbound build network
disabled, cgroup/workspace limits verified, and hostile-input tests passing.
