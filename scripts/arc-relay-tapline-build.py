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
    # the owner-facing side of the Well room must be a room mouth, not a
    # single tile: sealing x9 from y4 to y8 left one gate at (9,3) and
    # bodies could not get within three tiles of their own Well.
    (9, 4, 1, 2), (12, 4, 3, 1), (13, 5, 1, 2), (10, 8, 2, 1),

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

# Carved back out after the blocks are painted, upper half only.
#
# Movement is corner-strict: a diagonal step needs both orthogonal companions
# open, exactly as LOS and the strike cone have always required. Two wall
# clusters that touch only at a corner therefore do NOT form a passage -- but
# they read like one, and the first draft of this map had fourteen of them.
# That is a lie to the player, and it also stretched the map badly: under the
# real rule, reactor to reactor was 38 steps rather than 28, and the centre
# Well 19 rather than 14, because every planner correctly refused shortcuts
# the geometry appeared to offer.
#
# These seven openings (fourteen tiles with their rotation partners) remove
# every corner-touch. Each was chosen by search for the largest reduction in
# defects, not by taste. With no corner-touch left, the permissive and
# corner-strict rules agree everywhere here, so the map plays identically
# under either -- and it plays at the distances it was drawn for.
#
# The search may not touch the shadow corridor's seal (y = 1 and y = 2). Row 1
# runs the full width and row 2 admits exactly three gaps per side; that is the
# authored flank, and an earlier pass that opened (1, 2) and (29, 2) bought two
# fewer nooks by quietly turning the committed flank into a highway.
OPENINGS = [
    (12, 13), (8, 3), (21, 5), (5, 3), (5, 9), (12, 10), (17, 9),
]


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
    for (x, y) in OPENINGS:
        assert y <= MID, f"opening {(x, y)} is not in the authored half"
        for (ax, ay) in ((x, y), rot((x, y))):
            assert 0 < ax < W - 1 and 0 < ay < H - 1, f"opening {(ax, ay)} on the border"
            g[ay][ax] = '.'
    return [''.join(r) for r in g]


def step_ok(rows, a, b):
    """The engine's corner-strict movement rule, and the mind's CanStep."""
    op = lambda p: (0 <= p[0] < W and 0 <= p[1] < H and rows[p[1]][p[0]] != '#')
    dx, dy = b[0] - a[0], b[1] - a[1]
    if not op(b):
        return False
    return dx == 0 or dy == 0 or (op((a[0] + dx, a[1])) and op((a[0], a[1] + dy)))


def neighbours(rows, p):
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            if not (dx or dy):
                continue
            q = (p[0] + dx, p[1] + dy)
            if 0 <= q[0] < W and 0 <= q[1] < H and step_ok(rows, p, q):
                yield q


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
        for n in neighbours(rows, c):
            if n not in seen:
                seen.add(n); q.append(n)
    if len(seen) != len(op):
        errs.append(f"unreachable {len(op) - len(seen)}")

    # No wall corner may pretend to be a passage: if two floor tiles touch
    # only diagonally, corner-strict movement cannot cross between them.
    inb = lambda p: 0 <= p[0] < W and 0 <= p[1] < H
    for (x, y) in op:
        for dx, dy in ((1, 1), (1, -1)):
            d = (x + dx, y + dy)
            if (d in op and inb(d)
                    and (x + dx, y) not in op and (x, y + dy) not in op):
                errs.append(f"corner-touch {(x, y)}-{d} reads open but is not")

    # No body standing still may sever a meaningful piece of the map -- that is
    # what makes routing around traffic possible. Severity is the measure, not
    # mere existence: the shadow corridor's four ends are two-tile stubs beyond
    # the outermost seal gap, so a body there cuts off a corner nook. That
    # follows from the authored flank (row 1 full width, three gaps in row 2)
    # and is harmless. A body that severs more than that is not.
    STUB = 2
    for p in op:
        rest = op - {p}
        parts, saw = [], set()
        for s in rest:
            if s in saw: continue
            n, qq = 0, deque([s]); saw.add(s)
            while qq:
                c = qq.popleft(); n += 1
                for q in neighbours(rows, c):
                    if q in rest and q not in saw:
                        saw.add(q); qq.append(q)
            parts.append(n)
        if len(parts) > 1 and min(parts) > STUB:
            errs.append(f"cut vertex {p}: severs {min(parts)} tiles")
    return errs, len(op)


def path_len(rows, a, b):
    q, seen = deque([(a, 0)]), {a}
    while q:
        (x, y), d = q.popleft()
        if (x, y) == b: return d
        for n in neighbours(rows, (x, y)):
            if n not in seen:
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
