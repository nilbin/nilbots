#!/usr/bin/env python3
"""The two data graphics in the views section, drawn from real numbers.

The timeline uses the actual event list of bastion-01 seed 4711 (39 ticks,
Pincer gen-10 v Bastille gen-5) as read out of the running review build, so the
lumpiness is the match's own rather than something evenly spaced to flatter the
design.
"""

PINCER, BASTILLE = "#22d3ee", "#ef4444"
DIM, INK = "#513a2b", "#e6ddd4"

TICKS = 39
PLAYHEAD = 30

# tick -> what happened, per bot
FIRES = {"p": [23, 30, 33], "b": [14]}
HITS = {"p": [20], "b": [20, 27, 38]}          # damage received
KILLED = {"b": 38}


def timeline(width=620, height=48) -> str:
    pad = 14
    span = width - pad * 2

    def x(t):
        return pad + t / TICKS * span

    lanes = {"p": (15, PINCER), "b": (33, BASTILLE)}
    out = []

    # The elapsed part of each lane carries the bot's accent; the rest is structure.
    for key, (y, accent) in lanes.items():
        out.append(f'<line x1="{pad}" y1="{y}" x2="{width - pad}" y2="{y}" '
                   f'stroke="{DIM}" stroke-width="1.5" stroke-linecap="round"/>')
        out.append(f'<line x1="{pad}" y1="{y}" x2="{x(PLAYHEAD):.1f}" y2="{y}" '
                   f'stroke="{accent}" stroke-width="1.5" stroke-linecap="round" opacity=".5"/>')

    for key, (y, accent) in lanes.items():
        up = -1 if key == "p" else 1
        for t in FIRES[key]:                     # a shot: a hairline off the lane
            out.append(f'<line x1="{x(t):.1f}" y1="{y}" x2="{x(t):.1f}" y2="{y + up * 8}" '
                       f'stroke="{accent}" stroke-width="1.6" opacity=".75"/>')
        for t in HITS[key]:                      # a hit taken: a notch on the lane
            out.append(f'<rect x="{x(t) - 2.5:.1f}" y="{y - 5.5}" width="5" height="11" rx="2" '
                       f'fill="{accent}"/>')
        if key in KILLED:                        # a destruction: a block, and a rule
            t = KILLED[key]
            out.append(f'<line x1="{x(t):.1f}" y1="6" x2="{x(t):.1f}" y2="{height - 6}" '
                       f'stroke="{accent}" stroke-width="1" opacity=".4" stroke-dasharray="2 3"/>')
            out.append(f'<rect x="{x(t) - 5:.1f}" y="{y - 5}" width="10" height="10" rx="2.5" '
                       f'fill="{accent}"/>')

    px = x(PLAYHEAD)
    out.append(f'<line x1="{px:.1f}" y1="4" x2="{px:.1f}" y2="{height - 4}" '
               f'stroke="{INK}" stroke-width="1.5"/>')
    out.append(f'<circle cx="{px:.1f}" cy="{height / 2}" r="4.5" fill="{INK}"/>')

    return (f'<svg viewBox="0 0 {width} {height}" role="img" '
            f'aria-label="Timeline of 39 ticks. Pincer gen-10 fired on ticks 23, 30 and 33 and '
            f'took one hit on tick 20. Bastille gen-5 fired once on tick 14, took hits on ticks '
            f'20, 27 and 38, and was destroyed on tick 38. The playhead is on tick 30.">'
            + "".join(out) + "</svg>")


def generations(width=1120, height=210) -> str:
    """Three generations of one bot on a single rating axis."""
    lines = {
        "gen-8":  (DIM, [1150, 1163, 1171, 1168, 1182, 1190, 1198]),
        "gen-9":  (DIM, [1198, 1188, 1205, 1214, 1209, 1226, 1231]),
        "gen-10": (PINCER, [1231, 1226, 1244, 1252, 1249, 1268, 1284]),
    }
    lo, hi = 1130, 1300
    pad_l, pad_r, pad_t, pad_b = 52, 14, 16, 28
    w = width - pad_l - pad_r
    h = height - pad_t - pad_b

    def y(rating):
        return pad_t + (hi - rating) / (hi - lo) * h

    out = []
    for grid in (1300, 1200):
        out.append(f'<line x1="{pad_l}" y1="{y(grid):.1f}" x2="{width - pad_r}" y2="{y(grid):.1f}" '
                   f'stroke="#33241b" stroke-width="1"/>')
        out.append(f'<text x="6" y="{y(grid) + 3.5:.1f}" fill="#8a7561" font-size="11.5" '
                   f'style="font-family:var(--t-mono)">{grid}</text>')

    # Each generation occupies its own third of the axis: they are consecutive, not concurrent.
    for i, (name, (colour, series)) in enumerate(lines.items()):
        seg = w / 3
        pts = " ".join(
            f"{pad_l + i * seg + n / (len(series) - 1) * seg:.1f},{y(v):.1f}"
            for n, v in enumerate(series))
        live = name == "gen-10"
        out.append(f'<polyline points="{pts}" fill="none" stroke="{colour}" '
                   f'stroke-width="{2.4 if live else 1.7}" stroke-linejoin="round"/>')
        end = pts.split()[-1].split(",")
        out.append(f'<circle cx="{end[0]}" cy="{end[1]}" r="{4.5 if live else 3.5}" fill="#0b0705" '
                   f'stroke="{colour}" stroke-width="{2.5 if live else 1.6}"/>')
        out.append(f'<text x="{pad_l + i * seg + 2:.1f}" y="{height - 6}" fill="#8a7561" '
                   f'font-size="11.5" style="font-family:var(--t-mono)">{name}</text>')
        if i:
            xd = pad_l + i * seg
            out.append(f'<line x1="{xd:.1f}" y1="{pad_t}" x2="{xd:.1f}" y2="{pad_t + h:.1f}" '
                       f'stroke="#33241b" stroke-width="1" stroke-dasharray="2 4"/>')

    return (f'<svg class="chart" viewBox="0 0 {width} {height}" role="img" '
            f'aria-label="Rating across three generations of Pincer: gen-8 ends at 1198, '
            f'gen-9 at 1231, and the live gen-10 at 1284.">' + "".join(out) + "</svg>")
