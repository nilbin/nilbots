#!/usr/bin/env python3
"""The nilbots logotype, generated from one tile-grid construction.

The arena is tiles. A tile is either wall or floor, and a bot is a path across it.
So the wordmark is drawn on the arena grid and rendered three ways from the same
cell set — as the path (trace), as the wall (matter), as the floor (plan).

Letters are polylines through cell centres. Rasterising them gives the cells;
tracing the cells gives the outlines; rounding every corner — convex and concave
alike, at the radius the renderer chamfers walls with — gives the finished shape.
"""
from __future__ import annotations

U = 12          # path units per tile
R = 0.32 * U    # corner radius, matching the wall chamfer
ASC, XH, BASE = 0, 2, 6   # ascender top, x-height top, baseline (row indices)

# Each letter is one or more polylines through cell centres, plus its advance.
# Only right angles: a bot moves on the grid, so the letters do too.
LETTERS = {
    "n": (4, [[(0, 6), (0, 2), (3, 2), (3, 6)]]),
    "i": (1, [[(0, 6), (0, 2)], [(0, 0)]]),          # the tittle is the only loose tile
    "l": (1, [[(0, 6), (0, 0)]]),
    "b": (4, [[(0, 0), (0, 6), (3, 6), (3, 2), (0, 2)]]),
    "o": (4, [[(0, 2), (3, 2), (3, 6), (0, 6), (0, 2)]]),
    # A symmetrical crossbar reads as a dagger, so the arm runs twice as far right
    # as left and the stem turns out at the foot.
    "t": (4, [[(1, 1), (1, 6), (2, 6)], [(0, 2), (3, 2)]]),
    "s": (4, [[(3, 2), (0, 2), (0, 4), (3, 4), (3, 6), (0, 6)]]),
}


def layout(word: str, start: int = 0) -> list[tuple[str, int]]:
    """Place letters left to right with a one-tile gap between them."""
    out, x = [], start
    for ch in word:
        out.append((ch, x))
        x += LETTERS[ch][0] + 1
    return out


def advance(word: str) -> int:
    return sum(LETTERS[c][0] + 1 for c in word) - 1


def polylines(placed: list[tuple[str, int]]) -> list[list[tuple[int, int]]]:
    return [[(c + dx, r) for (c, r) in poly]
            for ch, dx in placed for poly in LETTERS[ch][1]]


def cells(placed: list[tuple[str, int]]) -> set[tuple[int, int]]:
    """Rasterise the skeletons: every tile a polyline passes through."""
    out: set[tuple[int, int]] = set()
    for poly in polylines(placed):
        out.add(poly[0])
        for (c1, r1), (c2, r2) in zip(poly, poly[1:]):
            step = (sign(c2 - c1), sign(r2 - r1))
            c, r = c1, r1
            while (c, r) != (c2, r2):
                c, r = c + step[0], r + step[1]
                out.add((c, r))
    return out


def sign(n: int) -> int:
    return (n > 0) - (n < 0)


# ------------------------------------------------------------------ outlines ---
def trace(cellset: set[tuple[int, int]]) -> list[list[tuple[int, int]]]:
    """Boundary loops of a polyomino, wound so material is enclosed consistently."""
    edges: dict[tuple[int, int], list[tuple[int, int]]] = {}

    def add(a, b):
        edges.setdefault(a, []).append(b)

    for (c, r) in sorted(cellset):
        if (c, r - 1) not in cellset: add((c, r), (c + 1, r))
        if (c + 1, r) not in cellset: add((c + 1, r), (c + 1, r + 1))
        if (c, r + 1) not in cellset: add((c + 1, r + 1), (c, r + 1))
        if (c - 1, r) not in cellset: add((c, r + 1), (c, r))

    loops = []
    while edges:
        start = min(edges)
        loop, cur = [start], start
        while True:
            nxt = edges[cur].pop()
            if not edges[cur]:
                del edges[cur]
            if nxt == start:
                break
            loop.append(nxt)
            cur = nxt
        loops.append(collapse(loop))
    return loops


def collapse(loop: list[tuple[int, int]]) -> list[tuple[int, int]]:
    """Drop the vertices in the middle of a straight run."""
    out, n = [], len(loop)
    for i in range(n):
        a, b, c = loop[i - 1], loop[i], loop[(i + 1) % n]
        if (b[0] - a[0], b[1] - a[1]) != (c[0] - b[0], c[1] - b[1]):
            out.append(b)
    return out


