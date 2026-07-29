#!/usr/bin/env python3
"""The shared type system for both spec pages.

One family, three cuts, plus one machine voice. Archivo carries a width axis, so
the condensed labels, the running text and the expanded display all come out of a
single variable file — the same economy as one cell set with three finishes. Mono
is demoted to exactly one job: values a machine wrote.
"""
import base64
from pathlib import Path

here = Path(__file__).parent


def _faces() -> str:
    """Inline both faces: the artifact CSP blocks CDNs, and a self-contained page is
    what makes this openable straight from disk."""
    out = []
    for family, weights, stretch, name in (
        ("Archivo", "400 700", "  font-stretch:75% 125%;\n", "archivo.woff2"),
        ("Geist Mono", "400 500", "", "mono.woff2"),
    ):
        data = (here / "fonts" / name).read_bytes()
        out.append(
            f'  @font-face {{ font-family:"{family}"; font-style:normal;\n'
            f'               font-weight:{weights};\n{stretch}'
            f'               font-display:swap;\n'
            f'               src:url(data:font/woff2;base64,'
            f'{base64.b64encode(data).decode()}) format("woff2"); }}\n')
    return "".join(out)


CHROME = """
  :root {
    --sans:"Archivo", system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
    --mono:"Geist Mono", ui-monospace, "SF Mono", Menlo, Consolas, monospace;
  }

  /* The three cuts. Width does the work that a second family or a mono usually
     does: condensed for labels, normal for reading, expanded for display. */
  .eyebrow, section > h2 { font-family:var(--sans); font-stretch:82%; font-weight:550;
                           text-transform:uppercase; letter-spacing:.15em; }
  h1, section > h3 { font-family:var(--sans); font-stretch:117%; font-weight:640;
                     letter-spacing:-.022em; text-wrap:balance; }

  .eyebrow { font-size:12px; color:var(--ink-2); margin:0 0 14px; }
  section > h2 { font-size:12px; color:var(--ink-2); margin:0 0 6px; }
  h1 { font-size:clamp(30px,4.4vw,46px); line-height:1.06; margin:0 0 18px; }
  section > h3 { font-size:26px; line-height:1.16; margin:0 0 12px; }

  /* Mono means a machine wrote it: ratings, deltas, ticks, seeds, identifiers,
     code. Never a label, a name, a button or a sentence. */
  code, .val { font-family:var(--mono); font-size:.88em; font-variant-numeric:tabular-nums; }
"""


def block() -> str:
    return _faces() + CHROME
