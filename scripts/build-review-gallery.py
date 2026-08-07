#!/usr/bin/env python3
"""Build a servable review gallery from a replay sample manifest.

Consolidates the blind-review flow used for the classes wave-1 factorial:
take the header-only sample manifest written by ``replay-review-sample.py``,
put each replay behind a page built from a viewer template, and emit an
index. Two viewer modes:

- ``--viewer hosted`` (default): build from ``web/dist/index.html`` and
  copy everything ``dist`` ships beside it — the hashed ``assets/`` *and*
  the ``public/`` passthrough the viewer fetches at runtime, which is
  where the soundtrack lives (``soundtracks/index.json``). Full renderer
  (lazy WebGL); must be SERVED (``scripts/serve-gallery.py`` behind
  cloudflared per the replay-highlights skill) — file:// blocks module
  loading, and the pages reference ``/assets`` and ``/soundtracks``
  absolutely, so serve the gallery directory as the server root.

  The replay is **not** inlined here. Each page is a few kB that fetches
  ``replays/<sample>.json`` beside it, assigns
  ``window.__BOTARENA_REPLAY__``, and only then appends the bundle's
  module script — the mode switch in ``web/src/main.tsx`` reads that
  global at module-evaluation time, so the order is load-bearing. That
  keeps the per-match download to one compressible JSON instead of a
  15-25 MB HTML document that re-ships the whole bundle every match, and
  lets the browser cache ``/assets`` across pages. The builder also
  writes ``.gz`` siblings for everything compressible;
  ``scripts/serve-gallery.py`` serves them with ``Content-Encoding:
  gzip`` (replays shrink ~10x).

- ``--viewer self-contained``: inject into a ``web/dist-cli/<theme>``
  template at the CLI's ``<!--BOTARENA_REPLAY-->`` marker (the
  ``ReplayOutput.WriteViewer`` semantics, including the ``</`` escape).
  Portable single files, Canvas2D only (dist-cli excludes Three.js by
  design). Unchanged: this is the path whose whole point is one file.

``--review-panel`` appends the rating panel to every page (two 1-5
scores — fun to watch, easy to follow — plus free notes; autosaved to
localStorage) and gives the index per-sample progress plus a "Copy review
JSON" button that copies the decision-record artifact to the clipboard and
shows it in a selectable box (clipboard needs a secure context, so the box
is the fallback on plain-http LAN).

An index is generated from the sample manifest by default: pairing and map,
never outcomes — keep a blind gallery that way. A *curated* gallery (an
already-unblinded highlight reel) supplies its own card copy with
``--index-cards`` and its own lede with ``--intro``, and still gets the
progress markers and the export button:

    [{"id": "sample-02", "title": "vector-edge beats iron-root",
      "subtitle": "striker over bulwark, seed 960017", "win": true,
      "opponent": "the actual opposing entrant and its doctrine",
      "score": "final scoreline with the operation and match outcomes separated",
      "trigger": "visible carrier enters the fork",
      "tactic": "two Towlines close the return lane",
      "counterplay": "screen the carrier or change route before commitment",
      "fallback": "participant loss aborts and releases survivors",
      "watch": "commit t25, hook t26, Core loose t31"}, ...]

(a bare list, or an object with ``cards`` plus optional ``intro``/``title``;
``win`` only picks the win colour). Card order is the index order. With
``--index-cards`` the per-sample ids keep MANIFEST order (sample-01 is the
manifest's first replay) — write curated copy directly against the
manifest, no build-and-inspect round trip. The deterministic hash shuffle
applies only to blind galleries, where ordering must not leak arms.

Example:
    python3 scripts/build-review-gallery.py \
        --sample /tmp/run/blind-review-sample.json \
        --output sandbox/review-gallery \
        --title "Classes wave 1 — blind review" \
        --review-panel
"""

from __future__ import annotations

import argparse
import gzip
import hashlib
import html
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

MARKER = "<!--BOTARENA_REPLAY-->"
REPO = Path(__file__).resolve().parent.parent
DEFAULT_ARC_RELAY_BARS = (
    REPO / "balance/arc-relay-felt-degeneracy-bars-v4.json"
)
ARC_RELAY_SCORECARD = REPO / "scripts/arc-relay-scorecard.py"

