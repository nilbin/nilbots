#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT
deploy_root="$test_root/deployment"
source_root="$test_root/source"
mkdir -p "$deploy_root/shared/secrets" "$source_root/deploy"
printf 'BOTARENA_DOMAIN=nilbots.test\n' >"$deploy_root/shared/.env"
touch \
  "$deploy_root/shared/secrets/openiddict-signing.pfx" \
  "$deploy_root/shared/secrets/openiddict-encryption.pfx"

cp deploy/install-release.sh "$source_root/deploy/install-release.sh"
touch \
  "$source_root/deploy/Caddyfile" \
  "$source_root/deploy/compose.production.yml" \
  "$source_root/deploy/init-garage.sh"
test_log="$test_root/activations"
export TEST_RELEASE_ACTIVATION_LOG="$test_log"

cat >"$source_root/deploy/deploy.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
deploy_dir="$(cd "$(dirname "$0")" && pwd)"
# shellcheck disable=SC1091
source "$deploy_dir/release.env"
printf '%s\n' "$BOTARENA_RELEASE_GIT_SHA" >>"$TEST_RELEASE_ACTIVATION_LOG"
EOF
cat >"$source_root/deploy/deploy-worker.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
deploy_dir="$(cd "$(dirname "$0")" && pwd)"
# shellcheck disable=SC1091
source "$deploy_dir/release.env"
printf 'worker:%s\n' "$BOTARENA_RELEASE_GIT_SHA" >>"$TEST_RELEASE_ACTIVATION_LOG"
EOF
chmod 755 "$source_root/deploy/"*.sh

install_fake_release() {
  local operation="$1"
  local target_root="$2"
  local release_sha="$3"
  local bundle="$target_root/incoming/$release_sha/release.tar.gz"
  mkdir -p "$(dirname "$bundle")"
  tar -czf "$bundle" -C "$source_root" deploy
  local bundle_sha
  bundle_sha="$(sha256sum "$bundle" | awk '{ print $1 }')"
  bash deploy/install-release.sh "$operation" \
    "$target_root" \
    "$release_sha" \
    "ghcr.io/nilbin/nilbots-runtime@sha256:$(printf '1%.0s' {1..64})" \
    "ghcr.io/nilbin/nilbots-compiler@sha256:$(printf '2%.0s' {1..64})" \
    "$bundle" \
    "$bundle_sha"
}

first="$(printf 'a%.0s' {1..40})"
second="$(printf 'b%.0s' {1..40})"
install_fake_release install-primary "$deploy_root" "$first"
[[ "$(readlink "$deploy_root/current")" == "releases/$first" ]]
[[ -L "$deploy_root/releases/$first/deploy/.env" ]]
[[ "$(cat "$test_log")" == "$first" ]]
[[ "$(<"$deploy_root/shared/role")" == "primary" ]]

install_fake_release install-primary "$deploy_root" "$second"
[[ "$(readlink "$deploy_root/current")" == "releases/$second" ]]
[[ "$(readlink "$deploy_root/previous")" == "releases/$first" ]]
[[ "$(tail -1 "$test_log")" == "$second" ]]

bash "$deploy_root/bin/release" rollback "$deploy_root"
[[ "$(readlink "$deploy_root/current")" == "releases/$first" ]]
[[ "$(readlink "$deploy_root/previous")" == "releases/$second" ]]
[[ "$(tail -1 "$test_log")" == "$first" ]]

worker_root="$test_root/worker"
worker_log="$test_root/worker-activations"
mkdir -p "$worker_root/shared"
printf '%s\n' \
  'POSTGRES_PASSWORD=test' \
  'BOTARENA_DB_HOST=10.0.0.10' \
  'BOTARENA_S3_ENDPOINT=http://10.0.0.10:3900' \
  >"$worker_root/shared/.env"
export TEST_RELEASE_ACTIVATION_LOG="$worker_log"
install_fake_release install-worker "$worker_root" "$first"
[[ "$(<"$worker_root/shared/role")" == "worker" ]]
[[ "$(cat "$worker_log")" == "worker:$first" ]]
if install_fake_release install-primary "$worker_root" "$second" 2>/dev/null; then
  echo "role-locked worker unexpectedly accepted a primary release" >&2
  exit 1
fi

echo "Primary and worker release install, role lock, activation, and rollback passed"
