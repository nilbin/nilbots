#!/usr/bin/env python3
"""Build + verify the Crucible diagonal map.

Authoring rule: only the upper half (y < 11) and the self-symmetric middle
row are authored. The lower half is the exact 180-degree rotation, so
chirality is guaranteed by construction rather than by inspection.
"""
from collections import deque

W, H = 31, 23
MID = 11  # middle row index; rot(x,y) = (W-1-x, H-1-y)


def rot(p):
    return (W - 1 - p[0], H - 1 - p[1])


# --- landmarks (team 0 / north-west side; team 1 is the rotation) ---------
REACTOR0 = (2, 2)
PAD0 = [(x, y) for x in range(1, 5) for y in range(1, 5)]
DELIVER0 = [(x, y) for x in range(5, 8) for y in range(5, 8)]
WELL_NEAR0 = (9, 3)
WELL_CENTRE = (15, 11)
HEAL_A = (20, 6)          # rotation gives HEAL_B = (10,16)

PROTECTED = (
    [REACTOR0, WELL_NEAR0, WELL_CENTRE, HEAL_A]
    + PAD0 + DELIVER0
    + [rot(REACTOR0), rot(WELL_NEAR0), rot(HEAL_A)]
    + [rot(p) for p in PAD0] + [rot(p) for p in DELIVER0]
)

# --- authored walls, upper half only (y = 1..10) --------------------------
UPPER_WALLS = [
    # base shell: the NW pad has exactly two mouths - east along y3-4,
    # and south along x3-4. A camper cannot plug both.
    (5, 1), (5, 2), (1, 5), (2, 5),

    # north strip staggers: the run from base past the near well to the
    # north-east is broken twice, so a straight sprint along y1-2 is not
    # a highway. Gaps stay at (12,3) and (17,1).
    (12, 1), (12, 2), (17, 2), (17, 3),

    # near-well pocket: (9,3) is walled from the north edge and from the
    # east, leaving one short mouth from the base side (8,3) and one
    # longer approach from the south (9,5). Raidable, but you commit.
    (9, 2), (10, 3), (10, 4), (8, 4),

    # the delivery shoulder: cover that lets a defender hold the mouth of
    # the scoring zone without standing inside spawn protection.
    (8, 6), (8, 7), (4, 8), (5, 8),

    # diagonal weave: blockers offset either side of the NW->centre line
    # so the approach bends instead of running straight to the middle.
    (12, 6), (11, 9), (14, 8),

    # centre-north pillars: cover on the northern lip of the centre well,
    # two separate mouths rather than one open face.
    (13, 10), (16, 9), (17, 10),

    # north-east quadrant + heal at (20,6): shoulders on three sides so
    # healing is defensible, with mouths west (19,6) and south (20,7).
    (19, 5), (20, 5), (21, 5), (21, 6), (21, 7),

    # east approach breakers, keeping the NE wing from being one open room
    (24, 3), (24, 4), (25, 7), (26, 7), (23, 9), (24, 9),

    # --- density pass: no long open runs, no border highways -------------
    # north edge and strip texture
    (13, 3), (14, 3), (22, 1), (23, 1),
    # the direct north face of the centre well must bend, not run
    (15, 6), (15, 7),
    # east band breaks so the north-east wing is rooms, not a hall
    (20, 9), (21, 9), (26, 2), (27, 2), (26, 9), (27, 9), (18, 8),
    # border lanes: the outer column is a corridor unless it is broken
    (29, 4), (29, 5), (2, 8),
    # the west descent out of the NW base gets one stagger
    (6, 9), (7, 9),
]

# --- middle row (y = 11): must equal its own reversal ---------------------
# Single-tile passages at x=6 and x=24 make the two side lanes real chokes,
# while the centre well sits open with pillar shoulders either side.
MIDDLE_WALLS = [5, 7, 12, 18, 23, 25]


