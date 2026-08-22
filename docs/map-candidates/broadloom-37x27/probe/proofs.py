#!/usr/bin/env python3
"""Deterministic Workshop practice proofs on the current broadloom-2 profile.

Each scenario is a seeded, reproducible match between materialized Tactics
documents. Assertions read replay events and authoritative post-state, never
summaries. Records here are mechanics evidence, not balance verdicts.

    python3 docs/map-candidates/broadloom-37x27/probe/proofs.py run
    python3 docs/map-candidates/broadloom-37x27/probe/proofs.py report
"""
import gzip
import json
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
MIND = REPO / 'arena-bots/arc-relay/tactical-playbook-mind-v1'
BUILD = REPO / 'out/tactics-build'
OUT = REPO / 'out/broadloom-proofs'
RESULTS = Path(__file__).resolve().parent / 'proofs.json'

SHEETS = {
    'trainer': BUILD / 'trainer/materialized-orders.json',
    'trainer-rotate': BUILD / 'trainer-rotate/materialized-orders.json',
    'disruptor': BUILD / 'disruptor/materialized-orders.json',
    'disruptor-east': BUILD / 'disruptor-east/materialized-orders.json',
    'feint-raid': BUILD / 'feint-raid/materialized-orders.json',
    'workshop-guard': BUILD / 'workshop-guard/materialized-orders.json',
    'counter-demolition': BUILD / 'counter-demolition/materialized-orders.json',
    'demolition': BUILD / 'demolition/materialized-orders.json',
    'turtle': BUILD / 'turtle/materialized-orders.json',
    'trade': BUILD / 'trade/materialized-orders.json',
}
SEEDS = [7101, 7102, 7103]
OWN_WELL = {0: 'south', 1: 'north'}

# Authored geometry mirrors 180 degrees for the east seat while workshop ids
# stay global, so contest routes are authored per seat (disruptor vs
# disruptor-east) and each scenario pins which seat runs which document.
# scenario -> (sheet0, sheet1, seeds)
SCENARIOS = {
    'protected-training': ('trainer', 'turtle', SEEDS),
    'disruption': ('trainer', 'disruptor-east', SEEDS),
    'disruption-suppression': ('disruptor', 'trainer', [7101]),
    'feint-raid': ('feint-raid', 'workshop-guard', SEEDS),
    'train-then-rotate': ('trainer-rotate', 'turtle', SEEDS),
    'counter-demolition': ('counter-demolition', 'demolition', SEEDS),
    'counter-demolition-mirrored': ('demolition', 'counter-demolition', SEEDS),
}

# Tracks worth keeping per match: trainees, contest loops, and guard custody.
TRACKED_UNITS = [(0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2), (1, 6), (1, 7)]
NORTH_CONTEST = {
    (18, 2), (14, 3), (15, 3), (16, 3), (18, 3), (19, 3), (20, 3), (21, 3),
    (14, 4), (15, 4), (16, 4), (17, 4), (18, 4), (19, 4), (21, 4), (22, 4),
    (15, 5), (17, 5), (18, 5), (19, 5), (20, 5), (21, 5), (22, 5), (15, 6),
    (16, 6), (17, 6), (18, 6), (19, 6), (20, 6), (21, 6), (22, 6), (16, 7),
    (17, 7), (18, 7), (19, 7), (20, 7),
}


def run_match(sheet0: str, sheet1: str, seed: int) -> Path:
    out = OUT / f'{sheet0}-vs-{sheet1}-s{seed}'
    replay = out / 'replay.json.gz'
    if replay.exists():
        return replay
    subprocess.run(
        ['dotnet', 'run', '--project', str(REPO / 'src/BotArena.Cli'),
         '--no-build', '--', 'experiment', 'arc-relay',
         '--bot', str(MIND), '--opponent', str(MIND),
         '--runtime', 'in-process',
         '--loop-profile', 'broadloom-2', '--seed', str(seed),
         '--sheet0', str(SHEETS[sheet0]), '--sheet1', str(SHEETS[sheet1]),
         '--out', str(out)],
        check=True, cwd=REPO, capture_output=True, text=True)
    return replay


