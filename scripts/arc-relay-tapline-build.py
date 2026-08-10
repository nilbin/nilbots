#!/usr/bin/env python3
"""Build + verify Tapline.

Owner verdict after playing Crucible and Drawbench: the well-line warren had
the better geometry -- corridors, wall placement, rooms. So this keeps the
warren's grammar and canvas (31x27, an outer shadow corridor with three ways
in per side, chunky wall clusters, corridors four to six wide) and changes
only what the warren could not express:

  * the three Wells sit on the chiral line through the centre, so they are
    TIERED rather than all thirteen steps from both bases;
  * the scoring shoulder is not the spawn pad;
  * west enters high and east enters low, so the Well line runs ALONG the
    axis of play: yours is up-line, theirs is down-line.

Only the upper half (y < 13) and the self-symmetric middle row are authored;
the lower half IS the 180-degree rotation.
"""
from collections import deque

W, H = 31, 27
MID = 13
rot = lambda p: (W - 1 - p[0], H - 1 - p[1])

REACTOR0 = (2, 5)
PAD0 = [(x, y) for x in range(1, 5) for y in range(3, 9)]
DELIVER0 = [(x, y) for x in range(6, 9) for y in range(6, 9)]
WELL_NEAR0 = (11, 6)          # west's, up-line
WELL_CENTRE = (15, 13)
WELL_FAR0 = rot(WELL_NEAR0)   # east's, down-line -> (19, 20)
HEAL_A = (21, 7)              # rot -> (9, 19)

PROTECTED = ([REACTOR0, WELL_NEAR0, WELL_CENTRE, WELL_FAR0, HEAL_A]
             + PAD0 + DELIVER0 + [rot(REACTOR0), rot(HEAL_A)]
             + [rot(p) for p in PAD0] + [rot(p) for p in DELIVER0])

# Wall clusters, upper half only. Written as (x, y, w, h) the way the warren
# reads: short runs and small blocks, not scattered single tiles.
BLOCKS = [
    # the shadow corridor: row 1 runs the full width and row 2 seals it off
    # except at three gaps (x3, x15, x27), so the outside lane is a committed
    # flank rather than a free highway.
    (1, 2, 2, 1), (4, 2, 11, 1), (16, 2, 11, 1), (28, 2, 2, 1),

    # base shell: the pad opens east along y4-7 and south along x3-4
    (5, 3, 1, 2), (1, 9, 2, 1), (5, 8, 1, 2),

    # the delivery shoulder's far cover -- a defender holds this without
    # standing inside spawn protection
    (9, 6, 1, 3), (6, 10, 3, 1),

    # the near-Well room: cover on three sides, mouths from the base side
    # and from below, so a raid has to commit down-line
    (9, 4, 1, 2), (12, 4, 3, 1), (13, 5, 1, 2), (10, 8, 3, 1),

    # north rooms between the base and the line
    (16, 4, 3, 1), (17, 5, 1, 3), (20, 4, 2, 2), (24, 4, 3, 1),
    (25, 5, 1, 3), (13, 9, 2, 1), (16, 8, 2, 2),

    # the heal room at (21,7): shoulders, mouths west and south
    (20, 6, 1, 1), (22, 6, 2, 1), (22, 8, 1, 1),

    # the centre approach: rooms either side of the line's upper half
    (10, 10, 3, 1), (11, 11, 1, 2), (18, 10, 3, 1), (19, 11, 1, 2),
    (6, 12, 3, 1), (23, 10, 2, 1), (24, 11, 1, 2), (27, 9, 2, 1),
    (2, 11, 2, 1), (5, 5, 1, 1), (7, 3, 2, 1),
]
# middle row (y = 13) must equal its own reversal
MIDDLE_WALLS = [3, 4, 8, 9, 12, 18, 21, 22, 26, 27]


def build():
    g = [['.'] * W for _ in range(H)]
    for x in range(W):
        g[0][x] = g[H - 1][x] = '#'
    for y in range(H):
        g[y][0] = g[y][W - 1] = '#'
    for (bx, by, bw, bh) in BLOCKS:
        for x in range(bx, bx + bw):
            for y in range(by, by + bh):
                assert y < MID, f"block {(bx, by)} crosses the middle row"
                if (x, y) in PROTECTED or rot((x, y)) in PROTECTED:
                    continue
                g[y][x] = '#'
                rx, ry = rot((x, y))
                g[ry][rx] = '#'
    for x in MIDDLE_WALLS:
        for xx in (x, W - 1 - x):
            if (xx, MID) not in PROTECTED:
                g[MID][xx] = '#'
    return [''.join(r) for r in g]


def analyse(rows):
    errs = []
    for y in range(H):
        for x in range(W):
            rx, ry = rot((x, y))
            if rows[y][x] != rows[ry][rx]:
                errs.append(f"chirality break {(x, y)}")
    for p in PROTECTED:
        if rows[p[1]][p[0]] != '.':
            errs.append(f"protected tile walled {p}")
    op = {(x, y) for y in range(H) for x in range(W) if rows[y][x] == '.'}
    s0 = next(iter(op)); seen, q = {s0}, deque([s0])
    while q:
        c = q.popleft()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (c[0] + dx, c[1] + dy)
                if n in op and n not in seen:
                    seen.add(n); q.append(n)
    if len(seen) != len(op):
        errs.append(f"unreachable {len(op) - len(seen)}")
    return errs, len(op)


def path_len(rows, a, b):
    op = {(x, y) for y in range(H) for x in range(W) if rows[y][x] == '.'}
    q, seen = deque([(a, 0)]), {a}
    while q:
        (x, y), d = q.popleft()
        if (x, y) == b: return d
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (x + dx, y + dy)
                if n in op and n not in seen:
                    seen.add(n); q.append((n, d + 1))
    return None


if __name__ == '__main__':
    rows = build()
    errs, opens = analyse(rows)
    marks = {REACTOR0: 'R', rot(REACTOR0): 'R', WELL_NEAR0: 'N',
             WELL_CENTRE: 'C', WELL_FAR0: 'S', HEAL_A: 'h', rot(HEAL_A): 'h'}
    for p in DELIVER0 + [rot(q) for q in DELIVER0]: marks.setdefault(p, 'd')
    for p in PAD0 + [rot(q) for q in PAD0]: marks.setdefault(p, '_')
    print('    ' + ''.join(str(x % 10) for x in range(W)))
    for y, r in enumerate(rows):
        print(f'{y:3} ' + ''.join(marks.get((x, y), c) for x, c in enumerate(r)))
    print(f"\nopen {opens} ({opens/(W*H):.0%})   errors: {errs or 'none'}")
    b0, b1 = REACTOR0, rot(REACTOR0)
    for label, tgt in [('own delivery', (7, 7)), ('own Well', WELL_NEAR0),
                       ('own heal', rot(HEAL_A)), ('centre Well', WELL_CENTRE),
                       ('enemy heal', HEAL_A), ('enemy Well', WELL_FAR0),
                       ('enemy reactor', b1)]:
        d0, d1 = path_len(rows, b0, tgt), path_len(rows, b1, rot(tgt))
        flag = '' if d0 == d1 else '  <-- ASYMMETRIC'
        print(f'  {label:14} {d0:3}  (mirror {d1}){flag}')