def rect(x0: float, y0: float, x1: float, y1: float) -> list[tuple[float, float]]:
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def round_loop(loop, radius=R, scale=U) -> str:
    """Cut every corner back and bridge it with a quarter arc."""
    pts = [(x * scale, y * scale) for (x, y) in loop]
    n, out = len(pts), []
    for i in range(n):
        p, prev, nxt = pts[i], pts[i - 1], pts[(i + 1) % n]
        d1, l1 = direction(prev, p)
        d2, l2 = direction(p, nxt)
        rr = min(radius, l1 / 2, l2 / 2)
        a = (p[0] - d1[0] * rr, p[1] - d1[1] * rr)
        b = (p[0] + d2[0] * rr, p[1] + d2[1] * rr)
        sweep = 1 if d1[0] * d2[1] - d1[1] * d2[0] > 0 else 0
        out.append(f"{'M' if i == 0 else 'L'}{fmt(a)}")
        out.append(f"A{num(rr)} {num(rr)} 0 0 {sweep} {fmt(b)}")
    return " ".join(out) + " Z"


def direction(a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = abs(dx) + abs(dy)
    return ((dx / length, dy / length), length)


def num(v: float) -> str:
    return f"{v:.2f}".rstrip("0").rstrip(".")


def fmt(p) -> str:
    return f"{num(p[0])} {num(p[1])}"


def stroke_path(placed) -> str:
    """The skeleton itself, ready to be stroked one tile wide."""
    out = []
    for poly in polylines(placed):
        pts = [((c + 0.5) * U, (r + 0.5) * U) for (c, r) in poly]
        # A lone point needs the degenerate lineto, or the round cap draws nothing.
        tail = pts[1:] or pts
        out.append("M" + fmt(pts[0]) + "".join("L" + fmt(p) for p in tail))
    return " ".join(out)


# --------------------------------------------------------------------- SVGs ---
def svg(view, body, cls="", extra="") -> str:
    x0, y0, x1, y1 = view
    box = f"{num(x0 * U)} {num(y0 * U)} {num((x1 - x0) * U)} {num((y1 - y0) * U)}"
    klass = f' class="{cls}"' if cls else ""
    return (f'<svg{klass} viewBox="{box}" fill="none" '
            f'xmlns="http://www.w3.org/2000/svg" aria-hidden="true"{extra}>{body}</svg>')


def solid(placed, view=None, cls="", pad=0.0) -> str:
    """Matter: the cells, filled and milled."""
    word = cells(placed)
    d = " ".join(round_loop(loop) for loop in trace(word))
    view = view or bounds(word, pad)
    return svg(view, f'<path d="{d}" fill="currentColor" fill-rule="evenodd"/>', cls)


def void(placed, pad=1.0, cls="", slab=None) -> str:
    """Plan: the same cells cut out of a wall slab, so the letters are floor."""
    word = cells(placed)
    x0, y0, x1, y1 = slab or bounds(word, pad)
    d = round_loop(rect(x0, y0, x1, y1))
    d += " " + " ".join(round_loop(loop) for loop in trace(word))
    return svg((x0, y0, x1, y1),
               f'<path d="{d}" fill="currentColor" fill-rule="evenodd"/>', cls)


def line(placed, view=None, cls="", pad=0.0) -> str:
    """Trace: one tile wide, round-capped — a route rather than a shape."""
    view = view or bounds(cells(placed), pad)
    return svg(view,
               f'<path d="{stroke_path(placed)}" stroke="currentColor" '
               f'stroke-width="{U}" stroke-linecap="round" stroke-linejoin="round"/>',
               cls)


def bounds(cellset, pad=0.0):
    xs = [c for c, _ in cellset]
    ys = [r for _, r in cellset]
    return (min(xs) - pad, min(ys) - pad, max(xs) + 1 + pad, max(ys) + 1 + pad)


def ground(word="nilbots", split=3, pad=1.0) -> str:
    """Nil is absence, bots is matter: the first syllable is cut out of a block.

    The slab's right edge is set a full tile clear of the b, so the block reads as
    a letter in the sequence rather than a box the word is sitting in.
    """
    lw = advance(word[:split])
    left = layout(word[:split])
    right = layout(word[split:], int(lw + pad + 1))
    slab = (-pad, -pad, lw + pad, BASE + 1 + pad)
    d = round_loop(rect(*slab))
    d += " " + " ".join(round_loop(loop) for loop in trace(cells(left)))
    d += " " + " ".join(round_loop(loop) for loop in trace(cells(right)))
    view = (slab[0], slab[1], lw + pad + 1 + advance(word[split:]), slab[3])
    return svg(view, f'<path d="{d}" fill="currentColor" fill-rule="evenodd"/>')


def mark(finish="plan") -> str:
    """The mark: n on its own. In plan it is a corridor cut through a wall block —
    a fragment of arena that happens to be the first letter."""
    placed = layout("n")
    if finish == "plan":
        slab = (-2, 0.5, 6, 8.5)          # square, and clear of the iOS corner mask
        return void(placed, cls="mk", slab=slab)
    view = (-0.5, 1.5, 4.5, 7.5)
    return solid(placed, view=view) if finish == "matter" else line(placed, view=view)


def running(placed, seconds=1.9) -> str:
    """Trace, drawn stroke by stroke at a constant speed — the word as a route
    somebody runs. The pen never changes pace, so each stroke's duration is just
    its own length."""
    strokes, run = [], 0.0
    for poly in polylines(placed):
        pts = [((c + 0.5) * U, (r + 0.5) * U) for (c, r) in poly]
        length = sum(abs(b[0] - a[0]) + abs(b[1] - a[1]) for a, b in zip(pts, pts[1:]))
        d = "M" + fmt(pts[0]) + "".join("L" + fmt(p) for p in (pts[1:] or pts))
        strokes.append((d, length, run))
        run += length + U * 1.6     # the hop to the next stroke costs time too

    rate = seconds / run
    parts = []
    for d, length, at in strokes:
        delay = f"animation-delay:{num(at * rate)}s"
        if length:
            parts.append(f'<path class="run" d="{d}" style="--len:{num(length)};'
                         f'animation-duration:{num(length * rate)}s;{delay}"/>')
        else:
            parts.append(f'<path class="pip" d="{d}" style="{delay}"/>')
    body = (f'<g fill="none" stroke="currentColor" stroke-width="{U}" '
            f'stroke-linecap="round" stroke-linejoin="round">{"".join(parts)}</g>')
    return svg(bounds(cells(placed)), body, cls="wm runwm")


def construction(word="nilbots") -> str:
    """The grid, the skeleton and the tiles it lands on, drawn over each other."""
    placed = layout(word)
    word_cells = cells(placed)
    x0, y0, x1, y1 = bounds(word_cells, 1)
    parts = []
    grid = []
    for c in range(int(x0), int(x1) + 1):
        grid.append(f"M{num(c * U)} {num(y0 * U)}V{num(y1 * U)}")
    for r in range(int(y0), int(y1) + 1):
        grid.append(f"M{num(x0 * U)} {num(r * U)}H{num(x1 * U)}")
    parts.append(f'<path d="{" ".join(grid)}" stroke="currentColor" stroke-width="0.5" opacity=".16"/>')
    tiles = " ".join(f"M{num(c * U)} {num(r * U)}h{U}v{U}h-{U}Z" for c, r in sorted(word_cells))
    parts.append(f'<path d="{tiles}" fill="currentColor" opacity=".13"/>')
    for row in (XH, BASE + 1):
        parts.append(f'<path d="M{num(x0 * U)} {num(row * U)}H{num(x1 * U)}" '
                     f'stroke="currentColor" stroke-width="0.8" opacity=".38"/>')
    parts.append(f'<path d="{stroke_path(placed)}" stroke="currentColor" stroke-width="1.6" '
                 f'stroke-linecap="round" stroke-linejoin="round" opacity=".9"/>')
    dots = []
    for poly in polylines(placed):
        for (c, r) in poly:
            dots.append(f'<circle cx="{num((c + .5) * U)}" cy="{num((r + .5) * U)}" r="2" fill="currentColor"/>')
    parts.append("".join(dots))
    return svg((x0, y0, x1, y1), "".join(parts))
