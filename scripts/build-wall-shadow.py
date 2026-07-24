#!/usr/bin/env python3
"""Bake a reusable transparent shadow donor from a wall-trim PNG."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageFilter


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--blur", type=float, default=8)
    parser.add_argument("--offset-x", type=int, default=3)
    parser.add_argument("--offset-y", type=int, default=6)
    parser.add_argument("--opacity", type=float, default=0.48)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    source = Image.open(args.source).convert("RGBA")
    alpha = source.getchannel("A")
    shifted = Image.new("L", source.size)
    shifted.paste(alpha, (args.offset_x, args.offset_y))
    blurred = shifted.filter(ImageFilter.GaussianBlur(args.blur))
    blurred = blurred.point(
        lambda value: round(value * max(0.0, min(args.opacity, 1.0))),
    )
    shadow = Image.new("RGBA", source.size, (7, 10, 13, 0))
    shadow.putalpha(blurred)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    shadow.save(args.output, optimize=True)


if __name__ == "__main__":
    main()
