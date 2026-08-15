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
# Standard review set (keep this order):
#   w-9001 w-9002 e-9005 e-9009 w-9012 e-9004
#
# Example:
#   BATTERY_OUT=/tmp/arc-batteries \
#   EXHIBIT_TITLE="My change, described" \
#   scripts/arc-relay-exhibit.sh mychange1 \
#     arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/hunter-v1.json \
#     arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/wellwright-v1.json \
#     /tmp/arc-batteries/site \
#     w-9001 w-9002 e-9005 e-9009 w-9012 e-9004
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
#   EXHIBIT_CARDS   optional pre-authored card overlays. The script always
#                   supplies the mechanical win/loss, end tick, opponent and
#                   unit-0 audit line after the run; an overlay may add or
#                   replace explanatory copy without outcome cherry-picking.
#                   Accepted shape:
#                   {"title":"...","cards":[{"id":"sample-01",
#                   "title":"...","win":true,"opponent":"...",
#                   "score":"...","trigger":"...","tactic":"...",
#                   "counterplay":"...","fallback":"...",
#                   "watch":"..."}]}
#   EXHIBIT_MATCH_ARGS
#                   extra flags passed verbatim to every cell's
#                   `experiment arc-relay` invocation, e.g.
#                   "--mind-stepper greedy" for the historical diagnostic
#                   control. Coordinated planning is the product default.
#                   Word-split, so keep
#                   each token space-separated and unquoted.
#   EXHIBIT_LOOP_PROFILE
#                   registered loop profile for every cell (default:
#                   tapline-4). The gallery manifest derives its
#                   actual rules/map ids from each replay, so Drawbench and
#                   future registered arenas remain honest exhibits.
#
# Timing on the reference machine: ~2s per match, ~10s per broadcast
# (parallel), one gallery build ~20s. Six cells land in ~1 minute.
#
# Cache rule: this script deliberately reuses $BATTERY_OUT/botarena-pub, but
# republishes by itself when src/ (engine, CLI, or compiler) is newer than
# the cached binary, so a stale engine cannot silently produce the exhibit.
# Changes under arena-bots/ need no republish: minds compile in-process per
# match.
#
# The same rule applies to the VIEWER, and it is easier to miss: the gallery
# serves `web/dist`, which is gitignored and only ever copied, never built
# here. After changing web/src, run `npm run build` in web/ or the gallery
# ships the old viewer and your change looks like it was never made. The
# gallery builder now refuses a stale bundle rather than serving one.
#
# This is an EXHIBIT, not a battery. Its outcomes describe these six games
# and are not a balance verdict.
#
# It is also NOT a promotion. Mind sources compile in-process on purpose. Do
# not rebuild tracked WASM, mint a frozen stock/playlist version, reset ratings,
# bump release versions, or run a release gauntlet just to produce this gallery.
# Those belong to a separately owner-authorized promotion gate.
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
LOOP_PROFILE=${EXHIBIT_LOOP_PROFILE:-tapline-4}
mkdir -p "$OUTDIR"

# Republish when the cache predates src/, not only when it is absent. The
# reuse is a real saving and worth keeping, but "remove the directory
# yourself after changing src/" is a rule that gets forgotten exactly when
# it matters: an exhibit was handed over showing all six cells grinding to
# max-ticks on an engine five hours behind the fixes under review, which is
# a wrong answer that looks like a finding rather than like a mistake.
# Slow and right beats fast and stale — the check costs one `find`.
if [ ! -x "$PUB/botarena" ]; then
  echo "publishing CLI (first run only)..."
  dotnet publish "$REPO/src/BotArena.Cli" -c Release -o "$PUB" >/dev/null
# `obj/` and `bin/` are pruned because publish writes them after the binary
# it just produced, so an unpruned search always matches and the cache would
# republish every run — a check that never passes is as useless as none.
elif [ -n "$(find "$REPO/src" \( -type d \( -name obj -o -name bin \) -prune \) -o \
             \( -type f -newer "$PUB/botarena" -print -quit \) 2>/dev/null)" ]; then
  echo "src/ is newer than the published binary — republishing..."
  dotnet publish "$REPO/src/BotArena.Cli" -c Release -o "$PUB" >/dev/null
fi