# The hashed module script(s) `web/dist/index.html` loads. Hosted pages strip
# them out of the template and re-append them from JS once the replay is in
# `window.__BOTARENA_REPLAY__`; see BOOT_TEMPLATE.
MODULE_SCRIPT = re.compile(
    r"[ \t]*<script\b(?=[^>]*\btype=\"module\")[^>]*>\s*</script>[ \t]*\n?")
SCRIPT_SRC = re.compile(r"\bsrc=\"([^\"]+)\"")

# Pre-compressed siblings for `scripts/serve-gallery.py` (stdlib http.server
# sends no Content-Encoding of its own). Only what actually shrinks: the
# replays are the point (~10x), the bundle and the pages come along cheaply,
# and the textures/audio beside them are already compressed formats.
GZIP_SUFFIXES = {".css", ".html", ".js", ".json", ".map", ".svg", ".txt"}
GZIP_MIN_BYTES = 1024

DEFAULT_INTRO = """Outcome-blind review: outcomes are hidden. Watch at normal
speed and give two quick 1&ndash;5 scores per sample &mdash; fun to watch, easy
to follow &mdash; plus any notes."""

CARD_EXPLANATION_FIELDS = (
    ("opponent", "Actual opponent"),
    ("score", "Final score"),
    ("trigger", "Trigger"),
    ("tactic", "Intended tactic"),
    ("counterplay", "Counterplay"),
    ("fallback", "Fallback"),
    ("watch", "Watch for"),
)

# Hosted boot: fetch the replay, publish it, *then* load the bundle. The
# placeholder is plain markup so it is visible before any of this runs, and it
# survives until the module has executed — removing it at fetch-time would
# trade a "fetching" line for a blank page while the bundle loads.
BOOT_TEMPLATE = """
<style>#brvboot{position:fixed;inset:0;z-index:9990;display:flex;
 align-items:center;justify-content:center;background:#0b1020;color:#9fb2d8;
 font:15px/1.5 system-ui}</style>
<div id=brvboot>Fetching replay&hellip;</div>
<script>
(function(){
var SOURCES=__SOURCES__,REPLAY="__REPLAY__";
var note=document.getElementById("brvboot");
function say(text){if(note)note.textContent=text}
function boot(){
 var pending=SOURCES.length;
 SOURCES.forEach(function(src){
  var script=document.createElement("script");
  script.type="module";script.crossOrigin="anonymous";script.src=src;
  script.onload=function(){if(--pending===0)
   requestAnimationFrame(function(){requestAnimationFrame(function(){
    if(note&&note.parentNode)note.parentNode.removeChild(note)})})};
  script.onerror=function(){say("Viewer bundle failed to load \\u2014 reload.")};
  document.head.appendChild(script)})}
fetch(REPLAY).then(function(response){
  if(!response.ok)throw new Error("HTTP "+response.status);
  return response.json()})
 .then(function(replay){
  window.__BOTARENA_REPLAY__=replay;say("Starting viewer\\u2026");boot()})
 .catch(function(error){say("Replay failed to load: "+error.message)});
})();
</script>
"""

