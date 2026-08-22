#!/usr/bin/env python3
"""Tactical-geometry verification battery for the Broadloom candidate.

Checks the gates that route distance alone cannot: spawn-protected
projection onto neutral objectives, one-/two-Mason workshop seals with
bounded detours, trainee facing information, straight interrupt lanes,
Null Field blanket coverage, and Dash/Toss progress spans.
"""
import importlib.util
from collections import deque
from itertools import combinations
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    'bb', Path(__file__).resolve().parent / 'build.py')
bb = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bb)

rows = bb.build()
W, H = bb.W, bb.H
open_t = {(x, y) for y in range(H) for x in range(W) if rows[y][x] == '.'}
is_open = lambda p: p in open_t


def supercover(a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    nx, ny, sx, sy = abs(dx), abs(dy), (dx > 0) - (dx < 0), (dy > 0) - (dy < 0)
    x, y = a
    out = [a]
    ix = iy = 0
    while ix < nx or iy < ny:
        cx, cy = (1 + 2 * ix) * ny, (1 + 2 * iy) * nx
        if cx == cy:
            out += [(x + sx, y), (x, y + sy)]
            x += sx; y += sy; ix += 1; iy += 1
        elif cx < cy:
            x += sx; ix += 1
        else:
            y += sy; iy += 1
        out.append((x, y))
    return out


def los(a, b):
    return all(p in (a, b) or is_open(p) for p in supercover(a, b))


cheb = lambda a, b: max(abs(a[0] - b[0]), abs(a[1] - b[1]))


def bfs_dist(src, blocked=frozenset()):
    dist = {src: 0}
    q = deque([src])
    while q:
        c = q.popleft()
        for n in bb.neighbours(rows, c):
            if n not in dist and n not in blocked:
                dist[n] = dist[c] + 1
                q.append(n)
    return dist


PADS = {0: bb.PAD0, 1: [bb.rot(p) for p in bb.PAD0]}
NEUTRAL = {
    'N workshop': bb.WORKSHOP_N, 'S workshop': bb.WORKSHOP_S,
    'centre well': bb.WELL_CENTRE,
    'W own well': bb.WELL_OWN0, 'E own well': bb.WELL_OWN1,
    'W heal': bb.HEAL_W, 'E heal': bb.rot(bb.HEAL_W),
}

print('== spawn-protected projection audit (rail 12 needs LOS; vision 5) ==')
issues = 0
for team, pad in PADS.items():
    for name, tgt in NEUTRAL.items():
        hits = [p for p in pad if is_open(p) and cheb(p, tgt) <= 12 and los(p, tgt)]
        seen = [p for p in pad if is_open(p) and cheb(p, tgt) <= 5 and los(p, tgt)]
        if hits or seen:
            issues += 1
            print(f'  team{team} pad -> {name}: rail-capable {hits[:4]} vision {seen[:4]}')
print('  none' if not issues else f'  {issues} findings')

print('== workshop seal audit ==')
for wname, wtile in (('N', bb.WORKSHOP_N), ('S', bb.WORKSHOP_S)):
    # mouths: open tiles adjacent to the room boundary; take every open tile
    # within Chebyshev 3 of the workshop that has an open neighbour further
    # than 3 (i.e. door-zone tiles), then test all 1- and 2-tile removals.
    zone = [p for p in open_t if cheb(p, wtile) <= 3]
    base = bfs_dist(wtile)
    reach_all = {p for p in open_t if p in base}
    origins = [bb.REACTOR0, bb.rot(bb.REACTOR0), bb.WELL_CENTRE]
    cands = sorted({p for p in zone if p != wtile})
    worst1 = worst2 = None
    sealed2 = []
    for r in cands:
        d = bfs_dist(wtile, blocked=frozenset([r]))
        for o in origins:
            if o not in d:
                print(f'  {wname}: single removal {r} disconnects {o}!')
            else:
                grow = d[o] - base[o]
                if worst1 is None or grow > worst1[0]:
                    worst1 = (grow, r, o)
    for r1, r2 in combinations(cands, 2):
        d = bfs_dist(wtile, blocked=frozenset([r1, r2]))
        for o in origins:
            if o not in d:
                sealed2.append((r1, r2, o))
            else:
                grow = d[o] - base[o]
                if worst2 is None or grow > worst2[0]:
                    worst2 = (grow, r1, r2, o)
    print(f'  {wname}: worst 1-seal detour +{worst1[0]} steps (seal {worst1[1]} vs {worst1[2]})')
    if sealed2:
        print(f'  {wname}: {len(sealed2)} 2-seal DISCONNECTS e.g. {sealed2[:3]}')
    else:
        print(f'  {wname}: no 2-seal disconnect; worst 2-seal detour +{worst2[0]} '
              f'(seals {worst2[1]},{worst2[2]} vs {worst2[3]})')

print('== workshop facing audit (vision 5, facing quadrant, from training tile) ==')
VEC = {'north': (0, -1), 'east': (1, 0), 'south': (0, 1), 'west': (-1, 0)}
def visible(origin, facing, rng=5):
    out = set()
    fx, fy = VEC[facing]
    for x in range(W):
        for y in range(H):
            t = (x, y)
            d = cheb(origin, t)
            if d == 0 or d > rng or not is_open(t):
                continue
            dx, dy = x - origin[0], y - origin[1]
            fwd = dx * fx + dy * fy
            lat = abs(dx * fy) + abs(dy * fx)
            if fwd >= 0 and lat <= fwd and los(origin, t):
                out.add(t)
    return out

MOUTHS_N = {
    'W mouth': [(14, 3), (14, 4)],
    'S antechamber': [(16, 7), (17, 7), (18, 7), (19, 7), (20, 7)],
    'E mouth': [(22, 4), (22, 5), (22, 6)],
    'skylight': [(18, 2)],
}
for facing in VEC:
    vis = visible(bb.WORKSHOP_N, facing)
    got = [m for m, tiles in MOUTHS_N.items() if any(t in vis for t in tiles)]
    print(f'  trainee facing {facing}: sees {got or "none"}')

print('== longest straight open lanes through the training tile ==')
for name, tile in (('N', bb.WORKSHOP_N), ('S', bb.WORKSHOP_S)):
    x0, y0 = tile
    h = v = 1
    x = x0 - 1
    while is_open((x, y0)): h += 1; x -= 1
    x = x0 + 1
    while is_open((x, y0)): h += 1; x += 1
    y = y0 - 1
    while is_open((x0, y)): v += 1; y -= 1
    y = y0 + 1
    while is_open((x0, y)): v += 1; y += 1
    print(f'  {name}: horizontal {h}, vertical {v}')

print('== null-field blanket check (radius 3 disc vs site floor) ==')
for name, tile in (('N workshop', bb.WORKSHOP_N), ('centre', bb.WELL_CENTRE),
                   ('W heal', bb.HEAL_W)):
    floor = [p for p in open_t if cheb(p, tile) <= 3]
    esc = [p for p in open_t if cheb(p, tile) == 4]
    print(f'  {name}: {len(floor)} tiles inside radius-3, {len(esc)} escape ring tiles')

print('== key straight spans for Dash(4)/Toss(5) on return lanes ==')
def span(a, d, n):
    x, y = a
    seq = [(x + d[0] * i, y + d[1] * i) for i in range(n + 1)]
    return all(is_open(p) for p in seq)
checks = [
    ('mid corridor W toss  (12,13)->(12,... )', (12, 11), (0, 1), 4),
    ('gallery dash x12-16 y1', (12, 1), (1, 0), 4),
    ('NE field toss (26,1)->(31,1)', (26, 1), (1, 0), 5),
    ('S band toss (24,17)->(29,17)', (24, 17), (1, 0), 5),
]
for label, a, d, n in checks:
    print(f'  {label}: {"ok" if span(a, d, n) else "NO"}')