echo "running ${#CELLS[@]} cells..."
read -r -a MATCH_ARGS <<< "${EXHIBIT_MATCH_ARGS:-}"
pids=()
for cell in "${CELLS[@]}"; do
  side=${cell%%-*}; seed=${cell##*-}
  if [ "$side" = w ]; then S0=$SHEET_A; S1=$SHEET_B; else S0=$SHEET_B; S1=$SHEET_A; fi
  OUT=$OUTDIR/$NAME-$cell
  rm -rf "$OUT"
  "$PUB/botarena" experiment arc-relay \
    --bot "$MIND" --opponent "$MIND" --runtime in-process \
    --loop-profile "$LOOP_PROFILE" --seed "$seed" \
    --sheet0 "$S0" --sheet1 "$S1" --out "$OUT" \
    ${MATCH_ARGS[@]+"${MATCH_ARGS[@]}"} \
    > "$OUTDIR/$NAME-$cell.log" 2>&1 &
  pids+=($!)
done
for pid in "${pids[@]}"; do wait "$pid"; done
grep -H "Result:" "$OUTDIR/$NAME"-*.log | sed "s|$OUTDIR/||"

# The replay header, score bug and end card all use provenance participant
# names. A package-local filename such as `orders.json` is not a sheet
# identity and once leaked into an owner gallery with no way to tell which
# side was under review. Pin the authored sheetId end-to-end before doing any
# further exhibit work.
python3 - "$SHEET_A" "$SHEET_B" "$OUTDIR" "$NAME" "$REPO" "${CELLS[@]}" <<'EOF'
import gzip, json, os, sys
sheet_a, sheet_b, outdir, name, repo = sys.argv[1:6]
cells = sys.argv[6:]

def absolute(path):
    return os.path.realpath(path if os.path.isabs(path) else os.path.join(repo, path))

def sheet_id(path):
    with open(absolute(path), encoding='utf-8') as stream:
        value = json.load(stream)
    identity = value.get('sheetId')
    if not isinstance(identity, str) or not identity.strip():
        raise SystemExit(f'{path}: exhibit sheets require a non-empty sheetId')
    return identity

identity_a, identity_b = sheet_id(sheet_a), sheet_id(sheet_b)
for cell in cells:
    expected = ([identity_a, identity_b] if cell.startswith('w-')
                else [identity_b, identity_a])
    replay_path = f'{outdir}/{name}-{cell}/replay.json.gz'
    with gzip.open(replay_path, 'rt', encoding='utf-8') as stream:
        replay = json.load(stream)
    participants = sorted(
        replay['header']['provenance']['participants'],
        key=lambda participant: participant['teamId'])
    actual = [participant['name'] for participant in participants]
    if actual != expected:
        raise SystemExit(
            f'{cell}: replay participant names {actual!r} do not match '
            f'authored sheet identities {expected!r}')
print(f'participant identities: {identity_a} vs {identity_b}')
EOF

# Horizon resolution is a valid product outcome, but several in one tiny
# stock exhibit are a stop-and-trace signal rather than something to hand to
# the owner. An explicitly horizon-focused diagnostic can opt out; its author
# must then explain the cells rather than silently treating them as healthy.
horizon_count=$({ grep -h "Result:.*max-ticks" \
  "$OUTDIR/$NAME"-*.log 2>/dev/null || true; } | wc -l | tr -d ' ')
if [ "$horizon_count" -gt 1 ] \
    && [ "${EXHIBIT_ALLOW_MULTIPLE_HORIZON:-0}" != 1 ]; then
  echo "refusing review gallery: $horizon_count/${#CELLS[@]} cells reached max-ticks" >&2
  echo "trace them first, or set EXHIBIT_ALLOW_MULTIPLE_HORIZON=1 for an explicitly labelled diagnostic" >&2
  exit 1
fi

# A review gallery is allowed to be small; it is not allowed to conceal a
# mechanical failure. This raw-replay gate catches planner/executor seams that
# aggregate traffic rates smooth away. Gallery eligibility below separately
# follows Core possession across handoffs and equal-distance dancing.
echo "checking replay-visible movement sanity..."
for cell in "${CELLS[@]}"; do
  python3 "$REPO/scripts/arc-relay-motion-sanity.py" \
    "$OUTDIR/$NAME-$cell/replay.json.gz" --fail
done

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
import gzip, json, os, subprocess, sys
name, outdir, bcast, sheet_a, repo = sys.argv[1:6]
cells = sys.argv[6:]
sheet_path = os.path.realpath(
    sheet_a if os.path.isabs(sheet_a) else os.path.join(repo, sheet_a))

def sheet_id(path):
    try:
        with open(path, encoding='utf-8') as stream:
            value = json.load(stream)
        return value.get('sheetId') or os.path.basename(path).removesuffix('.json')
    except (OSError, ValueError, TypeError):
        return os.path.basename(path).removesuffix('.json')

sheet_key = sheet_id(sheet_path)

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
    with gzip.open(
            f'{outdir}/{name}-{cell}/replay.json.gz',
            'rt', encoding='utf-8') as raw_stream:
        contract = json.load(raw_stream)['header']['contract']
    entrant = next(
        p for p in run['Participants']
        if os.path.realpath(p.get('SheetPath') or '') == sheet_path)
    team = entrant['TeamId']
    opponent_path = next(
        p.get('SheetPath') or '?'
        for p in run['Participants'] if p['TeamId'] != team)
    opponent = sheet_id(opponent_path)
    winner = run['Result'].get('WinnerTeamId')
    win = winner == team
    outcome = 'win' if win else 'draw' if winner is None else 'loss'
    end = run['Result'].get('EndTick', '?')
    with gzip.open(f'{bcast}/{cell}.json.gz', 'rt', encoding='utf-8') as stream:
        result = json.load(stream)['result']
    standings = {row['teamId']: row for row in result['standings']['teams']}
    reactors = {row['teamId']: row for row in result['mode']['state']['reactors']}
    opponent_team = next(team_id for team_id in standings if team_id != team)
    def channel(team_id, name):
        return next(score['value'] for score in standings[team_id]['scores']
                    if score['channel'] == name)
    scoreline = (
        f"pulses {channel(team, 'pulses')}–{channel(opponent_team, 'pulses')} · "
        f"integrity {reactors[team]['integritySegments']}–"
        f"{reactors[opponent_team]['integritySegments']} · "
        f"charge {reactors[team]['chargePips']}–"
        f"{reactors[opponent_team]['chargePips']} · "
        f"{result['completionReason']} t{end}"
    )
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
        'rules': contract['rules']['rulesetId'],
        'map': contract['map']['mapId'],
        'matchSeed': cell.split('-')[1],
        'participants': [sheet_key, opponent],
    })
    cards.append({
        'id': f'sample-{i:02d}',
        'title': f'{cell} — {outcome} at t{end}',
        'win': win,
        'opponent': opponent,
        'score': scoreline,
        'watch': row or 'unit 0 of the sheet under exhibition',
    })
