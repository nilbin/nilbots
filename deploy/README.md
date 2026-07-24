# Nilbots production deployment

This directory is the public-beta, single-VPS production shape from
[`docs/DEPLOYMENT-SCALING-PLAN.md`](../docs/DEPLOYMENT-SCALING-PLAN.md):
Caddy, one web role, one match worker, a compilation coordinator, a
networkless compiler runner, PostgreSQL, and a private local object volume.
The application roles remain separate processes of the same modular monolith.

Production uses two custom images:

- `nilbots-runtime`: web, migrations, match worker, and compilation
  coordinator;
- `nilbots-compiler`: the unprivileged, offline compiler runner.

GitHub Actions publishes both to GHCR by immutable digest. It is deliberately
manual-only: no push or pull-request event consumes Actions minutes.

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

Check that DNS is live, then perform a local-image deployment:

```bash
bash deploy/deploy.sh --build-local
```

The normal production release path is the repository's **Manual release**
workflow:

- `verify` runs the end-to-end test pipeline only;
- `publish` also publishes both SHA-tagged images, SBOMs, and GitHub build
  provenance to GHCR;
- `publish-and-deploy` additionally deploys those exact image digests to the
  production environment.

The deploy script validates immutable GHCR digests, starts PostgreSQL, takes
and validates a local pre-release database dump, drains workers, runs the
one-shot migration/seeding role, waits for the compiler runner, web, and
workers, and then starts Caddy. Verify:

```bash
docker compose --env-file deploy/.env -f deploy/compose.production.yml ps
curl --fail "https://$(awk -F= '/^BOTARENA_DOMAIN=/{print $2}' deploy/.env)/health/ready"
```

## Updating

1. Select the exact reviewed commit in GitHub Actions.
2. Run **Manual release** with `publish-and-deploy`.
3. Confirm every service is healthy.
4. Test registration/login, `/api/meta`, a submission, its public build
   receipt and WASM artifact, one match, and replay playback.

The deploy script retains the previous digest pair. To roll back application
images:

```bash
bash deploy/deploy.sh --rollback
```

The migration uses an expand-first schema change where rollback compatibility
matters. Do not reverse a database migration unless that release explicitly
documents a safe downgrade.

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

Registration is public. Compilation admission is bounded durably in
PostgreSQL by account, server-keyed `/24` IPv4 or `/64` IPv6 network
pseudonym, per-account queue depth, and global queue depth. Caddy and Kestrel
both reject oversized submission bodies.

The networked `compile-worker` is only a coordinator: it reads jobs and moves
files through a shared queue. The `compiler-runner` has no network, database,
object-store, authentication, registry, or Docker credentials. It runs as UID
1654 with a read-only root filesystem, dropped capabilities, PID/memory/CPU/
file limits, and disposable tmpfs workspaces. Its required SDK, guest, NuGet,
and WASI inputs are baked into the compiler image.

Successful builds are validated as WASM before storage. The bot detail API
publishes a build receipt and content hash, and the immutable WASM artifact is
publicly downloadable; submitted source and compiler logs remain owner-only.

This is a strong hobby public-beta boundary, not a claim that compiling hostile
code is risk-free. Monitor queue pressure and failures. A dedicated compiler
VPS remains the next defense-in-depth move if usage or abuse justifies it.
