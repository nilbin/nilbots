#!/usr/bin/env bash
# One-command Arc Relay exhibition: sheet variant -> a few cells -> live
# gallery. The fast path for "change X and show me" (owner request
# 2026-08-08: no battery for exhibitions) - matches run in parallel off
# the reused published binary, broadcasts project in parallel, the
# gallery builds ONCE (with --index-cards the builder keeps manifest
# ids), and the site swaps in place so the standing tunnel URL just
# starts serving the new thing.
#
# Usage:
#   scripts/arc-relay-exhibit.sh NAME SHEET_A SHEET_B SITE_DIR CELL...
#
#   NAME      exhibition label (outputs land in $BATTERY_OUT/NAME-CELL)
#   SHEET_A   playbook under exhibition (team 0 in w cells, 1 in e)
#   SHEET_B   opponent playbook
#   SITE_DIR  served gallery root to swap (rsync --delete) - use the
#             DIAGNOSTIC site, never an unreviewed owner gallery
#   CELL      w-9001 e-9005 ... (side-seed)
#
# Env:
#   BATTERY_OUT     scratch root (default /tmp/arc-batteries)
#   EXHIBIT_TITLE   gallery title (default: NAME)
#   EXHIBIT_CARDS   optional curated cards JSON; without it the script
#                   writes honest minimal cards (win/loss, end tick, the
#                   sheet-under-test unit-0 audit line)
#
# Timing on the reference machine: ~2s per match, ~10s per broadcast
# (parallel), one gallery build ~20s. Six cells land in ~1 minute.
set -euo pipefail

NAME=${1:?exhibition name}
SHEET_A=${2:?sheet under exhibition}
SHEET_B=${3:?opponent sheet}
SITE=${4:?served site directory to swap}
shift 4
CELLS=("$@")
[ ${#CELLS[@]} -ge 1 ] || { echo "need at least one CELL (w-9001 ...)"; exit 2; }

OUTDIR=${BATTERY_OUT:-/tmp/arc-batteries}
REPO=$(cd "$(dirname "$0")/.." && pwd)
MIND=$REPO/arena-bots/arc-relay/tactical-playbook-mind-v1
PUB=$OUTDIR/botarena-pub
mkdir -p "$OUTDIR"

if [ ! -x "$PUB/botarena" ]; then
  echo "publishing CLI (first run only)..."
  dotnet publish "$REPO/src/BotArena.Cli" -c Release -o "$PUB" >/dev/null
fi

echo "running ${#CELLS[@]} cells..."
pids=()
for cell in "${CELLS[@]}"; do
  side=${cell%%-*}; seed=${cell##*-}
  if [ "$side" = w ]; then S0=$SHEET_A; S1=$SHEET_B; else S0=$SHEET_B; S1=$SHEET_A; fi
  OUT=$OUTDIR/$NAME-$cell
  rm -rf "$OUT"
  "$PUB/botarena" experiment arc-relay \
    --bot "$MIND" --opponent "$MIND" --runtime in-process \
    --loop-profile ambush-warren-11 --seed "$seed" \
    --sheet0 "$S0" --sheet1 "$S1" --out "$OUT" \
    > "$OUTDIR/$NAME-$cell.log" 2>&1 &
  pids+=($!)
done
for pid in "${pids[@]}"; do wait "$pid"; done
grep -H "Result:" "$OUTDIR/$NAME"-*.log | sed "s|$OUTDIR/||"

echo "broadcasting..."
BCAST=$OUTDIR/$NAME-bcast
mkdir -p "$BCAST"
pids=()
for cell in "${CELLS[@]}"; do
  (
    gunzip -c "$OUTDIR/$NAME-$cell/replay.json.gz" > "$BCAST/$cell-plain.json"
    python3 "$REPO/scripts/arc-relay-broadcast.py" "$BCAST/$cell-plain.json" \
      --output "$BCAST/$cell.json.gz" >/dev/null
    rm -f "$BCAST/$cell-plain.json"
  ) &
  pids+=($!)
done
for pid in "${pids[@]}"; do wait "$pid"; done

echo "building gallery..."
python3 - "$NAME" "$OUTDIR" "$BCAST" "$SHEET_A" "$REPO" "${CELLS[@]}" <<'EOF'
import json, os, subprocess, sys
name, outdir, bcast, sheet_a, repo = sys.argv[1:6]
cells = sys.argv[6:]
sheet_key = os.path.basename(sheet_a).removesuffix('.json')

manifest = {
    'sampleVersion': 2,
    'selection': f'exhibition {name}: {sheet_key} on hand-picked cells, no battery',
    'selectionSeed': 1,
    'outcomeBlind': False,
    'identitiesBlind': False,
    'populationSize': len(cells),
    'replays': [],
}
cards = []
for i, cell in enumerate(cells, 1):
    run = json.load(open(f'{outdir}/{name}-{cell}/run.json'))
    team = next(p['TeamId'] for p in run['Participants']
                if sheet_key in (p.get('SheetPath') or ''))
    opponent = next(
        os.path.basename(p.get('SheetPath') or '?').removesuffix('.json')
        for p in run['Participants'] if p['TeamId'] != team)
    win = run['Result'].get('WinnerTeamId') == team
    end = run['Result'].get('EndTick', '?')
    audit = subprocess.run(
        ['python3', f'{repo}/scripts/arc-relay-unit-audit.py',
         f'{outdir}/{name}-{cell}/replay.json.gz', '--team', str(team)],
        capture_output=True, text=True).stdout
    row = next((line.strip() for line in audit.splitlines()
                if f' u0 ' in line), '')
    manifest['replays'].append({
        'id': f'sample-{i:02d}',
        'source': f'{bcast}/{cell}.json.gz',
        'replayVersion': 3,
        'rules': 'arc-relay-ambush-11',
        'map': 'arc-relay-ambush-warren-06',
        'matchSeed': cell.split('-')[1],
        'participants': [sheet_key, opponent],
    })
    cards.append({
        'id': f'sample-{i:02d}',
        'title': f'{cell} — {"win" if win else "loss"} at t{end}',
        'win': win,
        'opponent': opponent,
        'score': row or f'ended t{end}',
        'watch': 'unit 0 of the sheet under exhibition',
    })
open(f'{outdir}/{name}-sample.json', 'w').write(json.dumps(manifest, indent=1))
cards_path = os.environ.get('EXHIBIT_CARDS')
if not cards_path:
    cards_path = f'{outdir}/{name}-cards.json'
    open(cards_path, 'w').write(json.dumps(
        {'title': os.environ.get('EXHIBIT_TITLE', name), 'cards': cards},
        indent=1))
print(f'cards: {cards_path}')
EOF
CARDS=${EXHIBIT_CARDS:-$OUTDIR/$NAME-cards.json}
python3 "$REPO/scripts/build-review-gallery.py" \
  --sample "$OUTDIR/$NAME-sample.json" \
  --output "$OUTDIR/$NAME-site" \
  --title "${EXHIBIT_TITLE:-$NAME}" \
  --index-cards "$CARDS" \
  --skip-arc-relay-eligibility
rsync -a --delete "$OUTDIR/$NAME-site/" "$SITE/"
echo "live: swapped into $SITE"
