#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat >&2 <<'EOF'
usage:
  HOSTUP_API_KEY=... hostup-vps.sh attach-private VPS_ID_OR_PUBLIC_IP NETWORK_NAME [PRIVATE_IP|auto]
  HOSTUP_API_KEY=... hostup-vps.sh status VPS_ID_OR_PUBLIC_IP
  HOSTUP_API_KEY=... hostup-vps.sh shutdown VPS_ID_OR_PUBLIC_IP
  HOSTUP_API_KEY=... hostup-vps.sh start VPS_ID_OR_PUBLIC_IP

The API key is read only from the environment. `attach-private` is idempotent.
With `auto`, it selects the first unassigned address from .10 through .254 in
the private network's first active /24 subnet.
EOF
  exit 2
}

[[ -n "${HOSTUP_API_KEY:-}" ]] ||
  { echo "HOSTUP_API_KEY is required" >&2; exit 2; }
command_name="${1:-}"
vps_reference="${2:-}"
[[ -n "$command_name" && -n "$vps_reference" ]] || usage

api_request() {
  local method="$1"
  local path="$2"
  local body="${3:-}"
  local arguments=(
    --fail-with-body
    --silent
    --show-error
    -X "$method"
    -H "Authorization: Bearer $HOSTUP_API_KEY"
    -H "Accept: application/json"
  )
  if [[ -n "$body" ]]; then
    arguments+=(
      -H "Content-Type: application/json"
      --data "$body"
    )
  fi
  curl "${arguments[@]}" "https://cloud.hostup.se$path"
}

vps_id=""
vps_public_ip=""
resolve_vps() {
  local reference="$1"
  local inventory
  inventory="$(api_request GET '/api/v2/vps?limit=100')"
  local matches
  if [[ "$reference" == vps_* ]]; then
    matches="$(jq -c --arg reference "$reference" \
      '[.data[] | select(.id == $reference)]' <<<"$inventory")"
  else
    [[ "$reference" =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ ]] ||
      { echo "VPS reference must be a HostUp VPS ID or public IPv4" >&2; exit 2; }
    matches="$(jq -c --arg reference "$reference" \
      '[.data[] | select(.primaryIp == $reference)]' <<<"$inventory")"
  fi
  [[ "$(jq 'length' <<<"$matches")" == "1" ]] ||
    { echo "HostUp VPS reference did not resolve exactly once" >&2; exit 1; }
  vps_id="$(jq -r '.[0].id' <<<"$matches")"
  vps_public_ip="$(jq -r '.[0].primaryIp // empty' <<<"$matches")"
}

poll_operation() {
  local poll_url="$1"
  [[ "$poll_url" == /api/jobs/job_* ]] ||
    { echo "HostUp returned an invalid operation poll URL" >&2; exit 1; }
  local response operation_state
  for _ in {1..60}; do
    response="$(api_request GET "$poll_url")"
    operation_state="$(
      jq -r '.data.status // .data.state // .operation.status // empty' \
        <<<"$response"
    )"
    case "$operation_state" in
      completed)
        return 0
        ;;
      failed)
        jq '{job: .data.jobId, error: .data.error}' <<<"$response" >&2
        return 1
        ;;
      pending|queued|in_progress|running|waiting|"")
        sleep 2
        ;;
      *)
        echo "HostUp returned unknown operation state '$operation_state'" >&2
        return 1
        ;;
    esac
  done
  echo "HostUp operation did not finish within two minutes" >&2
  return 1
}

run_power_action() {
  local action="$1"
  local desired_state="$2"
  resolve_vps "$vps_reference"
  local current_state
  current_state="$(
    api_request GET "/api/v2/vps/$vps_id/status" |
      jq -r '.powerState'
  )"
  if [[ "$current_state" == "$desired_state" ]]; then
    printf '%s\t%s\t%s\n' "$vps_id" "$vps_public_ip" "$current_state"
    return
  fi

  local response poll_url
  response="$(api_request POST "/api/v2/vps/$vps_id/actions/$action" '{}')"
  poll_url="$(jq -r '.operation.pollUrl // empty' <<<"$response")"
  if [[ -n "$poll_url" ]]; then
    poll_operation "$poll_url"
  fi
  for _ in {1..30}; do
    current_state="$(
      api_request GET "/api/v2/vps/$vps_id/status" |
        jq -r '.powerState'
    )"
    if [[ "$current_state" == "$desired_state" ]]; then
      printf '%s\t%s\t%s\n' "$vps_id" "$vps_public_ip" "$current_state"
      return
    fi
    sleep 2
  done
  echo "VPS did not reach power state '$desired_state'" >&2
  exit 1
}

