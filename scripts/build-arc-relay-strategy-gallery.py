#!/usr/bin/env python3
"""Build the self-contained Arc Relay Tapline strategy concept gallery.

The Markdown catalog is authoritative. This builder parses its six strategy
families and four required fields per brief, validates the 36-entry boundary,
and embeds the result into one dependency-free HTML review artifact.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


REPO = Path(__file__).resolve().parent.parent
SOURCE = REPO / "docs/ARC-RELAY-TAPLINE-SHEET-STRATEGY-CATALOG.md"
OUTPUT = REPO / "art/reviews/arc-relay-tapline-strategy-gallery/index.html"
RECOMMENDED = {3, 7, 13, 16, 20, 23, 25, 30}
REQUIRED_FIELDS = {
    "Roster",
    "Thesis",
    "Why it earns a sheet",
    "Failure/counterplay",
}
CATEGORY_META = {
    "A": {"slug": "charge", "short": "Charge routes", "mark": "1·2·4"},
    "B": {"slug": "logistics", "short": "Theft & logistics", "mark": "↗"},
    "C": {"slug": "veteran", "short": "Veteran command", "mark": "★"},
    "D": {"slug": "wave", "short": "Respawn waves", "mark": "30"},
    "E": {"slug": "deployment", "short": "Deployments", "mark": "▦"},
    "F": {"slug": "hybrid", "short": "Adaptive hybrids", "mark": "◇"},
}


def clean_inline(value: str) -> str:
    value = re.sub(r"`([^`]+)`", r"\1", value)
    return re.sub(r"\s+", " ", value).strip()


def parse_catalog() -> tuple[list[dict], list[dict]]:
    lines = SOURCE.read_text(encoding="utf-8").splitlines()
    categories: list[dict] = []
    strategies: list[dict] = []
    current_category: dict | None = None
    index = 0

    while index < len(lines):
        category_match = re.match(r"^### ([A-F])\. (.+)$", lines[index])
        if category_match:
            key, title = category_match.groups()
            current_category = {
                "key": key,
                "title": title,
                **CATEGORY_META[key],
            }
            categories.append(current_category)
            index += 1
            continue

        strategy_match = re.match(r"^#### (\d{2}) — (.+)$", lines[index])
        if not strategy_match:
            index += 1
            continue
        if current_category is None:
            raise ValueError(f"{SOURCE}: strategy before category at line {index + 1}")

        number_text, name = strategy_match.groups()
        fields: dict[str, str] = {}
        index += 1
        while index < len(lines) and not lines[index].startswith("#### ") \
                and not lines[index].startswith("### ") \
                and not lines[index].startswith("## "):
            field_match = re.match(r"^- \*\*(.+?):\*\*\s*(.*)$", lines[index])
            if not field_match:
                index += 1
                continue
            label, first = field_match.groups()
            parts = [first]
            index += 1
            while index < len(lines):
                line = lines[index]
                if re.match(r"^- \*\*(.+?):\*\*", line) or line.startswith("#"):
                    break
                if line.strip():
                    parts.append(line.strip())
                index += 1
            fields[label] = clean_inline(" ".join(parts))

        missing = REQUIRED_FIELDS - fields.keys()
        if missing:
            raise ValueError(
                f"{SOURCE}: strategy {number_text} missing {sorted(missing)}"
            )
        number = int(number_text)
        strategies.append(
            {
                "id": number,
                "number": number_text,
                "name": name,
                "category": current_category["slug"],
                "categoryKey": current_category["key"],
                "categoryTitle": current_category["short"],
                "recommended": number in RECOMMENDED,
                "roster": fields["Roster"].removesuffix("."),
                "thesis": fields["Thesis"],
                "why": fields["Why it earns a sheet"],
                "counter": fields["Failure/counterplay"],
            }
        )

    if [strategy["id"] for strategy in strategies] != list(range(1, 37)):
        raise ValueError(f"{SOURCE}: expected exact strategy ids 01..36")
    if len(categories) != 6:
        raise ValueError(f"{SOURCE}: expected six strategy families")
    if sum(strategy["recommended"] for strategy in strategies) != 8:
        raise ValueError("expected exactly eight first-authoring strategies")
    return categories, strategies


def render() -> str:
    categories, strategies = parse_catalog()
    payload = json.dumps(
        {"categories": categories, "strategies": strategies},
        ensure_ascii=False,
        separators=(",", ":"),
    ).replace("</", "<\\/")
    return HTML.replace("__STRATEGY_DATA__", payload)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="fail when the tracked gallery is not current",
    )
    args = parser.parse_args()
    rendered = render()
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text(encoding="utf-8") != rendered:
            raise SystemExit(f"{OUTPUT.relative_to(REPO)} is stale; rebuild it")
        print("strategy gallery is current")
        return 0
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(rendered, encoding="utf-8")
    print(f"wrote {OUTPUT.relative_to(REPO)}")
    return 0


HTML = r'''<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="theme-color" content="#081012">
  <title>Tapline strategy atlas · Nilbots</title>
  <style>
    :root {
      color-scheme: dark;
      --ink: #edf6f2;
      --muted: #9cadaa;
      --faint: #6f807d;
      --ground: #081012;
      --panel: #0e181a;
      --panel-2: #111e20;
      --line: #263739;
      --line-bright: #3b5355;
      --mint: #72e0c1;
      --amber: #f2b866;
      --red: #ef8476;
      --blue: #7cb6ff;
      --violet: #b49afb;
      --content: 1240px;
      font: 16px/1.5 Inter, ui-sans-serif, system-ui, -apple-system,
        BlinkMacSystemFont, "Segoe UI", sans-serif;
      background: var(--ground);
      color: var(--ink);
    }
    * { box-sizing: border-box; }
    html { scroll-behavior: smooth; }
    body {
      margin: 0;
      min-width: 280px;
      background:
        linear-gradient(90deg, transparent 24px, rgba(114,224,193,.035) 25px,
          transparent 26px) 0 0 / 52px 52px,
        linear-gradient(0deg, transparent 24px, rgba(114,224,193,.035) 25px,
          transparent 26px) 0 0 / 52px 52px,
        radial-gradient(circle at 72% -10%, rgba(114,224,193,.12), transparent 34rem),
        var(--ground);
    }
    button, input { font: inherit; }
    button { color: inherit; }
    .skip {
      position: fixed; left: 1rem; top: -5rem; z-index: 20;
      padding: .7rem 1rem; background: var(--mint); color: #07100e;
    }
    .skip:focus { top: 1rem; }
    .wrap { width: min(calc(100% - 2rem), var(--content)); margin-inline: auto; }
    .eyebrow {
      margin: 0 0 .75rem; color: var(--mint); font: 700 .74rem/1.2
      ui-monospace, SFMono-Regular, Menlo, monospace; letter-spacing: .14em;
      text-transform: uppercase;
    }
    header.hero { padding: clamp(2.75rem, 8vw, 6.5rem) 0 2rem; }
    h1 {
      max-width: 900px; margin: 0; font-size: clamp(2.65rem, 7.8vw, 6.8rem);
      line-height: .9; letter-spacing: -.065em; font-weight: 760;
    }
    h1 span { color: var(--mint); }
    .lede {
      max-width: 760px; margin: 1.6rem 0 0; color: #c2d1cd;
      font-size: clamp(1.03rem, 2vw, 1.3rem); line-height: 1.55;
    }
    .verdict {
      display: grid; grid-template-columns: minmax(0, 1.45fr) minmax(280px, .8fr);
      gap: 1rem; margin-top: clamp(2rem, 5vw, 4rem);
    }
    .verdict-copy, .route-board {
      border: 1px solid var(--line); background: rgba(14,24,26,.82);
      min-height: 200px;
    }
    .verdict-copy { padding: clamp(1.25rem, 3vw, 2.2rem); }
    .verdict-copy h2 { margin: 0 0 .65rem; font-size: clamp(1.45rem, 3vw, 2.25rem); }
    .verdict-copy p { margin: 0; max-width: 65ch; color: var(--muted); }
    .verdict-copy strong { color: var(--ink); }
    .route-board { padding: 1.25rem; display: grid; align-content: space-between; }
    .route-title { color: var(--muted); font-size: .78rem; letter-spacing: .08em; text-transform: uppercase; }
    .route {
      position: relative; display: grid; grid-template-columns: repeat(3, 1fr);
      gap: .5rem; align-items: start; margin: 1.5rem .25rem .75rem;
    }
    .route::before {
      content: ""; position: absolute; height: 1px; left: 14%; right: 14%; top: 1.55rem;
      background: linear-gradient(90deg, var(--mint), var(--amber), var(--red));
    }
    .well { position: relative; text-align: center; }
    .well b {
      position: relative; display: grid; place-items: center; width: 3.1rem;
      height: 3.1rem; margin: 0 auto .55rem; border: 1px solid currentColor;
      border-radius: 50%; background: var(--ground); font: 750 1.35rem/1
      ui-monospace, SFMono-Regular, Menlo, monospace;
    }
    .well:nth-child(1) { color: var(--mint); }
    .well:nth-child(2) { color: var(--amber); }
    .well:nth-child(3) { color: var(--red); }
    .well span { display: block; color: var(--muted); font-size: .78rem; }
    .route-note { color: var(--faint); font-size: .78rem; }
    .stats {
      display: grid; grid-template-columns: repeat(4, 1fr); gap: 1px;
      margin: 1rem 0 0; border: 1px solid var(--line); background: var(--line);
    }
    .stat { padding: 1rem; background: var(--panel); }
    .stat b { display: block; font: 720 1.55rem/1 ui-monospace, monospace; }
    .stat span { display: block; margin-top: .35rem; color: var(--faint); font-size: .76rem; }
    .primer { padding: 3rem 0 1rem; }
    .primer h2, .gallery-head h2 { margin: 0; font-size: clamp(1.8rem, 4vw, 3rem); letter-spacing: -.035em; }
    .primer-grid {
      display: grid; grid-template-columns: repeat(5, 1fr); gap: 1px;
      margin-top: 1.25rem; border: 1px solid var(--line); background: var(--line);
    }
    .axis { min-height: 172px; padding: 1rem; background: var(--panel); }
    .axis b { display: block; margin-bottom: .65rem; color: var(--mint); }
    .axis p { margin: 0; color: var(--muted); font-size: .88rem; }
    .axis code { font: inherit; color: var(--amber); }
    .gallery-head { padding: 4rem 0 1rem; }
    .gallery-head p { max-width: 70ch; color: var(--muted); margin: .75rem 0 0; }
    .controls-shell {
      position: sticky; top: 0; z-index: 10; margin-top: 1.25rem;
      border-block: 1px solid var(--line); background: rgba(8,16,18,.94);
      backdrop-filter: blur(14px);
    }
    .controls { padding: .75rem 0; display: grid; gap: .75rem; }
    .search-row { display: flex; gap: .65rem; align-items: center; }
    .search {
      flex: 1; min-width: 0; color: var(--ink); background: var(--panel);
      border: 1px solid var(--line-bright); padding: .72rem .85rem; border-radius: 0;
    }
    .search:focus { outline: 2px solid var(--mint); outline-offset: 2px; }
    .utility, .filter {
      border: 1px solid var(--line); background: var(--panel); padding: .65rem .8rem;
      cursor: pointer;
    }
    .utility:hover, .filter:hover { border-color: var(--line-bright); }
    .utility[aria-pressed="true"], .filter[aria-pressed="true"] {
      border-color: var(--mint); color: var(--mint); background: rgba(114,224,193,.08);
    }
    .filters { display: flex; gap: .45rem; overflow-x: auto; padding-bottom: .12rem; }
    .filter { white-space: nowrap; font-size: .82rem; }
    .filter small { margin-left: .35rem; color: var(--faint); }
    .result-line {
      display: flex; justify-content: space-between; gap: 1rem; align-items: baseline;
      padding-top: 1rem; color: var(--muted); font-size: .84rem;
    }
    .result-line strong { color: var(--ink); }
    .cards {
      display: grid; grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 1rem; padding: 1rem 0 5rem;
    }
    .card {
      --accent: var(--mint); position: relative; display: flex; flex-direction: column;
      min-width: 0; border: 1px solid var(--line); border-top: 3px solid var(--accent);
      background: linear-gradient(155deg, color-mix(in srgb, var(--accent) 5%, var(--panel)), var(--panel) 42%);
      scroll-margin-top: 9rem;
    }
    .card[data-category="charge"] { --accent: var(--amber); }
    .card[data-category="logistics"] { --accent: var(--blue); }
    .card[data-category="veteran"] { --accent: #f2cf68; }
    .card[data-category="wave"] { --accent: var(--violet); }
    .card[data-category="deployment"] { --accent: #79d5a0; }
    .card[data-category="hybrid"] { --accent: #ed8ba8; }
    .card.recommended::after {
      content: "FIRST 8"; position: absolute; right: .85rem; top: .85rem;
      color: var(--accent); font: 700 .64rem/1 ui-monospace, monospace;
      letter-spacing: .12em;
    }
    .card-head { padding: 1rem 1rem 0; }
    .card-meta { display: flex; align-items: center; gap: .55rem; color: var(--faint); }
    .number { color: var(--accent); font: 700 .78rem/1 ui-monospace, monospace; }
    .family { font-size: .73rem; text-transform: uppercase; letter-spacing: .08em; }
    .card h3 { margin: .55rem 3.8rem .85rem 0; font-size: clamp(1.32rem, 2.4vw, 1.75rem); letter-spacing: -.025em; }
    .plan { margin: 0; padding: 0 1rem 1rem; color: #c7d6d2; }
    .roster {
      display: flex; flex-wrap: wrap; gap: .36rem; padding: .8rem 1rem;
      border-block: 1px solid var(--line); background: rgba(4,10,11,.3);
    }
    .bot {
      padding: .24rem .45rem; color: var(--muted); border: 1px solid var(--line);
      font: 650 .69rem/1.2 ui-monospace, SFMono-Regular, Menlo, monospace;
    }
    .bot.pair { color: var(--accent); border-color: color-mix(in srgb, var(--accent) 45%, var(--line)); }
    details { border-bottom: 1px solid var(--line); }
    summary {
      padding: .78rem 1rem; cursor: pointer; color: var(--accent); font-size: .82rem;
      list-style: none;
    }
    summary::-webkit-details-marker { display: none; }
    summary::before { content: "+"; display: inline-block; width: 1.25rem; }
    details[open] summary::before { content: "−"; }
    .details { display: grid; gap: .75rem; padding: 0 1rem 1rem; }
    .detail-label { display: block; color: var(--faint); font: 700 .66rem/1.2 ui-monospace, monospace; letter-spacing: .1em; text-transform: uppercase; }
    .details p { margin: .22rem 0 0; color: var(--muted); font-size: .88rem; }
    .card-actions { display: flex; gap: .5rem; margin-top: auto; padding: .8rem 1rem; }
    .compare-add { width: 100%; border: 1px solid var(--line-bright); background: transparent; padding: .6rem; cursor: pointer; }
    .compare-add[aria-pressed="true"] { color: var(--accent); border-color: var(--accent); }
    .empty {
      grid-column: 1 / -1; padding: 4rem 1rem; border: 1px dashed var(--line);
      color: var(--muted); text-align: center;
    }
    .compare-tray {
      position: fixed; left: 50%; bottom: 1rem; z-index: 14; width: min(calc(100% - 2rem), 680px);
      transform: translateX(-50%); display: flex; align-items: center; gap: .6rem;
      padding: .65rem; border: 1px solid var(--line-bright); background: rgba(8,16,18,.97);
      box-shadow: 0 18px 60px rgba(0,0,0,.4);
    }
    .compare-tray[hidden] { display: none; }
    .compare-picks { display: flex; gap: .35rem; flex: 1; min-width: 0; overflow-x: auto; }
    .pick { white-space: nowrap; color: var(--muted); font-size: .74rem; border: 1px solid var(--line); padding: .4rem; }
    .compare-open { border: 0; background: var(--mint); color: #07100e; padding: .58rem .75rem; font-weight: 750; cursor: pointer; }
    dialog {
      width: min(calc(100% - 2rem), 1080px); max-height: min(88dvh, 900px);
      padding: 0; border: 1px solid var(--line-bright); color: var(--ink);
      background: var(--ground);
    }
    dialog::backdrop { background: rgba(0,0,0,.78); backdrop-filter: blur(4px); }
    .dialog-head { position: sticky; top: 0; z-index: 2; display: flex; justify-content: space-between; gap: 1rem; padding: 1rem; border-bottom: 1px solid var(--line); background: var(--ground); }
    .dialog-head h2 { margin: 0; font-size: 1.2rem; }
    .dialog-close { border: 1px solid var(--line); background: var(--panel); padding: .45rem .65rem; cursor: pointer; }
    .compare-grid { display: grid; grid-template-columns: repeat(var(--compare-count), minmax(0, 1fr)); gap: 1px; background: var(--line); }
    .compare-column { background: var(--panel); padding: 1rem; }
    .compare-column h3 { margin: .4rem 0 1rem; font-size: 1.35rem; }
    .compare-column section { margin-top: 1rem; }
    .compare-column p { color: var(--muted); margin: .3rem 0; font-size: .88rem; }
    .first-eight { padding: 1rem 0 4rem; }
    .first-eight-box { border: 1px solid var(--line); background: var(--panel); padding: clamp(1.2rem, 3vw, 2rem); }
    .first-eight h2 { margin: 0; font-size: clamp(1.65rem, 3vw, 2.5rem); }
    .first-eight p { color: var(--muted); max-width: 72ch; }
    .quick-list { display: grid; grid-template-columns: repeat(4, 1fr); gap: .6rem; margin-top: 1.25rem; }
    .quick {
      display: block; min-height: 92px; padding: .75rem; border: 1px solid var(--line);
      color: var(--ink); text-decoration: none; background: var(--panel-2);
    }
    .quick:hover, .quick:focus { border-color: var(--mint); }
    .quick small { display: block; color: var(--mint); font: .65rem/1.2 ui-monospace, monospace; }
    .quick b { display: block; margin-top: .35rem; font-size: .88rem; }
    footer { border-top: 1px solid var(--line); padding: 2rem 0 5rem; color: var(--faint); font-size: .8rem; }
    [hidden] { display: none !important; }
    @media (max-width: 900px) {
      .verdict { grid-template-columns: 1fr; }
      .primer-grid { grid-template-columns: repeat(2, 1fr); }
      .primer-grid .axis:last-child { grid-column: span 2; }
      .quick-list { grid-template-columns: repeat(2, 1fr); }
    }
    @media (max-width: 720px) {
      .wrap { width: min(calc(100% - 1.25rem), var(--content)); }
      header.hero { padding-top: 2.5rem; }
      h1 { font-size: clamp(2.65rem, 16vw, 4.6rem); }
      .stats { grid-template-columns: repeat(2, 1fr); }
      .cards { grid-template-columns: 1fr; }
      .controls-shell { top: 0; }
      .search-row { align-items: stretch; }
      .search-row .utility { padding-inline: .65rem; font-size: .78rem; }
      .compare-grid { grid-template-columns: 1fr; }
      .compare-column { border-bottom: 1px solid var(--line); }
    }
    @media (max-width: 430px) {
      .primer-grid { grid-template-columns: 1fr; }
      .primer-grid .axis:last-child { grid-column: auto; }
      .quick-list { grid-template-columns: 1fr; }
      .route-board { min-height: 220px; }
      .compare-tray { bottom: .5rem; width: calc(100% - 1rem); }
      .pick { max-width: 100px; overflow: hidden; text-overflow: ellipsis; }
    }
    @media (prefers-reduced-motion: reduce) {
      html { scroll-behavior: auto; }
    }
  </style>
</head>
<body>
  <a class="skip" href="#strategy-grid">Skip to strategies</a>
  <header class="hero wrap">
    <p class="eyebrow">Nilbots · Arc Relay · candidate rules</p>
    <h1>Tapline has <span>room to grow.</span></h1>
    <p class="lede">Thirty-six genuinely different sheet concepts built from five
      proposed changes. This is the readable atlas: what each plan commits to,
      why it could be fun, and how an opponent can answer it.</p>
    <div class="verdict">
      <section class="verdict-copy" aria-labelledby="verdict-title">
        <p class="eyebrow">Current verdict</p>
        <h2 id="verdict-title">Promising enough to prototype. Not proven enough to ship.</h2>
        <p>The encouraging signal is not the number 36. It is that these plans ask
          different questions and invite different counters. The next gate is whether
          eight maximally separated examples actually look and play differently.</p>
      </section>
      <section class="route-board" aria-label="Core value by Well from your perspective">
        <div class="route-title">Core value · your perspective</div>
        <div class="route">
          <div class="well"><b>1</b><span>own Well</span></div>
          <div class="well"><b>2</b><span>centre</span></div>
          <div class="well"><b>4</b><span>enemy Well</span></div>
        </div>
        <div class="route-note">3 charge = 1 Pulse · 3 Pulses = win · remainder carries</div>
      </section>
    </div>
    <div class="stats" aria-label="Strategy-space summary">
      <div class="stat"><b>36</b><span>distinct concepts</span></div>
      <div class="stat"><b>6</b><span>strategy families</span></div>
      <div class="stat"><b>8</b><span>first prototypes</span></div>
      <div class="stat"><b>29,376</b><span>raw macro combinations</span></div>
    </div>
  </header>

  <main>
    <section class="primer wrap" aria-labelledby="primer-title">
      <p class="eyebrow">The five interacting decisions</p>
      <h2 id="primer-title">What a sheet is choosing</h2>
      <div class="primer-grid">
        <article class="axis"><b>01 · Score route</b><p>Take reliable <code>1s</code>, fight for <code>2s</code>, raid for <code>4s</code>, or build an exact mixed recipe.</p></article>
        <article class="axis"><b>02 · Veteran</b><p>Pre-select an ace, let one emerge, build a successor, use a decoy, or hunt the enemy's investment.</p></article>
        <article class="axis"><b>03 · Wave</b><p>Commit, retreat, regroup, or arrange kills around one shared public 30-tick respawn clock.</p></article>
        <article class="axis"><b>04 · Wildcard</b><p>Field eight different classes or spend the one duplicate pair on a defining specialization.</p></article>
        <article class="axis"><b>05 · Deployment</b><p>Preserve, refresh, relocate, feint with, suppress, or deliberately demolish persistent map control.</p></article>
      </div>
    </section>

    <section class="gallery-head wrap" aria-labelledby="gallery-title">
      <p class="eyebrow">Strategy atlas</p>
      <h2 id="gallery-title">Find the plans that interest you</h2>
      <p>Filter by family, search a class or idea, or start with the eight marked
        candidates. Add up to three cards to compare their commitments directly.</p>
    </section>

    <div class="controls-shell">
      <div class="controls wrap">
        <div class="search-row">
          <label for="search" hidden>Search strategies</label>
          <input class="search" id="search" type="search" placeholder="Search: Mason, raid, veteran, centre…" autocomplete="off">
          <button class="utility" id="recommended-toggle" type="button" aria-pressed="false">First 8</button>
          <button class="utility" id="expand-toggle" type="button" aria-pressed="false">Expand</button>
        </div>
        <nav class="filters" id="filters" aria-label="Strategy families"></nav>
      </div>
    </div>

    <section class="wrap" aria-labelledby="results-title">
      <div class="result-line"><span id="results-title"><strong id="result-count">36</strong> strategies shown</span><span>Concepts, not balance results</span></div>
      <div class="cards" id="strategy-grid"></div>
    </section>

    <section class="first-eight wrap" aria-labelledby="first-eight-title">
      <div class="first-eight-box">
        <p class="eyebrow">Recommended next gate</p>
        <h2 id="first-eight-title">Build these eight first</h2>
        <p>They deliberately oppose one another: pure raid and interception,
          fixed and emergent veterans, offensive and defensive wave timing,
          durable construction and its direct demolition counter.</p>
        <div class="quick-list" id="quick-list"></div>
      </div>
    </section>
  </main>

  <div class="compare-tray" id="compare-tray" hidden>
    <div class="compare-picks" id="compare-picks" aria-live="polite"></div>
    <button class="compare-open" id="compare-open" type="button">Compare</button>
  </div>

  <dialog id="compare-dialog" aria-labelledby="compare-title">
    <div class="dialog-head">
      <h2 id="compare-title">Strategy comparison</h2>
      <button class="dialog-close" id="dialog-close" type="button">Close</button>
    </div>
    <div class="compare-grid" id="compare-grid"></div>
  </dialog>

  <footer>
    <div class="wrap">Generated from ARC-RELAY-TAPLINE-SHEET-STRATEGY-CATALOG.md · candidate design surface · Tapline-3 remains unchanged.</div>
  </footer>

  <script>
    const DATA = __STRATEGY_DATA__;
    const state = { category: "all", query: "", recommended: false, expanded: false, compare: [] };
    const byId = new Map(DATA.strategies.map((strategy) => [strategy.id, strategy]));
    const grid = document.getElementById("strategy-grid");
    const filters = document.getElementById("filters");
    const resultCount = document.getElementById("result-count");
    const searchInput = document.getElementById("search");
    const recommendedToggle = document.getElementById("recommended-toggle");
    const expandToggle = document.getElementById("expand-toggle");
    const compareTray = document.getElementById("compare-tray");
    const comparePicks = document.getElementById("compare-picks");
    const compareDialog = document.getElementById("compare-dialog");
    const compareGrid = document.getElementById("compare-grid");

    function escapeHtml(value) {
      return String(value).replace(/[&<>"]/g, (character) => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;"
      })[character]);
    }

    function rosterChips(roster) {
      return roster.split(/[;,]/).map((entry) => entry.trim()).filter(Boolean)
        .map((entry) => `<span class="bot ${/x2$/.test(entry) ? "pair" : ""}">${escapeHtml(entry)}</span>`).join("");
    }

    function card(strategy) {
      return `<article class="card ${strategy.recommended ? "recommended" : ""}" data-id="${strategy.id}" data-category="${strategy.category}">
        <div class="card-head">
          <div class="card-meta"><span class="number">${strategy.number}</span><span class="family">${escapeHtml(strategy.categoryTitle)}</span></div>
          <h3>${escapeHtml(strategy.name)}</h3>
        </div>
        <p class="plan">${escapeHtml(strategy.thesis)}</p>
        <div class="roster" aria-label="Roster">${rosterChips(strategy.roster)}</div>
        <details>
          <summary>Why it is distinct · how to answer it</summary>
          <div class="details">
            <div><span class="detail-label">Why it could be fun</span><p>${escapeHtml(strategy.why)}</p></div>
            <div><span class="detail-label">Failure / counterplay</span><p>${escapeHtml(strategy.counter)}</p></div>
          </div>
        </details>
        <div class="card-actions"><button class="compare-add" type="button" data-compare="${strategy.id}" aria-pressed="false">Add to compare</button></div>
      </article>`;
    }

    function renderCards() {
      grid.innerHTML = DATA.strategies.map(card).join("") + `<div class="empty" hidden>No strategy matches those filters.</div>`;
      grid.querySelectorAll("[data-compare]").forEach((button) => {
        button.addEventListener("click", () => toggleCompare(Number(button.dataset.compare)));
      });
      applyFilters();
      syncCompare();
    }

    function renderFilters() {
      const buttons = [{ slug: "all", short: "All", count: DATA.strategies.length }, ...DATA.categories.map((category) => ({
        slug: category.slug,
        short: category.short,
        count: DATA.strategies.filter((strategy) => strategy.category === category.slug).length
      }))];
      filters.innerHTML = buttons.map((item) => `<button class="filter" type="button" data-filter="${item.slug}" aria-pressed="${item.slug === "all"}">${escapeHtml(item.short)} <small>${item.count}</small></button>`).join("");
      filters.querySelectorAll("[data-filter]").forEach((button) => {
        button.addEventListener("click", () => {
          state.category = button.dataset.filter;
          filters.querySelectorAll("[data-filter]").forEach((candidate) => candidate.setAttribute("aria-pressed", String(candidate === button)));
          applyFilters();
        });
      });
    }

    function applyFilters() {
      const needle = state.query.toLocaleLowerCase();
      let shown = 0;
      grid.querySelectorAll(".card").forEach((element) => {
        const strategy = byId.get(Number(element.dataset.id));
        const haystack = `${strategy.name} ${strategy.roster} ${strategy.thesis} ${strategy.why} ${strategy.counter}`.toLocaleLowerCase();
        const visible = (state.category === "all" || strategy.category === state.category)
          && (!state.recommended || strategy.recommended)
          && (!needle || haystack.includes(needle));
        element.hidden = !visible;
        shown += visible ? 1 : 0;
        const details = element.querySelector("details");
        if (details) details.open = state.expanded;
      });
      grid.querySelector(".empty").hidden = shown !== 0;
      resultCount.textContent = String(shown);
    }

    function toggleCompare(id) {
      const current = state.compare.indexOf(id);
      if (current >= 0) state.compare.splice(current, 1);
      else if (state.compare.length < 3) state.compare.push(id);
      else {
        compareTray.animate([{ transform: "translateX(-50%)" }, { transform: "translateX(-51.5%)" }, { transform: "translateX(-48.5%)" }, { transform: "translateX(-50%)" }], { duration: 220 });
      }
      syncCompare();
    }

    function syncCompare() {
      grid.querySelectorAll("[data-compare]").forEach((button) => {
        const selected = state.compare.includes(Number(button.dataset.compare));
        button.setAttribute("aria-pressed", String(selected));
        button.textContent = selected ? "Remove from compare" : "Add to compare";
      });
      compareTray.hidden = state.compare.length === 0;
      comparePicks.innerHTML = state.compare.map((id) => `<span class="pick">${String(id).padStart(2, "0")} · ${escapeHtml(byId.get(id).name)}</span>`).join("");
      document.getElementById("compare-open").textContent = `Compare ${state.compare.length}`;
    }

    function openComparison() {
      compareGrid.style.setProperty("--compare-count", String(Math.max(1, state.compare.length)));
      compareGrid.innerHTML = state.compare.map((id) => {
        const strategy = byId.get(id);
        return `<article class="compare-column">
          <span class="number">${strategy.number} · ${escapeHtml(strategy.categoryTitle)}</span>
          <h3>${escapeHtml(strategy.name)}</h3>
          <section><span class="detail-label">Plan</span><p>${escapeHtml(strategy.thesis)}</p></section>
          <section><span class="detail-label">Roster</span><p>${escapeHtml(strategy.roster)}</p></section>
          <section><span class="detail-label">Why distinct</span><p>${escapeHtml(strategy.why)}</p></section>
          <section><span class="detail-label">Counterplay</span><p>${escapeHtml(strategy.counter)}</p></section>
        </article>`;
      }).join("");
      compareDialog.showModal();
    }

    function renderQuickList() {
      const quick = DATA.strategies.filter((strategy) => strategy.recommended);
      document.getElementById("quick-list").innerHTML = quick.map((strategy) => `<a class="quick" href="#strategy-${strategy.id}" data-jump="${strategy.id}"><small>${strategy.number} · ${escapeHtml(strategy.categoryTitle)}</small><b>${escapeHtml(strategy.name)}</b></a>`).join("");
      document.querySelectorAll("[data-jump]").forEach((link) => {
        link.addEventListener("click", (event) => {
          event.preventDefault();
          state.category = "all";
          state.recommended = false;
          state.query = "";
          searchInput.value = "";
          recommendedToggle.setAttribute("aria-pressed", "false");
          filters.querySelectorAll("[data-filter]").forEach((button) => button.setAttribute("aria-pressed", String(button.dataset.filter === "all")));
          applyFilters();
          const target = grid.querySelector(`[data-id="${link.dataset.jump}"]`);
          target.id = `strategy-${link.dataset.jump}`;
          target.scrollIntoView({ block: "start" });
        });
      });
    }

    function updateSearch(event) {
      state.query = event.target.value.trim();
      applyFilters();
    }
    searchInput.addEventListener("input", updateSearch);
    searchInput.addEventListener("search", updateSearch);
    searchInput.addEventListener("change", updateSearch);
    recommendedToggle.addEventListener("click", () => {
      state.recommended = !state.recommended;
      if (state.recommended) {
        state.category = "all";
        state.query = "";
        searchInput.value = "";
        filters.querySelectorAll("[data-filter]").forEach((button) => button.setAttribute("aria-pressed", String(button.dataset.filter === "all")));
      }
      recommendedToggle.setAttribute("aria-pressed", String(state.recommended));
      applyFilters();
    });
    expandToggle.addEventListener("click", () => {
      state.expanded = !state.expanded;
      expandToggle.setAttribute("aria-pressed", String(state.expanded));
      expandToggle.textContent = state.expanded ? "Collapse" : "Expand";
      applyFilters();
    });
    document.getElementById("compare-open").addEventListener("click", openComparison);
    document.getElementById("dialog-close").addEventListener("click", () => compareDialog.close());
    compareDialog.addEventListener("click", (event) => {
      if (event.target === compareDialog) compareDialog.close();
    });

    renderFilters();
    renderCards();
    renderQuickList();
  </script>
</body>
</html>
'''


if __name__ == "__main__":
    raise SystemExit(main())
