#!/usr/bin/env python3
"""Traffic read for Arc Relay replays: who got in whose way.

Movement quality is invisible in a scoreline. A mind can win while its
bodies grind against each other in every corridor, and the only honest
evidence is the engine's own refusal: a movement action the mind
submitted, the engine accepted, and then resolved `blocked`.

Attribution matters more than the count. A blocked step is almost never
a body walking into a wall - the mind checks terrain before it commands
- it is two bodies reaching for the SAME tile on the same tick, and the
engine refusing both. So each blockade is attributed by looking at
every movement destination submitted that tick:

  own-contest    a teammate reached for the same tile - the only kind a
                 cooperative stepper can actually remove
  enemy-contest  an opposing body reached for it; nobody's pathing bug
  occupied       the tile already held a live body at tick start
  other          neither - a reservation, a pad, a mid-tick arrival

Plus the two shapes that read as bad pathing on screen:

  chokes   runs of >= 3 consecutive ticks where a body commanded a move
           and did not end up anywhere else - stuck, not stepping
  cycles   A->B->A position loops inside 4 ticks, the signature of two
           bodies trading one tile back and forth

Everything is per SCORING TEAM, because a mirror cell runs one mind on
both sides and a total would hide which side did the grinding.

Usage:
  arc-relay-traffic-read.py REPLAY.json[.gz] [more...]
  arc-relay-traffic-read.py --team 0 REPLAY.json.gz
"""
import argparse
import gzip
import json
from collections import defaultdict
from pathlib import Path

STALL_RUN = 3
CYCLE_WINDOW = 4

HEADINGS = {
    'north': (0, -1), 'north-east': (1, -1), 'east': (1, 0),
    'south-east': (1, 1), 'south': (0, 1), 'south-west': (-1, 1),
    'west': (-1, 0), 'north-west': (-1, -1),
}


def load(path: Path):
    opener = gzip.open if path.suffix == '.gz' else open
    with opener(path, 'rt', encoding='utf-8') as handle:
        return json.load(handle)


def movement_actions(replay) -> set:
    return {
        action['id']
        for action in replay['header']['contract']['rules']['actions']
        if action.get('kind') == 'movement'
    }


def wanted_tile(previous_position, action):
    for argument in action.get('arguments') or []:
        vector = HEADINGS.get(argument.get('value'))
        if vector and previous_position:
            return (previous_position['x'] + vector[0],
                    previous_position['y'] + vector[1])
    return None


def scan(replay, moves):
    """Per tick: submitted movement destinations and the resolved outcome.

    A body's resolution for tick T arrives in its observation at T+1, so
    both are keyed back to the tick the move was actually attempted.
    """
    attempts = defaultdict(list)
    occupied = {}
    for tick in replay['ticks']:
        state = tick.get('tickStart', {}).get('state', {})
        occupied[tick['tick']] = {
            (life['position']['x'], life['position']['y'])
            for life in state.get('activeLives', []) or []
        }
        for turn in tick.get('mindTurns') or []:
            for body in turn['observation']['bodies']:
                resolution = body.get('previousActionResolution')
                if not resolution:
                    continue
                accepted = resolution.get('acceptedAction') or {}
                if accepted.get('actionId') not in moves:
                    continue
                target = wanted_tile(body.get('previousPosition'), accepted)
                if target is None:
                    continue
                attempts[tick['tick'] - 1].append((
                    turn['teamId'],
                    body['actorId']['unitId'],
                    target,
                    resolution.get('outcome') == 'success'))
    return attempts, occupied


def read(path: Path, only_team):
    replay = load(path)
    moves = movement_actions(replay)
    attempts, occupied = scan(replay, moves)
    stats = defaultdict(lambda: defaultdict(int))

    for tick, rows in attempts.items():
        for team, _unit, target, success in rows:
            if only_team is not None and team != only_team:
                continue
            stats[team]['moves'] += 1
            if success:
                continue
            stats[team]['blockades'] += 1
            rivals = {
                other for other, _u, tile, _s in rows
                if tile == target and (other, _u) != (team, _unit)
            }
            if team in rivals:
                stats[team]['own-contest'] += 1
            elif rivals:
                stats[team]['enemy-contest'] += 1
            elif target in occupied.get(tick, set()):
                stats[team]['occupied'] += 1
            else:
                stats[team]['other'] += 1

    history = defaultdict(list)
    for tick in replay['ticks']:
        for turn in tick.get('mindTurns') or []:
            if only_team is not None and turn['teamId'] != only_team:
                continue
            for body in turn['observation']['bodies']:
                accepted = (body.get('previousActionResolution')
                            or {}).get('acceptedAction') or {}
                history[(turn['teamId'], body['actorId']['unitId'])].append((
                    (body['position']['x'], body['position']['y']),
                    accepted.get('actionId') in moves))

    for (team, _unit), frames in history.items():
        run = 0
        for index, (position, commanded) in enumerate(frames):
            if index and commanded and position == frames[index - 1][0]:
                run += 1
                if run == STALL_RUN:
                    stats[team]['chokes'] += 1
            else:
                run = 0
        for index in range(len(frames)):
            for back in range(2, CYCLE_WINDOW + 1):
                if index - back < 0:
                    break
                if (frames[index][0] == frames[index - back][0]
                        and frames[index][0] != frames[index - 1][0]):
                    stats[team]['cycles'] += 1
                    break
    return stats


def line(label, row):
    share = 100.0 * row['blockades'] / row['moves'] if row['moves'] else 0.0
    return (
        f'{label}: moves {row["moves"]} '
        f'blockades {row["blockades"]} ({share:.2f}%) '
        f'[own-contest {row["own-contest"]}, '
        f'enemy-contest {row["enemy-contest"]}, '
        f'occupied {row["occupied"]}, other {row["other"]}] '
        f'chokes {row["chokes"]} cycles {row["cycles"]}')


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('replays', nargs='+')
    parser.add_argument('--team', type=int, default=None)
    args = parser.parse_args()

    totals = defaultdict(lambda: defaultdict(int))
    for name in args.replays:
        path = Path(name)
        stats = read(path, args.team)
        for team in sorted(stats):
            print(line(f'{path.parent.name or path.name} t{team}', stats[team]))
            for key, value in stats[team].items():
                totals[team][key] += value

    if len(args.replays) > 1:
        print()
        for team in sorted(totals):
            print(line(f'TOTAL t{team}', totals[team]))


if __name__ == '__main__':
    main()