def extract(replay_path: Path) -> dict:
    d = json.load(gzip.open(replay_path))
    workshop_events = []
    level_ups = []
    banks = {}
    destructions = {}
    for tick in d['ticks']:
        for e in tick.get('events', []) or []:
            if e.get('kind') == 'destruction':
                team = (e.get('payload', {}).get('actorId', {}) or
                        e.get('actorId', {}) or {}).get('teamId')
                if team is None:
                    team = e.get('payload', {}).get('teamId', -1)
                destructions[team] = destructions.get(team, 0) + 1
            if e.get('kind') != 'arc-relay':
                continue
            f = e['payload']['fact']
            k = f['kind']
            if k.startswith('workshop'):
                workshop_events.append({'tick': tick['tick'], **f})
            elif 'level' in k:
                level_ups.append({'tick': tick['tick'], **f})
            elif k == 'core-banked':
                team = f['teamId']
                well = f['coreId']['sourceWellId']
                cls = ('centre' if well == 'centre'
                       else 'own' if OWN_WELL[team] == well else 'enemy')
                key = f'{team}:{cls}'
                banks[key] = banks.get(key, 0) + 1
    tracks = {f't{team}u{unit}': [] for team, unit in TRACKED_UNITS}
    for tick in d['ticks']:
        for life in tick['postState']['activeLives']:
            aid = life['actorId']
            key = f"t{aid['teamId']}u{aid['unitId']}"
            if key in tracks:
                p = life['position']
                tracks[key].append((tick['tick'], p['x'], p['y']))
    raw = d['result']
    standings = raw.get('standings', raw) if isinstance(raw, dict) else {}
    return {
        'endTick': d['ticks'][-1]['tick'],
        'completionReason': raw.get('completionReason'),
        'winnerTeamId': standings.get('winnerTeamId'),
        'workshopEvents': workshop_events,
        'levelUps': level_ups,
        'banks': dict(sorted(banks.items())),
        'destructions': dict(sorted(destructions.items())),
        'tracks': tracks,
    }


def cmd_run() -> None:
    jobs = [(name, s0, s1, seed)
            for name, (s0, s1, seeds) in SCENARIOS.items()
            for seed in seeds]
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(lambda j: run_match(j[1], j[2], j[3]), jobs))
    rows = []
    for name, s0, s1, seed in jobs:
        match_dir = f'{s0}-vs-{s1}-s{seed}'
        data = extract(OUT / match_dir / 'replay.json.gz')
        tracks = data.pop('tracks')
        data['matchDir'] = match_dir
        rows.append({'scenario': name, 'sheet0': s0, 'sheet1': s1,
                     'seed': seed, **data})
        # keep the full tracks on disk beside the replay for the checker
        (OUT / match_dir / 'tracks.json').write_text(json.dumps(tracks))
    RESULTS.write_text(json.dumps(rows, indent=1))
    print(f'wrote {RESULTS} ({len(rows)} matches)')


def sessions(events, workshop, team):
    """Pair training-started with its terminal event for one workshop+team."""
    out, start = [], None
    for e in events:
        if e.get('workshopId') != workshop:
            continue
        k = e['kind']
        if k == 'workshop-training-started' and e.get('teamId') == team:
            start = e['tick']
        elif k == 'workshop-training-completed' and e.get('teamId') == team:
            out.append(('completed', start, e['tick']))
            start = None
        elif k == 'workshop-training-interrupted' and start is not None:
            out.append((e.get('reason', 'interrupted'), start, e['tick']))
            start = None
    return out


