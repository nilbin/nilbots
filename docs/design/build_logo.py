#!/usr/bin/env python3
"""Compose the logotype page from the generated marks."""
import re
from pathlib import Path

import logotype as L
import typeface

here = Path(__file__).parent
word = L.layout("nilbots")

TOKENS = {
    "typography": typeface.block(),
    "run": L.running(word),
    "trace": L.line(word, cls="wm"),
    "matter": L.solid(word, cls="wm"),
    "plan": L.void(word, cls="wm tall"),
    "ground": L.ground().replace("<svg ", '<svg class="wm tall" ', 1),
    "construction": L.construction(),
    "mark_plan": L.mark("plan"),
    "mark_matter": L.mark("matter"),
    "mark_trace": L.mark("trace"),
    # Every glyph shares the full seven-row body so they set at one scale.
    "alphabet": "".join(
        f'<figure><span class="cap">{ch}</span>'
        f'{L.solid(L.layout(ch), view=(0, 0, L.LETTERS[ch][0], 7), cls="glyph")}</figure>'
        for ch in "nilbots"
    ),
}

html = (here / "logo.template.html").read_text()
for key, value in TOKENS.items():
    html = html.replace("{{" + key + "}}", value)

leftover = sorted(set(re.findall(r"\{\{[a-z_]+\}\}", html)))
if leftover:
    raise SystemExit(f"unresolved: {leftover}")

(here / "logotype.html").write_text(html)
print(f"wrote logotype.html — {len(html)} bytes")
