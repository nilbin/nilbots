#!/usr/bin/env bash
set -euo pipefail

keys=(
  BOTARENA_FRONTLINE_LABS_ENABLED
  BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY
  BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE
  BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE
)

usage() {
  cat >&2 <<'EOF'
usage:
  sync-worker-feature-env.sh render PRIMARY_ENV
  sync-worker-feature-env.sh apply WORKER_ENV KEY=VALUE...
EOF
  exit 2
}

line_for() {
  local source_env="$1"
  local key="$2"
  awk -v key="$key" '
    index($0, key "=") == 1 {
      count += 1
      value = substr($0, length(key) + 2)
    }
    END {
      if (count == 1) print value
      else if (count > 1) exit 2
    }
  ' "$source_env"
}

normalize_value() {
  local key="$1"
  local value="$2"
  local minimum maximum
  case "$key" in
    BOTARENA_FRONTLINE_LABS_ENABLED)
      [[ "$value" == "true" || "$value" == "false" ]] ||
        { echo "$key must be true or false" >&2; return 1; }
      printf '%s\n' "$value"
      ;;
    BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY)
      minimum=1
      maximum=100
      ;;
    BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE)
      minimum=1
      maximum=10
      ;;
    BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE)
      minimum=1
      maximum=100
      ;;
    *)
      echo "unsupported worker feature key: $key" >&2
      return 1
      ;;
  esac
  if [[ "$key" != "BOTARENA_FRONTLINE_LABS_ENABLED" ]]; then
    [[ "$value" =~ ^[0-9]+$ && ${#value} -le 3 ]] ||
      { echo "$key must be an integer" >&2; return 1; }
    local normalized=$((10#$value))
    ((normalized >= minimum && normalized <= maximum)) ||
      {
        echo "$key must be between $minimum and $maximum" >&2
        return 1
      }
    printf '%d\n' "$normalized"
  fi
}

render() {
  [[ $# -eq 1 ]] || usage
  local source_env="$1"
  [[ -f "$source_env" ]] ||
    { echo "primary environment does not exist" >&2; exit 1; }

  local key value normalized fallback
  for key in "${keys[@]}"; do
    case "$key" in
      BOTARENA_FRONTLINE_LABS_ENABLED) fallback=false ;;
      BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY) fallback=10 ;;
      BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE) fallback=1 ;;
      BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE) fallback=4 ;;
    esac
    if grep -q "^$key=" "$source_env"; then
      if ! value="$(line_for "$source_env" "$key")"; then
        echo "primary environment contains duplicate $key" >&2
        exit 1
      fi
    else
      value="$fallback"
    fi
    normalized="$(normalize_value "$key" "$value")"
    printf '%s=%s\n' "$key" "$normalized"
  done
}

file_mode() {
  local path="$1"
  if stat -c '%a' "$path" >/dev/null 2>&1; then
    stat -c '%a' "$path"
  else
    stat -f '%Lp' "$path"
  fi
}

apply_values() {
  [[ $# -eq 5 ]] || usage
  local worker_env="$1"
  shift
  [[ -f "$worker_env" ]] ||
    { echo "worker environment does not exist" >&2; exit 1; }

  local enabled="" account_daily="" account_active="" global_active=""
  local seen=" " pair key value normalized
  for pair in "$@"; do
    [[ "$pair" == *=* ]] ||
      { echo "worker feature input must be KEY=VALUE" >&2; exit 1; }
    key="${pair%%=*}"
    value="${pair#*=}"
    case "$seen" in
      *" $key "*)
        echo "worker feature input contains duplicate $key" >&2
        exit 1
        ;;
    esac
    normalized="$(normalize_value "$key" "$value")"
    seen+="$key "
    case "$key" in
      BOTARENA_FRONTLINE_LABS_ENABLED) enabled="$normalized" ;;
      BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY)
        account_daily="$normalized"
        ;;
      BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE)
        account_active="$normalized"
        ;;
      BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE)
        global_active="$normalized"
        ;;
    esac
  done
  [[ -n "$enabled" &&
     -n "$account_daily" &&
     -n "$account_active" &&
     -n "$global_active" ]] ||
    { echo "worker feature input is incomplete" >&2; exit 1; }

  local directory basename temporary mode
  directory="$(cd "$(dirname "$worker_env")" && pwd -P)"
  basename="$(basename "$worker_env")"
  temporary="$(mktemp "$directory/.${basename}.XXXXXX")"
  trap 'rm -f "$temporary"' EXIT
  mode="$(file_mode "$worker_env")"
  awk '
    !/^BOTARENA_FRONTLINE_LABS_ENABLED=/ &&
    !/^BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY=/ &&
    !/^BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE=/ &&
    !/^BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE=/
  ' "$worker_env" >"$temporary"
  printf '%s\n' \
    "BOTARENA_FRONTLINE_LABS_ENABLED=$enabled" \
    "BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY=$account_daily" \
    "BOTARENA_FRONTLINE_LABS_ACCOUNT_ACTIVE=$account_active" \
    "BOTARENA_FRONTLINE_LABS_GLOBAL_ACTIVE=$global_active" \
    >>"$temporary"
  chmod "$mode" "$temporary"
  mv "$temporary" "$worker_env"
  trap - EXIT
}

[[ $# -ge 1 ]] || usage
operation="$1"
shift
case "$operation" in
  render) render "$@" ;;
  apply) apply_values "$@" ;;
  *) usage ;;
esac