def cmd_report() -> None:
    rows = json.loads(RESULTS.read_text())
    by = {}
    for r in rows:
        by.setdefault(r['scenario'], []).append(r)
    failures = []

    def check(cond, label):
        print(('  PASS  ' if cond else '  FAIL  ') + label)
        if not cond:
            failures.append(label)

    print('== protected-training (trainer vs turtle) ==')
    for r in by['protected-training']:
        ss = sessions(r['workshopEvents'], 'north', 0)
        done = [s for s in ss if s[0] == 'completed']
        check(len(done) >= 1, f"s{r['seed']}: >=1 north completion "
                              f"(got {len(done)} of {len(ss)} sessions)")
        for _, a, b in done:
            check(b - a == 14, f"s{r['seed']}: session {a}->{b} spans "
                               f"15 consecutive qualifying ticks")
        readies = [e['tick'] for e in r['workshopEvents']
                   if e['kind'] == 'workshop-ready'
                   and e['workshopId'] == 'north']
        for _, a, b in done:
            expected = b + 1 + 45
            if expected <= r['endTick']:
                check(expected in readies,
                      f"s{r['seed']}: dormancy exact (ready {expected})")
        ups = [u for u in r['levelUps'] if u.get('teamId', 0) == 0 or
               u.get('actorId', {}).get('teamId') == 0]
        check(len(ups) >= 1,
              f"s{r['seed']}: trainee levels up ({len(ups)} level events)")

    def load_tracks(r):
        return json.loads((OUT / r['matchDir'] / 'tracks.json').read_text())

    print('== disruption (trainer vs disruptor-east) ==')
    for r in by['disruption']:
        inters = [e for e in r['workshopEvents']
                  if e['kind'] == 'workshop-training-interrupted'
                  and e['workshopId'] == 'north']
        reasons = sorted({e.get('reason') for e in inters})
        check(len(inters) >= 1,
              f"s{r['seed']}: disruption route interrupts training "
              f"({len(inters)} interruptions, reasons {reasons})")
        tracks = load_tracks(r)
        loop_ticks = sum(1 for u in ('t1u0', 't1u1')
                         for (_, x, y) in tracks[u]
                         if (x, y) in NORTH_CONTEST)
        check(loop_ticks >= 20,
              f"s{r['seed']}: east loop actually stands in north contest "
              f"({loop_ticks} body-ticks)")

    print('== disruption-suppression (loop denies the trainer, seed 7101) ==')
    for r in by['disruption-suppression']:
        # Trainer sits east here; the west disruptor owns the authored loop.
        done = [e for e in r['workshopEvents']
                if e['kind'] == 'workshop-training-completed']
        check(len(done) == 0,
              f"s{r['seed']}: standing contest denies every completion "
              f"({len(done)} completions)")
        tracks = load_tracks(r)
        loop_ticks = sum(1 for u in ('t0u0', 't0u1')
                         for (_, x, y) in tracks[u]
                         if (x, y) in NORTH_CONTEST)
        check(loop_ticks >= 50,
              f"s{r['seed']}: west loop holds the contest region "
              f"({loop_ticks} body-ticks)")

    print('== feint-raid (feint-raid vs workshop-guard) ==')
    raided = 0
    for r in by['feint-raid']:
        starts = [e for e in r['workshopEvents']
                  if e['kind'] == 'workshop-training-started'
                  and e['workshopId'] == 'north' and e.get('teamId') == 0]
        check(len(starts) >= 1,
              f"s{r['seed']}: feint trainee publicly starts training "
              f"({len(starts)} starts)")
        if not starts:
            continue
        t0 = starts[0]['tick']
        tracks = load_tracks(r)
        # Guard custody (team 1 slots 6/7) leaves the well room for the
        # workshop contest region after the public training signal.
        responded = sum(1 for u in ('t1u6', 't1u7')
                        for (t, x, y) in tracks[u]
                        if t > t0 and (x, y) in NORTH_CONTEST)
        check(responded >= 10,
              f"s{r['seed']}: guard custody answers the public signal "
              f"({responded} body-ticks in contest after t{t0})")
        n = r['banks'].get('0:enemy', 0)
        raided += 1 if n else 0
        print(f"  s{r['seed']}: enemy-well banks by feint side: {n}; "
              f"all banks {r['banks']}")
    check(raided >= 2, f"feint opens the outer-Well raid in {raided}/3 seeds")

    print('== train-then-rotate (trainer-rotate vs turtle) ==')
    for r in by['train-then-rotate']:
        ss = sessions(r['workshopEvents'], 'north', 0)
        done = [s for s in ss if s[0] == 'completed']
        check(len(done) >= 1, f"s{r['seed']}: completion before rotate "
                              f"({len(done)} completions)")
        if done:
            c = done[0][2]
            track = load_tracks(r)['t0u2']
            after = [(t, x, y) for (t, x, y) in track if t >= c + 120]
            near_centre = [p for p in after
                           if max(abs(p[1] - 16), abs(p[2] - 12)) <= 2]
            check(bool(near_centre),
                  f"s{r['seed']}: trainee rotated to centre post after "
                  f"workshopDone (completion t{c})")
            on_tile_late = [p for p in after if (p[1], p[2]) == (18, 4)]
            check(not on_tile_late,
                  f"s{r['seed']}: trainee left the training tile after done")

    print('== counter-demolition (mechanics evidence, not balance) ==')
    for key in ('counter-demolition', 'counter-demolition-mirrored'):
        for r in by[key]:
            counter_team = 0 if key == 'counter-demolition' else 1
            evs = r['workshopEvents']
            done = [e for e in evs if e['kind'] == 'workshop-training-completed'
                    and e.get('teamId') == counter_team]
            enemy_banks = r['banks'].get(f'{counter_team}:enemy', 0)
            w = r['winnerTeamId']
            print(f"  {key} s{r['seed']}: winner team {w}, counter side "
                  f"completions {len(done)}, enemy-well banks {enemy_banks}, "
                  f"banks {r['banks']}, destructions {r['destructions']}")
    both = by['counter-demolition'] + by['counter-demolition-mirrored']
    trained = sum(1 for r in both for e in r['workshopEvents']
                  if e['kind'] == 'workshop-training-completed')
    check(trained >= 1, 'workshop allocation happens under march pressure '
                        f'({trained} completions across 6 matches)')

    print()
    if failures:
        print(f'{len(failures)} FAILING CHECKS'); sys.exit(1)
    print('all proof checks pass')


if __name__ == '__main__':
    {'run': cmd_run, 'report': cmd_report}[sys.argv[1]]()
