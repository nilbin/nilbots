#!/usr/bin/env python3
"""Compose the redesign study, inlining the real chassis sprites once as <symbol>s.

The sprites are the product's own art; a coloured square stood in for them in the first
pass and read as a placeholder, which it was. Defined once and referenced with <use> so
eight directions can show them without eight copies of the path data.
"""
import json
import re
from pathlib import Path

here = Path(__file__).parent
looks = json.loads((here / "looks.json").read_text())

# Turn each standalone <svg> into a <symbol> for the sheet.
symbols = []
for name, svg in looks.items():
    inner = re.sub(r"^<svg[^>]*>", "", svg)
    inner = re.sub(r"</svg>$", "", inner)
    symbols.append(f'<symbol id="look-{name}" viewBox="0 0 512 512">{inner}</symbol>')
sheet = (
    '<svg aria-hidden="true" focusable="false" '
    'style="position:absolute;width:0;height:0;overflow:hidden"><defs>'
    + "".join(symbols)
    + "</defs></svg>"
)


def look(name, size=22):
    return (
        f'<svg class="look" width="{size}" height="{size}" aria-hidden="true">'
        f'<use href="#look-{name}"></use></svg>'
    )


def who(name, chassis, sub=None, size=22, extra=""):
    s = f'<span class="s">{sub}</span>' if sub else ""
    return (
        f'<span class="who">{look(chassis, size)}'
        f'<span class="stack"><span class="n">{name}</span>{s}</span>{extra}</span>'
    )


HTML = (here / "page.template.html").read_text()
HTML = HTML.replace("<!--SPRITE-SHEET-->", sheet)

# Every identity chip in the page, resolved from one helper so they cannot drift.
tokens = {
    "{{who_pincer}}": who("Pincer gen-10", "vanguard"),
    "{{who_pincer_sub}}": who("Pincer gen-10", "vanguard", "your best · Vanguard", 26),
    "{{who_pincer_cli}}": who("Pincer gen-10", "vanguard", "nilbots submit · 4h ago · sdk 0.5.6", 26),
    "{{who_warden}}": who("Warden gen-1", "aureate-warden"),
    "{{who_warden_n}}": who("Warden gen-1", "needle"),
    "{{who_bastille}}": who("Bastille gen-5", "bulwark"),
    "{{who_rampart}}": who("Rampart gen-2", "orbiter"),
    "{{look_vanguard_lg}}": look("vanguard", 64),
    "{{look_needle_lg}}": look("needle", 64),
    "{{look_bulwark_lg}}": look("bulwark", 64),
    "{{look_orbiter_lg}}": look("orbiter", 64),
    "{{look_mantis_lg}}": look("mantis", 64),
    "{{look_warden_lg}}": look("aureate-warden", 64),
    "{{look_vanguard_xl}}": look("vanguard", 108),
    "{{look_mantis_xl}}": look("mantis", 96),
    "{{look_vanguard_sm}}": look("vanguard", 18),
    "{{look_needle_sm}}": look("needle", 18),
    "{{look_bulwark_sm}}": look("bulwark", 18),
    "{{look_orbiter_sm}}": look("orbiter", 18),
}
for key, value in tokens.items():
    HTML = HTML.replace(key, value)

leftover = re.findall(r"\{\{[a-z_]+\}\}", HTML)
if leftover:
    raise SystemExit(f"unresolved placeholders: {sorted(set(leftover))}")

(here / "directions.html").write_text(HTML)
print(f"wrote {len(HTML)} bytes, {len(symbols)} sprites inlined")