PANEL_TEMPLATE = """
<style>
#brv{position:fixed;right:.8rem;bottom:.8rem;z-index:9999;font:13px/1.4 system-ui;
 background:#0e1630f2;color:#dbe7ff;border:1px solid #33477e;border-radius:.6rem;
 width:270px;padding:.6rem .8rem}
#brv.min{width:auto}#brv.min .body{display:none}
#brv h4{margin:0 0 .2rem;font-size:13px;cursor:pointer}
#brv .row{margin:.25rem 0}#brv .row span{display:inline-block;width:110px}
#brv button.r{width:22px;height:22px;margin:0 1px;border:1px solid #33477e;
 background:#141d3d;color:#dbe7ff;border-radius:4px;cursor:pointer}
#brv button.r.on{background:#3a5fd0;border-color:#6c8bff}
#brv textarea,#brv input{width:100%;box-sizing:border-box;background:#141d3d;
 color:#dbe7ff;border:1px solid #33477e;border-radius:4px;margin:.15rem 0;
 font:12px system-ui}
#brv .done{color:#7fd6a0;font-size:11px}
</style>
<div id=brv class=min><h4 onclick="document.getElementById('brv').classList.toggle('min')">
&#9998; Review __SID__ <span class=done id=brvdone></span></h4><div class=body>
<div id=brvrows></div>
<textarea id=brvnotes rows=3 placeholder="notes — dull/confusing stretches (ticks if handy)"></textarea>
</div></div>
<script>
(function(){
var SID="__SID__",KEY="nilbots-blind-review::"+SID,
DIMS=[["fun","Fun to watch"],["clarity","Easy to follow"]];
// The viewer binds global playback shortcuts (space = play/pause); keys
// typed into the panel must never reach them.
["keydown","keyup","keypress"].forEach(function(kind){
 document.getElementById("brv").addEventListener(kind,function(e){
  e.stopPropagation()})});
var data=JSON.parse(localStorage.getItem(KEY)||"{}");
var rows=document.getElementById("brvrows");
DIMS.forEach(function(d){
 var div=document.createElement("div");div.className="row";
 var lbl=document.createElement("span");lbl.textContent=d[1];div.appendChild(lbl);
 for(var v=1;v<=5;v++)(function(v){
  var b=document.createElement("button");b.className="r";b.textContent=v;
  if(data[d[0]]===v)b.classList.add("on");
  b.onclick=function(){data[d[0]]=v;save();
   div.querySelectorAll("button").forEach(function(x){x.classList.remove("on")});
   b.classList.add("on")};
  div.appendChild(b)})(v);
 rows.appendChild(div)});
var notes=document.getElementById("brvnotes");
notes.value=data.notes||"";
notes.oninput=function(){data.notes=notes.value;save()};
function save(){data.updated=new Date().toISOString();
 localStorage.setItem(KEY,JSON.stringify(data));badge()}
function badge(){var n=DIMS.filter(function(d){return data[d[0]]}).length;
 document.getElementById("brvdone").textContent=n?n+"/"+DIMS.length:""}
badge();
})();
</script>
"""


def sample_entries(manifest_path: Path) -> list[dict]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    entries = (
        manifest
        if isinstance(manifest, list)
        else manifest.get("replays") or manifest.get("sample")
    )
    if not entries:
        raise ValueError(f"{manifest_path}: no replays in sample manifest")
    return entries


def card_labels(entry: dict) -> str:
    # Movement-coupling tokens are stripped so a movement-arm review stays
    # arm-blind on its index; the sample manifest keeps the full ruleset id
    # for un-blinding.
    pairing = re.sub(r"^frontline-labs-1-(experiment-classes-|classes-)", "",
                     entry.get("rules", ""))
    pairing = re.sub(r"-(sets-facing|facing-locked)$", "", pairing)
    arm = re.sub(r"^frontline-labs-01-|-classes$", "",
                 entry.get("map", "")) or "current"
    return f"{pairing} — {arm} map" if pairing else entry.get("rules", "")


def gzip_sibling(path: Path) -> int:
    """Write ``<path>.gz`` and return its size."""
    target = path.with_name(path.name + ".gz")
    with path.open("rb") as source, target.open("wb") as raw:
        # mtime=0 keeps the sibling byte-stable for an unchanged input.
        with gzip.GzipFile(fileobj=raw, mode="wb", compresslevel=6,
                           mtime=0) as compressed:
            shutil.copyfileobj(source, compressed)
    return target.stat().st_size


def gzip_tree(root: Path) -> int:
    """Pre-compress every servable text file under ``root``."""
    count = 0
    for path in sorted(root.rglob("*")):
        if (path.is_file()
                and path.suffix in GZIP_SUFFIXES
                and path.stat().st_size >= GZIP_MIN_BYTES):
            gzip_sibling(path)
            count += 1
    return count


