#!/usr/bin/env bash
# The standard Arc Relay sheet battery, as one command.
#
# Publishes the CLI once (the perf-audited fast path), runs the 24-cell
# seed grid (9001-9012 x west/east arrangements), then scores everything
# that matters:
#   - win/loss record by the sheet under test
#   - felt-degeneracy bars per cell (the gallery gate)
#   - engagement resolution (unresolved-contact streaks)
#   - per-unit behavior audit (confinement flags - the camping-ghost
#     lesson: bars-clean does not mean behaving)
#
# Usage:
#   scripts/arc-relay-battery.sh NAME SHEET_A SHEET_B [OUTDIR]
#
#   NAME     battery label (cells land in OUTDIR/NAME-{w,e}-SEED)
#   SHEET_A  playbook under test (west team 0 in w cells, east team 1 in
#            e cells)
#   SHEET_B  opponent playbook
#   OUTDIR   scratch root (default: $BATTERY_OUT or /tmp/arc-batteries)
#
# HARD RULE: never edit src/ or sheets while this runs.
set -euo pipefail

NAME=${1:?battery name}
SHEET_A=${2:?sheet under test}
SHEET_B=${3:?opponent sheet}
OUTDIR=${4:-${BATTERY_OUT:-/tmp/arc-batteries}}
REPO=$(cd "$(dirname "$0")/.." && pwd)
BARS=$REPO/balance/arc-relay-felt-degeneracy-bars-v7.json
MIND=$REPO/arena-bots/arc-relay/tactical-playbook-mind-v1
PUB=$OUTDIR/botarena-pub

mkdir -p "$OUTDIR"
echo "publishing CLI once..."
dotnet publish "$REPO/src/BotArena.Cli" -c Release -o "$PUB" >/dev/null

for seed in 9001 9002 9003 9004 9005 9006 9007 9008 9009 9010 9011 9012; do
  for side in w e; do
    OUT=$OUTDIR/$NAME-$side-$seed
    if [ "$side" = w ]; then S0=$SHEET_A; S1=$SHEET_B; else S0=$SHEET_B; S1=$SHEET_A; fi
    rm -rf "$OUT"
    "$PUB/botarena" experiment arc-relay \
      --bot "$MIND" --opponent "$MIND" --runtime in-process \
      --loop-profile ambush-warren-11 --seed "$seed" \
      --sheet0 "$S0" --sheet1 "$S1" --out "$OUT" \
      > "$OUTDIR/$NAME-$side-$seed.log" 2>&1 \
      || echo "CELL FAILED: $NAME-$side-$seed"
  done
done

echo "scoring..."
BCAST=$OUTDIR/$NAME-bcast
mkdir -p "$BCAST"
python3 - "$NAME" "$OUTDIR" "$SHEET_A" "$BCAST" "$BARS" "$REPO" <<'EOF'
import json, gzip, glob, os, subprocess, sys
name, outdir, sheet_a, bcast, bars, repo = sys.argv[1:7]
sheet_key = os.path.basename(sheet_a).removesuffix('.json')
wins = losses = clean = total = streaks = 0
trips_report = []
confined_report = []
for seed in range(9001, 9013):
    for side in 'we':
        cell = f'{name}-{side}-{seed}'
        run_path = f'{outdir}/{cell}/run.json'
        if not os.path.exists(run_path):
            continue
        run = json.load(open(run_path))
        team = next(p['TeamId'] for p in run['Participants']
                    if sheet_key in (p.get('SheetPath') or ''))
        if run['Result'].get('WinnerTeamId') == team:
            wins += 1
        else:
            losses += 1
        total += 1
        raw = f'{outdir}/{cell}/replay.json.gz'
        plain = f'{bcast}/{cell}-c.json'
        open(plain, 'w').write(gzip.open(raw, 'rt').read())
        subprocess.run(['python3', f'{repo}/scripts/arc-relay-broadcast.py',
                        plain, '--output', f'{bcast}/{cell}.json.gz'],
                       capture_output=True)
        os.remove(plain)
        card = json.loads(subprocess.run(
            ['python3', f'{repo}/scripts/arc-relay-scorecard.py',
             f'{bcast}/{cell}.json.gz', '--bars', bars],
            capture_output=True, text=True).stdout)
        felt = card['feltDegeneracy']
        trips = {k: [t for t, v in val.get('barTrippedByTeam', {}).items() if v]
                 for k, val in felt.items()
                 if isinstance(val, dict)
                 and any(val.get('barTrippedByTeam', {}).values())}
        if trips:
            trips_report.append((cell, trips))
        else:
            clean += 1
        streaks += card['engagementResolution']['unresolvedContactStreaks']
        aud = subprocess.run(
            ['python3', f'{repo}/scripts/arc-relay-unit-audit.py', raw],
            capture_output=True, text=True).stdout
        for line in aud.splitlines():
            if 'CONFINED' in line:
                confined_report.append(f'{cell}: {line.strip()}')
print(f'record ({sheet_key}): {wins}-{losses}')
print(f'bars-clean: {clean}/{total} | unresolved streaks: {streaks}')
for cell, trips in trips_report:
    print(f'  TRIP {cell} {json.dumps(trips)}')
print(f'confined units (>=60t in a radius-2 pocket): {len(confined_report)}')
for line in confined_report:
    print(f'  {line}')
EOF
