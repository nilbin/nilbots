#!/usr/bin/env python3
"""Build a deployable, phone-friendly gallery from self-contained replays.

Usage:
    scripts/build-replay-gallery.py <viewer-dir> <site-dir>

The viewer directory may contain gallery.json:

{
  "title": "Nilbots Gen-8 highlights",
  "subtitle": "Four tournament moments.",
  "replays": [
    {
      "file": "01-active-holder.html",
      "title": "ActiveHolder dethrones Bastille",
      "description": "Crossfire, bolt2 — elimination on tick 46."
    }
  ]
}

Without a manifest, every HTML file except index.html is included in filename
order and its filename becomes the display title.
"""

from __future__ import annotations

import argparse
import html
import json
import re
import shutil
from dataclasses import dataclass
from pathlib import Path


REPLAY_MARKER = "window.__BOTARENA_REPLAY__"
DEFAULT_TITLE = "Nilbots replay highlights"
DEFAULT_SUBTITLE = "Self-contained Bot Arena replays. Tap one to watch."
WORKER = """export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (url.pathname === "/") {
      url.pathname = "/index.html";
      return env.ASSETS.fetch(new Request(url, request));
    }

    return env.ASSETS.fetch(request);
  },
};
"""


@dataclass(frozen=True)
class ReplayCard:
    source: Path
    filename: str
    title: str
    description: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("viewer_dir", type=Path, help="directory containing viewer HTML files")
    parser.add_argument("site_dir", type=Path, help="generated Sites source directory")
    parser.add_argument(
        "--hosting-config",
        type=Path,
        default=Path(".openai/hosting.json"),
        help="Sites config to copy (default: .openai/hosting.json)",
    )
    return parser.parse_args()


def display_title(filename: str) -> str:
    stem = Path(filename).stem
    stem = re.sub(r"^\d+[-_. ]*", "", stem)
    return re.sub(r"[-_.]+", " ", stem).strip().title()


def load_gallery(viewer_dir: Path) -> tuple[str, str, list[ReplayCard]]:
    manifest_path = viewer_dir / "gallery.json"
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        title = require_text(manifest, "title", manifest_path)
        subtitle = manifest.get("subtitle", DEFAULT_SUBTITLE)
        if not isinstance(subtitle, str):
            raise ValueError(f"{manifest_path}: subtitle must be a string")
        replay_items = manifest.get("replays")
        if not isinstance(replay_items, list) or not replay_items:
            raise ValueError(f"{manifest_path}: replays must be a non-empty array")

        cards = []
        for item in replay_items:
            if not isinstance(item, dict):
                raise ValueError(f"{manifest_path}: each replay must be an object")
            filename = require_text(item, "file", manifest_path)
            cards.append(
                ReplayCard(
                    source=viewer_dir / filename,
                    filename=filename,
                    title=require_text(item, "title", manifest_path),
                    description=require_text(item, "description", manifest_path),
                )
            )
        return title, subtitle, cards

    viewers = sorted(path for path in viewer_dir.glob("*.html") if path.name != "index.html")
    if not viewers:
        raise ValueError(f"{viewer_dir}: no replay HTML files found")
    cards = [
        ReplayCard(
            source=viewer,
            filename=viewer.name,
            title=display_title(viewer.name),
            description="Self-contained Bot Arena replay.",
        )
        for viewer in viewers
    ]
    return DEFAULT_TITLE, DEFAULT_SUBTITLE, cards


def require_text(data: dict[str, object], key: str, source: Path) -> str:
    value = data.get(key)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{source}: {key} must be a non-empty string")
    return value.strip()


