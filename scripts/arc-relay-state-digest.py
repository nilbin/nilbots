#!/usr/bin/env python3
"""Gameplay-state digest for Arc Relay raw replays.

The replay hash covers header artifact provenance and mindTurns debug
messages, so it diverges whenever a binary or a sheet's internal NAMES
change even if not a single game decision did. This digest answers the
question parity work actually asks - did the same things HAPPEN - by
hashing only per-tick actor state (identity, position, health, form,
facing) plus the result. Two replays with equal digests played the
identical match.

Born in the ghost-doctrine-v3 parity adjudication (ab72 vs ab74, where
7/24 cells proved state-identical under renamed orders and an extended
schema), after the flow-field proof hit the same wall with replayHash.

Usage: arc-relay-state-digest.py REPLAY.json.gz [more...]
       (prints one digest per file; diff them however you like)
"""
import gzip
import hashlib
import json
import sys
from pathlib import Path


def state_digest(path: Path) -> str:
    opener = gzip.open if path.suffix == '.gz' else open
    with opener(path, 'rt', encoding='utf-8') as handle:
        replay = json.load(handle)
    digest = hashlib.sha256()
    for tick in replay['ticks']:
        state = tick.get('tickStart', {}).get('state', {})
        lives = [
            (life['actorId']['teamId'], life['actorId']['unitId'],
             life['actorId']['lifeId'],
             life['position']['x'], life['position']['y'],
             life.get('health'), life.get('formId'),
             str(life.get('facing')))
            for life in state.get('activeLives', []) or []
        ]
        digest.update(json.dumps(sorted(lives)).encode())
    digest.update(json.dumps(
        replay.get('result', {}), sort_keys=True).encode())
    return digest.hexdigest()


def main() -> None:
    for name in sys.argv[1:]:
        path = Path(name)
        print(f'{state_digest(path)}  {path}')


if __name__ == '__main__':
    main()
