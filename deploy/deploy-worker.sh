#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$deploy_dir/.." && pwd)"
release_env="$deploy_dir/release.env"

if [[ ! -f "$deploy_dir/.env" ]]; then
  echo "missing $deploy_dir/.env; configure private database and S3 endpoints first" >&2
  exit 1
fi

env_value() {
  local key="$1"
  local process_value="${!key-}"
  if [[ -n "$process_value" ]]; then
    printf '%s\n' "$process_value"
    return
  fi
  awk -F= -v key="$key" '
    $1 == key {
      sub(/^[^=]*=/, "")
      gsub(/^["'"'"']|["'"'"']$/, "")
      print
      exit
    }
  ' "$deploy_dir/.env"
}

database_host="$(env_value BOTARENA_DB_HOST)"
s3_endpoint="$(env_value BOTARENA_S3_ENDPOINT)"
case "$database_host" in
  ""|db|localhost|127.*|::1)
    echo "worker BOTARENA_DB_HOST must name the primary host's private address" >&2
    exit 1
    ;;
esac
case "$s3_endpoint" in
  ""|*garage-gateway*|*localhost*|*127.0.0.1*|*"[::1]"*)
    echo "worker BOTARENA_S3_ENDPOINT must use the primary host's private S3 endpoint" >&2
    exit 1
    ;;
esac

cd "$repo_root"
compose_base=(
  docker compose
  --env-file "$deploy_dir/.env"
)
if [[ -f "$release_env" ]]; then
  compose_base+=(--env-file "$release_env")
fi
compose_base+=(
  -f "$deploy_dir/compose.production.yml"
)
compose=(
  "${compose_base[@]}"
  --profile web
  --profile compile
)

# Compose interpolates the whole file even when stateful profiles are inactive.
# Workers intentionally do not receive Garage administration credentials.
export GARAGE_RPC_SECRET="${GARAGE_RPC_SECRET:-unused-on-worker}"
export GARAGE_ADMIN_TOKEN="${GARAGE_ADMIN_TOKEN:-unused-on-worker}"
export GARAGE_METRICS_TOKEN="${GARAGE_METRICS_TOKEN:-unused-on-worker}"

"${compose[@]}" config --quiet
active_services="$("${compose[@]}" config --services)"
for required in web compile-worker compiler-runner; do
  if ! grep -qx "$required" <<<"$active_services"; then
    echo "worker deployment is missing required service '$required'" >&2
    exit 1
  fi
done
while IFS= read -r service; do
  case "$service" in
    web|compile-worker|compiler-runner) ;;
    *)
      echo "refusing unexpected worker service '$service'" >&2
      exit 1
      ;;
  esac
done <<<"$active_services"

# Older worker releases also ran a match worker. The primary deliberately owns
# the current single match consumer; finalization can scale safely, but adding
# lanes or containers should follow measured demand rather than node count.
# Reconcile that obsolete worker container before activating this release.
reconcile=(
  "${compose_base[@]}"
  --profile match
)
"${reconcile[@]}" stop match-worker >/dev/null 2>&1 || true
"${reconcile[@]}" rm -f match-worker >/dev/null 2>&1 || true

running_services="$(
  docker ps \
    --filter label=com.docker.compose.project=botarena \
    --format '{{.Label "com.docker.compose.service"}}'
)"
for forbidden in db garage-a garage-b garage-c garage-gateway migrate caddy match-worker; do
  if grep -qx "$forbidden" <<<"$active_services"; then
    echo "refusing worker deployment because '$forbidden' is active" >&2
    exit 1
  fi
  if grep -qx "$forbidden" <<<"$running_services"; then
    echo "refusing worker deployment because an existing '$forbidden' container is running" >&2
    exit 1
  fi
done

if [[ -f "$release_env" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$release_env"
  set +a
  "${compose[@]}" pull web compile-worker compiler-runner
else
  export BOTARENA_IMAGE_TAG
  BOTARENA_IMAGE_TAG="$(git rev-parse --short=12 HEAD)"
  "${compose[@]}" build web compile-worker compiler-runner
fi

"${compose[@]}" up -d --wait compiler-runner web compile-worker
"${compose[@]}" ps

if [[ -f "$release_env" ]]; then
  echo "deployed worker Git ${BOTARENA_RELEASE_GIT_SHA} from immutable GHCR image digests"
else
  echo "deployed worker Git $(git rev-parse --short=12 HEAD) as local image tag $BOTARENA_IMAGE_TAG"
fi