case "$command_name" in
  status)
    [[ $# -eq 2 ]] || usage
    resolve_vps "$vps_reference"
    power_state="$(
      api_request GET "/api/v2/vps/$vps_id/status" |
        jq -r '.powerState'
    )"
    printf '%s\t%s\t%s\n' "$vps_id" "$vps_public_ip" "$power_state"
    ;;
  shutdown)
    [[ $# -eq 2 ]] || usage
    run_power_action shutdown stopped
    ;;
  start)
    [[ $# -eq 2 ]] || usage
    run_power_action start running
    ;;
  attach-private)
    [[ $# -ge 3 && $# -le 4 ]] || usage
    network_name="$3"
    requested_private_ip="${4:-auto}"
    [[ "$network_name" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$ ]] ||
      { echo "invalid private network name" >&2; exit 2; }
    if [[ "$requested_private_ip" != "auto" &&
          ! "$requested_private_ip" =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ ]]; then
      echo "private address must be IPv4 or auto" >&2
      exit 2
    fi

    resolve_vps "$vps_reference"
    networks="$(api_request GET '/api/v2/private-networks?limit=100')"
    network_matches="$(
      jq -c --arg name "$network_name" \
        '[.data[] | select(.name == $name and .status == "active")]' \
        <<<"$networks"
    )"
    [[ "$(jq 'length' <<<"$network_matches")" == "1" ]] ||
      { echo "active HostUp private network name did not resolve exactly once" >&2; exit 1; }
    network_id="$(jq -r '.[0].id' <<<"$network_matches")"
    network="$(api_request GET "/api/v2/private-networks/$network_id")"
    [[ "$(jq -r '.unusedAddressReservationCount // 0' <<<"$network")" == "0" ]] ||
      { echo "private network has stale reserved addresses; choose an explicit IP" >&2; exit 1; }
    subnet_id="$(
      jq -r '[.subnets[] | select(.status == "active")][0].id // empty' \
        <<<"$network"
    )"
    subnet_cidr="$(
      jq -r '[.subnets[] | select(.status == "active")][0].subnetCidr // empty' \
        <<<"$network"
    )"
    [[ -n "$subnet_id" && "$subnet_cidr" =~ ^([0-9]{1,3}\.){3}0/24$ ]] ||
      { echo "HostUp network must have an active /24 subnet" >&2; exit 1; }
    subnet_prefix="${subnet_cidr%0/24}"

    vps_network="$(api_request GET "/api/v2/vps/$vps_id/network")"
    all_private_ips="$(
      jq -r '.interfaces[].ip[]? | select(.isPrivate == true) | .ip' \
        <<<"$vps_network"
    )"
    existing_private_ips="$(
      jq -r --arg cidr "$subnet_cidr" \
        '.interfaces[].ip[]? |
         select(.isPrivate == true and .subnet.parentCidr == $cidr) |
         .ip' <<<"$vps_network"
    )"
    all_private_count="$(
      awk 'NF { count += 1 } END { print count + 0 }' \
        <<<"$all_private_ips"
    )"
    matching_private_count="$(
      awk 'NF { count += 1 } END { print count + 0 }' \
        <<<"$existing_private_ips"
    )"
    if [[ "$all_private_count" -gt 0 ]]; then
      if [[ "$all_private_count" -eq 1 &&
            "$matching_private_count" -eq 1 &&
            ("$requested_private_ip" == "auto" ||
             "$existing_private_ips" == "$requested_private_ip") ]]; then
        printf '%s\n' "$existing_private_ips"
        exit 0
      fi
      echo "VPS already has a different private interface; refusing another" >&2
      exit 1
    fi

    private_ip="$requested_private_ip"
    if [[ "$private_ip" == "auto" ]]; then
      vps_inventory="$(api_request GET '/api/v2/vps?limit=100')"
      assigned_file="$(mktemp)"
      cleanup() {
        if [[ -f "$assigned_file" ]]; then
          rm -- "$assigned_file"
        fi
      }
      trap cleanup EXIT
      while IFS= read -r candidate_vps_id; do
        api_request GET "/api/v2/vps/$candidate_vps_id/network" |
          jq -r '.interfaces[].ip[]? | select(.isPrivate == true) | .ip' \
          >>"$assigned_file"
      done < <(jq -r '.data[].id' <<<"$vps_inventory")
      private_ip=""
      for host_octet in {10..254}; do
        candidate_ip="${subnet_prefix}${host_octet}"
        if ! grep -Fqx "$candidate_ip" "$assigned_file"; then
          private_ip="$candidate_ip"
          break
        fi
      done
      [[ -n "$private_ip" ]] ||
        { echo "no unassigned private address found in $subnet_cidr" >&2; exit 1; }
    else
      [[ "$private_ip" == "$subnet_prefix"* ]] ||
        { echo "$private_ip is outside $subnet_cidr" >&2; exit 2; }
      host_octet="${private_ip##*.}"
      [[ "$host_octet" =~ ^[0-9]{1,3}$ ]] &&
        ((10#$host_octet >= 2 && 10#$host_octet <= 254)) ||
        { echo "$private_ip is not a usable host address" >&2; exit 2; }
    fi

    request_body="$(
      jq -nc \
        --arg action create_private_interface \
        --arg subnetId "$subnet_id" \
        --arg ipAddress "$private_ip" \
        '{action: $action, subnetId: $subnetId, ipAddress: $ipAddress}'
    )"
    response="$(
      api_request POST "/api/v2/vps/$vps_id/network" "$request_body"
    )"
    poll_url="$(jq -r '.operation.pollUrl // empty' <<<"$response")"
    if [[ -n "$poll_url" ]]; then
      poll_operation "$poll_url"
    fi

    vps_network="$(api_request GET "/api/v2/vps/$vps_id/network")"
    jq -e --arg address "$private_ip" \
      'any(.interfaces[].ip[]?; .isPrivate == true and .ip == $address)' \
      <<<"$vps_network" >/dev/null ||
      { echo "HostUp did not attach expected private address" >&2; exit 1; }
    printf '%s\n' "$private_ip"
    ;;
  *)
    usage
    ;;
esac
