# nilbots production deployment

This directory is the public-beta, single-VPS production shape from
[`docs/DEPLOYMENT-SCALING-PLAN.md`](../docs/DEPLOYMENT-SCALING-PLAN.md):
Caddy, one web role, one match worker, a compilation coordinator, a
networkless compiler runner, PostgreSQL, and a private S3-compatible Garage
cluster.
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

Production does not need a Git checkout or GitHub repository credential. The
manual release workflow sends a small, SHA-256-verified deployment bundle over
SSH. Application code is delivered only through immutable GHCR image digests.

## First deployment

From the repository root:

```bash
cp deploy/.env.example deploy/.env
chmod 600 deploy/.env
```

Replace every `CHANGE_ME` and set the real domain. Long hexadecimal secrets
avoid connection-string and `.env` escaping surprises:

```bash
openssl rand -hex 32 # POSTGRES_PASSWORD
openssl rand -hex 32 # BOTARENA_OPENIDDICT_CERT_PASSWORD
openssl rand -hex 32 # BOTARENA_NETWORK_HASH_KEY
openssl rand -hex 32 # GARAGE_RPC_SECRET
openssl rand -hex 32 # GARAGE_ADMIN_TOKEN
openssl rand -hex 32 # GARAGE_METRICS_TOKEN
printf 'GK%s\n' "$(openssl rand -hex 16)" # BOTARENA_S3_ACCESS_KEY
openssl rand -hex 32 # BOTARENA_S3_SECRET_KEY
```

On an existing deployment, generate and append only the missing Garage/S3
settings without replacing any configured values:

```bash
bash deploy/configure-garage-env.sh
```

Garage starts as three storage containers plus a gateway, all on the private
Compose network and all in the same real zone. Replication factor 3 is fixed
from the beginning so physical nodes can replace the co-located bootstrap
nodes later without changing the replication factor. It is not host-level
high availability while all three storage containers share this VPS.

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

For the normal bundle-based release path, provision persistent configuration
once under the deployment root configured by the GitHub
`NILBOTS_DEPLOY_PATH` variable:

```bash
deploy_root=/srv/nilbots/deployment
install -d -m 700 "$deploy_root/shared/secrets" "$deploy_root/shared/backups"
install -m 600 deploy/.env "$deploy_root/shared/.env"
install -m 600 deploy/secrets/*.pfx "$deploy_root/shared/secrets/"
```

The workflow creates `releases/<git-sha>`, links its `.env`, certificates and
backup directory to `shared/`, and atomically advances `current` only after
the candidate release is healthy. `previous` remains available for rollback.
Docker named volumes retain PostgreSQL, Garage and Caddy state independently
of those release directories.

The normal production release path is the repository's **Manual release**
workflow:

- `verify` runs the end-to-end test pipeline only;
- `publish` also publishes both SHA-tagged images, SBOMs, and GitHub build
  provenance to GHCR;
- `publish-cli` packs and publishes the `Nilbots` global tool to NuGet.org;
- `publish-and-deploy` additionally deploys those exact image digests to the
  production environment.

**Publish the CLI before deploying a revision that changes the toolchain.**
`nilbots submit` refuses to build against a server whose SDK or build-pipeline
version it cannot match (DECISIONS #93), and the fix it prints —
`dotnet tool update -g Nilbots` — only works if that CLI version exists. These
stay two separate runs on purpose: a NuGet publish cannot be undone, so it must
not happen as a side effect of a deploy that might fail. `publish-cli` asserts
its version is not yet on NuGet, publishes, and tags the commit `cli-v<version>`;
`publish-and-deploy` then requires that tag to point at the revision being
deployed (`scripts/assert-cli-release.sh`). A toolchain change is therefore a
two-run release on the same commit: `publish-cli`, then `publish-and-deploy`.

The bundle installer validates the bundle hash and immutable GHCR digests.
The deploy script then starts PostgreSQL, takes and validates a local
pre-release database dump, drains workers, runs the one-shot migration/seeding
role, waits for the compiler runner, web, and workers, and then starts Caddy.
Verify on the VPS:

```bash
release=/srv/nilbots/deployment/current/deploy
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" ps
curl --fail "https://$(awk -F= '/^BOTARENA_DOMAIN=/{print $2}' "$release/.env")/health/ready"
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" \
  exec garage-gateway /garage status
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" \
  exec garage-gateway /garage layout show
```

## Updating

1. Select the exact reviewed commit in GitHub Actions.
2. If the revision changes `SdkVersion`, `BuildPipelineVersion`, or
   `CliVersion`, run **Manual release** with `publish-cli` on that commit first.
3. Run **Manual release** with `publish-and-deploy`.
4. Confirm every service is healthy.
5. Test registration/login, `/api/meta`, a submission, its public build
   receipt and WASM artifact, one match, and replay playback.

The release manager retains the previous deployment bundle and digest pair.
To roll back:

```bash
bash /srv/nilbots/deployment/bin/release \
  rollback /srv/nilbots/deployment
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
copy it to another provider/location and alert on failure. Garage's four
persistent volumes, including LMDB metadata snapshots, also need an off-host
backup. The three storage nodes are co-located and therefore do not protect
against loss of the VPS. Run a restore rehearsal into a disposable deployment
at least quarterly.

The legacy `objectdata` volume remains mounted during the first S3 release as
the idempotent migration source and an immediate rollback aid. New writes after
the cutover exist in Garage only; a rollback after accepting such writes must
backfill the local store or roll forward to an S3-capable release.

## Operational checks

```bash
release=/srv/nilbots/deployment/current/deploy
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" logs --tail=200 web
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" logs --tail=200 match-worker
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" logs --tail=200 compile-worker
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" logs --tail=200 garage-gateway
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" exec garage-gateway \
  /garage bucket info nilbots
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" exec db \
  psql -U botarena -d botarena -c \
  'select "Type", "Status", count(*) from "BackgroundJobs" group by 1,2 order by 1,2;'
```

Alert at minimum on HTTPS downtime, failed/stale backups, disk usage, repeated
job failure, and the age of the oldest pending job.

## Adding physical Garage nodes

Join future Garage nodes only over a provider-private or WireGuard network;
never publish ports 3900-3903 to the internet. Keep `replication_factor = 3`
and the same RPC secret on every node. Connect the new nodes, assign their true
datacenter/host zones and capacities, inspect the staged layout, apply exactly
one new layout version, and wait for data synchronization before removing any
bootstrap node. With fewer than three physical failure domains, the cluster is
network-addressable but not physically highly available.

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