def validate_cards(cards: list[ReplayCard]) -> None:
    seen: set[str] = set()
    for card in cards:
        if Path(card.filename).name != card.filename or not card.filename.endswith(".html"):
            raise ValueError(f"{card.filename}: file must be a plain .html filename")
        if card.filename == "index.html":
            raise ValueError("index.html is reserved for the gallery")
        if card.filename in seen:
            raise ValueError(f"{card.filename}: duplicate replay filename")
        seen.add(card.filename)
        if not card.source.is_file():
            raise ValueError(f"{card.source}: replay file does not exist")
        if REPLAY_MARKER not in card.source.read_text(encoding="utf-8"):
            raise ValueError(f"{card.source}: missing embedded replay marker")


def validate_hosting_config(path: Path) -> dict[str, object]:
    config = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(config.get("project_id"), str) or not config["project_id"]:
        raise ValueError(f"{path}: missing project_id")
    return config


def render_index(title: str, subtitle: str, cards: list[ReplayCard]) -> str:
    links = "\n".join(
        f"""    <a href="./{html.escape(card.filename, quote=True)}">
      <strong>{html.escape(card.title)}</strong>
      <span>{html.escape(card.description)}</span>
    </a>"""
        for card in cards
    )
    return f"""<!doctype html>
<html lang="en">
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="color-scheme" content="dark">
<title>{html.escape(title)}</title>
<style>
  * {{ box-sizing: border-box; }}
  body {{ margin: 0; padding: 24px; color: #e8eef7; background: #091018;
    font: 16px/1.45 system-ui, sans-serif; }}
  main {{ max-width: 760px; margin: auto; }}
  h1 {{ margin: 0 0 6px; font-size: clamp(1.7rem, 7vw, 2.5rem); }}
  p {{ margin: 0 0 24px; color: #9fb0c5; }}
  a {{ display: block; margin: 14px 0; padding: 18px; color: #f5fbff;
    text-decoration: none; border: 1px solid #29445d; border-radius: 12px;
    background: #101c28; touch-action: manipulation; }}
  a:hover, a:focus-visible {{ border-color: #54d4ff; outline: none; }}
  strong {{ display: block; margin-bottom: 4px; color: #54d4ff; }}
  span {{ color: #afbed0; }}
</style>
<main>
  <h1>{html.escape(title)}</h1>
  <p>{html.escape(subtitle)}</p>
{links}
</main>
</html>
"""


def build_site(
    viewer_dir: Path,
    site_dir: Path,
    hosting_config_path: Path,
) -> tuple[int, int]:
    viewer_dir = viewer_dir.resolve()
    site_dir = site_dir.resolve()
    if viewer_dir == site_dir:
        raise ValueError("viewer and site directories must be different")

    title, subtitle, cards = load_gallery(viewer_dir)
    validate_cards(cards)
    hosting_config = validate_hosting_config(hosting_config_path)

    assets_dir = site_dir / ".open-next" / "assets"
    assets_dir.mkdir(parents=True, exist_ok=True)
    expected = {card.filename for card in cards} | {"index.html"}
    for old_html in assets_dir.glob("*.html"):
        if old_html.name not in expected:
            old_html.unlink()

    total_bytes = 0
    for card in cards:
        destination = assets_dir / card.filename
        shutil.copyfile(card.source, destination)
        total_bytes += destination.stat().st_size

    index = render_index(title, subtitle, cards)
    (assets_dir / "index.html").write_text(index, encoding="utf-8")
    (site_dir / ".open-next" / "worker.js").write_text(WORKER, encoding="utf-8")
    output_config = site_dir / ".openai" / "hosting.json"
    output_config.parent.mkdir(parents=True, exist_ok=True)
    output_config.write_text(json.dumps(hosting_config, indent=2) + "\n", encoding="utf-8")
    return len(cards), total_bytes


def main() -> None:
    args = parse_args()
    count, total_bytes = build_site(args.viewer_dir, args.site_dir, args.hosting_config)
    print(
        f"Built {count} self-contained replays "
        f"({total_bytes / 1024 / 1024:.1f} MiB) in {args.site_dir.resolve()}"
    )


if __name__ == "__main__":
    main()