def append_to_body(page: str, block: str) -> str:
    return (page.replace("</body>", block + "</body>", 1)
            if "</body>" in page else page + block)


def hosted_page(template: str, replay_url: str) -> str:
    """A page that fetches its replay before booting the bundle."""
    sources = []
    for tag in MODULE_SCRIPT.findall(template):
        src = SCRIPT_SRC.search(tag)
        if src:
            sources.append(src.group(1))
    if not sources:
        raise SystemExit(
            "web/dist/index.html: no <script type=\"module\" src=…> to defer")
    page = MODULE_SCRIPT.sub("", template).replace(
        MARKER, "<!-- replay fetched at runtime by the boot script -->")
    return append_to_body(page, BOOT_TEMPLATE
                          .replace("__SOURCES__", json.dumps(sources))
                          .replace("__REPLAY__", replay_url))


def inlined_page(template: str, replay: str) -> str:
    """The CLI's single-file semantics: the replay lives in the document."""
    return template.replace(
        MARKER,
        "<script>window.__BOTARENA_REPLAY__ = "
        + replay.replace("</", "<\\/")
        + ";</script>")


def curated_cards(path: Path) -> tuple[list[dict], str | None, str | None]:
    document = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(document, list):
        return document, None, None
    cards = document.get("cards")
    if not cards:
        raise SystemExit(f"{path}: no cards")
    return cards, document.get("intro"), document.get("title")


def index_cards(entries: list[dict], curated: list[dict] | None) -> list[dict]:
    """Card order + copy for the index: curated when supplied, else derived."""
    derived = {
        entry["id"]: {
            "id": entry["id"],
            "title": f"Sample {index}",
            "subtitle": card_labels(entry),
        }
        for index, entry in enumerate(entries, start=1)
    }
    if not curated:
        return list(derived.values())
    cards, seen = [], set()
    for card in curated:
        sid = card.get("id") or card.get("sample")
        if sid not in derived:
            raise SystemExit(
                f"--index-cards names {sid!r}, which this sample does not "
                f"build (have: {', '.join(derived)})")
        seen.add(sid)
        resolved = {
            "id": sid,
            "title": card.get("title", derived[sid]["title"]),
            "subtitle": card.get("subtitle", derived[sid]["subtitle"]),
            "win": bool(card.get("win")),
        }
        for field, _ in CARD_EXPLANATION_FIELDS:
            if field not in card:
                continue
            value = card[field]
            if not isinstance(value, str) or not value.strip():
                raise SystemExit(
                    f"--index-cards {sid!r} field {field!r} must be a "
                    "non-empty string")
            resolved[field] = value.strip()
        cards.append(resolved)
    # A sample the curated list forgot still gets a link rather than
    # vanishing from a gallery that was built with it.
    for sid, card in derived.items():
        if sid not in seen:
            print(f"note: --index-cards omits {sid}; appended with defaults")
            cards.append(card)
    return cards


def render_card(card: dict) -> str:
    explanation = "".join(
        f'<div><dt>{html.escape(label)}</dt>'
        f'<dd>{html.escape(card[field])}</dd></div>'
        for field, label in CARD_EXPLANATION_FIELDS
        if field in card
    )
    details = f'<dl>{explanation}</dl>' if explanation else ""
    return (
        f'<li><a href="{card["id"]}.html">'
        f'<strong{" class=w" if card.get("win") else ""}>'
        f'{html.escape(card["title"])}</strong>'
        f' <span class=meta>{html.escape(card["subtitle"])}</span>'
        f'{details}'
        f'<em class=prog data-sid="{card["id"]}"></em></a></li>'
    )


def replay_document(path: Path) -> dict:
    with path.open("rb") as source:
        compressed = source.read(2) == b"\x1f\x8b"
    opener = gzip.open if compressed else open
    with opener(path, "rt", encoding="utf-8") as source:
        document = json.load(source)
    return document if isinstance(document, dict) else {}


