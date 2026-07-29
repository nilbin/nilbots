#!/usr/bin/env python3
"""Normalize generated class-look concepts and build a review sheet."""

from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile

from PIL import Image, ImageDraw, ImageFont


CANVAS_SIZE = 512
SUBJECT_SPAN = 368
CLASS_ORDER = ("striker", "bulwark", "fabricator")
CONCEPT_ORDER = {
    "striker": ("vector-kestrel", "arc-viper", "trident-wasp"),
    "bulwark": ("gatehouse", "aegis-tortoise", "mirror-bastion"),
    "fabricator": ("copyforge", "lattice-loom", "rivet-mantis"),
}
PROJECTILE_ORDER = {
    "striker": ("vector-fork", "arc-cutter", "trident-spark"),
    "bulwark": ("gate-slug", "rebound-diamond", "mirror-wedge"),
    "fabricator": ("copy-bit", "lattice-rivet", "rivet-punch"),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Fit transparent class-look concepts onto canonical 512px canvases "
            "and build a gameplay-scale comparison sheet."
        )
    )
    parser.add_argument(
        "root",
        nargs="?",
        type=Path,
        default=Path("art/class-look-concepts"),
        help="concept root containing <class>/<id>/concept.png",
    )
    return parser.parse_args()


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = (
        (
            Path("/System/Library/Fonts/Supplemental/Arial Bold.ttf")
            if bold
            else Path("/System/Library/Fonts/Supplemental/Arial.ttf")
        ),
        (
            Path("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf")
            if bold
            else Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
        ),
    )
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


