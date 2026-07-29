#!/usr/bin/env python3
"""Build a servable review gallery from a replay sample manifest.

Consolidates the blind-review flow used for the classes wave-1 factorial:
take the header-only sample manifest written by ``replay-review-sample.py``,
inject each replay into a built viewer template at the CLI's
``<!--BOTARENA_REPLAY-->`` marker (the ``ReplayOutput.WriteViewer``
semantics, including the ``</`` escape), and emit an index. Two viewer
modes:

- ``--viewer hosted`` (default): inject into ``web/dist/index.html`` and
  copy its hashed assets beside the pages. Full renderer (lazy WebGL);
  must be SERVED (http.server / cloudflared per the replay-highlights
  skill) — file:// blocks module loading.
- ``--viewer self-contained``: inject into a ``web/dist-cli/<theme>``
  template. Portable single files, Canvas2D only (dist-cli excludes
  Three.js by design).

``--review-panel`` appends the outcome-blind rating panel to every page
(two 1-5 scores — fun to watch, easy to follow — plus free notes;
autosaved to localStorage) and gives the index per-sample progress plus a
"Copy review JSON" button that copies the decision-record artifact to the
clipboard and shows it in a selectable box (clipboard needs a secure
context, so the box is the fallback on plain-http LAN).
The index shows pairing and map but never outcomes; keep it that way.

Example:
    python3 scripts/build-review-gallery.py \
        --sample /tmp/run/blind-review-sample.json \
        --output sandbox/review-gallery \
        --title "Classes wave 1 — blind review" \
        --review-panel
"""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import re
import shutil
from pathlib import Path

MARKER = "<!--BOTARENA_REPLAY-->"
REPO = Path(__file__).resolve().parent.parent

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
    entries.sort(key=lambda entry: hashlib.sha256(
        str(entry.get("source", entry)).encode()).hexdigest())
    for index, entry in enumerate(entries, start=1):
        entry["id"] = f"sample-{index:02}"
    output = args.output
    output.mkdir(parents=True, exist_ok=True)

    if args.viewer == "hosted":
        dist = REPO / "web" / "dist"
        template_path = dist / "index.html"
        if not template_path.exists():
            raise SystemExit(
                "web/dist/index.html missing — run `npm run build` in web/")
        assets = output / "assets"
        if assets.exists():
            shutil.rmtree(assets)
        shutil.copytree(dist / "assets", assets)
        for extra in dist.iterdir():
            if extra.is_file() and extra.name != "index.html":
                shutil.copy2(extra, output / extra.name)
    else:
        template_path = (
            REPO / "web" / "dist-cli" / args.theme / "index.html")
        if not template_path.exists():
            raise SystemExit(
                f"{template_path} missing — run `npm run build` in web/")
    template = template_path.read_text(encoding="utf-8")
    if MARKER not in template:
        raise SystemExit(f"{template_path}: injection marker missing")

    cards = []
    for index, entry in enumerate(entries, start=1):
        sid = f"sample-{index:02}"
        replay = Path(entry["source"]).read_text(encoding="utf-8")
        page = template.replace(
            MARKER,
            "<script>window.__BOTARENA_REPLAY__ = "
            + replay.replace("</", "<\\/")
            + ";</script>")
        if args.review_panel:
            panel = PANEL_TEMPLATE.replace("__SID__", sid)
            page = (
                page.replace("</body>", panel + "</body>")
                if "</body>" in page
                else page + panel)
        (output / f"{sid}.html").write_text(page, encoding="utf-8")
        cards.append((sid, f"Sample {index}", card_labels(entry)))

    items = "\n".join(
        f'<li><a href="{sid}.html"><strong>{html.escape(title)}</strong>'
        f" <span>{html.escape(subtitle)}</span>"
        f'<em class=prog data-sid="{sid}"></em></a></li>'
        for sid, title, subtitle in cards)
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
   protocol:'outcome-blind-review-v1',samples:{{}}}};
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
<title>{html.escape(args.title)}</title>
<style>
 body{{font:16px/1.5 system-ui;margin:2rem auto;max-width:40rem;padding:0 1rem;
      background:#0b1020;color:#dbe7ff}}
 h1{{font-size:1.3rem}} p{{color:#9fb2d8}}
 ul{{list-style:none;padding:0}} li{{margin:.4rem 0}}
 a{{display:block;padding:.7rem 1rem;border:1px solid #27355c;
   border-radius:.5rem;color:#dbe7ff;text-decoration:none;position:relative}}
 a:hover{{border-color:#4a6cc3}} a span{{color:#9fb2d8;font-size:.9rem;display:block}}
 em.prog{{position:absolute;right:1rem;top:.9rem;font-style:normal;color:#7fd6a0}}
 button{{padding:.6rem 1rem;border-radius:.5rem;border:1px solid #33477e;
   background:#1a2a55;color:#dbe7ff;cursor:pointer;font:inherit}}
</style>
<h1>{html.escape(args.title)}</h1>
<p>Outcome-blind review: outcomes are hidden. Watch at normal speed and give
two quick 1&ndash;5 scores per sample &mdash; fun to watch, easy to follow
&mdash; plus any notes.</p>
{review_block}
<ul>{items}</ul>
""", encoding="utf-8")
    mode = ("hosted (serve it — file:// will not load modules)"
            if args.viewer == "hosted" else "self-contained")
    print(f"built {len(cards)} review pages ({mode}) in {output}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sample", required=True, type=Path,
                        action="append",
                        help="replay-review-sample.py output manifest")
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--title", default="Replay review")
    parser.add_argument("--viewer", choices=("hosted", "self-contained"),
                        default="hosted")
    parser.add_argument("--theme", default="control-room",
                        help="dist-cli theme for self-contained mode")
    parser.add_argument("--review-panel", action="store_true")
    build(parser.parse_args())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