def is_complete_arc_relay_broadcast(path: Path) -> bool:
    document = replay_document(path)
    initial = document.get("initial")
    return (
        document.get("broadcastVersion") == 1
        and document.get("result") is not None
        and isinstance(initial, list)
        and len(initial) > 7
        and isinstance(initial[7], dict)
        and initial[7].get("kind") == "arc-relay"
    )


def enforce_arc_relay_eligibility(
    entries: list[dict], bars: Path
) -> None:
    failures = []
    checked = 0
    for entry in entries:
        source = Path(entry["source"])
        if not is_complete_arc_relay_broadcast(source):
            continue
        checked += 1
        completed = subprocess.run(
            [
                sys.executable,
                str(ARC_RELAY_SCORECARD),
                str(source),
                "--bars",
                str(bars),
            ],
            cwd=REPO,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            check=False,
        )
        if completed.returncode != 0:
            raise SystemExit(
                f"Arc Relay eligibility scoring failed for {source}:\n"
                + completed.stdout[-2000:]
            )
        scorecard = json.loads(completed.stdout)
        felt = scorecard["feltDegeneracy"]
        if felt["matchEligibleForCohortRead"]:
            continue
        trips = {
            metric: sorted(
                int(team)
                for team, value in details.get(
                    "barTrippedByTeam", {}
                ).items()
                if value
            )
            for metric, details in felt.items()
            if isinstance(details, dict)
            and any(details.get("barTrippedByTeam", {}).values())
        }
        failures.append(
            f"{entry['id']} ({source}): "
            + ", ".join(
                f"{metric}=teams{teams}"
                for metric, teams in sorted(trips.items())
            )
        )
    if failures:
        raise SystemExit(
            "Arc Relay gallery eligibility failed; fix or exclude these "
            "matches (use --skip-arc-relay-eligibility only for an "
            "explicitly labelled diagnostic gallery):\n- "
            + "\n- ".join(failures)
        )
    if checked:
        print(
            f"Arc Relay eligibility passed for {checked} replay(s) "
            f"under {bars}"
        )


