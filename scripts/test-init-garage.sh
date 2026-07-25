#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

write_fake_docker() {
  local fake_docker="$1"
  cat >"$fake_docker" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

while [[ "$#" -gt 0 && "$1" != "exec" ]]; do
  shift
done
[[ "${1:-}" == "exec" ]]
shift
while [[ "${1:-}" == -* ]]; do
  case "$1" in
    -T)
      shift
      ;;
    -e)
      shift 2
      ;;
    *)
      echo "unexpected docker compose exec option: $1" >&2
      exit 1
      ;;
  esac
done
service="$1"
shift
[[ "$1" == "/garage" ]]
shift

next_count() {
  local name="$1"
  local file="$FAKE_GARAGE_STATE/$name"
  local count=0
  if [[ -f "$file" ]]; then
    read -r count <"$file"
  fi
  count=$((count + 1))
  printf '%s\n' "$count" >"$file"
  printf '%s\n' "$count"
}

node_id() {
  case "$1" in
    garage-a) printf 'a%.0s' {1..64} ;;
    garage-b) printf 'b%.0s' {1..64} ;;
    garage-c) printf 'c%.0s' {1..64} ;;
    garage-gateway) printf 'd%.0s' {1..64} ;;
    *) return 1 ;;
  esac
}

if [[ "$1" == "node" && "$2" == "id" ]]; then
  printf '%s@%s:3901\n' "$(node_id "$service")" "$service"
  exit 0
fi

if [[ "$1" == "node" && "$2" == "connect" ]]; then
  printf '%s\n' "$3" >>"$FAKE_GARAGE_STATE/connects"
  exit 0
fi

if [[ "$1" == "status" ]]; then
  count="$(next_count status-count)"
  if (( count == 1 )); then
    cat <<'STATUS'
==== HEALTHY NODES ====
ID                Hostname  Address          Tags       Zone  Capacity  DataAvail  Version
dddddddddddddddd  gateway   172.18.0.7:3901  [gateway]  test  gateway   N/A        v2.3.0

==== FAILED NODES ====
ID                Hostname  Tags        Zone  Capacity  Last seen
aaaaaaaaaaaaaaaa  ?         [garage-a]  test  1 GB      never seen
bbbbbbbbbbbbbbbb  ?         [garage-b]  test  1 GB      never seen
cccccccccccccccc  ?         [garage-c]  test  1 GB      never seen
STATUS
  else
    cat <<'STATUS'
==== HEALTHY NODES ====
ID                Hostname  Address          Tags        Zone  Capacity  DataAvail  Version
aaaaaaaaaaaaaaaa  node-a    172.18.0.6:3901  [garage-a]  test  1 GB      1 GB       v2.3.0
bbbbbbbbbbbbbbbb  node-b    172.18.0.9:3901  [garage-b]  test  1 GB      1 GB       v2.3.0
cccccccccccccccc  node-c    172.18.0.8:3901  [garage-c]  test  1 GB      1 GB       v2.3.0
dddddddddddddddd  gateway   172.18.0.7:3901  [gateway]   test  gateway   N/A        v2.3.0
STATUS
  fi
  exit 0
fi

if [[ "$1" == "layout" && "$2" == "show" ]]; then
  count="$(next_count layout-count)"
  if [[ "$FAKE_GARAGE_SCENARIO" == "existing-transient" && "$count" == 1 ]]; then
    echo "Error: ServiceUnavailable (503): Could not reach quorum of 2" >&2
    exit 1
  fi
  cat <<'LAYOUT'
Current cluster layout version: 1
aaaaaaaaaaaaaaaa garage-a
bbbbbbbbbbbbbbbb garage-b
cccccccccccccccc garage-c
dddddddddddddddd gateway
LAYOUT
  exit 0
fi

if [[ "$1" == "bucket" && "$2" == "info" ]]; then
  count="$(next_count bucket-info-count)"
  if [[ "$FAKE_GARAGE_SCENARIO" == "existing-transient" ]]; then
    if (( count == 1 )); then
      echo "Error: ServiceUnavailable (503): Could not reach quorum of 2" >&2
      exit 1
    fi
  elif (( count <= 2 )); then
    echo "Error: GetBucketInfo returned NoSuchBucket (404): Bucket not found" >&2
    exit 1
  fi
  echo "Bucket: nilbots"
  exit 0
fi

if [[ "$1" == "bucket" && "$2" == "create" ]]; then
  count="$(next_count bucket-create-count)"
  if [[ "$FAKE_GARAGE_SCENARIO" == "missing-race" ]]; then
    if (( count == 1 )); then
      echo "Error: ServiceUnavailable (503): Could not reach quorum of 2" >&2
    else
      echo "Error: BucketAlreadyExists" >&2
    fi
    exit 1
  fi
  echo "unexpected bucket create" >&2
  exit 1