def build():
    grid = [['.'] * W for _ in range(H)]
    for x in range(W):
        grid[0][x] = '#'
        grid[H - 1][x] = '#'
    for y in range(H):
        grid[y][0] = '#'
        grid[y][W - 1] = '#'
    for (x, y) in UPPER_WALLS:
        assert y < MID, f"authored wall {(x, y)} is not in the upper half"
        grid[y][x] = '#'
        rx, ry = rot((x, y))
        grid[ry][rx] = '#'
    for x in MIDDLE_WALLS:
        grid[MID][x] = '#'
        grid[MID][W - 1 - x] = '#'
    return [''.join(r) for r in grid]


def check(rows):
    errs = []
    # chirality
    for y in range(H):
        for x in range(W):
            rx, ry = rot((x, y))
            if rows[y][x] != rows[ry][rx]:
                errs.append(f"chirality break at {(x, y)}")
    # protected tiles open
    for p in PROTECTED:
        if rows[p[1]][p[0]] != '.':
            errs.append(f"protected tile walled: {p}")
    # reachability over all open tiles (8-way)
    opens = {(x, y) for y in range(H) for x in range(W) if rows[y][x] == '.'}
    start = next(iter(opens))
    seen, q = {start}, deque([start])
    while q:
        x, y = q.popleft()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (x + dx, y + dy)
                if n in opens and n not in seen:
                    seen.add(n)
                    q.append(n)
    if len(seen) != len(opens):
        errs.append(f"unreachable: {len(opens) - len(seen)} of {len(opens)}")
    return errs, len(opens)


def path_len(rows, a, b):
    """8-way shortest path over open tiles."""
    opens = {(x, y) for y in range(H) for x in range(W) if rows[y][x] == '.'}
    q, seen = deque([(a, 0)]), {a}
    while q:
        (x, y), d = q.popleft()
        if (x, y) == b:
            return d
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (x + dx, y + dy)
                if n in opens and n not in seen:
                    seen.add(n)
                    q.append((n, d + 1))
    return None


if __name__ == '__main__':
    rows = build()
    errs, opens = check(rows)
    marks = {REACTOR0: 'R', rot(REACTOR0): 'R', WELL_NEAR0: 'N',
             rot(WELL_NEAR0): 'S', WELL_CENTRE: 'C',
             HEAL_A: 'h', rot(HEAL_A): 'h'}
    for p in DELIVER0:
        marks.setdefault(p, 'd')
    for p in [rot(q) for q in DELIVER0]:
        marks.setdefault(p, 'd')
    for p in PAD0:
        marks.setdefault(p, '_')
    for p in [rot(q) for q in PAD0]:
        marks.setdefault(p, '_')
    print('    ' + ''.join(str(x % 10) for x in range(W)))
    for y, r in enumerate(rows):
        line = ''.join(marks.get((x, y), c) for x, c in enumerate(r))
        print(f'{y:3} {line}')
    print(f'\nopen tiles: {opens}')
    print('errors:', errs or 'none')
    b0, b1 = REACTOR0, rot(REACTOR0)
    for label, tgt in [('own near well', WELL_NEAR0),
                       ('centre well', WELL_CENTRE),
                       ('enemy near well', rot(WELL_NEAR0)),
                       ('own delivery', (6, 6)),
                       ('heal A (20,6)', HEAL_A),
                       ('heal B (10,16)', rot(HEAL_A)),
                       ('enemy reactor', b1)]:
        d0 = path_len(rows, b0, tgt)
        d1 = path_len(rows, b1, rot(tgt))
        flag = '' if d0 == d1 else '   <-- ASYMMETRIC'
        print(f'  team0 base -> {label:18} {d0:3}   (team1 mirror {d1}){flag}')
    print(f"  centre well -> heal A: {path_len(rows, WELL_CENTRE, HEAL_A)}")
    print(f"  centre well -> heal B: {path_len(rows, WELL_CENTRE, rot(HEAL_A))}")