open(f'{outdir}/{name}-sample.json', 'w').write(json.dumps(manifest, indent=1))
cards_path = f'{outdir}/{name}-cards.json'
provided_value = os.environ.get('EXHIBIT_CARDS')
provided = None
if provided_value:
    stripped = provided_value.lstrip()
    provided = (json.loads(provided_value)
                if stripped.startswith(('{', '['))
                else json.load(open(provided_value)))
    provided_cards = provided if isinstance(provided, list) else provided['cards']
    overlays = {(card.get('id') or card.get('sample')): card
                for card in provided_cards}
    unknown = set(overlays) - {base['id'] for base in cards}
    if unknown:
        raise SystemExit('EXHIBIT_CARDS names unknown samples: '
                         + ', '.join(sorted(unknown)))
    cards = [base | overlays.get(base['id'], {}) for base in cards]
document = {
    'title': (provided.get('title') if isinstance(provided, dict) else None)
             or os.environ.get('EXHIBIT_TITLE', name),
    'cards': cards,
}
if isinstance(provided, dict) and provided.get('intro') is not None:
    document['intro'] = provided['intro']
open(cards_path, 'w').write(json.dumps(document, indent=1))
print(f'cards: {cards_path}')
EOF
CARDS=$OUTDIR/$NAME-cards.json
python3 "$REPO/scripts/build-review-gallery.py" \
  --sample "$OUTDIR/$NAME-sample.json" \
  --output "$OUTDIR/$NAME-site" \
  --title "${EXHIBIT_TITLE:-$NAME}" \
  --index-cards "$CARDS"
rsync -a --delete "$OUTDIR/$NAME-site/" "$SITE/"
echo "live: swapped into $SITE"
