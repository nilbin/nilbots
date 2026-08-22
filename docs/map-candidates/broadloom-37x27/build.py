#!/usr/bin/env python3
"""Build + verify Broadloom, the first 37x27 whole-map Arc Relay candidate.

Design intent (west perspective; see the candidate report):

  * the three functions Tapline's centre bundled -- best score source, best
    assembly, heal pivot, route hub -- are pulled apart onto a cross: the
    scoring run is the mid row, the development axis is the centre column;
  * the two owned Wells leave the chiral line and sit in OPPOSITE corners
    (west's in the SW, east's in the NE), so each team's 4-value raid path
    runs through the OTHER side's workshop theatre, not through centre;
  * the two neutral workshops sit on the centre column (18,4)/(18,22),
    equidistant from both reactors by construction, with three real mouths;
  * heals move out to the NW/SE home quarters, 12+ from centre, so recovery
    is a detour and not a centre reward;
  * the outer galleries belong to the workshop theatres (rooms with a
    payoff), not to a bare one-tile highway.

Only the upper half (y < 13) and the self-symmetric middle row are authored;
the lower half IS the 180-degree rotation.
"""
from collections import deque

W, H = 37, 27
MID = 13
rot = lambda p: (W - 1 - p[0], H - 1 - p[1])

REACTOR0 = (3, 13)                 # west; east is rot -> (33, 13)
PAD0 = [(x, y) for x in range(1, 5) for y in range(10, 17)]
DELIVER0 = [(x, y) for x in range(5, 8) for y in range(11, 16)]
WELL_OWN0 = (10, 19)               # west's, SW quarter; east's is rot -> (25, 7)
WELL_CENTRE = (18, 13)
WELL_OWN1 = rot(WELL_OWN0)
HEAL_W = (8, 6)                    # west's, NW quarter; east's is rot -> (28, 20)
WORKSHOP_N = (18, 4)               # neutral pair on the centre column
WORKSHOP_S = rot(WORKSHOP_N)       # -> (18, 22)

PROTECTED = ([REACTOR0, WELL_OWN0, WELL_CENTRE, WELL_OWN1, HEAL_W,
              WORKSHOP_N, WORKSHOP_S]
             + PAD0 + DELIVER0 + [rot(REACTOR0), rot(HEAL_W)]
             + [rot(p) for p in PAD0] + [rot(p) for p in DELIVER0])

# Wall clusters, upper half only, written as (x, y, w, h).
BLOCKS = [
    # west pad north shell: exit stays open at x3-4
    (1, 9, 2, 1),
    # east pad north shell: fully sealed (east exits south, by rotation)
    (32, 9, 4, 1),

    # NW heal room around (8,6): mouths SW (5,7)? no: S (8,8), E (11,7), W via x3-4 lane
    (5, 4, 6, 1),                     # north wall x5-10
    (5, 5, 1, 2),                     # west wall (5,5),(5,6); SW mouth (5,7)
    (11, 4, 1, 3),                    # east wall (11,4)-(11,6); E mouth (11,7)
    (6, 8, 2, 1), (9, 8, 2, 1),       # south wall, S mouth at (8,8)
    (7, 2, 2, 1), (5, 3, 1, 1),       # NW band cover teeth
    (2, 7, 1, 1),                     # west field tooth (breaks the y7 lane)
    (3, 4, 2, 2),                     # NW pad-lane block: no rail lane into the pad

    # NW seam teeth (break the x12/x13 vertical alleys)
    (12, 6, 1, 1), (13, 9, 1, 1),
    # gallery divider between the NW loop and the workshop roof gallery
    (11, 1, 1, 1),

    # N workshop room: floor x15-21, y3-6; training tile (18,4)
    (15, 2, 3, 1), (19, 2, 3, 1),     # north wall; skylight (18,2) open
    (14, 2, 1, 1), (14, 5, 1, 3),     # west wall y5-7; W mouth (14,3)-(14,4)
    (22, 2, 1, 2), (22, 7, 1, 1),     # east wall; E mouth (22,4)-(22,6)
    (15, 7, 1, 1), (21, 7, 1, 1),     # south antechamber shoulders; mouth x16-20
    (16, 5, 1, 1), (17, 3, 1, 1), (20, 4, 1, 1),   # interior cover pillars

    # centre-north island: the workshop approach forks around it
    (17, 8, 3, 2),

    # centre plaza: W wall corners; pillars (rot completes the stagger)
    (14, 9, 1, 3),
    (16, 11, 1, 1), (20, 11, 1, 1),

    # west mid corridor north edge + corridor cover
    (6, 10, 3, 1),
    (10, 11, 1, 1), (10, 12, 1, 1),
    # mid-band rail breaks (rot completes the y14 partners). Deliberately
    # NOT on the Well's own row-neighbours: a wall directly north or south
    # of the Well kills all four corner-strict diagonal exits and two
    # Hardlights could then entomb the Well on (17,13)/(19,13).
    (23, 12, 1, 1), (26, 12, 1, 1), (28, 11, 1, 1),

    # NE: east own-Well room around (26,7), floor x24-28 y5-9
    (25, 4, 3, 1),                    # north wall x25-27; seams (24,4),(28,4)
    (23, 5, 1, 1), (23, 9, 1, 2),     # west wall; W mouth (23,6)-(23,8)
    (26, 10, 3, 1),                   # south wall x26-28; S mouth (24,10)-(25,10)
    (29, 5, 1, 3),                    # east wall y5-7; E mouth (29,8)-(29,9)
    (25, 6, 1, 1), (27, 8, 1, 1), (24, 9, 1, 1),   # interior cover

    # NE gallery/field teeth
    (25, 1, 1, 1), (29, 2, 2, 1),
    # east home-north lane x30-31: cover + divider to the NE deep field
    (30, 6, 1, 1), (31, 9, 1, 1),
    (32, 3, 1, 4),
    (34, 2, 1, 1),
]
MIDDLE_WALLS = [10, 11, 15]
OPENINGS = []


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
            assert 0 < ax < W - 1 and 0 < ay < H - 1, \
                f"opening {(ax, ay)} on the border"
            g[ay][ax] = '.'
    return [''.join(r) for r in g]


