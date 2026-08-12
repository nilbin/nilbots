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

Plus the shapes that read as bad pathing on screen:

  chokes   runs of >= 3 consecutive ticks where a body commanded a move
           and did not end up anywhere else - stuck, not stepping
  cycles   A->B->A position loops inside 4 ticks, the signature of two
           bodies trading one tile back and forth

  flaps    the tight version of a cycle, and the one the movement layer
           can act on: the body stands where it stood two ticks ago and
           somewhere else last tick (A->B->A, both legs single steps).
           Reported as a share of BODY-TICKS, so a long match and a
           short one compare.
  reverse  axis reversals: two consecutive single-step displacements
           whose dot product is negative - the body took back ground it
           had just walked. Strictly wider than a flap (a diagonal
           A->B->C that doubles back counts), and the aggregate the
           dance shows up in first. Also a share of body-ticks.

  long-flaps
           maximal A-B-A-B... runs of at least five observed frames.
           Unlike `chokes`, these moves all SUCCEEDED; this is the detector
           for a body visibly dancing while still denying a corridor. The
           worst run is reported in frames so an 18-frame loop cannot hide
           behind a zero blocked-move count.

  squad-loops
           three nearby same-life bodies returning to the exact same assigned
           positions at a constant 2-12 tick period, at least twice, while
           make-way commands occur in both laps. This catches the subtler
           successful-but-circular failure: several bodies rotate through an
           opening, so no individual is stationary and every engine move may
           succeed, but the squad makes zero net progress.

Both dance measures skip any pair of frames that is not a legal single
step, so a respawn teleport is never counted as movement.

Everything is per SCORING TEAM, because a mirror cell runs one mind on
both sides and a total would hide which side did the grinding.

Usage:
  arc-relay-traffic-read.py REPLAY.json[.gz] [more...]
  arc-relay-traffic-read.py --team 0 REPLAY.json.gz