fi

if [[ "$1" == "key" && "$2" == "info" ]]; then
  count="$(next_count key-info-count)"
  if [[ "$FAKE_GARAGE_SCENARIO" == "existing-transient" ]]; then
    if (( count == 1 )); then
      echo "Error: ServiceUnavailable (503): Could not reach quorum of 2" >&2
      exit 1
    fi
  elif (( count <= 2 )); then
    echo "Error: GetKeyInfo returned NoSuchAccessKey (404): Access key not found" >&2
    exit 1
  fi
  printf 'Secret key: %s\n' "$BOTARENA_S3_SECRET_KEY"
  exit 0
fi

if [[ "$1" == "key" && "$2" == "import" ]]; then
  count="$(next_count key-import-count)"
  if [[ "$FAKE_GARAGE_SCENARIO" == "missing-race" ]]; then
    if (( count == 1 )); then
      echo "Error: ServiceUnavailable (503): Could not reach quorum of 2" >&2
    else
      echo "Error: KeyAlreadyExists" >&2
    fi
    exit 1
  fi
  echo "unexpected key import" >&2
  exit 1
fi

if [[ "$1" == "bucket" && "$2" == "allow" ]]; then
  count="$(next_count bucket-allow-count)"
  if [[ "$FAKE_GARAGE_SCENARIO" == "existing-transient" && "$count" == 1 ]]; then
    echo "Error: ServiceUnavailable (503): Could not reach quorum of 2" >&2
    exit 1
  fi
  exit 0
fi

printf 'unexpected Garage command for %s: %s\n' "$service" "$*" >&2
exit 1
EOF
  chmod 755 "$fake_docker"
}

run_scenario() {
  local scenario="$1"
  local scenario_root="$test_root/$scenario"
  local deploy_dir="$scenario_root/deploy"
  local fake_bin="$scenario_root/bin"
  local state="$scenario_root/state"
  mkdir -p "$deploy_dir" "$fake_bin" "$state"
  cp deploy/init-garage.sh "$deploy_dir/init-garage.sh"
  touch "$deploy_dir/compose.production.yml"
  cat >"$deploy_dir/.env" <<'EOF'
GARAGE_RPC_SECRET=1111111111111111111111111111111111111111111111111111111111111111
GARAGE_ADMIN_TOKEN=admin-token
GARAGE_METRICS_TOKEN=metrics-token
GARAGE_ZONE=test
GARAGE_NODE_CAPACITY=1GB
BOTARENA_S3_BUCKET=nilbots
BOTARENA_S3_ACCESS_KEY=GK22222222222222222222222222222222
BOTARENA_S3_SECRET_KEY=3333333333333333333333333333333333333333333333333333333333333333
GARAGE_INIT_ATTEMPTS=5
GARAGE_INIT_RETRY_DELAY_SECONDS=0
EOF
  write_fake_docker "$fake_bin/docker"

  FAKE_GARAGE_SCENARIO="$scenario" \
    FAKE_GARAGE_STATE="$state" \
    PATH="$fake_bin:$PATH" \
    bash "$deploy_dir/init-garage.sh"

  for peer in \
    "$(printf 'a%.0s' {1..64})@garage-a:3901" \
    "$(printf 'b%.0s' {1..64})@garage-b:3901" \
    "$(printf 'c%.0s' {1..64})@garage-c:3901"; do
    grep -Fqx "$peer" "$state/connects"
  done
  [[ "$(cat "$state/status-count")" == 2 ]]
}

run_scenario existing-transient
existing_state="$test_root/existing-transient/state"
[[ "$(cat "$existing_state/layout-count")" == 2 ]]
[[ "$(cat "$existing_state/bucket-info-count")" == 2 ]]
[[ ! -f "$existing_state/bucket-create-count" ]]
[[ "$(cat "$existing_state/key-info-count")" == 2 ]]
[[ ! -f "$existing_state/key-import-count" ]]
[[ "$(cat "$existing_state/bucket-allow-count")" == 2 ]]

run_scenario missing-race
missing_state="$test_root/missing-race/state"
[[ "$(cat "$missing_state/bucket-info-count")" == 3 ]]
[[ "$(cat "$missing_state/bucket-create-count")" == 2 ]]
[[ "$(cat "$missing_state/key-info-count")" == 3 ]]
[[ "$(cat "$missing_state/key-import-count")" == 2 ]]
[[ "$(cat "$missing_state/bucket-allow-count")" == 1 ]]

echo "Garage stale-peer recovery and metadata retries passed"