def step_ok(rows, a, b):
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

    inb = lambda p: 0 <= p[0] < W and 0 <= p[1] < H
    for (x, y) in sorted(op):
        for dx, dy in ((1, 1), (1, -1)):
            d = (x + dx, y + dy)
            if (d in op and inb(d)
                    and (x + dx, y) not in op and (x, y + dy) not in op):
                errs.append(f"corner-touch {(x, y)}-{d}")

    STUB = 2
    for p in op:
        rest = op - {p}
        parts, saw = [], set()
        for s in rest:
            if s in saw: continue
            n, qq = 0, deque([s]); saw.add(s)
            while qq:
                c = qq.popleft(); n += 1
                for q2 in neighbours(rows, c):
                    if q2 in rest and q2 not in saw:
                        saw.add(q2); qq.append(q2)
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
    marks = {REACTOR0: 'R', rot(REACTOR0): 'R', WELL_OWN0: 'w',
             WELL_CENTRE: 'C', WELL_OWN1: 'e', HEAL_W: 'h', rot(HEAL_W): 'h',
             WORKSHOP_N: 'W', WORKSHOP_S: 'W'}
    for p in DELIVER0 + [rot(q) for q in DELIVER0]: marks.setdefault(p, 'd')
    for p in PAD0 + [rot(q) for q in PAD0]: marks.setdefault(p, '_')
    print('    ' + ''.join(str(x % 10) for x in range(W)))
    for y, r in enumerate(rows):
        print(f'{y:3} ' + ''.join(marks.get((x, y), c) for x, c in enumerate(r)))
    print(f"\nopen {opens} ({opens/(W*H):.0%})   errors: {errs[:12] or 'none'}"
          f"{' (+more)' if len(errs) > 12 else ''}")
    runs = []
    for y in range(1, H - 1):
        run = 0
        for x in range(W):
            if rows[y][x] == '.':
                run += 1
                if x == W - 1 and run >= 12: runs.append(('h', x - run + 1, y, run))
            else:
                if run >= 12: runs.append(('h', x - run, y, run))
                run = 0
    for x in range(1, W - 1):
        run = 0
        for y in range(H):
            if rows[y][x] == '.':
                run += 1
                if y == H - 1 and run >= 12: runs.append(('v', x, y - run + 1, run))
            else:
                if run >= 12: runs.append(('v', x, y - run, run))
                run = 0
    print('  straight open runs >= 12:', runs or 'none')
    b0 = REACTOR0
    for label, tgt in [('own Well', WELL_OWN0), ('own heal', HEAL_W),
                       ('centre Well', WELL_CENTRE),
                       ('N workshop', WORKSHOP_N), ('S workshop', WORKSHOP_S),
                       ('enemy Well', WELL_OWN1), ('enemy heal', rot(HEAL_W)),
                       ('enemy reactor', rot(REACTOR0))]:
        d0, d1 = path_len(rows, b0, tgt), path_len(rows, rot(REACTOR0), rot(tgt))
        flag = '' if d0 == d1 else '  <-- ASYMMETRIC'
        print(f'  {label:14} {d0:3}  (mirror {d1}){flag}')
    for label, a, b in [
            ('workshopN <-> centre', WORKSHOP_N, WELL_CENTRE),
            ('workshopN <-> E well', WORKSHOP_N, WELL_OWN1),
            ('workshopN <-> W heal', WORKSHOP_N, HEAL_W),
            ('workshopN <-> workshopS', WORKSHOP_N, WORKSHOP_S),
            ('W heal <-> centre', HEAL_W, WELL_CENTRE),
            ('own Well <-> centre', WELL_OWN0, WELL_CENTRE),
            ('E well (raid) <-> centre', WELL_OWN1, WELL_CENTRE)]:
        print(f'  {label:24} {path_len(rows, a, b)}')
