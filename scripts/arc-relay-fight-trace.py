#!/usr/bin/env python3
"""Why did this unit stop fighting? - fight-interruption trace.

For one unit of one team in a raw replay: the compressed command-reason
timeline, and every FIGHT INTERRUPTION - a tick where the unit stops
acting on a fight (focus / close-on-focus / flank-approach / duel-stand)
while an enemy is still within --near tiles. Each interruption is
labeled with the reason that won the tick instead; when the unit simply
lost its focus assignment (nothing fight-shaped follows), the label is
SILENT - that is a lock release or an allocation filter (holdFire,
isolation, commit gate, attacker cap, returning-to-formation), which
never emit reasons. See docs/EXECUTOR-SILENT-POLICY.md for the catalog.

Born from the owner's 2026-08-08 catches: "AGAIN it disengaged" traced
in minutes once the reasons were laid on a timeline - this promotes
that scratch analysis to a standing tool.

Usage: arc-relay-fight-trace.py REPLAY.json.gz --team N [--unit U]
       [--near 4] [--timeline]
"""
from __future__ import annotations

import argparse
import gzip
import json
import re
from pathlib import Path

FIGHT = ('focus', 'close-on-focus', 'flank-approach', 'duel-stand',
         'prepare focus')


def read_replay(path: Path) -> dict:
    if path.suffix == '.gz':
        with gzip.open(path, 'rt', encoding='utf-8') as handle:
            return json.load(handle)
    return json.loads(path.read_text(encoding='utf-8'))


def short(reason: str) -> str:
    tail = reason.split(':')[-1] if reason.startswith('tp:') else reason
    return re.sub(r' via .*$', '', tail).strip()


def trace(replay: dict, team: int, unit: int, near_limit: int,
          show_timeline: bool) -> None:
    rows = []
    for tick in replay['ticks']:
        state = tick.get('tickStart', {}).get('state', {})
        lives = state.get('activeLives', []) or []
        body = next((l for l in lives
                     if l['actorId']['teamId'] == team
                     and l['actorId']['unitId'] == unit), None)
        reason = ''
        decline = ''
        for turn in tick.get('mindTurns', []) or []:
            if turn['teamId'] != team:
                continue
            # The allocation plane's voice (see EXECUTOR-SILENT-POLICY.md):
            # which filter dropped this body's best candidate this tick.
            found = re.search(r'declines=([^;]*);',
                              turn.get('debugMessage') or '')
            if found:
                for entry in found.group(1).split(','):
                    if entry.startswith(f'{unit}:'):
                        decline = entry.split(':', 1)[1]
            for cmd in turn.get('commands', []) or []:
                if cmd['unitId'] == unit:
                    reason = cmd.get('debugMessage') or ''
        if body is None:
            rows.append((tick['tick'], None, 99, reason, decline))
            continue
        pos = (body['position']['x'], body['position']['y'])
        near = min((max(abs(l['position']['x'] - pos[0]),
                        abs(l['position']['y'] - pos[1]))
                    for l in lives if l['actorId']['teamId'] != team),
                   default=99)
        rows.append((tick['tick'], pos, near, reason, decline))

    if show_timeline:
        run_start, run_reason = None, None
        for tick_number, _, near, reason, _decline in rows:
            label = short(reason) or '(no command)'
            if label != run_reason:
                if run_reason is not None:
                    print(f'  t{run_start}-t{previous}: {run_reason}')
                run_start, run_reason = tick_number, label
            previous = tick_number
        if run_reason is not None:
            print(f'  t{run_start}-t{previous}: {run_reason}')

    interruptions = []
    for index in range(1, len(rows)):
        _, _, _, prev_reason, _ = rows[index - 1]
        tick_number, _, near, reason, decline = rows[index]
        was = any(k in prev_reason for k in FIGHT)
        now = any(k in reason for k in FIGHT)
        if was and not now and near <= near_limit:
            label = short(reason) if reason else \
                'SILENT (lock release / allocation filter)'
            interruptions.append((tick_number, near, label, decline))
    print(f'\n{len(interruptions)} fight interruptions '
          f'(enemy <= {near_limit} tiles):')
    for tick_number, near, label, decline in interruptions:
        why = f'   [allocation: {decline}]' if decline else ''
        print(f'  t{tick_number} (enemy at {near}): {label}{why}')


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('replay', type=Path)
    parser.add_argument('--team', type=int, required=True)
    parser.add_argument('--unit', type=int, default=0)
    parser.add_argument('--near', type=int, default=4)
    parser.add_argument('--timeline', action='store_true')
    args = parser.parse_args()
    trace(read_replay(args.replay), args.team, args.unit, args.near,
          args.timeline)


if __name__ == '__main__':
    main()
