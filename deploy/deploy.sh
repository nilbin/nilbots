#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$deploy_dir/.." && pwd)"
release_env="$deploy_dir/release.env"
previous_release_env="$deploy_dir/release.previous.env"
if [[ ! -f "$deploy_dir/.env" ]]; then
  echo "missing $deploy_dir/.env; copy .env.example and fill every secret first" >&2
  exit 1
fi
if [[ ! -f "$deploy_dir/secrets/openiddict-signing.pfx" ||
      ! -f "$deploy_dir/secrets/openiddict-encryption.pfx" ]]; then
  echo "missing OpenIddict certificates under $deploy_dir/secrets" >&2
  exit 1
fi
bash "$deploy_dir/configure-database-env.sh" primary "$deploy_dir/.env"

deploy_mode="existing"
if [[ $# -gt 0 ]]; then
  deploy_mode="$1"
  shift
fi

case "$deploy_mode" in
  --images)
    if [[ $# -ne 4 ]]; then
      echo "usage: $0 --images RUNTIME_IMAGE_REF COMPILER_IMAGE_REF PGBOUNCER_IMAGE_REF GIT_SHA" >&2
      exit 2
    fi
    runtime_image="$1"
    compiler_image="$2"
    pgbouncer_image="$3"
    release_sha="$4"
    if [[ ! "$runtime_image" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$ ||
          ! "$compiler_image" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$ ||
          ! "$pgbouncer_image" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$ ||
          ! "$release_sha" =~ ^[0-9a-f]{40}$ ]]; then
      echo "release images must be immutable GHCR digests and Git SHA must be full length" >&2
      exit 2
    fi
    if [[ -f "$release_env" ]]; then
      cp -p "$release_env" "$previous_release_env"
    fi
    temporary_env="$release_env.tmp"
    {
      printf 'BOTARENA_RUNTIME_IMAGE=%s\n' "$runtime_image"
      printf 'BOTARENA_COMPILER_IMAGE=%s\n' "$compiler_image"
      printf 'BOTARENA_PGBOUNCER_IMAGE=%s\n' "$pgbouncer_image"
      printf 'BOTARENA_RELEASE_GIT_SHA=%s\n' "$release_sha"
    } > "$temporary_env"
    chmod 600 "$temporary_env"
    mv "$temporary_env" "$release_env"
    ;;
  --rollback)
    if [[ $# -ne 0 || ! -f "$previous_release_env" ]]; then
      echo "no previous immutable release is available for rollback" >&2
      exit 2
    fi
    rollback_env="$release_env.rollback"
    if [[ -f "$release_env" ]]; then
      mv "$release_env" "$rollback_env"
    fi
    mv "$previous_release_env" "$release_env"
    if [[ -f "$rollback_env" ]]; then
      mv "$rollback_env" "$previous_release_env"
    fi
    ;;
  --build-local)
    if [[ $# -ne 0 ]]; then
      echo "usage: $0 --build-local" >&2
      exit 2
    fi
    if [[ -f "$release_env" ]]; then
      cp -p "$release_env" "$previous_release_env"
    fi
    rm -f "$release_env"
    ;;
  existing)
    if [[ $# -ne 0 ]]; then
      echo "usage: $0 [--build-local | --rollback | --images RUNTIME COMPILER PGBOUNCER GIT_SHA]" >&2
      exit 2
    fi
    ;;
  *)
    echo "unknown deployment mode '$deploy_mode'" >&2
    exit 2
    ;;
esac

cd "$repo_root"

# The primary's authenticated, persistent inventory is the source of truth for
# private web replicas. It is deliberately outside release bundles and contains
# no secrets. A local/single-node deployment without an inventory stays local.
shared_env="$deploy_dir/.env"
shared_dir="$deploy_dir"
if [[ -L "$shared_env" ]]; then
  shared_env_target="$(readlink "$shared_env")"
  if [[ "$shared_env_target" != /* ]]; then
    shared_env_target="$deploy_dir/$shared_env_target"
  fi
  shared_dir="$(cd "$(dirname "$shared_env_target")" && pwd -P)"
fi
worker_inventory="$shared_dir/workers.tsv"
if [[ -f "$worker_inventory" ]]; then
  export BOTARENA_WEB_UPSTREAMS
  BOTARENA_WEB_UPSTREAMS="$(
    bash "$deploy_dir/worker-inventory.sh" upstreams "$worker_inventory"
  )"
fi

compose=(
  docker compose
  --env-file "$deploy_dir/.env"
)
if [[ -f "$release_env" ]]; then
  compose+=(--env-file "$release_env")
fi
compose+=(
  -f "$deploy_dir/compose.production.yml"
  --profile stateful
  --profile web
  --profile ingress
  --profile match
  --profile compile
)

"${compose[@]}" config --quiet
if [[ -f "$release_env" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$release_env"
  set +a
  "${compose[@]}" pull \
    garage-a garage-b garage-c garage-gateway \
    pgbouncer migrate web match-worker compile-worker compiler-runner
else
  export BOTARENA_IMAGE_TAG
  BOTARENA_IMAGE_TAG="$(git rev-parse --short=12 HEAD)"
  "${compose[@]}" build
fi
"${compose[@]}" up -d --wait db garage-a garage-b garage-c garage-gateway
if [[ -f "$worker_inventory" ]]; then
  sudo -n bash "$deploy_dir/configure-primary-worker-access.sh" \
    sync "$worker_inventory"
fi
bash "$deploy_dir/configure-pgbouncer.sh" "$deploy_dir"
# The credential file is atomically replaced, so recreate PgBouncer to attach
# the new inode and make password rotation deterministic.
"${compose[@]}" up -d --no-deps --force-recreate --wait pgbouncer
bash "$deploy_dir/init-garage.sh"
bash "$deploy_dir/backup-postgres.sh" "$deploy_dir/backups"
if [[ -L "$shared_env" ]]; then
  deployment_root="$(cd "$shared_dir/.." && pwd -P)"
  sudo -n bash "$deploy_dir/install-primary-maintenance.sh" "$deployment_root"
fi
"${compose[@]}" stop compile-worker compiler-runner match-worker >/dev/null 2>&1 || true
"${compose[@]}" run --rm migrate
"${compose[@]}" up -d --no-deps --wait compiler-runner
"${compose[@]}" up -d --no-deps --wait web match-worker compile-worker
# Release activation replaces tracked files by inode. Recreate Caddy so its
# bind mount cannot remain pinned to the previous release's Caddyfile.
"${compose[@]}" up -d --no-deps --force-recreate caddy
"${compose[@]}" ps

if [[ -f "$release_env" ]]; then
  echo "deployed Git ${BOTARENA_RELEASE_GIT_SHA} from immutable GHCR image digests"
else
  echo "deployed Git $(git rev-parse --short=12 HEAD) as local image tag $BOTARENA_IMAGE_TAG"
fi
