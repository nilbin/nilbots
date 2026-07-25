#!/usr/bin/env bash
set -euo pipefail

deploy_dir="$(cd "$(dirname "$0")" && pwd)"

usage() {
  cat >&2 <<'EOF'
usage: bootstrap-worker.sh \
  --primary SSH_USER@PUBLIC_HOST \
  --primary-private-ip PRIVATE_IP \
  --worker-admin SSH_USER@PUBLIC_HOST \
  --worker-private-ip PRIVATE_IP \
  --name WORKER_NAME \
  [--size standard|xs-smoke] \
  [--operator nilbots] \
  [--deploy-root /srv/nilbots/deployment] \
  [--identity /path/to/private-key] \
  [--adopt] \
  [--no-register]

Fresh HostUp nodes use --worker-admin root@HOST. `--adopt` skips OS/Docker
provisioning and permits an existing passwordless-sudo operator as worker admin.
The VPS must already run Ubuntu 26.04 amd64 and be attached to the same private
network as the primary.
EOF
  exit 2
}

primary_target=""
primary_private_ip=""
worker_admin_target=""
worker_private_ip=""
worker_name=""
worker_size="standard"
operator="nilbots"
deploy_root="/srv/nilbots/deployment"
identity="${BOTARENA_SSH_IDENTITY:-}"
adopt=0
register=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    --primary) primary_target="${2:-}"; shift 2 ;;
    --primary-private-ip) primary_private_ip="${2:-}"; shift 2 ;;
    --worker-admin) worker_admin_target="${2:-}"; shift 2 ;;
    --worker-private-ip) worker_private_ip="${2:-}"; shift 2 ;;
    --name) worker_name="${2:-}"; shift 2 ;;
    --size) worker_size="${2:-}"; shift 2 ;;
    --operator) operator="${2:-}"; shift 2 ;;
    --deploy-root) deploy_root="${2:-}"; shift 2 ;;
    --identity) identity="${2:-}"; shift 2 ;;
    --adopt) adopt=1; shift ;;
    --no-register) register=0; shift ;;
    *) usage ;;
  esac
done

[[ -n "$primary_target" &&
   -n "$primary_private_ip" &&
   -n "$worker_admin_target" &&
   -n "$worker_private_ip" &&
   -n "$worker_name" ]] || usage
[[ "$operator" =~ ^[a-z_][a-z0-9_-]*$ ]] ||
  { echo "invalid operator user" >&2; exit 2; }
[[ "$deploy_root" =~ ^/[A-Za-z0-9._/-]+$ &&
   "$deploy_root" != "/" &&
   "$deploy_root" != *"//"* &&
   "$deploy_root" != *"/./"* &&
   "$deploy_root" != *"/../"* ]] ||
  { echo "invalid deployment root" >&2; exit 2; }
[[ "$worker_size" == "standard" || "$worker_size" == "xs-smoke" ]] ||
  { echo "worker size must be standard or xs-smoke" >&2; exit 2; }
if [[ -n "$identity" && ! -f "$identity" ]]; then
  echo "SSH identity does not exist: $identity" >&2
  exit 2
fi

parse_target() {
  local target="$1"
  local output_user_name="$2"
  local output_host_name="$3"
  [[ "$target" =~ ^([a-z_][a-z0-9_-]*)@([A-Za-z0-9][A-Za-z0-9.-]{0,252})$ ]] ||
    { echo "invalid SSH target '$target'" >&2; exit 2; }
  printf -v "$output_user_name" '%s' "${BASH_REMATCH[1]}"
  printf -v "$output_host_name" '%s' "${BASH_REMATCH[2]}"
}

primary_user=""
primary_host=""
worker_admin_user=""
worker_host=""
parse_target "$primary_target" primary_user primary_host
parse_target "$worker_admin_target" worker_admin_user worker_host
[[ "$primary_host" != "$worker_host" ]] ||
  { echo "primary and worker SSH hosts must differ" >&2; exit 2; }
if [[ "$adopt" == "0" && "$worker_admin_user" != "root" ]]; then
  echo "a fresh worker must initially use root SSH; use --adopt for an existing host" >&2
  exit 2
fi
worker_target="$operator@$worker_host"

