#!/usr/bin/env python3
"""Compose the Climb specification page, reusing the inlined chassis sprites."""
import json
import re
from pathlib import Path

import base64

import charts
import logotype
import typeface

here = Path(__file__).parent
looks = json.loads((here / "looks.json").read_text())

symbols = []
for name, svg in looks.items():
    inner = re.sub(r"^<svg[^>]*>", "", svg)
    inner = re.sub(r"</svg>$", "", inner)
    symbols.append(f'<symbol id="look-{name}" viewBox="0 0 512 512">{inner}</symbol>')
sheet = (
    '<svg aria-hidden="true" focusable="false" '
    'style="position:absolute;width:0;height:0;overflow:hidden"><defs>'
    + "".join(symbols) + "</defs></svg>"
)


def look(name, size=22):
    return (f'<svg class="look" width="{size}" height="{size}" aria-hidden="true">'
            f'<use href="#look-{name}"></use></svg>')


def bot(name, chassis, accent, sub=None, size=22):
    """Identity chip: the chassis the player chose, ringed in the accent they chose."""
    s = f'<span class="s">{sub}</span>' if sub else ""
    return (
        f'<span class="who"><span class="ident" style="--accent:{accent}">{look(chassis, size)}</span>'
        f'<span class="stack"><span class="n">{name}</span>{s}</span></span>'
    )


def data_uri(name: str) -> str:
    """Inlined because the artifact CSP blocks every external host."""
    raw = (here / "shots" / name).read_bytes()
    return "data:image/jpeg;base64," + base64.b64encode(raw).decode()


HTML = (here / "climb.template.html").read_text()
HTML = HTML.replace("<!--SPRITE-SHEET-->", sheet)
HTML = HTML.replace("<!--VIEWS-->", (here / "views.fragment.html").read_text())

word = logotype.layout("nilbots")

tokens = {
    "{{typography}}": typeface.block(),
    # The logotype, generated from the tile grid — see logotype.py.
    "{{wordmark}}": logotype.solid(word, cls="wm"),
    "{{plan}}": logotype.void(word, cls="wm tall"),
    "{{ground}}": logotype.ground().replace("<svg ", '<svg class="wm tall" ', 1),
    "{{mark}}": logotype.mark("plan"),
    "{{arena_hero}}": data_uri("arena-hero.jpg"),
    "{{arena_alt}}": data_uri("arena-alt.jpg"),
    "{{timeline}}": charts.timeline(),
    "{{gens_chart}}": charts.generations(),
    "{{bot_pincer_sm}}": bot("Pincer gen-10", "vanguard", "#22d3ee", size=20),
    "{{bot_roomba_sm}}": bot("Murder Roomba", "mantis", "#f5a623", size=20),
    "{{bot_sprig_sm}}": bot("Sprig gen-2", "bulwark", "#a855f7", size=20),
    "{{bot_bastille_sm}}": bot("Bastille gen-5", "bulwark", "#ef4444", size=20),
    "{{bot_bastille_lg}}": bot("Bastille gen-5", "bulwark", "#ef4444", "Bulwark · slot 1", 34),
    "{{bot_pincer_hero}}": bot("Pincer gen-10", "vanguard", "#22d3ee",
                               "Vanguard · rank 7 · rating 1284", 52),
    "{{bot_pincer}}": bot("Pincer gen-10", "vanguard", "#22d3ee"),
    "{{bot_pincer_lg}}": bot("Pincer gen-10", "vanguard", "#22d3ee", "Vanguard · rank 7", 34),
    "{{bot_roomba}}": bot("Murder Roomba", "mantis", "#f5a623"),
    "{{bot_roomba_lg}}": bot("Murder Roomba", "mantis", "#f5a623", "Mantis · rank 22", 34),
    "{{bot_sprig}}": bot("Sprig gen-2", "bulwark", "#a855f7"),
    "{{bot_sprig_lg}}": bot("Sprig gen-2", "bulwark", "#a855f7", "Bulwark · unranked", 34),
    "{{bot_warden}}": bot("Warden gen-1", "aureate-warden", "#7dd3fc"),
    "{{bot_bastille}}": bot("Bastille gen-5", "bulwark", "#ef4444"),
    "{{bot_rampart}}": bot("Rampart gen-2", "orbiter", "#bef264"),
    "{{bot_halyard}}": bot("Halyard gen-3", "needle", "#fb7185"),
    "{{look_vanguard_24}}": look("vanguard", 24),
    "{{look_mantis_24}}": look("mantis", 24),
    "{{look_bulwark_24}}": look("bulwark", 24),
}
for key, value in tokens.items():
    HTML = HTML.replace(key, value)

leftover = re.findall(r"\{\{[a-z_]+\}\}", HTML)
if leftover:
    raise SystemExit(f"unresolved: {sorted(set(leftover))}")

(here / "climb.html").write_text(HTML)
print(f"wrote {len(HTML)} bytes")
