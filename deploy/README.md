# nilbots production deployment

This directory is the public-beta production shape from
[`docs/DEPLOYMENT-SCALING-PLAN.md`](../docs/DEPLOYMENT-SCALING-PLAN.md):
Caddy, horizontally repeatable web roles, one match worker, compilation
coordinators, co-located networkless compiler runners, PostgreSQL, a
primary-only PgBouncer connection pool, and a private S3-compatible Garage
cluster.
The application roles remain separate processes of the same modular monolith.

Production currently has one **primary** node and may have any number of
**worker** nodes:

- the primary is the sole owner of PostgreSQL, PgBouncer, Garage, migrations,
  web ingress, the sole match worker, one web replica, and initial compiler
  capacity;
- a worker runs one web replica plus the compilation coordinator and its
  co-located, networkless compiler runner;
- workers reach PgBouncer and the S3 gateway on the primary's private HostUp
  network. They never create their own database, pooler, or object-store
  volumes.

Garage is intentionally not coupled to every compute worker. A later
high-availability phase will introduce a distinct storage-node role and place
one Garage storage node in each of at least three failure domains. Adding a
Garage container to the present second VPS would not by itself make storage,
PostgreSQL, or ingress survive loss of the primary.

Every service has an explicit Compose profile (`stateful`, `web`, `ingress`,
`match`, or `compile`). Running Compose without profiles starts nothing.
`deploy.sh` selects every profile for the primary; `deploy-worker.sh` selects
only `web` and `compile`, explicitly retires match containers left by older
worker releases, and refuses any stateful, ingress, or match service. The
release installer records
`primary` or `worker` under `shared/role` and rejects later role changes,
including during rollback.

Production uses three custom images:

- `nilbots-runtime`: web, migrations, match worker, and compilation
  coordinator;
- `nilbots-compiler`: the unprivileged, offline compiler runner.
- `nilbots-pgbouncer`: the pinned, unprivileged database connection pooler.

GitHub Actions publishes all three to GHCR by immutable digest. It is deliberately
manual-only: no push or pull-request event consumes Actions minutes.

## Host prerequisites

- Ubuntu Server 26.04 LTS on x86-64/AMD64
- Docker Engine and the Compose plugin from Docker's official Ubuntu repository
- a domain whose A/AAAA record points at the VPS
- inbound TCP 22, 80, and 443 plus UDP 443; no publicly published
  PostgreSQL/application ports
- SSH-key access through a non-root operator account

Docker-published ports can bypass uncomplicated host firewall rules. This
Compose file publishes Caddy publicly, PgBouncer on
`BOTARENA_PGBOUNCER_BIND_ADDRESS`, PostgreSQL on
`BOTARENA_POSTGRES_BIND_ADDRESS`, and Garage's S3 API on
`BOTARENA_GARAGE_BIND_ADDRESS`; private services default to loopback. Never set
one to `0.0.0.0`, `::`, or a public interface. Verify the effective listeners
and `iptables`/`DOCKER-USER` policy on the VPS.

On a fresh matching VPS, `deploy/provision-host.sh` installs Docker from its
official repository, creates the `nilbots` operator from root's authorized
keys, enables unattended security updates and ufw, and applies conservative
SSH and Docker log settings. `bootstrap-worker.sh` orchestrates this script,
verifies operator access, installs a persistent `DOCKER-USER` policy, and only
then disables root SSH.

The default provisions a public ingress host and opens TCP 80/443 plus UDP
443. Provision a worker, database, or other private-only node without those
rules by setting:

```bash
BOTARENA_PUBLIC_INGRESS=0 bash deploy/provision-host.sh
```

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

For a multi-host deployment, set `BOTARENA_PGBOUNCER_BIND_ADDRESS` to the
primary's private-interface address. Keep
`BOTARENA_POSTGRES_BIND_ADDRESS` there only during the direct-connection
rollback window, and set `BOTARENA_GARAGE_BIND_ADDRESS` to the private address
for remote S3 clients. Permit TCP 6432 (and temporarily 5432) plus 3900 only
from exact registered-worker addresses. Remote roles use the `botarena`
transaction alias and `botarena_session` notification alias with the same
credentials; never route them over the public interface. Garage's admin and
RPC interfaces stay unpublished until a deliberate storage expansion.

## Adding a worker VPS

For HostUp, create an Ubuntu 26.04 amd64 VPS with the operator's public key.
The end-to-end wrapper discovers the VPS and `nilbots-production` network,
allocates and attaches a private address, waits through HostUp's network
restart, performs the complete bootstrap, installs the primary's current
immutable release, and refreshes Caddy:

```bash
read -rs HOSTUP_API_KEY
export HOSTUP_API_KEY
bash deploy/bootstrap-hostup-worker.sh \
  --primary nilbots@PRIMARY_PUBLIC_HOST \
  --primary-private-ip PRIMARY_PRIVATE_IP \
  --worker NEW_WORKER_PUBLIC_IP_OR_VPS_ID \
  --network nilbots-production \
  --name worker-3 \
  --private-ip auto \
  --size standard
unset HOSTUP_API_KEY
```