ssh_options=(
  -o BatchMode=yes
  -o ConnectTimeout=10
  -o ServerAliveInterval=15
  -o ServerAliveCountMax=3
)
new_host_ssh_options=("${ssh_options[@]}" -o StrictHostKeyChecking=accept-new)
if [[ -n "$identity" ]]; then
  ssh_options+=(-i "$identity" -o IdentitiesOnly=yes)
  new_host_ssh_options+=(-i "$identity" -o IdentitiesOnly=yes)
fi

remote_command() {
  local command=""
  printf -v command '%q ' "$@"
  printf '%s\n' "${command% }"
}

run_worker_root_script() {
  local script="$1"
  shift
  local command
  if [[ "$worker_admin_user" == "root" ]]; then
    command="$(remote_command bash -s -- "$@")"
  else
    command="$(remote_command sudo bash -s -- "$@")"
  fi
  ssh "${new_host_ssh_options[@]}" "$worker_admin_target" "$command" <"$script"
}

echo "Checking primary deployment and fresh worker access..."
ssh "${ssh_options[@]}" "$primary_target" \
  "test \"\$(cat '$deploy_root/shared/role')\" = primary &&
   test -f '$deploy_root/shared/.env' &&
   test -s '$deploy_root/shared/secrets/openiddict-signing.pfx' &&
   test -s '$deploy_root/shared/secrets/openiddict-encryption.pfx'"
ssh "${new_host_ssh_options[@]}" "$worker_admin_target" true

if [[ "$adopt" == "0" ]]; then
  echo "Provisioning Ubuntu, non-root access, Docker, updates, SSH, and ufw..."
  provision_command="$(
    remote_command env \
      BOTARENA_PUBLIC_INGRESS=0 \
      "BOTARENA_OPERATOR=$operator" \
      bash -s
  )"
  ssh "${new_host_ssh_options[@]}" \
    "$worker_admin_target" \
    "$provision_command" \
    <"$deploy_dir/provision-host.sh"
else
  echo "Adopting existing worker; OS and Docker provisioning skipped..."
  ssh "${new_host_ssh_options[@]}" "$worker_admin_target" \
    "$(remote_command sudo -n true)"
fi

echo "Verifying operator SSH before root access is disabled..."
ssh "${new_host_ssh_options[@]}" "$worker_target" true

echo "Synchronizing the primary's operator and GitHub deployment public keys..."
ssh "${ssh_options[@]}" "$primary_target" \
  "cat '/home/$primary_user/.ssh/authorized_keys'" |
  ssh "${new_host_ssh_options[@]}" "$worker_target" \
    "umask 077
     temporary=\$(mktemp '/home/$operator/.ssh/authorized_keys.XXXXXX')
     trap 'rm -f \"\$temporary\"' EXIT
     cat >\"\$temporary\"
     chmod 600 \"\$temporary\"
     mv \"\$temporary\" '/home/$operator/.ssh/authorized_keys'
     trap - EXIT"
ssh "${new_host_ssh_options[@]}" "$worker_target" true

operator_gid="$(
  ssh "${new_host_ssh_options[@]}" "$worker_target" id -g
)"
[[ "$operator_gid" =~ ^[0-9]+$ ]] ||
  { echo "could not resolve worker operator group" >&2; exit 1; }

echo "Creating persistent worker state with restrictive ownership..."
ssh "${new_host_ssh_options[@]}" "$worker_target" \
  "sudo install -d -m 750 -o '$operator' -g '$operator_gid' /srv/nilbots
   sudo install -d -m 700 -o '$operator' -g '$operator_gid' \
     '$deploy_root' \
     '$deploy_root/bin' \
     '$deploy_root/incoming' \
     '$deploy_root/releases' \
     '$deploy_root/shared' \
     '$deploy_root/shared/backups'
   sudo install -d -m 770 -o '$operator' -g '$operator_gid' \
     '$deploy_root/shared/secrets'
   sudo chown '1654:$operator_gid' '$deploy_root/shared/secrets'"

echo "Rendering the minimal worker environment without Garage administration secrets..."
ssh "${ssh_options[@]}" "$primary_target" \
  "$(remote_command \
    bash -s -- \
    "$deploy_root/shared/.env" \
    "$primary_private_ip" \
    "$worker_private_ip" \
    "compile-$worker_name" \
    "$worker_size")" \
  <"$deploy_dir/render-worker-env.sh" |
  ssh "${new_host_ssh_options[@]}" "$worker_target" \
    "set -e
     temporary=\$(mktemp)
     trap 'rm -f \"\$temporary\"' EXIT
     cat >\"\$temporary\"
     sudo install -m 600 -o '$operator' -g '$operator_gid' \
       \"\$temporary\" '$deploy_root/shared/.env'
     rm -f \"\$temporary\"
     trap - EXIT"