def normalized_preview(source: Path) -> Image.Image:
    image = Image.open(source).convert("RGBA")
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError(f"{source} has no visible pixels")
    subject = image.crop(bounds)
    width, height = subject.size
    scale = min(SUBJECT_SPAN / width, SUBJECT_SPAN / height)
    resized = subject.resize(
        (max(1, round(width * scale)), max(1, round(height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    offset = (
        (CANVAS_SIZE - resized.width) // 2,
        (CANVAS_SIZE - resized.height) // 2,
    )
    canvas.alpha_composite(resized, offset)
    return canvas


def card_background(size: tuple[int, int]) -> Image.Image:
    width, height = size
    card = Image.new("RGBA", size, "#171c24")
    draw = ImageDraw.Draw(card)
    preview_top = 66
    preview_bottom = 506
    midpoint = width // 2
    draw.rectangle((20, preview_top, midpoint, preview_bottom), fill="#151a21")
    draw.rectangle((midpoint, preview_top, width - 20, preview_bottom), fill="#c8cdd2")
    draw.rounded_rectangle(
        (20, preview_top, width - 20, preview_bottom),
        radius=18,
        outline="#56606c",
        width=2,
    )
    return card


def build_contact_sheet(root: Path, previews: dict[tuple[str, str], Image.Image]) -> None:
    card_width = 520
    card_height = 590
    gutter = 24
    label_width = 180
    margin = 40
    header_height = 120
    sheet_width = margin * 2 + label_width + card_width * 3 + gutter * 2
    sheet_height = header_height + margin + card_height * 3 + gutter * 2 + margin
    sheet = Image.new("RGBA", (sheet_width, sheet_height), "#090d13")
    draw = ImageDraw.Draw(sheet)
    draw.text(
        (margin, 30),
        "NILBOTS — FRONTLINE CLASS LOOK CONCEPTS",
        font=font(36, bold=True),
        fill="#f4f7fb",
    )
    draw.text(
        (margin, 78),
        "Transparent concept references · normalized to 72% of a 512px canvas · 48px check shown per card",
        font=font(18),
        fill="#9da8b5",
    )

    for row, class_id in enumerate(CLASS_ORDER):
        y = header_height + row * (card_height + gutter)
        draw.text(
            (margin, y + 24),
            class_id.upper(),
            font=font(24, bold=True),
            fill="#d9e2ec",
        )
        for column, concept_id in enumerate(CONCEPT_ORDER[class_id]):
            x = margin + label_width + column * (card_width + gutter)
            card = card_background((card_width, card_height))
            card_draw = ImageDraw.Draw(card)
            card_draw.text(
                (24, 20),
                concept_id.replace("-", " ").title(),
                font=font(24, bold=True),
                fill="#f4f7fb",
            )
            preview = previews[(class_id, concept_id)]
            display = preview.resize((420, 420), Image.Resampling.LANCZOS)
            card.alpha_composite(display, (50, 76))
            gameplay = preview.resize((48, 48), Image.Resampling.LANCZOS)
            card_draw.rounded_rectangle(
                (386, 510, 488, 574),
                radius=10,
                fill="#2a313b",
                outline="#66717e",
                width=1,
            )
            card.alpha_composite(gameplay, (413, 518))
            card_draw.text(
                (24, 532),
                f"{class_id}/{concept_id}",
                font=font(16),
                fill="#9da8b5",
            )
            sheet.alpha_composite(card, (x, y))

    sheet.convert("RGB").save(root / "contact-sheet.jpg", quality=92)


def rasterize_svg(source: Path) -> Image.Image:
    with tempfile.TemporaryDirectory(prefix="nilbots-projectile-") as temporary:
        output = Path(temporary) / "mask.png"
        rsvg_convert = shutil.which("rsvg-convert")
        sips = shutil.which("sips")
        if rsvg_convert is not None:
            command = (
                rsvg_convert,
                "--width",
                "256",
                "--height",
                "256",
                "--output",
                str(output),
                str(source),
            )
        elif sips is not None:
            command = (
                sips,
                "-s",
                "format",
                "png",
                str(source),
                "--out",
                str(output),
            )
        else:
            raise RuntimeError(
                "Projectile sheet generation needs rsvg-convert or macOS sips."
            )
        subprocess.run(
            command,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.PIPE,
            text=True,
        )
        with Image.open(output) as image:
            return image.convert("RGBA").copy()


def tinted_mask(mask: Image.Image, color: str, size: int) -> Image.Image:
    resized = mask.resize((size, size), Image.Resampling.LANCZOS)
    tinted = Image.new("RGBA", resized.size, color)
    tinted.putalpha(resized.getchannel("A"))
    return tinted


def build_projectile_contact_sheet(root: Path) -> None:
    card_width = 420
    card_height = 292
    gutter = 24
    label_width = 180
    margin = 40
    header_height = 120
    sheet_width = margin * 2 + label_width + card_width * 3 + gutter * 2
    sheet_height = header_height + margin + card_height * 3 + gutter * 2 + margin
    sheet = Image.new("RGBA", (sheet_width, sheet_height), "#090d13")
    draw = ImageDraw.Draw(sheet)
    draw.text(
        (margin, 30),
        "NILBOTS — FRONTLINE PROJECTILE CONCEPTS",
        font=font(36, bold=True),
        fill="#f4f7fb",
    )
    draw.text(
        (margin, 78),
        "White-alpha SVG masks · large silhouette plus actual 24px cyan/orange renderer-tint checks",
        font=font(18),
        fill="#9da8b5",
    )

    for row, class_id in enumerate(CLASS_ORDER):
        y = header_height + row * (card_height + gutter)
        draw.text(
            (margin, y + 24),
            class_id.upper(),
            font=font(24, bold=True),
            fill="#d9e2ec",
        )
        for column, projectile_id in enumerate(PROJECTILE_ORDER[class_id]):
            x = margin + label_width + column * (card_width + gutter)
            card = Image.new("RGBA", (card_width, card_height), "#171c24")
            card_draw = ImageDraw.Draw(card)
            card_draw.rounded_rectangle(
                (0, 0, card_width - 1, card_height - 1),
                radius=18,
                outline="#3b4653",
                width=2,
            )
            card_draw.text(
                (24, 20),
                projectile_id.replace("-", " ").title(),
                font=font(24, bold=True),
                fill="#f4f7fb",
            )
            mask = rasterize_svg(
                root / "projectiles" / class_id / f"{projectile_id}.svg"
            )
            card.alpha_composite(tinted_mask(mask, "#ffffff", 176), (28, 66))
            card_draw.text(
                (226, 86),
                "24 px",
                font=font(15, bold=True),
                fill="#9da8b5",
            )
            card_draw.rounded_rectangle(
                (218, 112, 278, 172),
                radius=9,
                fill="#0e141c",
                outline="#46515e",
                width=1,
            )
            card_draw.rounded_rectangle(
                (294, 112, 354, 172),
                radius=9,
                fill="#d6dbe0",
                outline="#69737e",
                width=1,
            )
            card.alpha_composite(tinted_mask(mask, "#38bdf8", 24), (236, 130))
            card.alpha_composite(tinted_mask(mask, "#fb923c", 24), (312, 130))
            card_draw.text(
                (226, 204),
                f"{class_id}/{projectile_id}",
                font=font(15),
                fill="#9da8b5",
            )
            sheet.alpha_composite(card, (x, y))

    sheet.convert("RGB").save(
        root / "projectile-contact-sheet.jpg",
        quality=92,
    )


def main() -> None:
    root = parse_args().root.resolve()
    previews: dict[tuple[str, str], Image.Image] = {}
    for class_id in CLASS_ORDER:
        for concept_id in CONCEPT_ORDER[class_id]:
            concept_dir = root / class_id / concept_id
            preview = normalized_preview(concept_dir / "concept.png")
            preview.save(concept_dir / "preview.png")
            preview.resize((48, 48), Image.Resampling.LANCZOS).save(
                concept_dir / "gameplay-preview.png"
            )
            previews[(class_id, concept_id)] = preview
    build_contact_sheet(root, previews)
    build_projectile_contact_sheet(root)


if __name__ == "__main__":
    main()