The key is never accepted as a command-line argument or stored on either VPS.
It needs HostUp's VPS/service read, VM/network write, and—only for power
commands—`power:vm` scopes.

For another provider, attach the VPS to the provider-private network first,
then run the provider-neutral bootstrap:

```bash
bash deploy/bootstrap-worker.sh \
  --primary nilbots@PRIMARY_PUBLIC_HOST \
  --primary-private-ip PRIMARY_PRIVATE_IP \
  --worker-admin root@NEW_WORKER_PUBLIC_HOST \
  --worker-private-ip NEW_WORKER_PRIVATE_IP \
  --name worker-3 \
  --size standard
```

The bootstrap performs the full host and fleet setup:

- verifies Ubuntu 26.04 amd64 and the assigned private address;
- creates non-root `nilbots` access and synchronizes the primary's operator and
  GitHub deployment public keys;
- installs Docker, bounded logs, unattended upgrades, SSH hardening, ufw, and
  a reboot-persistent `DOCKER-USER` policy;
- renders a minimal worker environment from the primary without ever copying
  Garage RPC/admin/metrics credentials;
- streams and SHA-256-verifies the shared OpenIddict certificates without
  writing them to the invoking workstation;
- binds Kestrel only to the new worker's private address and permits port 8080
  only from the primary private address;
- proves PgBouncer and Garage connectivity over the private network;
- installs a reboot-persistent exact `/32` Docker firewall admission for
  PgBouncer and the temporary PostgreSQL rollback port, plus a matching SCRAM
  PostgreSQL HBA rule;
- disables root SSH only after operator access works; and
- records the verified SSH host key, deployment target, and private endpoint in
  the primary's non-secret `shared/workers.tsv`.

That inventory is the production fleet source of truth. Caddy derives its
private upstreams from it, and the manual GitHub workflow retrieves it through
the authenticated primary before deploying every registered worker. GitHub
therefore needs no per-worker variables or separately maintained worker
known-host secret.

`--adopt` performs the configuration, hardening, and registration steps on an
already-provisioned passwordless-sudo operator without reinstalling the OS
packages. The HostUp wrapper uses it when resuming a drained node.
`--no-register` prepares and verifies a disposable node without granting
database access or placing it behind Caddy or future releases.

The `xs-smoke` size profile is for cheaply rehearsing provisioning, networking,
web startup, release installation, and removal. It is not evidence that a
memory-constrained XS node can complete hostile NativeAOT builds reliably.
Use `standard` for real compiler capacity.

To drain a disposable, pausable, or retired node:

```bash
bash deploy/unregister-worker.sh \
  nilbots@PRIMARY_PUBLIC_HOST \
  worker-3
```

This removes the node from the inventory, immediately recreates Caddy without
its upstream, gracefully stops its web/compiler containers, and revokes its
exact database firewall/HBA rules. The VPS, Docker volumes, cached images,
release files, private-network interface, and disk remain intact.

For a HostUp PAYG worker, power it off only after that drain:

```bash
read -rs HOSTUP_API_KEY
export HOSTUP_API_KEY
bash deploy/hostup-vps.sh shutdown VPS_ID_OR_PUBLIC_IP
unset HOSTUP_API_KEY
```

Resume it with the stable HostUp VPS ID and the same end-to-end wrapper:

```bash
read -rs HOSTUP_API_KEY
export HOSTUP_API_KEY
bash deploy/hostup-vps.sh start VPS_ID
bash deploy/bootstrap-hostup-worker.sh \
  --primary nilbots@PRIMARY_PUBLIC_HOST \
  --primary-private-ip PRIMARY_PRIVATE_IP \
  --worker VPS_ID \
  --network nilbots-production \
  --name worker-3 \
  --private-ip auto \
  --size standard \
  --adopt
unset HOSTUP_API_KEY
```

The adopted path revalidates SSH, networking, secrets, and firewalls, restores
the exact database grant and inventory entry, starts the already-cached
current release, waits for health, and only then refreshes Caddy. If a provider
ever changes the public address, the stable VPS ID resolves its current public
IP and the inventory upsert records the replacement SSH target and host key.
Permanent VPS deletion remains a separate provider action after a successful
drain and exact-target verification.

Caddy uses a sticky load-balancer cookie for WebSocket/SignalR affinity and
actively checks `/health/ready`. The remote Kestrel listener must bind only to
the worker's exact private address. The second web replica increases
application capacity and lets Caddy route around a failed web process; because
Caddy, PostgreSQL, and Garage still live on the primary, this is not primary
host high availability.

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
Garage RPC uses the dedicated internal `garage-rpc` network and fixed
`172.30.0.2`–`172.30.0.5` addresses. Its persisted peer records therefore
remain valid across Docker and host restarts instead of depending on dynamic
container-address reuse. The initializer still reconnects and verifies all
four IDs as a recovery check.

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
operator_gid="$(id -g)"
install -d -m 700 "$deploy_root/shared/backups"
install -m 600 deploy/.env "$deploy_root/shared/.env"
sudo install -d -m 770 -o "$(id -un)" -g "$operator_gid" \
  "$deploy_root/shared/secrets"
