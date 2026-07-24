#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || "$1" != /* ]]; then
  echo "usage: BOTARENA_OPENIDDICT_CERT_PASSWORD=... $0 /absolute/output/directory" >&2
  exit 2
fi
if [[ -z "${BOTARENA_OPENIDDICT_CERT_PASSWORD:-}" ]]; then
  echo "BOTARENA_OPENIDDICT_CERT_PASSWORD must be set" >&2
  exit 2
fi

output_dir="$1"
signing_pfx="$output_dir/openiddict-signing.pfx"
encryption_pfx="$output_dir/openiddict-encryption.pfx"
if [[ -e "$signing_pfx" || -e "$encryption_pfx" ]]; then
  echo "refusing to overwrite an existing OpenIddict certificate" >&2
  exit 1
fi

mkdir -p "$output_dir"
# The production web container runs as an unprivileged UID. Bind mounts keep
# the host directory's traversal bits, so the directory must be searchable.
# The PFX files remain password protected and the password stays in mode-0600
# deployment configuration.
chmod 755 "$output_dir"
certificate_tmp="$(mktemp -d)"
trap 'rm -rf "$certificate_tmp"' EXIT

generate_certificate() {
  local purpose="$1"
  local key_usage="$2"
  local output="$3"
  openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes \
    -subj "/CN=nilbots $purpose" \
    -addext "keyUsage=critical,$key_usage" \
    -keyout "$certificate_tmp/$purpose.key" \
    -out "$certificate_tmp/$purpose.crt" >/dev/null 2>&1
  openssl pkcs12 -export \
    -inkey "$certificate_tmp/$purpose.key" \
    -in "$certificate_tmp/$purpose.crt" \
    -out "$output" \
    -passout env:BOTARENA_OPENIDDICT_CERT_PASSWORD
  # World-read inside the explicitly mounted web container lets its
  # unprivileged UID read the password-protected bind-mounted file.
  chmod 644 "$output"
}

generate_certificate "signing" "digitalSignature" "$signing_pfx"
generate_certificate "encryption" "keyEncipherment,dataEncipherment" "$encryption_pfx"
echo "created $signing_pfx and $encryption_pfx"
