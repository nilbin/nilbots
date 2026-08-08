#!/usr/bin/env python3
"""Pre-registered Threefold contribution audit.

Implements docs/briefs/THREEFOLD-CONTRIBUTION-MEASURE.md: per completed
Pulse cycle, the distinct own-team unit slots with >=1 qualifying
predicate (CARRY, BANK, PICKUP-CONTEST, COMBAT-IN-CONTEST,
CARRIER-SUPPORT, DENIAL-HOLD). Reads only replay facts.
"""
import json, gzip, sys, collections, pathlib

CONTEST_R = 3
CONTEST_W = 8

def audit(path):
    replay = json.load(gzip.open(pathlib.Path(path) / 'replay.json.gz'))
    ticks = replay['ticks']

    # --- per-tick indices ---
    pos = {}          # tick -> {(team,unit): (x,y)}
    filled = {}       # tick -> {team: set(origins)}
    carrying = {}     # tick -> {(team,unit): origin}
    events = collections.defaultdict(list)
    for t in ticks:
        n = t['tick']
        st = t['postState']
        pos[n] = {}
        for l in st['activeLives']:
            aid = l['actorId']
            pos[n][(aid['teamId'], aid['unitId'])] = (
                l['position']['x'], l['position']['y'])
        filled[n] = {r['teamId']: set(r.get('filledSocketWellIds') or [])
                     for r in st['mode']['reactors']}
        carrying[n] = {}
        for c in st['mode']['visibleCores']:
            if c.get('carrierActorId'):
                a = c['carrierActorId']
                carrying[n][(a['teamId'], a['unitId'])] = \
                    c['coreId']['sourceWellId']
        for ev in t.get('events', []):
            events[n].append(ev)

    all_ticks = sorted(pos)
    pulses = collections.defaultdict(list)
    for n in all_ticks:
        for ev in events[n]:
            if ev.get('kind') == 'arc-relay' \
                    and ev['payload']['fact'].get('kind') == 'pulse':
                pulses[ev['payload']['fact']['teamId']].append(n)

    def required(team, origin, tick):
        return origin not in filled.get(tick, {}).get(team, set())

    # core events (for contest windows): (tick, origin, position)
    core_events = []
    for n in all_ticks:
        for ev in events[n]:
            if ev.get('kind') == 'arc-relay':
                f = ev['payload']['fact']
                if f.get('kind') in ('core-picked-up', 'core-dropped',
                                     'core-banked'):
                    p = f.get('position') or {}
                    core_events.append((n, f['coreId']['sourceWellId'],
                                        (p.get('x'), p.get('y'))))
            elif ev.get('kind') == 'destruction':
                p = ev['payload']
                a = p['actorId']
                if (a['teamId'], a['unitId']) in carrying.get(n - 1, {}):
                    origin = carrying[n - 1][(a['teamId'], a['unitId'])]
                    core_events.append((n, origin,
                                        (p['position']['x'],
                                         p['position']['y'])))

    def in_contest(team, unit, tick):
        p = pos.get(tick, {}).get((team, unit))
        if p is None:
            return False
        for (etick, origin, ep) in core_events:
            if abs(etick - tick) > CONTEST_W or ep[0] is None:
                continue
            if not (required(0, origin, etick) or required(1, origin, etick)):
                continue
            if max(abs(p[0] - ep[0]), abs(p[1] - ep[1])) <= CONTEST_R:
                return True
        return False

    out = []
    for team, plist in sorted(pulses.items()):
        prev = 0
        for ci, ptick in enumerate(plist):
            lo, hi = prev + 1, ptick
            prev = ptick
            contributors = collections.defaultdict(set)

            carry_ticks = collections.Counter()
            for n in range(lo, hi + 1):
                for (tm, u), origin in carrying.get(n, {}).items():
                    if tm != team:
                        continue
                    if required(team, origin, n):
                        carry_ticks[u] += 1
                    elif required(1 - team, origin, n):
                        carry_ticks[(u, 'denial')] += 1
            for key, ct in carry_ticks.items():
                if isinstance(key, tuple):
                    if ct >= 20:
                        contributors[key[0]].add('DENIAL-HOLD')
                elif ct >= 8:
                    contributors[key].add('CARRY')

            for n in range(lo, hi + 1):
                for ev in events[n]:
                    k = ev.get('kind')
                    if k == 'arc-relay':
                        f = ev['payload']['fact']
                        fk = f.get('kind')
                        if fk == 'core-banked' and f['teamId'] == team:
                            contributors[f['carrierActorId']['unitId']] \
                                .add('BANK')
                        if fk == 'core-picked-up':
                            a = f['carrierActorId']
                            if a['teamId'] == team and required(
                                    team, f['coreId']['sourceWellId'], n):
                                contributors[a['unitId']] \
                                    .add('PICKUP-CONTEST')
                    elif k == 'destruction':
                        p = ev['payload']
                        src = p.get('sourceActorId')
                        victim = p['actorId']
                        if src and src['teamId'] == team \
                                and (victim['teamId'], victim['unitId']) \
                                    in carrying.get(n - 1, {}):
                            contributors[src['unitId']] \
                                .add('PICKUP-CONTEST')
                    elif k == 'damage':
                        p = ev['payload']
                        for side in ('actorId', 'sourceActorId'):
                            a = p.get(side)
                            if a and a['teamId'] == team and in_contest(
                                    team, a['unitId'], n):
                                contributors[a['unitId']] \
                                    .add('COMBAT-IN-CONTEST')
            # CARRIER-SUPPORT: damage interactions within 3 of an own
            # required-core carrier
            for n in range(lo, hi + 1):
                carriers = [(u, pos.get(n, {}).get((team, u)))
                            for (tm, u), o in carrying.get(n, {}).items()
                            if tm == team and required(team, o, n)]
                if not carriers:
                    continue
                for ev in events[n]:
                    if ev.get('kind') != 'damage':
                        continue
                    p = ev['payload']
                    for side in ('actorId', 'sourceActorId'):
                        a = p.get(side)
                        if not a or a['teamId'] != team:
                            continue
                        ap = pos.get(n, {}).get((team, a['unitId']))
                        if ap is None:
                            continue
                        for cu, cp in carriers:
                            if cu == a['unitId'] or cp is None:
                                continue
                            if max(abs(ap[0] - cp[0]),
                                   abs(ap[1] - cp[1])) <= CONTEST_R:
                                contributors[a['unitId']] \
                                    .add('CARRIER-SUPPORT')
            out.append({
                'team': team, 'cycle': ci, 'pulseTick': ptick,
                'participants': {
                    str(u): sorted(preds)
                    for u, preds in sorted(contributors.items())},
                'count': len(contributors),
            })
    return out

for path in sys.argv[1:]:
    rows = audit(path)
    counts = [r['count'] for r in rows]
    print(f"{path}: cycles {len(rows)}, participant counts {counts}")
    for r in rows:
        print(f"  t{r['team']} c{r['cycle']} @{r['pulseTick']}: "
              f"{r['count']}/8 {r['participants']}")