sudo install -m 660 -o "$(id -un)" -g "$operator_gid" \
  deploy/secrets/*.pfx "$deploy_root/shared/secrets/"
sudo chown -R "1654:$operator_gid" "$deploy_root/shared/secrets"
```

The workflow creates `releases/<git-sha>`, links persistent configuration to
`shared/`, and atomically advances `current` only after the candidate release
is healthy. `previous` remains available for rollback. Primary releases use
`install-primary`; every host in the primary's strictly validated worker
inventory uses `install-worker`. A primary deployment completes migrations
before workers receive the new images.
UID 1654 is the unprivileged runtime account baked into the image; the
operator's private group retains certificate-management access without making
the private keys world-readable.
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
deployed or verifies that the enumerated CLI compatibility surface has not
changed since the tagged release (`scripts/assert-cli-release.sh`). A CLI,
toolchain, engine/runtime, compiler-input, map, packaged-bot, or replay-viewer
change is therefore a two-run release on the same commit: `publish-cli`, then
`publish-and-deploy`. Server/auth/deployment and site-only changes can reuse
the already-published compatible CLI.

The bundle installer validates the bundle hash and immutable GHCR digests.
The primary deploy script then starts PostgreSQL, installs
`pg_stat_statements`, derives PgBouncer's SCRAM file without exposing the
password, synchronizes exact worker firewall rules, starts PgBouncer, takes
and validates a local pre-release database dump, drains its workers, runs the one-shot
migration/seeding role, waits for the compiler runner, web, and workers, and
then starts Caddy. Worker deployments activate the web replica and compiler
roles while preserving the deliberate single match-worker production default.
Ranked finalization is now concurrency-safe, so match lanes or consumers can be
raised later when measured queue pressure warrants it.
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
2. Run `bash scripts/assert-cli-release.sh published <revision>`. If it reports
   a CLI compatibility-surface change, run **Manual release** with
   `publish-cli` on that commit first.
3. Run **Manual release** with `publish-and-deploy`.
4. Confirm every service is healthy.
5. Test registration/login, `/api/meta`, a submission, its public build
   receipt and WASM artifact, one match, and replay playback.

Frontline Labs is a separate experimental enablement after the compatible
binary rollout. Its shared environment keys are:

```text
BOTARENA_FRONTLINE_LABS_ENABLED=false
BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY=10
BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE=1
BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE=4
```

Routine fleet deployment copies only those four allowlisted values from the
primary `shared/.env` into every worker `shared/.env`, preserving secrets and
host-specific resource settings. Compose injects them into every web and
compile-worker replica. The flag gates both Labs discovery/admission and
activation of newly compiled generic-only artifacts. Turning it off does not
deactivate existing artifacts or cancel identity-pinned work.

Enablement is a maintenance operation, not an ordinary rolling flag flip:

1. Publish and tag CLI 0.9.0 from the exact compatibility revision.
2. Deploy and soak the profile-aware web, compile, and match-worker binary
   everywhere with the flag false. Confirm the retained `previous` release is
   also profile-aware and contains the scoped legacy backfiller; this may
   require a second flag-false release before enablement.
3. Hold new submissions and Labs creation, drain the compile queue, then stop
   every compile-worker and compiler-runner, including remote nodes.
4. Set and validate the four values on the primary, propagate them to every
   node, and restart compile workers on the intended revision before exposing
   any enabled web replica.
5. Restore web traffic only after every web/compiler replica reports the same
   revision and configuration, then smoke-test one generic-only build.

Do not roll back to a pre-profile-aware or pre-scoped-backfiller image while a
generic-only artifact or Labs match exists. The map and numeric mechanics
remain experimental and still require the measurement gates in
`docs/FRONTLINE-IMPLEMENTATION-PLAN.md`.

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
bash deploy/backup-postgres.sh /absolute/local/path
```

Primary releases install a persistent systemd timer for nightly dumps and a
weekly disposable restore rehearsal. Pre-deploy and scheduled dumps share a
bounded retention count (`BOTARENA_LOCAL_BACKUPS`, default 32). Rehearse a
specific dump manually with:

```bash
bash deploy/restore-postgres-backup.sh /absolute/local/path/botarena-TIMESTAMP.dump
```

These local recovery points help with bad migrations and operator mistakes.
The owner has explicitly accepted that loss of the primary VPS may lose
PostgreSQL, the dumps, and co-located Garage together; off-site backup remains
a later value-triggered upgrade.

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
docker compose --env-file "$release/.env" -f "$release/compose.production.yml" logs --tail=200 pgbouncer
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
