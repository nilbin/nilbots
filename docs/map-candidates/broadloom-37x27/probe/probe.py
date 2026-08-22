#!/usr/bin/env python3
"""Deterministic Broadloom doctrine probe.

Settles the candidate report's strategic hypotheses with real matches on the
broadloom-probe profile (Tapline-14 ruleset + Broadloom map): deathball vs
raid-and-trade, turtle economics, and own-Well run interception. Every match
is seeded and reproducible; stats come from replay events, not summaries.

    python3 docs/map-candidates/broadloom-37x27/probe/probe.py run
    python3 docs/map-candidates/broadloom-37x27/probe/probe.py report
"""
import gzip
import json
import subprocess
import sys
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
PLAYBOOKS = REPO / 'arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks'
MIND = REPO / 'arena-bots/arc-relay/tactical-playbook-mind-v1'
OUT = REPO / 'out/broadloom-probe'
RESULTS = Path(__file__).resolve().parent / 'results.json'

SHEETS = {
    'ball': PLAYBOOKS / 'broadloom-ball-v1.json',
    'trade': PLAYBOOKS / 'broadloom-trade-v1.json',
    'turtle': PLAYBOOKS / 'broadloom-turtle-v1.json',
}
SEEDS = [9001, 9002, 9003, 9004, 9005, 9006]
PAIRINGS = [
    ('ball', 'trade'), ('trade', 'ball'),
    ('ball', 'turtle'), ('turtle', 'ball'),
    ('trade', 'turtle'), ('turtle', 'trade'),
    ('ball', 'ball'), ('trade', 'trade'),
]

# Well ownership on Broadloom by reactor distance: team 0 (west) owns
# "south" (10,19); team 1 (east) owns "north" (26,7); "centre" is neutral.
OWN_WELL = {0: 'south', 1: 'north'}


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
         '--loop-profile', 'broadloom-probe', '--seed', str(seed),
         '--sheet0', str(SHEETS[sheet0]), '--sheet1', str(SHEETS[sheet1]),
         '--out', str(out)],
        check=True, cwd=REPO, capture_output=True, text=True)
    return replay


def classify(team: int, source_well: str) -> str:
    if source_well == 'centre':
        return 'centre'
    return 'own' if OWN_WELL[team] == source_well else 'enemy'


def extract(replay_path: Path) -> dict:
    d = json.load(gzip.open(replay_path))
    banks = Counter()          # (team, class) -> count
    charge = Counter()         # team -> pips
    pulses = Counter()
    destructions = Counter()
    carry_drops = Counter()    # (carrier team, class of dropped core) on death
    first_bank = {}
    end_tick = d['ticks'][-1]['tick']
    for tick in d['ticks']:
        for e in tick.get('events', []) or []:
            if e.get('kind') == 'destruction':
                team = (e.get('payload', {}).get('actorId', {}) or
                        e.get('actorId', {}) or {}).get('teamId')
                if team is None:
                    team = e.get('payload', {}).get('teamId', -1)
                destructions[team] += 1
            if e.get('kind') != 'arc-relay':
                continue
            f = e['payload']['fact']
            k = f['kind']
            if k == 'core-banked':
                team = f['teamId']
                cls = classify(team, f['coreId']['sourceWellId'])
                banks[(team, cls)] += 1
                charge[team] += f['chargePips']
                first_bank.setdefault((team, cls), tick['tick'])
            elif k == 'pulse':
                pulses[f['teamId']] += 1
            elif k == 'core-dropped' and f.get('dropKind', '').startswith('destr'):
                team = f['sourceActorId']['teamId']
                cls = classify(team, f['coreId']['sourceWellId'])
                carry_drops[(team, cls)] += 1
    result = d['result']
    return {
        'endTick': end_tick,
        'result': result,
        'banks': {f'{t}:{c}': n for (t, c), n in sorted(banks.items())},
        'chargePips': dict(sorted(charge.items())),
        'pulses': dict(sorted(pulses.items())),
        'destructions': dict(sorted(destructions.items())),
        'carryDropsOnDeath': {f'{t}:{c}': n
                              for (t, c), n in sorted(carry_drops.items())},
        'firstBankTick': {f'{t}:{c}': n
                          for (t, c), n in sorted(first_bank.items())},
    }


def cmd_run() -> None:
    rows = []
    for sheet0, sheet1 in PAIRINGS:
        for seed in SEEDS:
            replay = run_match(sheet0, sheet1, seed)
            stats = extract(replay)
            stats.update({'sheet0': sheet0, 'sheet1': sheet1, 'seed': seed})
            rows.append(stats)
            r = stats['result']
            print(f"{sheet0:6} vs {sheet1:6} s{seed}: "
                  f"end t{stats['endTick']} "
                  f"pulses {stats['pulses']} charge {stats['chargePips']} "
                  f"{r.get('winnerTeamId', r)}", flush=True)
    RESULTS.write_text(json.dumps(rows, indent=1) + '\n', encoding='utf-8')
    print(f'wrote {RESULTS} ({len(rows)} matches)')


def cmd_report() -> None:
    rows = json.loads(RESULTS.read_text(encoding='utf-8'))

    def wins(rowset, name):
        w = l = d = 0
        for row in rowset:
            winner = row['result'].get('winnerTeamId')
            mine = 0 if row['sheet0'] == name else 1
            if row['sheet0'] == row['sheet1']:
                continue
            if winner is None:
                d += 1
            elif winner == mine:
                w += 1
            else:
                l += 1
        return w, l, d

    for a, b in [('ball', 'trade'), ('ball', 'turtle'), ('trade', 'turtle')]:
        rel = [r for r in rows if {r['sheet0'], r['sheet1']} == {a, b}]
        wa, la, da = wins(rel, a)
        print(f'{a} vs {b}: {a} {wa}W {la}L {da}D over {len(rel)} matches')
        agg = Counter()
        for r in rel:
            for key, n in r['banks'].items():
                team, cls = key.split(':')
                name = r['sheet0'] if team == '0' else r['sheet1']
                agg[(name, cls)] += n
        for (name, cls), n in sorted(agg.items()):
            print(f'   banks {name:6} {cls:6} {n}')
        drops = Counter()
        for r in rel:
            for key, n in r['carryDropsOnDeath'].items():
                team, cls = key.split(':')
                name = r['sheet0'] if team == '0' else r['sheet1']
                drops[(name, cls)] += n
        for (name, cls), n in sorted(drops.items()):
            print(f'   carrier killed {name:6} {cls:6} {n}')


if __name__ == '__main__':
    {'run': cmd_run, 'report': cmd_report}[sys.argv[1]]()
