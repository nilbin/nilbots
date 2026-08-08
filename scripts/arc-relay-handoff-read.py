#!/usr/bin/env python3
"""Hand-off cost read: what every custody transfer cost the passer.

An episode is a run of `custody:transfer-approach*` ticks ending in a
`custody:transfer-drop`. Each line reports the passer, the ticks it spent
approaching, and how far it actually walked - which IS the cost the
receiver rule is now chosen to cut. Owner ruling 2026-08-10 (DECISIONS
#238) replaced the combined-cost rule of #228 with "the closest ally that
is towards the own base": nearest to the PASSER among the bodies strictly
nearer home. So a long approach is no longer a trade the rule accepted -
it is the rule failing, and worth reading.

Usage: arc-relay-handoff-read.py REPLAY.json.gz TEAM
"""
import gzip, json, sys

path, team = sys.argv[1], int(sys.argv[2])
r = json.load(gzip.open(path, 'rt') if path.endswith('.gz') else open(path))
rows = []
for tk in r['ticks']:
    lives = tk.get('tickStart', {}).get('state', {}).get('activeLives', []) or []
    pos = {l['actorId']['unitId']: (l['position']['x'], l['position']['y'])
           for l in lives if l['actorId']['teamId'] == team}
    cmds = {}
    for turn in (tk.get('mindTurns') or []):
        if turn['teamId'] != team:
            continue
        for cmd in (turn.get('commands') or []):
            cmds[cmd['unitId']] = cmd.get('debugMessage') or ''
    rows.append((tk['tick'], pos, cmds))

open_runs, done = {}, []
for tick, pos, cmds in rows:
    for unit, reason in cmds.items():
        if 'custody:transfer-approach' in reason:
            open_runs.setdefault(unit, (tick, pos.get(unit)))
        elif 'custody:transfer-drop' in reason and unit in open_runs:
            start, at = open_runs.pop(unit)
            done.append((unit, start, tick, at, pos.get(unit)))
        elif unit in open_runs and 'custody:transfer' not in reason:
            open_runs.pop(unit)

def cheb(a, b):
    return max(abs(a[0] - b[0]), abs(a[1] - b[1])) if a and b else -1

print(f'{path.split("/")[-2]}  team {team}: {len(done)} completed hand-offs')
for unit, start, end, at, drop in done:
    print(f'  u{unit} t{start}-t{end} ({end - start:2d} ticks) '
          f'{at} -> {drop}, walked {cheb(at, drop)}')
if done:
    ticks = [e - s for _, s, e, _, _ in done]
    print(f'  approach ticks: total {sum(ticks)}  worst {max(ticks)}  '
          f'mean {sum(ticks) / len(ticks):.1f}')
