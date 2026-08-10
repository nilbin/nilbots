#!/usr/bin/env python3
"""Build + verify the Drawbench horizontal map.

Same authoring rule as Crucible: only the upper half (y < 11) and the
self-symmetric middle row are authored, so the 180-degree chirality that
makes rotational spawn assignment fair is a property of the construction.

The difference from Crucible is where the reactors sit. Corner-to-corner
made every route a diagonal. Here they sit on the west and east EDGES but
at opposite heights -- west high, east low -- so the main lane runs
horizontally while still asking you to give up ground vertically.
"""
from collections import deque

W, H = 31, 23
MID = 11
rot = lambda p: (W - 1 - p[0], H - 1 - p[1])

REACTOR0 = (2, 6)
PAD0 = [(x, y) for x in range(1, 5) for y in range(4, 8)]
DELIVER0 = [(x, y) for x in range(6, 9) for y in range(5, 8)]
# The owned Wells sit on the OPPOSITE height from the spawn that owns them:
# west spawns high and farms low, east spawns low and farms high. Authored as
# the upper member of the rotation pair; its partner comes free.
WELL_NORTH = (21, 5)          # east's own Well, high on their side
WELL_SOUTH = rot(WELL_NORTH)  # west's own Well, low on ours -> (9, 17)
WELL_CENTRE = (15, 11)
HEAL_A = (18, 3)              # east's, beside their Well; rot -> (12, 19)

PROTECTED = ([REACTOR0, WELL_NORTH, WELL_SOUTH, WELL_CENTRE, HEAL_A]
             + PAD0 + DELIVER0
             + [rot(REACTOR0), rot(HEAL_A)]
             + [rot(p) for p in PAD0] + [rot(p) for p in DELIVER0])

UPPER_WALLS = [
    # base shell: the pad opens east along y5-6 and south along x3-4, so a
    # camper cannot plug both mouths at once.
    (5, 4), (1, 8), (2, 8), (5, 8),

    # the delivery shoulder sits forward of the pad and is NOT protected;
    # these give a defender cover without standing inside spawn protection.
    (9, 5), (9, 6), (6, 9), (7, 9),

    # The owned Well pocket at (21,5) -- east's. It is far from the spawn that
    # owns it, so it is OPENED toward that owner instead: the mouths face east
    # and south, its own side, while the west face a raider must come through
    # is walled. Rotation gives west the same deal at (9,17), facing west.
    (20, 4), (20, 5), (20, 6), (21, 4),

    # the north run: break the y1-2 corridor twice so the top edge is not a
    # free highway between the two halves.
    (12, 1), (12, 2), (17, 1), (17, 2), (22, 2), (23, 2),

    # the horizontal main lane bends: staggered blockers either side of the
    # west->centre line so the approach weaves instead of running straight.
    (12, 7), (13, 8), (11, 9), (14, 9),

    # centre-north lip: cover on the upper face of the centre Well, two
    # separate mouths rather than one open face.
    (13, 10), (16, 9), (17, 10),

    # heal at (18,3), beside the Well it shares a quarter with
    (17, 2), (18, 2), (19, 2), (17, 3), (17, 4),

    # east approach texture so the far half is rooms, not one hall
    (24, 4), (25, 4), (26, 6), (27, 6), (24, 8), (25, 8), (28, 9),

    # vertical seams: the two halves must talk, but not freely
    (18, 6), (18, 7), (15, 4), (15, 5),

    # west descent stagger
    (3, 10), (4, 10),

    # --- density pass ------------------------------------------------------
    # the two rows flanking the middle ARE the horizontal lane; they must
    # bend rather than run. Authoring y=10 mirrors y=12 for free.
    (7, 10), (8, 10), (20, 10), (21, 10), (26, 10),
    # the top edge is a 12-tile sprint otherwise (and mirrors to the bottom)
    (5, 1), (20, 1), (27, 1),
    # west and east midfield texture: rooms, not halls
    (6, 2), (14, 6), (22, 5), (23, 8), (27, 3),
    (2, 3), (3, 3), (13, 3), (14, 3),
    # the far shoulder of the enemy approach
    (25, 6), (19, 9), (24, 10),
]

