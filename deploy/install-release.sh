#!/usr/bin/env bash
set -euo pipefail

lock_dir=""
staging_dir=""

cleanup() {
  if [[ -n "$staging_dir" &&
        -n "${deploy_root:-}" &&
        "$staging_dir" == "$deploy_root"/releases/.* &&
        -d "$staging_dir" ]]; then
    rm -rf -- "$staging_dir"
  fi
  if [[ -n "$lock_dir" ]]; then
    rmdir "$lock_dir" 2>/dev/null || true
  fi
}
trap cleanup EXIT

usage() {
  cat >&2 <<'EOF'
usage:
  install-release.sh install DEPLOY_ROOT GIT_SHA RUNTIME_IMAGE COMPILER_IMAGE BUNDLE BUNDLE_SHA256
  install-release.sh rollback DEPLOY_ROOT
EOF
  exit 2
}

validate_root() {
  local requested="$1"
  if [[ "$requested" != /* ||
        "$requested" == "/" ||
        "$requested" == *"//"* ||
        "$requested" == *"/./"* ||
        "$requested" == *"/../"* ||
        "$requested" == */. ||
        "$requested" == */.. ]]; then
    echo "deployment root must be a normalized absolute path below /" >&2
    exit 2
  fi
}

validate_release_target() {
  local target="$1"
  if [[ ! "$target" =~ ^releases/[0-9a-f]{40}$ ]]; then
    echo "invalid release link target '$target'" >&2
    exit 1
  fi
}

atomic_link() {
  local target="$1"
  local link="$2"
  local temporary="${link}.new.$$"
  rm -f "$temporary"
  ln -s "$target" "$temporary"
  if mv --help 2>&1 | grep -q -- "--no-target-directory"; then
    mv -Tf "$temporary" "$link"
  else
    mv -fh "$temporary" "$link"
  fi
}

acquire_lock() {
  lock_dir="$deploy_root/.release-lock"
  if ! mkdir "$lock_dir" 2>/dev/null; then
    echo "another deployment is active, or $lock_dir needs manual recovery" >&2
    exit 1
  fi
}

validate_shared_state() {
  local shared="$deploy_root/shared"
  if [[ ! -f "$shared/.env" ]]; then
    echo "missing $shared/.env" >&2
    exit 1
  fi
  if [[ ! -f "$shared/secrets/openiddict-signing.pfx" ||
        ! -f "$shared/secrets/openiddict-encryption.pfx" ]]; then
    echo "missing shared OpenIddict certificates under $shared/secrets" >&2
    exit 1
  fi
  mkdir -p "$shared/backups"
}

write_release_environment() {
  local release_dir="$1"
  local runtime_image="$2"
  local compiler_image="$3"
  local release_sha="$4"
  local environment="$release_dir/deploy/release.env"
  local temporary="${environment}.tmp"
  {
    printf 'BOTARENA_RUNTIME_IMAGE=%s\n' "$runtime_image"
    printf 'BOTARENA_COMPILER_IMAGE=%s\n' "$compiler_image"
    printf 'BOTARENA_RELEASE_GIT_SHA=%s\n' "$release_sha"
  } >"$temporary"
  chmod 600 "$temporary"
  mv "$temporary" "$environment"
}

activate_release() {
  local target="$1"
  local old_current=""
  validate_release_target "$target"
  if [[ -L "$deploy_root/current" ]]; then
    old_current="$(readlink "$deploy_root/current")"
    validate_release_target "$old_current"
  elif [[ -e "$deploy_root/current" ]]; then
    echo "$deploy_root/current exists but is not a release link" >&2
    exit 1
  fi

  if [[ -n "$old_current" && "$old_current" != "$target" ]]; then
    atomic_link "$old_current" "$deploy_root/previous"
  fi
  atomic_link "$target" "$deploy_root/current"
}