copy_certificate() {
  local name="$1"
  ssh "${ssh_options[@]}" "$primary_target" \
    "cat '$deploy_root/shared/secrets/$name'" |
    ssh "${new_host_ssh_options[@]}" "$worker_target" \
      "set -e
       temporary=\$(mktemp)
       trap 'rm -f \"\$temporary\"' EXIT
       cat >\"\$temporary\"
       sudo install -m 660 -o '$operator' -g '$operator_gid' \
         \"\$temporary\" '$deploy_root/shared/secrets/$name'
       sudo chown '1654:$operator_gid' '$deploy_root/shared/secrets/$name'
       rm -f \"\$temporary\"
       trap - EXIT"

  primary_hash="$(
    ssh "${ssh_options[@]}" "$primary_target" \
      "sha256sum '$deploy_root/shared/secrets/$name'" |
      awk '{ print $1 }'
  )"
  worker_hash="$(
    ssh "${new_host_ssh_options[@]}" "$worker_target" \
      "sudo sha256sum '$deploy_root/shared/secrets/$name'" |
      awk '{ print $1 }'
  )"
  [[ "$primary_hash" == "$worker_hash" ]] ||
    { echo "$name did not copy exactly" >&2; exit 1; }
}

echo "Copying and hash-verifying the shared OpenIddict certificate pair..."
copy_certificate openiddict-signing.pfx
copy_certificate openiddict-encryption.pfx

echo "Installing persistent Docker ingress rules and disabling root SSH..."
run_worker_root_script \
  "$deploy_dir/finalize-worker-host.sh" \
  "$primary_private_ip" \
  "$worker_private_ip" \
  "$operator" \
  8080

echo "Running worker hardening, secret-boundary, and private-network preflight..."
verify_command="$(
  remote_command \
    sudo bash -s -- \
    "$deploy_root" \
    "$primary_private_ip" \
    "$worker_private_ip" \
    "$operator"
)"
ssh "${new_host_ssh_options[@]}" \
  "$worker_target" \
  "$verify_command" \
  <"$deploy_dir/verify-worker-host.sh"

if ssh "${ssh_options[@]}" "root@$worker_host" true 2>/dev/null; then
  echo "root SSH remains available after worker finalization" >&2
  exit 1
fi

if [[ "$register" == "1" ]]; then
  echo "Granting exact-address PostgreSQL access on the primary..."
  primary_access_added=0
  cleanup_primary_access() {
    if [[ "$primary_access_added" == "1" ]]; then
      ssh "${ssh_options[@]}" "$primary_target" \
        "$(remote_command sudo bash -s -- remove "$worker_private_ip")" \
        <"$deploy_dir/configure-primary-worker-access.sh" || true
    fi
  }
  trap cleanup_primary_access EXIT
  ssh "${ssh_options[@]}" "$primary_target" \
    "$(remote_command sudo bash -s -- add "$worker_private_ip")" \
    <"$deploy_dir/configure-primary-worker-access.sh"
  primary_access_added=1

  echo "Registering the verified worker in the primary's non-secret fleet inventory..."
  read -r host_key_type host_key_base64 _ < <(
    ssh "${new_host_ssh_options[@]}" "$worker_target" \
      "sudo cat /etc/ssh/ssh_host_ed25519_key.pub"
  )
  [[ "$host_key_type" == "ssh-ed25519" && -n "$host_key_base64" ]] ||
    { echo "could not read the worker Ed25519 host key" >&2; exit 1; }
  inventory="$deploy_root/shared/workers.tsv"
  ssh "${ssh_options[@]}" "$primary_target" \
    "$(remote_command \
      bash -s -- \
      upsert \
      "$inventory" \
      "$worker_name" \
      "$worker_host" \
      "$operator" \
      "$deploy_root" \
      "$worker_private_ip" \
      "$host_key_type" \
      "$host_key_base64")" \
    <"$deploy_dir/worker-inventory.sh"
  primary_access_added=0
  trap - EXIT
  echo "Worker '$worker_name' registered; the next manual release will deploy it."
else
  echo "Worker verified but not registered (--no-register)."
fi

echo "Bootstrap complete. No application release was triggered."