# middle row must equal its own reversal
MIDDLE_WALLS = [4, 6, 12, 14, 16, 18, 24, 26]


def build():
    grid = [['.'] * W for _ in range(H)]
    for x in range(W):
        grid[0][x] = grid[H - 1][x] = '#'
    for y in range(H):
        grid[y][0] = grid[y][W - 1] = '#'
    for (x, y) in UPPER_WALLS:
        assert y < MID, f"authored wall {(x, y)} is not in the upper half"
        grid[y][x] = '#'
        rx, ry = rot((x, y))
        grid[ry][rx] = '#'
    for x in MIDDLE_WALLS:
        grid[MID][x] = grid[MID][W - 1 - x] = '#'
    return [''.join(r) for r in grid]


def check(rows):
    errs = []
    for y in range(H):
        for x in range(W):
            rx, ry = rot((x, y))
            if rows[y][x] != rows[ry][rx]:
                errs.append(f"chirality break at {(x, y)}")
    for p in PROTECTED:
        if rows[p[1]][p[0]] != '.':
            errs.append(f"protected tile walled: {p}")
    opens = {(x, y) for y in range(H) for x in range(W) if rows[y][x] == '.'}
    start = next(iter(opens)); seen, q = {start}, deque([start])
    while q:
        x, y = q.popleft()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                n = (x + dx, y + dy)
                if n in opens and n not in seen:
                    seen.add(n); q.append(n)
    if len(seen) != len(opens):
        errs.append(f"unreachable: {len(opens) - len(seen)} of {len(opens)}")
    return errs, len(opens)


def path_len(rows, a, b):
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
                    seen.add(n); q.append((n, d + 1))
    return None


if __name__ == '__main__':
    rows = build()
    errs, opens = check(rows)
    marks = {REACTOR0: 'R', rot(REACTOR0): 'R', WELL_NORTH: 'N',
             WELL_SOUTH: 'S', WELL_CENTRE: 'C',
             HEAL_A: 'h', rot(HEAL_A): 'h'}
    for p in DELIVER0 + [rot(q) for q in DELIVER0]:
        marks.setdefault(p, 'd')
    for p in PAD0 + [rot(q) for q in PAD0]:
        marks.setdefault(p, '_')
    print('    ' + ''.join(str(x % 10) for x in range(W)))
    for y, r in enumerate(rows):
        print(f'{y:3} ' + ''.join(marks.get((x, y), c) for x, c in enumerate(r)))
    print(f'\nopen tiles: {opens}')
    print('errors:', errs or 'none')
    b0, b1 = REACTOR0, rot(REACTOR0)
    for label, tgt in [('own delivery', (7, 6)), ('own Well (low)', WELL_SOUTH),
                       ('own heal', rot(HEAL_A)), ('centre Well', WELL_CENTRE),
                       ('enemy heal', HEAL_A), ('enemy Well (high)', WELL_NORTH),
                       ('enemy reactor', b1)]:
        d0, d1 = path_len(rows, b0, tgt), path_len(rows, b1, rot(tgt))
        flag = '' if d0 == d1 else '   <-- ASYMMETRIC'
        print(f'  team0 base -> {label:17} {d0:3}   (team1 mirror {d1}){flag}')
    print(f"  centre -> own Well:   {path_len(rows, WELL_CENTRE, WELL_SOUTH)}")
    print(f"  centre -> enemy Well: {path_len(rows, WELL_CENTRE, WELL_NORTH)}")
    print(f"  centre -> own heal:   {path_len(rows, WELL_CENTRE, rot(HEAL_A))}")
    print(f"  own Well -> own heal: {path_len(rows, WELL_SOUTH, rot(HEAL_A))}")
