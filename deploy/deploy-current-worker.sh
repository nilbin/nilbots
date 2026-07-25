#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$deploy_dir/.." && pwd)"

if [[ $# -lt 2 || $# -gt 4 ]]; then
  echo "usage: deploy-current-worker.sh PRIMARY_USER@HOST WORKER_NAME [DEPLOY_ROOT] [SSH_IDENTITY]" >&2
  exit 2
fi

primary_target="$1"
worker_name="$2"
deploy_root="${3:-/srv/nilbots/deployment}"
identity="${4:-${BOTARENA_SSH_IDENTITY:-}}"

[[ "$primary_target" =~ ^[a-z_][a-z0-9_-]*@[A-Za-z0-9][A-Za-z0-9.-]{0,252}$ ]] ||
  { echo "invalid primary SSH target" >&2; exit 2; }
[[ "$worker_name" =~ ^[a-z0-9][a-z0-9-]{0,62}$ ]] ||
  { echo "invalid worker name" >&2; exit 2; }
[[ "$deploy_root" =~ ^/[A-Za-z0-9._/-]+$ &&
   "$deploy_root" != "/" &&
   "$deploy_root" != *"//"* &&
   "$deploy_root" != *"/./"* &&
   "$deploy_root" != *"/../"* ]] ||
  { echo "invalid deployment root" >&2; exit 2; }
if [[ -n "$identity" && ! -f "$identity" ]]; then
  echo "SSH identity does not exist: $identity" >&2
  exit 2
fi

ssh_options=(-o BatchMode=yes -o ConnectTimeout=10)
if [[ -n "$identity" ]]; then
  ssh_options+=(-i "$identity" -o IdentitiesOnly=yes)
fi

remote_command() {
  local command=""
  printf -v command '%q ' "$@"
  printf '%s\n' "${command% }"
}

inventory="$deploy_root/shared/workers.tsv"
worker_record="$(
  ssh "${ssh_options[@]}" "$primary_target" \
    "$(remote_command bash -s -- record "$inventory" "$worker_name")" \
    <"$deploy_dir/worker-inventory.sh"
)"
[[ -n "$worker_record" ]] ||
  { echo "worker '$worker_name' is not registered" >&2; exit 1; }
IFS=$'\t' read -r \
  record_name worker_host worker_user worker_path worker_private_ip \
  host_key_type host_key_base64 extra <<<"$worker_record"
[[ "$record_name" == "$worker_name" && -z "$extra" ]] ||
  { echo "primary returned an invalid worker record" >&2; exit 1; }

release_environment="$(
  ssh "${ssh_options[@]}" "$primary_target" \
    "cat '$deploy_root/current/deploy/release.env'"
)"
release_sha="$(
  awk -F= '$1 == "BOTARENA_RELEASE_GIT_SHA" { print $2 }' \
    <<<"$release_environment"
)"
runtime_ref="$(
  awk -F= '$1 == "BOTARENA_RUNTIME_IMAGE" { print $2 }' \
    <<<"$release_environment"
)"
compiler_ref="$(
  awk -F= '$1 == "BOTARENA_COMPILER_IMAGE" { print $2 }' \
    <<<"$release_environment"
)"
[[ "$release_sha" =~ ^[0-9a-f]{40}$ &&
   "$runtime_ref" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$ &&
   "$compiler_ref" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$ ]] ||
  { echo "primary active release metadata is invalid" >&2; exit 1; }

git -C "$repo_root" cat-file -e "${release_sha}^{commit}" 2>/dev/null ||
  { echo "local repository does not contain primary release $release_sha" >&2; exit 1; }

temporary_dir="$(mktemp -d /tmp/nilbots-current-worker.XXXXXX)"
cleanup() {
  if [[ "$temporary_dir" == /tmp/* && -d "$temporary_dir" ]]; then
    rm -rf -- "$temporary_dir"
  fi
}
trap cleanup EXIT
known_hosts="$temporary_dir/known_hosts"
printf '%s %s %s\n' \
  "$worker_host" "$host_key_type" "$host_key_base64" >"$known_hosts"
chmod 600 "$known_hosts"
worker_ssh_options=(
  "${ssh_options[@]}"
  -o UserKnownHostsFile="$known_hosts"
  -o StrictHostKeyChecking=yes
)

bundle="$temporary_dir/nilbots-deploy-$release_sha.tar.gz"
bash "$deploy_dir/build-release-bundle.sh" "$release_sha" "$bundle"
bundle_sha="$(sha256sum "$bundle" 2>/dev/null | awk '{ print $1 }')"
if [[ -z "$bundle_sha" ]]; then
  bundle_sha="$(shasum -a 256 "$bundle" | awk '{ print $1 }')"
fi
incoming="$worker_path/incoming/$release_sha"
worker_target="$worker_user@$worker_host"
ssh "${worker_ssh_options[@]}" "$worker_target" \
  "install -d -m 700 '$incoming'"
scp "${worker_ssh_options[@]}" \
  "$bundle" "$deploy_dir/install-release.sh" \
  "$worker_target:$incoming/"
ssh "${worker_ssh_options[@]}" "$worker_target" \
  "bash '$incoming/install-release.sh' install-worker \
    '$worker_path' '$release_sha' '$runtime_ref' '$compiler_ref' \
    '$incoming/$(basename "$bundle")' '$bundle_sha'"

ssh "${ssh_options[@]}" "$primary_target" \
  "$(remote_command bash -s -- "$deploy_root")" \
  <"$deploy_dir/refresh-primary-upstreams.sh"
echo "Worker '$worker_name' is running primary release $release_sha."