def build(args: argparse.Namespace) -> None:
    # Multiple --sample manifests merge into one blind sequence. The order
    # is a deterministic hash shuffle: concatenation order would otherwise
    # leak the per-manifest block structure (e.g. which samples share an
    # experiment arm) to the reviewer.
    entries = [
        entry
        for manifest in args.sample
        for entry in sample_entries(manifest)
    ]
    if args.index_cards:
        # A curated gallery is already unblinded and its cards reference the
        # manifest's sample ids. Re-shuffling and renumbering here silently
        # pointed every card at the wrong game once; keep manifest identity.
        for index, entry in enumerate(entries, start=1):
            entry.setdefault("id", f"sample-{index:02}")
    else:
        entries.sort(key=lambda entry: hashlib.sha256(
            str(entry.get("source", entry)).encode()).hexdigest())
        for index, entry in enumerate(entries, start=1):
            entry["id"] = f"sample-{index:02}"
    if args.skip_arc_relay_eligibility:
        print(
            "warning: Arc Relay eligibility was explicitly skipped; label "
            "this as a diagnostic gallery"
        )
    else:
        enforce_arc_relay_eligibility(
            entries, args.arc_relay_bars.resolve())
    output = args.output
    output.mkdir(parents=True, exist_ok=True)
    hosted = args.viewer == "hosted"

    if hosted:
        dist = (args.viewer_build.resolve() if args.viewer_build
                else REPO / "web" / "dist")
        template_path = dist / "index.html"
        if not template_path.exists():
            raise SystemExit(
                f"{template_path} missing — build the selected hosted viewer")
        # Everything `web/dist` ships beside the entry point comes along,
        # DIRECTORIES INCLUDED. Copying `assets/` plus the loose files was
        # the whole gallery's audio bug: Vite bundles the three sound-effect
        # cues into `assets/` (they are imported), but the 22 MB adaptive
        # soundtrack is a `public/` passthrough served from
        # `dist/soundtracks/`, and `useAdaptiveSoundtrack` fetches it at
        # `<base>soundtracks/index.json`. A directory-blind copy left every
        # gallery page with working effects and a soundtrack that 404'd into
        # "SCORE ERROR" no matter how the reviewer interacted with it.
        for extra in dist.iterdir():
            if extra.name == "index.html":
                continue
            target = output / extra.name
            if extra.is_dir():
                if target.exists():
                    shutil.rmtree(target)
                shutil.copytree(extra, target)
            else:
                shutil.copy2(extra, target)
    else:
        template_path = (
            REPO / "web" / "dist-cli" / args.theme / "index.html")
        if not template_path.exists():
            raise SystemExit(
                f"{template_path} missing — run `npm run build` in web/")
    template = template_path.read_text(encoding="utf-8")
    if MARKER not in template:
        raise SystemExit(f"{template_path}: injection marker missing")

    replays = output / "replays"
    if hosted:
        replays.mkdir(exist_ok=True)
        # App treats this index as optional, but still probes for it in the
        # standalone viewer.  Gallery navigation lives in our outcome-blind
        # index page, so an empty index is the truthful response and avoids a
        # noisy 404 during browser review without introducing a second path to
        # the sample identities.
        (output / "replays.json").write_text("[]\n", encoding="utf-8")
    for entry in entries:
        sid = entry["id"]
        if hosted:
            source = Path(entry["source"])
            # The hosted viewer consumes the Arc Relay spectator broadcast,
            # not the raw canonical replay — serving a raw document renders
            # as "replay not valid" for the reviewer. Refuse with the remedy.
            document = replay_document(source)
            rules = str(document.get("header", {}).get(
                "gameRulesVersion", ""))
            if (rules.startswith("arc-relay")
                    and document.get("broadcastVersion") is None):
                raise SystemExit(
                    f"{source}: raw Arc Relay replay in hosted mode — "
                    "project it first (scripts/arc-relay-broadcast.py) or "
                    "build with --viewer self-contained")
            # Compact Phase-D broadcasts are gzip-only durable artifacts.
            # Keep them that way: serve-gallery maps a request for sample.json
            # to its sample.json.gz sibling with Content-Encoding: gzip, so
            # neither the gallery nor the browser needs an inflated copy.
            target = (replays / f"{sid}.json.gz"
                      if source.suffix == ".gz"
                      else replays / f"{sid}.json")
            shutil.copyfile(source, target)
            page = hosted_page(template, f"replays/{sid}.json")
        else:
            page = inlined_page(
                template, Path(entry["source"]).read_text(encoding="utf-8"))
        if args.review_panel:
            page = append_to_body(page, PANEL_TEMPLATE.replace("__SID__", sid))
        (output / f"{sid}.html").write_text(page, encoding="utf-8")

    curated, curated_intro, curated_title = (
        curated_cards(args.index_cards) if args.index_cards
        else (None, None, None))
    cards = index_cards(entries, curated)
    title = args.title if args.title is not None else (
        curated_title or "Replay review")
    intro = args.intro if args.intro is not None else (
        curated_intro if curated_intro is not None else DEFAULT_INTRO)

    items = "\n".join(render_card(card) for card in cards)
    review_block = ""
    if args.review_panel:
        review_block = f"""
<button onclick="exportNotes()">Copy review JSON</button>
<textarea id=exported rows=8 readonly
 style="display:none;width:100%;box-sizing:border-box;margin-top:.5rem;
 background:#141d3d;color:#dbe7ff;border:1px solid #33477e;border-radius:.5rem;
 font:12px ui-monospace,monospace"></textarea>
<script>
function refresh(){{document.querySelectorAll('em.prog').forEach(function(el){{
 var d=JSON.parse(localStorage.getItem('nilbots-blind-review::'+el.dataset.sid)||'{{}}');
 var n=['fun','clarity'].filter(function(k){{return d[k]}}).length;
 el.textContent=n?(n===2?'\\u2713':n+'/2'):'';}})}}
function exportNotes(){{
 var out={{exported:new Date().toISOString(),
   protocol:'{args.review_protocol}',samples:{{}}}};
 for(var i=1;i<={len(cards)};i++){{
  var sid='sample-'+String(i).padStart(2,'0');
  var d=localStorage.getItem('nilbots-blind-review::'+sid);
  if(d)out.samples[sid]=JSON.parse(d);}}
 var text=JSON.stringify(out,null,2);
 var box=document.getElementById('exported');
 box.style.display='block';box.value=text;box.focus();box.select();
 if(navigator.clipboard&&navigator.clipboard.writeText)
  navigator.clipboard.writeText(text);}}
refresh();window.addEventListener('pageshow',refresh);
</script>"""
    (output / "index.html").write_text(f"""<!doctype html>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{html.escape(title)}</title>
<style>
 body{{font:16px/1.5 system-ui;margin:2rem auto;max-width:52rem;padding:0 1rem;
      background:#0b1020;color:#dbe7ff}}
 h1{{font-size:1.3rem}} p{{color:#9fb2d8}}
 ul{{list-style:none;padding:0}} li{{margin:.65rem 0}}
 a{{display:block;padding:.85rem 1rem;border:1px solid #27355c;
   border-radius:.5rem;color:#dbe7ff;text-decoration:none;position:relative}}
 a:hover{{border-color:#4a6cc3}} a span.meta{{color:#9fb2d8;font-size:.9rem;
   display:block;padding-right:2rem}}
 dl{{margin:.65rem 0 0;display:grid;gap:.32rem}}
 dl div{{display:grid;grid-template-columns:7.3rem 1fr;gap:.55rem}}
 dt{{color:#7f96c7;font-size:.78rem;font-weight:700;text-transform:uppercase;
   letter-spacing:.04em}}
 dd{{margin:0;color:#c9d7f4;font-size:.88rem;line-height:1.38}}
 strong.w{{color:#7fd6a0}}
 em.prog{{position:absolute;right:1rem;top:.9rem;font-style:normal;color:#7fd6a0}}
 @media(max-width:36rem){{dl div{{grid-template-columns:1fr;gap:0}}}}
 button{{padding:.6rem 1rem;border-radius:.5rem;border:1px solid #33477e;
   background:#1a2a55;color:#dbe7ff;cursor:pointer;font:inherit}}
</style>
<h1>{html.escape(title)}</h1>
{f"<p>{intro}</p>" if intro else ""}
{review_block}
<ul>{items}</ul>
""", encoding="utf-8")

    if hosted:
        compressed = gzip_tree(output)
        print(f"pre-compressed {compressed} files "
              f"(serve with scripts/serve-gallery.py)")
    mode = ("hosted (serve it — file:// will not load modules)"
            if hosted else "self-contained")
    print(f"built {len(cards)} review pages ({mode}) in {output}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sample", required=True, type=Path,
                        action="append",
                        help="replay-review-sample.py output manifest")
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--title", default=None,
                        help="index heading (default: --index-cards title, "
                             "else 'Replay review')")
    parser.add_argument("--intro", default=None,
                        help="index lede, raw HTML; '' omits it. Default is "
                             "the outcome-blind protocol paragraph, which a "
                             "non-blind gallery must replace")
    parser.add_argument("--index-cards", type=Path, default=None,
                        help="JSON with curated index card copy and order "
                             "(see module docstring)")
    parser.add_argument("--viewer", choices=("hosted", "self-contained"),
                        default="hosted")
    parser.add_argument("--viewer-build", type=Path, default=None,
                        help="hosted viewer build directory (default: web/dist)")
    parser.add_argument("--theme", default="control-room",
                        help="dist-cli theme for self-contained mode")
    parser.add_argument("--review-panel", action="store_true")
    parser.add_argument("--review-protocol", default="outcome-blind-review-v1",
                        help="protocol id stamped into the exported record")
    parser.add_argument(
        "--arc-relay-bars",
        type=Path,
        default=DEFAULT_ARC_RELAY_BARS,
        help="felt-degeneracy registration enforced automatically for "
             "complete Arc Relay broadcast-v1 inputs",
    )
    parser.add_argument(
        "--skip-arc-relay-eligibility",
        action="store_true",
        help="allow ineligible Arc Relay replays only for a clearly labelled "
             "diagnostic gallery",
    )
    build(parser.parse_args())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