"""
import argparse
import gzip
import itertools
import json
from collections import defaultdict
from pathlib import Path

STALL_RUN = 3
CYCLE_WINDOW = 4
SQUAD_LOOP_MAX_PERIOD = 12
SQUAD_LOOP_RADIUS = 2
SQUAD_LOOP_MIN_FRAMES = 13

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


def squad_loop_episodes(frames):
    """Return sustained local three-body make-way circuits.

    Exact actor lives and assigned positions form the state. Requiring three
    occurrences at one constant period separates a circuit from an ordinary
    one-off rejoin; requiring make-way in both laps ties the finding to allied
    traffic arbitration. Nearby triples keep an unrelated patrol elsewhere on
    the map from manufacturing a whole-team signature.
    """
    occurrences = defaultdict(list)
    episodes = {}
    for index, frame in enumerate(frames):
        bodies = sorted(frame['bodies'].items())
        for group in itertools.combinations(bodies, 3):
            members = tuple(actor for actor, _position in group)
            positions = tuple(position for _actor, position in group)
            if max(
                    max(abs(first[0] - second[0]),
                        abs(first[1] - second[1]))
                    for first, second in itertools.combinations(positions, 2)
            ) > SQUAD_LOOP_RADIUS:
                continue
            signature = (members, positions)
            visits = occurrences[signature]
            visits.append(index)
            if len(visits) < 3:
                continue
            first, middle, last = visits[-3:]
            period = frames[middle]['tick'] - frames[first]['tick']
            if (period < 2 or period > SQUAD_LOOP_MAX_PERIOD
                    or frames[last]['tick'] - frames[middle]['tick'] != period):
                continue
            unit_ids = {actor[0] for actor in members}
            first_lap_made_way = any(
                unit_ids & frames[cursor]['makeway']
                for cursor in range(first + 1, middle + 1))
            second_lap_made_way = any(
                unit_ids & frames[cursor]['makeway']
                for cursor in range(middle + 1, last + 1))
            if not first_lap_made_way or not second_lap_made_way:
                continue

            key = (members, period)
            start_tick = frames[first]['tick']
            end_tick = frames[last]['tick']
            prior = episodes.get(key)
            if prior is not None and start_tick <= prior['end'] + period:
                prior['end'] = end_tick
                prior['returns'] += 1
            else:
                episodes[key] = {
                    'members': members,
                    'period': period,
                    'start': start_tick,
                    'end': end_tick,
                    'returns': 2,
                }
    return [
        episode for episode in episodes.values()
        if episode['end'] - episode['start'] + 1 >= SQUAD_LOOP_MIN_FRAMES
    ]


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
    team_frames = defaultdict(list)
    for tick in replay['ticks']:
        for turn in tick.get('mindTurns') or []:
            if only_team is not None and turn['teamId'] != only_team:
                continue
            frame_bodies = {}
            for body in turn['observation']['bodies']:
                actor = (
                    body['actorId']['unitId'],
                    body['actorId']['lifeId'],
                )
                position = (body['position']['x'], body['position']['y'])
                accepted = (body.get('previousActionResolution')
                            or {}).get('acceptedAction') or {}
                history[(turn['teamId'], *actor)].append((
                    tick['tick'],
                    position,
                    accepted.get('actionId') in moves))
                frame_bodies[actor] = position
            team_frames[turn['teamId']].append({
                'tick': tick['tick'],
                'bodies': frame_bodies,
                'makeway': {
                    command['unitId']
                    for command in turn.get('commands') or []
                    if ':make-way:' in (command.get('debugMessage') or '')
                },
            })

    for (team, _unit, _life), frames in history.items():
        run = 0
        for index, (_tick, position, commanded) in enumerate(frames):
            if index and commanded and position == frames[index - 1][1]:
                run += 1
                if run == STALL_RUN:
                    stats[team]['chokes'] += 1
            else:
                run = 0
        for index in range(len(frames)):
            for back in range(2, CYCLE_WINDOW + 1):
                if index - back < 0:
                    break
                if (frames[index][1] == frames[index - back][1]
                        and frames[index][1] != frames[index - 1][1]):
                    stats[team]['cycles'] += 1
                    break
        stats[team]['body-ticks'] += len(frames)
        flap_run_start = None
        for index in range(2, len(frames)):
            tick, here, _commanded = frames[index]
            last_tick, last, _last_commanded = frames[index - 1]
            before_tick, before, _before_commanded = frames[index - 2]
            first = (last[0] - before[0], last[1] - before[1])
            second = (here[0] - last[0], here[1] - last[1])
            legal_pair = (tick == last_tick + 1 == before_tick + 2
                          and max(abs(first[0]), abs(first[1])) <= 1
                          and max(abs(second[0]), abs(second[1])) <= 1)
            flap = legal_pair and here == before and here != last
            if flap:
                stats[team]['flaps'] += 1
                if flap_run_start is None:
                    flap_run_start = index - 2
            elif flap_run_start is not None:
                flap_frames = index - flap_run_start
                if flap_frames >= 5:
                    stats[team]['long-flap-runs'] += 1
                    stats[team]['max-flap-frames'] = max(
                        stats[team]['max-flap-frames'], flap_frames)
                flap_run_start = None
            if not legal_pair:
                continue
            if first == (0, 0) or second == (0, 0):
                continue
            if first[0] * second[0] + first[1] * second[1] < 0:
                stats[team]['reverse'] += 1
        if flap_run_start is not None:
            flap_frames = len(frames) - flap_run_start
            if flap_frames >= 5:
                stats[team]['long-flap-runs'] += 1
                stats[team]['max-flap-frames'] = max(
                    stats[team]['max-flap-frames'], flap_frames)

    for team, frames in team_frames.items():
        episodes = squad_loop_episodes(frames)
        stats[team]['squad-loop-runs'] = len(episodes)
        stats[team]['squad-loop-returns'] = sum(
            episode['returns'] for episode in episodes)
        stats[team]['max-squad-loop-frames'] = max(
            (episode['end'] - episode['start'] + 1 for episode in episodes),
            default=0)
    return stats


def line(label, row):
    share = 100.0 * row['blockades'] / row['moves'] if row['moves'] else 0.0
    ticks = row['body-ticks']
    flap = 100.0 * row['flaps'] / ticks if ticks else 0.0
    reverse = 100.0 * row['reverse'] / ticks if ticks else 0.0
    return (
        f'{label}: moves {row["moves"]} '
        f'blockades {row["blockades"]} ({share:.2f}%) '
        f'[own-contest {row["own-contest"]}, '
        f'enemy-contest {row["enemy-contest"]}, '
        f'occupied {row["occupied"]}, other {row["other"]}] '
        f'chokes {row["chokes"]} cycles {row["cycles"]} '
        f'flaps {row["flaps"]}/{ticks} ({flap:.2f}%) '
        f'reverse {row["reverse"]} ({reverse:.2f}%) '
        f'long-flaps {row["long-flap-runs"]} '
        f'worst {row["max-flap-frames"]}f '
        f'squad-loops {row["squad-loop-runs"]} '
        f'({row["squad-loop-returns"]} returns) '
        f'worst {row["max-squad-loop-frames"]}f')


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
                if key in {'max-flap-frames', 'max-squad-loop-frames'}:
                    totals[team][key] = max(totals[team][key], value)
                else:
                    totals[team][key] += value

    if len(args.replays) > 1:
        print()
        for team in sorted(totals):
            print(line(f'TOTAL t{team}', totals[team]))


if __name__ == '__main__':
    main()
