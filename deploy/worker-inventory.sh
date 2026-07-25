#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
usage:
  worker-inventory.sh validate FILE
  worker-inventory.sh upstreams FILE
  worker-inventory.sh targets FILE
  worker-inventory.sh known-hosts FILE
  worker-inventory.sh record FILE NAME
  worker-inventory.sh upsert FILE NAME SSH_HOST SSH_USER DEPLOY_PATH PRIVATE_IP KEY_TYPE KEY_BASE64
  worker-inventory.sh remove FILE NAME

The inventory is a tab-separated, non-secret fleet registry:
  name  ssh_host  ssh_user  deploy_path  private_ip  key_type  key_base64
EOF
  exit 2
}

is_private_ipv4() {
  local ip="$1"
  local a b c d
  IFS=. read -r a b c d <<<"$ip"
  for octet in "$a" "$b" "$c" "$d"; do
    [[ "$octet" =~ ^[0-9]{1,3}$ ]] || return 1
    ((10#$octet <= 255)) || return 1
  done
  if ((10#$a == 10)); then
    return 0
  fi
  if ((10#$a == 172 && 10#$b >= 16 && 10#$b <= 31)); then
    return 0
  fi
  ((10#$a == 192 && 10#$b == 168))
}

validate_record() {
  local name="$1"
  local host="$2"
  local user="$3"
  local path="$4"
  local private_ip="$5"
  local key_type="$6"
  local key_base64="$7"

  [[ "$name" =~ ^[a-z0-9][a-z0-9-]{0,62}$ ]] ||
    { echo "invalid worker name '$name'" >&2; return 1; }
  [[ "$host" =~ ^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$ ]] ||
    { echo "invalid worker SSH host '$host'" >&2; return 1; }
  [[ "$user" =~ ^[a-z_][a-z0-9_-]*$ ]] ||
    { echo "invalid worker SSH user '$user'" >&2; return 1; }
  [[ "$path" =~ ^/[A-Za-z0-9._/-]+$ &&
     "$path" != "/" &&
     "$path" != *"//"* &&
     "$path" != *"/./"* &&
     "$path" != *"/../"* ]] ||
    { echo "invalid worker deployment path '$path'" >&2; return 1; }
  is_private_ipv4 "$private_ip" ||
    { echo "worker address '$private_ip' is not private IPv4" >&2; return 1; }
  [[ "$key_type" == "ssh-ed25519" ]] ||
    { echo "worker '$name' must use an Ed25519 SSH host key" >&2; return 1; }
  [[ "$key_base64" =~ ^[A-Za-z0-9+/]+={0,2}$ ]] ||
    { echo "invalid SSH host key for worker '$name'" >&2; return 1; }
}

read_inventory() {
  local file="$1"
  local callback="$2"
  [[ -f "$file" ]] || return 0

  local line_number=0
  local name host user path private_ip key_type key_base64 extra
  while IFS=$'\t' read -r \
    name host user path private_ip key_type key_base64 extra ||
    [[ -n "${name}${host}${user}${path}${private_ip}${key_type}${key_base64}${extra}" ]]; do
    ((line_number += 1))
    [[ -z "$name" || "$name" == \#* ]] && continue
    if [[ -n "$extra" ||
          -z "$host" ||
          -z "$user" ||
          -z "$path" ||
          -z "$private_ip" ||
          -z "$key_type" ||
          -z "$key_base64" ]]; then
      echo "invalid worker inventory record at $file:$line_number" >&2
      return 1
    fi
    validate_record \
      "$name" "$host" "$user" "$path" "$private_ip" "$key_type" "$key_base64"
    "$callback" \
      "$name" "$host" "$user" "$path" "$private_ip" "$key_type" "$key_base64"
  done <"$file"
}

seen_names=$'\n'
seen_hosts=$'\n'
seen_private_ips=$'\n'

validate_unique() {
  local name="$1"
  local host="$2"
  local private_ip="$5"
  [[ "$seen_names" != *$'\n'"$name"$'\n'* ]] ||
    { echo "duplicate worker name '$name'" >&2; return 1; }
  [[ "$seen_hosts" != *$'\n'"$host"$'\n'* ]] ||
    { echo "duplicate worker SSH host '$host'" >&2; return 1; }
  [[ "$seen_private_ips" != *$'\n'"$private_ip"$'\n'* ]] ||
    { echo "duplicate worker private address '$private_ip'" >&2; return 1; }
  seen_names+="$name"$'\n'
  seen_hosts+="$host"$'\n'
  seen_private_ips+="$private_ip"$'\n'
}

validate_inventory() {
  seen_names=$'\n'
  seen_hosts=$'\n'
  seen_private_ips=$'\n'
  read_inventory "$1" validate_unique
}

print_upstream() {
  printf ' %s:8080' "$5"
}

print_target() {
  printf '%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$4"
}

print_known_host() {
  printf '%s %s %s\n' "$2" "$6" "$7"
}

record_name=""
print_matching_record() {
  [[ "$1" == "$record_name" ]] || return 0
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$@"
}

write_header() {
  printf '# nilbots worker inventory v1\n'
  printf '# name\tssh_host\tssh_user\tdeploy_path\tprivate_ip\tkey_type\tkey_base64\n'
}

lock_dir=""
temporary=""
cleanup_mutation() {
  [[ -z "$temporary" ]] || rm -f "$temporary"
  [[ -z "$lock_dir" ]] || rmdir "$lock_dir" 2>/dev/null || true
}

acquire_mutation_lock() {
  lock_dir="${file}.lock"
  if ! mkdir "$lock_dir" 2>/dev/null; then
    echo "another worker inventory update is active" >&2
    exit 1
  fi
  trap cleanup_mutation EXIT
}

[[ $# -ge 2 ]] || usage
operation="$1"
file="$2"
shift 2

case "$operation" in
  validate)
    [[ $# -eq 0 ]] || usage
    validate_inventory "$file"
    ;;
  upstreams)
    [[ $# -eq 0 ]] || usage
    validate_inventory "$file"
    printf 'web:8080'
    read_inventory "$file" print_upstream
    printf '\n'
    ;;
  targets)
    [[ $# -eq 0 ]] || usage
    validate_inventory "$file"
    read_inventory "$file" print_target
    ;;
  known-hosts)
    [[ $# -eq 0 ]] || usage
    validate_inventory "$file"
    read_inventory "$file" print_known_host
    ;;
  record)
    [[ $# -eq 1 ]] || usage
    record_name="$1"
    [[ "$record_name" =~ ^[a-z0-9][a-z0-9-]{0,62}$ ]] ||
      { echo "invalid worker name '$record_name'" >&2; exit 2; }
    validate_inventory "$file"
    read_inventory "$file" print_matching_record
    ;;
  upsert)
    [[ $# -eq 7 ]] || usage
    name="$1"
    host="$2"
    user="$3"
    path="$4"
    private_ip="$5"
    key_type="$6"
    key_base64="$7"
    validate_record \
      "$name" "$host" "$user" "$path" "$private_ip" "$key_type" "$key_base64"
    mkdir -p "$(dirname "$file")"
    acquire_mutation_lock
    temporary="$(mktemp "${file}.XXXXXX")"
    {
      write_header
      if [[ -f "$file" ]]; then
        awk -F'\t' -v name="$name" '
          /^#/ || NF == 0 { next }
          $1 != name { print }
        ' "$file"
      fi
      printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
        "$name" "$host" "$user" "$path" "$private_ip" "$key_type" "$key_base64"
    } >"$temporary"
    chmod 600 "$temporary"
    validate_inventory "$temporary"
    mv "$temporary" "$file"
    temporary=""
    rmdir "$lock_dir"
    lock_dir=""
    trap - EXIT
    ;;
  remove)
    [[ $# -eq 1 ]] || usage
    name="$1"
    [[ "$name" =~ ^[a-z0-9][a-z0-9-]{0,62}$ ]] ||
      { echo "invalid worker name '$name'" >&2; exit 2; }
    mkdir -p "$(dirname "$file")"
    acquire_mutation_lock
    temporary="$(mktemp "${file}.XXXXXX")"
    {
      write_header
      if [[ -f "$file" ]]; then
        awk -F'\t' -v name="$name" '
          /^#/ || NF == 0 { next }
          $1 != name { print }
        ' "$file"
      fi
    } >"$temporary"
    chmod 600 "$temporary"
    validate_inventory "$temporary"
    mv "$temporary" "$file"
    temporary=""
    rmdir "$lock_dir"
    lock_dir=""
    trap - EXIT
    ;;
  *)
    usage
    ;;
esac
