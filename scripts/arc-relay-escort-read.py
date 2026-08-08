#!/usr/bin/env python3
"""Escort friction read: is the escort helping or standing in the doorway?

Escorted orders (DECISIONS #227) bind a leader to followers that take
their ground from a posture function rather than a formation slot. The
failure this exists to catch is the one that produced the ruling: an
escort parked on the tile its leader wants next, so a reversal in a
corridor costs the leader the corridor.

For one team's leader/follower pair, over the ticks the two share an order:
  same-order ticks         both bodies commanded under one order id
  backstep occupied        the escort stood on the leader's BACKSTEP tile
                           (the tile directly behind its last heading) -
                           the leader's reversal path
  refused steps            the leader commanded a move and did not end up
                           anywhere else - the cost, in ticks
  leader stalled           leader did not change tile (fighting counts, so
                           read it beside `refused steps`, not instead)
  stalled + escort near    ... with the escort within 2 tiles
  worst stall              longest run of stalled ticks while escorted

`--window=T` prints the tick-by-tick pair trace around T, which is what a
reversal trace in a report is made of.

Usage: arc-relay-escort-read.py REPLAY.json.gz TEAM [LEADER] [FOLLOWER]
                                [--window=T]
"""
import gzip, json, os, sys

args = [a for a in sys.argv[1:] if not a.startswith('--')]
path, team = args[0], int(args[1])
leader = int(args[2]) if len(args) > 2 else 0
follower = int(args[3]) if len(args) > 3 else 1
window = next((int(a.split('=')[1]) for a in sys.argv[1:]
               if a.startswith('--window=')), None)

r = json.load(gzip.open(path, 'rt') if path.endswith('.gz') else open(path))
rows = []
for tk in r['ticks']:
    lives = tk.get('tickStart', {}).get('state', {}).get('activeLives', []) or []
    def pos(u):
        b = next((l for l in lives if l['actorId']['teamId'] == team
                  and l['actorId']['unitId'] == u), None)
        return (b['position']['x'], b['position']['y']) if b else None
    reasons = {}
    for turn in (tk.get('mindTurns') or []):
        if turn['teamId'] != team:
            continue
        for cmd in (turn.get('commands') or []):
            reasons[cmd['unitId']] = cmd.get('debugMessage') or ''
    rows.append([tk['tick'], pos(leader), pos(follower),
                 reasons.get(leader, ''), reasons.get(follower, '')])

def order(reason):
    return reason.split(':')[3] if reason.startswith('tp:') else ''

def chan(reason):
    return reason.split(':')[-1] if reason.startswith('tp:') else reason

def cheb(a, b):
    return max(abs(a[0] - b[0]), abs(a[1] - b[1]))

same = backstep = stalled = stalled_near = refused = 0
run = worst = 0
heading = None
for i, (tick, lp, fp, lr, fr) in enumerate(rows):
    prev = rows[i - 1][1] if i else None
    if lp and prev and lp != prev:
        heading = (lp[0] - prev[0], lp[1] - prev[1])
    if not (lp and fp) or not order(lr) or order(lr) != order(fr):
        run = 0
        continue
    same += 1
    if heading and fp == (lp[0] - heading[0], lp[1] - heading[1]):
        backstep += 1
    nxt = rows[i + 1][1] if i + 1 < len(rows) else None
    if ' via ' in lr and nxt == lp:
        refused += 1
    if prev and lp == prev:
        stalled += 1
        run += 1
        worst = max(worst, run)
        if cheb(lp, fp) <= 2:
            stalled_near += 1
    else:
        run = 0

print(f'{os.path.basename(os.path.dirname(os.path.abspath(path)))}  '
      f'team {team}  u{leader}/u{follower}')
print(f'  same-order ticks {same}   backstep occupied {backstep}'
      f'   refused steps {refused}'
      f'   leader stalled {stalled} (escort near {stalled_near})'
      f'   worst stall run {worst}')
if window is not None:
    for tick, lp, fp, lr, fr in rows:
        if abs(tick - window) <= 10:
            print(f'  t{tick:<4} ghost {str(lp):<10} {chan(lr):<26} '
                  f'| escort {str(fp):<10} {chan(fr)}')
