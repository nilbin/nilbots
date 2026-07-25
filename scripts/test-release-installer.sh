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
chmod 755 "$source_root/deploy/"*.sh

install_fake_release() {
  local release_sha="$1"
  local bundle="$deploy_root/incoming/$release_sha/release.tar.gz"
  mkdir -p "$(dirname "$bundle")"
  tar -czf "$bundle" -C "$source_root" deploy
  local bundle_sha
  bundle_sha="$(sha256sum "$bundle" | awk '{ print $1 }')"
  bash deploy/install-release.sh install \
    "$deploy_root" \
    "$release_sha" \
    "ghcr.io/nilbin/nilbots-runtime@sha256:$(printf '1%.0s' {1..64})" \
    "ghcr.io/nilbin/nilbots-compiler@sha256:$(printf '2%.0s' {1..64})" \
    "$bundle" \
    "$bundle_sha"
}

first="$(printf 'a%.0s' {1..40})"
second="$(printf 'b%.0s' {1..40})"
install_fake_release "$first"
[[ "$(readlink "$deploy_root/current")" == "releases/$first" ]]
[[ -L "$deploy_root/releases/$first/deploy/.env" ]]
[[ "$(cat "$test_log")" == "$first" ]]

install_fake_release "$second"
[[ "$(readlink "$deploy_root/current")" == "releases/$second" ]]
[[ "$(readlink "$deploy_root/previous")" == "releases/$first" ]]
[[ "$(tail -1 "$test_log")" == "$second" ]]

bash "$deploy_root/bin/release" rollback "$deploy_root"
[[ "$(readlink "$deploy_root/current")" == "releases/$first" ]]
[[ "$(readlink "$deploy_root/previous")" == "releases/$second" ]]
[[ "$(tail -1 "$test_log")" == "$first" ]]

echo "Release bundle install, activation, and rollback passed"