install_release() {
  [[ $# -eq 5 ]] || usage
  local release_sha="$1"
  local runtime_image="$2"
  local compiler_image="$3"
  local bundle="$4"
  local expected_bundle_sha="$5"

  if [[ ! "$release_sha" =~ ^[0-9a-f]{40}$ ||
        ! "$runtime_image" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$ ||
        ! "$compiler_image" =~ ^ghcr\.io/[a-z0-9._/-]+@sha256:[0-9a-f]{64}$ ||
        ! "$expected_bundle_sha" =~ ^[0-9a-f]{64}$ ]]; then
    echo "invalid immutable release metadata" >&2
    exit 2
  fi
  if [[ ! -f "$bundle" ]]; then
    echo "release bundle does not exist" >&2
    exit 2
  fi
  bundle="$(cd "$(dirname "$bundle")" && pwd -P)/$(basename "$bundle")"
  if [[ "$bundle" != "$deploy_root"/incoming/* ]]; then
    echo "release bundle must be a file below $deploy_root/incoming" >&2
    exit 2
  fi

  local actual_bundle_sha
  actual_bundle_sha="$(sha256sum "$bundle" | awk '{ print $1 }')"
  if [[ "$actual_bundle_sha" != "$expected_bundle_sha" ]]; then
    echo "release bundle SHA-256 mismatch" >&2
    exit 1
  fi

  local entry
  while IFS= read -r entry; do
    if [[ "$entry" == /* ||
          "$entry" == *"/../"* ||
          "$entry" == ../* ||
          "$entry" != deploy &&
          "$entry" != deploy/* ]]; then
      echo "unsafe release bundle entry '$entry'" >&2
      exit 1
    fi
  done < <(tar -tzf "$bundle")

  local release_dir="$deploy_root/releases/$release_sha"
  if [[ -e "$release_dir" ]]; then
    if [[ ! -f "$release_dir/.bundle-sha256" ||
          "$(<"$release_dir/.bundle-sha256")" != "$expected_bundle_sha" ]]; then
      echo "existing release directory does not match this bundle" >&2
      exit 1
    fi
  else
    staging_dir="$(mktemp -d "$deploy_root/releases/.${release_sha}.XXXXXX")"
    tar -xzf "$bundle" -C "$staging_dir"
    for required in \
      deploy/Caddyfile \
      deploy/compose.production.yml \
      deploy/deploy.sh \
      deploy/init-garage.sh \
      deploy/install-release.sh; do
      if [[ ! -f "$staging_dir/$required" ]]; then
        echo "release bundle is missing $required" >&2
        exit 1
      fi
    done
    ln -s "$deploy_root/shared/.env" "$staging_dir/deploy/.env"
    ln -s "$deploy_root/shared/secrets" "$staging_dir/deploy/secrets"
    ln -s "$deploy_root/shared/backups" "$staging_dir/deploy/backups"
    printf '%s\n' "$expected_bundle_sha" >"$staging_dir/.bundle-sha256"
    mv "$staging_dir" "$release_dir"
    staging_dir=""
  fi

  write_release_environment \
    "$release_dir" \
    "$runtime_image" \
    "$compiler_image" \
    "$release_sha"
  install -m 755 "$release_dir/deploy/install-release.sh" "$deploy_root/bin/release"

  bash "$release_dir/deploy/deploy.sh"
  activate_release "releases/$release_sha"
  echo "activated release $release_sha from a verified deployment bundle"
}

rollback_release() {
  [[ $# -eq 0 ]] || usage
  if [[ ! -L "$deploy_root/current" || ! -L "$deploy_root/previous" ]]; then
    echo "both current and previous release links are required for rollback" >&2
    exit 1
  fi

  local current_target
  local previous_target
  current_target="$(readlink "$deploy_root/current")"
  previous_target="$(readlink "$deploy_root/previous")"
  validate_release_target "$current_target"
  validate_release_target "$previous_target"
  if [[ "$current_target" == "$previous_target" ]]; then
    echo "current and previous resolve to the same release" >&2
    exit 1
  fi

  bash "$deploy_root/$previous_target/deploy/deploy.sh"
  atomic_link "$current_target" "$deploy_root/previous"
  atomic_link "$previous_target" "$deploy_root/current"
  echo "rolled back to ${previous_target#releases/}"
}

[[ $# -ge 2 ]] || usage
operation="$1"
deploy_root="$2"
shift 2
validate_root "$deploy_root"
mkdir -p "$deploy_root"/{bin,incoming,releases,shared}
deploy_root="$(cd "$deploy_root" && pwd -P)"
acquire_lock
validate_shared_state

case "$operation" in
  install)
    install_release "$@"
    ;;
  rollback)
    rollback_release "$@"
    ;;
  *)
    usage
    ;;
esac
