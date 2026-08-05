#!/usr/bin/env python3
"""Pre-registered Ripening Cores metrics audit.

Implements the evaluation measures registered in
docs/briefs/RIPENING-CORES-PROTOTYPE-BRIEF.md before any evaluation game
was analyzed: banked charge-source mix, age-at-pickup distribution,
denial-pickup count, lead reversals, and the pulse list. Reads only
replay-v3 facts (core-born / core-ripened / core-picked-up / core-banked /
pulse); it never reads mind internals.

Usage: python3 scripts/arc-relay-ripening-metrics.py <gamedir> [gamedir ...]
       (each gamedir is an `experiment arc-relay --out` directory holding
        replay.json.gz and run.json)
Add --json for full per-game detail on stdout.
"""
import collections
import gzip
import json
import pathlib
import sys


def arc_facts(tick_frame):
    """Yields arc-relay facts in phase order: tick start, then resolution."""
    for source in (tick_frame['tickStart'].get('events') or [],
                   tick_frame.get('events') or []):
        for event in source:
            if event.get('kind') == 'arc-relay':
                yield event['payload']['fact']


def core_key(fact):
    core_id = fact.get('coreId')
    if core_id is None:
        return None
    return (core_id['sourceWellId'], core_id['sourceOrdinal'])


def analyze(path):
    path = pathlib.Path(path)
    replay = json.load(gzip.open(path / 'replay.json.gz'))
    run = json.load(open(path / 'run.json'))['Result']

    born_tick = {}
    value = {}          # live value per core, updated by facts only
    banked = []         # (tick, team, origin, value, ageAtBank)
    pickups = []        # (tick, team, key, value, ageAtPickup)
    ripens = []         # (tick, key, newValue)
    pulses = []         # (tick, team)
    for frame in replay['ticks']:
        tick = frame['tick']
        for fact in arc_facts(frame):
            kind = fact.get('kind')
            key = core_key(fact)
            if kind == 'core-born':
                born_tick[key] = tick
                value[key] = fact.get('chargeValue', 1)
            elif kind == 'core-ripened':
                value[key] = fact['value']
                ripens.append((tick, key, fact['value']))
            elif kind == 'core-picked-up':
                pickups.append((tick, fact['carrierActorId']['teamId'], key,
                                value.get(key, 1),
                                tick - born_tick.get(key, tick)))
            elif kind == 'core-banked':
                banked.append((tick, fact['teamId'], key[0],
                               value.pop(key, 1),
                               tick - born_tick.get(key, tick)))
            elif kind == 'pulse':
                pulses.append((tick, fact['teamId']))

    # Behind-to-ahead reversal: the pulse-count leader changes after both
    # teams have scored.
    reversals = 0
    leader = None
    counts = collections.defaultdict(int)
    for _, team in pulses:
        counts[team] += 1
        top = max(counts, key=lambda k: (counts[k], -k))
        if (leader is not None and top != leader
                and counts[leader] > 0 and counts[top] > counts[leader]):
            reversals += 1
        leader = max(counts, key=lambda k: counts[k])

    ages = sorted(age for _, _, _, _, age in pickups)
    base = min((v for _, _, _, v, _ in banked), default=1)
    denial = sum(1 for _, _, _, v, age in pickups if v == base and age < 40)
    return {
        'winner': run['WinnerTeamId'],
        'end': run['EndTick'],
        'pulses': pulses,
        'bankedMix': dict(collections.Counter(v for _, _, _, v, _ in banked)),
        'ageMedian': ages[len(ages) // 2] if ages else None,
        'ages': ages,
        'ripens': ripens,
        'denialEarlyPickups': denial,
        'reversals': reversals,
        'banked': banked,
    }


def main():
    args = [a for a in sys.argv[1:] if a != '--json']
    as_json = '--json' in sys.argv[1:]
    if not args:
        sys.exit(__doc__.strip())
    for path in args:
        result = analyze(path)
        if as_json:
            print(json.dumps({path: result}))
        else:
            print(f"{path}: win t{result['winner']} @{result['end']} | "
                  f"mix {result['bankedMix']} | "
                  f"age med {result['ageMedian']} | "
                  f"ripens {len(result['ripens'])} | "
                  f"reversals {result['reversals']}")


if __name__ == '__main__':
    main()
