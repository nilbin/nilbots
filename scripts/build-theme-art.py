#!/usr/bin/env python3
"""Build seamless material bundles and topology atlases for an arena theme.

The generated source image supplies the art direction. Everything after that is
deterministic: edge-safe normalization, derived PBR helper maps, and a complete
256-entry eight-neighbour atlas for each wall family.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Dict, Tuple

import numpy as np
from PIL import Image, ImageColor, ImageDraw, ImageFilter, ImageOps


CARDINAL_BITS = {
    "north": 0,
    "east": 2,
    "south": 4,
    "west": 6,
}
NEIGHBOURS = (
    (0, -1),   # N
    (1, -1),   # NE
    (1, 0),    # E
    (1, 1),    # SE
    (0, 1),    # S
    (-1, 1),   # SW
    (-1, 0),   # W
    (-1, -1),  # NW
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("spec", type=Path, help="Theme art JSON specification.")
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
    )
    return parser.parse_args()


def colour(value: str) -> np.ndarray:
    return np.array(ImageColor.getrgb(value), dtype=np.float32)


def odd_filter_size(radius: int) -> int:
    return max(3, radius * 2 + 1)


def edge_safe_material(source: Image.Image, size: int) -> Image.Image:
    """Make a guaranteed edge-matching material without smearing a seam.

    A mirrored 2x2 construction is periodic in both axes. The source remains
    recognizable, while opposite edges and their first derivative agree.
    """

    half = size // 2
    base = ImageOps.fit(
        source.convert("RGB"),
        (half, half),
        method=Image.Resampling.LANCZOS,
    )
    output = Image.new("RGB", (size, size))
    output.paste(base, (0, 0))
    output.paste(ImageOps.mirror(base), (half, 0))
    output.paste(ImageOps.flip(base), (0, half))
    output.paste(ImageOps.flip(ImageOps.mirror(base)), (half, half))
    return output


def derive_material_maps(
    albedo: Image.Image,
    normal_strength: float,
    roughness_base: int,
) -> Dict[str, Image.Image]:
    rgb = np.asarray(albedo, dtype=np.float32) / 255.0
    luminance = rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722
    gray = Image.fromarray(np.uint8(np.clip(luminance * 255.0, 0, 255)))
    broad = np.asarray(gray.filter(ImageFilter.GaussianBlur(12)), dtype=np.float32) / 255.0
    fine = luminance - broad

    height = np.clip(0.50 + fine * 1.55 + (luminance - 0.5) * 0.18, 0.0, 1.0)
    dy, dx = np.gradient(height)
    nx = -dx * normal_strength
    ny = -dy * normal_strength
    nz = np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.dstack(
        (
            nx / length * 0.5 + 0.5,
            ny / length * 0.5 + 0.5,
            nz / length * 0.5 + 0.5,
        ),
    )

    micro = np.abs(fine)
    roughness = np.clip(roughness_base / 255.0 + micro * 0.75, 0.18, 1.0)
    cavity = np.clip(broad - height, 0.0, 1.0)
    ao = np.clip(1.0 - cavity * 2.4, 0.35, 1.0)

    return {
        "height": Image.fromarray(np.uint8(height * 255.0)),
        "normal": Image.fromarray(np.uint8(np.clip(normal * 255.0, 0, 255))),
        "roughness": Image.fromarray(np.uint8(roughness * 255.0)),
        "ao": Image.fromarray(np.uint8(ao * 255.0)),
    }


def occupancy(mask: int, content: int, gutter: int) -> Image.Image:
    extent = content * 3 + gutter * 2
    image = Image.new("L", (extent, extent), 0)
    draw = ImageDraw.Draw(image)
    origin = gutter + content
    draw.rectangle(
        (origin, origin, origin + content - 1, origin + content - 1),
        fill=255,
    )
    for bit, (dx, dy) in enumerate(NEIGHBOURS):
        if not mask & (1 << bit):
            continue
        x = origin + dx * content
        y = origin + dy * content
        draw.rectangle((x, y, x + content - 1, y + content - 1), fill=255)
    return image


def material_patch(
    material: Image.Image,
    size: Tuple[int, int],
    seed: int,
) -> np.ndarray:
    width, height = size
    tiled = Image.new("RGB", size)
    source = material
    offset_x = (seed * 83) % source.width
    offset_y = (seed * 47) % source.height
    for y in range(-offset_y, height, source.height):
        for x in range(-offset_x, width, source.width):
            tiled.paste(source, (x, y))
    return np.asarray(tiled, dtype=np.float32)


def bake_variant(
    mask: int,
    material: Image.Image,
    geometry: Dict[str, Any],
) -> Tuple[Image.Image, Image.Image]:
    content = int(geometry["contentPixels"])
    gutter = int(geometry["gutterPixels"])
    tile_pixels = content + gutter * 2
    inset = int(geometry["insetPixels"])
    outset = int(geometry["outsetPixels"])
    bevel = int(geometry["bevelPixels"])
    footprint_image = occupancy(mask, content, gutter)
    footprint = np.asarray(footprint_image, dtype=np.float32) / 255.0
    outer_image = footprint_image.filter(ImageFilter.MaxFilter(odd_filter_size(outset)))
    inner_image = footprint_image.filter(ImageFilter.MinFilter(odd_filter_size(inset)))
    inner = np.asarray(inner_image, dtype=np.float32) / 255.0
    outer = np.asarray(outer_image, dtype=np.float32) / 255.0

    smooth = np.asarray(
        footprint_image.filter(ImageFilter.GaussianBlur(max(1.0, bevel / 1.8))),
        dtype=np.float32,
    ) / 255.0
    gy, gx = np.gradient(smooth)
    light_x, light_y = geometry.get("light", [-0.55, -0.83])
    directional = np.clip(-(gx * light_x + gy * light_y) * 2.7, -1.0, 1.0)

    side_mask = np.clip(outer - inner, 0.0, 1.0)
    lip_image = footprint_image.filter(ImageFilter.MinFilter(odd_filter_size(bevel)))
    lip = np.clip(footprint - np.asarray(lip_image, dtype=np.float32) / 255.0, 0.0, 1.0)
    edge_colour = colour(geometry["edgeColour"])
    side_colour = colour(geometry["sideColour"])
    highlight_colour = colour(geometry["highlightColour"])
    texture = material_patch(material, footprint_image.size, mask)
    base = side_colour[None, None, :] * 0.72 + texture * 0.28
    shade = np.clip(0.76 + directional[..., None] * 0.24, 0.48, 1.12)
    rgba = np.zeros((*footprint.shape, 4), dtype=np.float32)
    rgba[..., :3] = np.clip(base * shade, 0, 255)
    rgba[..., 3] = side_mask * 255.0

    lip_alpha = lip * (0.58 + np.clip(directional, 0.0, 1.0) * 0.38)
    lip_rgb = edge_colour[None, None, :] * 0.65 + highlight_colour[None, None, :] * 0.35
    rgba[..., :3] = (
        rgba[..., :3] * (1.0 - lip_alpha[..., None])
        + lip_rgb * lip_alpha[..., None]
    )
    rgba[..., 3] = np.maximum(rgba[..., 3], lip_alpha * 255.0)

    # Theme-authored seam hardware is part of the baked sprite, never canvas CSS.
    detail = Image.fromarray(np.uint8(np.clip(rgba, 0, 255)))
    draw = ImageDraw.Draw(detail, "RGBA")
    origin = gutter + content
    hardware = ImageColor.getrgb(geometry["hardwareColour"])
    radius = max(1, int(geometry.get("hardwareRadius", 2)))
    if not mask & (1 << CARDINAL_BITS["north"]):
        for fraction in (0.28, 0.72):
            x = origin + round(content * fraction)
            y = origin + inset
            draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=(*hardware, 205))
    if not mask & (1 << CARDINAL_BITS["south"]):
        for fraction in (0.28, 0.72):
            x = origin + round(content * fraction)
            y = origin + content - inset
            draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=(*hardware, 145))

    shadow_opacity = float(geometry["shadowOpacity"])
    shadow_mask = outer_image.filter(ImageFilter.GaussianBlur(float(geometry["shadowBlur"])))
    shadow = Image.new("L", footprint_image.size, 0)
    shadow.paste(
        shadow_mask,
        (
            int(geometry["shadowOffset"][0]),
            int(geometry["shadowOffset"][1]),
        ),
    )
    shadow_array = np.asarray(shadow, dtype=np.float32) * shadow_opacity
    shadow_colour = ImageColor.getrgb(geometry["shadowColour"])
    shadow_rgba = np.zeros((*footprint.shape, 4), dtype=np.uint8)
    shadow_rgba[..., :3] = shadow_colour
    shadow_rgba[..., 3] = np.uint8(np.clip(shadow_array, 0, 255))
    shadow_image = Image.fromarray(shadow_rgba)

    crop_box = (
        origin - gutter,
        origin - gutter,
        origin + content + gutter,
        origin + content + gutter,
    )
    return detail.crop(crop_box), shadow_image.crop(crop_box)


def bake_atlases(
    material: Image.Image,
    geometry: Dict[str, Any],
) -> Tuple[Image.Image, Image.Image]:
    columns = 16
    tile_pixels = int(geometry["contentPixels"]) + int(geometry["gutterPixels"]) * 2
    atlas_size = columns * tile_pixels
    edges = Image.new("RGBA", (atlas_size, atlas_size), (0, 0, 0, 0))
    shadows = Image.new("RGBA", (atlas_size, atlas_size), (0, 0, 0, 0))
    for mask in range(256):
        edge, shadow = bake_variant(mask, material, geometry)
        position = ((mask % columns) * tile_pixels, (mask // columns) * tile_pixels)
        edges.paste(edge, position, edge)
        shadows.paste(shadow, position, shadow)
    return edges, shadows


def write_bundle(
    repo_root: Path,
    spec_path: Path,
    theme_id: str,
    family_id: str,
    family: Dict[str, Any],
) -> None:
    family_root = (spec_path.parent / family["source"]).resolve().parent
    source_path = (spec_path.parent / family["source"]).resolve()
    runtime_root = repo_root / "web" / "src" / "assets" / "themes" / theme_id
    runtime_root.mkdir(parents=True, exist_ok=True)

    material_config = family["material"]
    albedo = edge_safe_material(
        Image.open(source_path),
        int(material_config.get("size", 1024)),
    )
    maps = derive_material_maps(
        albedo,
        float(material_config.get("normalStrength", 8.0)),
        int(material_config.get("roughnessBase", 176)),
    )
    albedo.save(family_root / "albedo.png", optimize=True)
    for map_id, image in maps.items():
        image.save(family_root / f"{map_id}.png", optimize=True)

    runtime_albedo = runtime_root / f"wall-{family_id}-albedo.webp"
    albedo.save(runtime_albedo, "WEBP", quality=90, method=6)
    edges, shadows = bake_atlases(albedo, family["geometry"])
    edges.save(
        runtime_root / f"wall-{family_id}-edges.webp",
        "WEBP",
        lossless=True,
        method=6,
    )
    shadows.save(
        runtime_root / f"wall-{family_id}-shadows.webp",
        "WEBP",
        lossless=True,
        method=6,
    )


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    spec_path = args.spec.resolve()
    spec = json.loads(spec_path.read_text())
    if spec.get("formatVersion") != 1:
        raise SystemExit("Unsupported theme art formatVersion.")
    theme_id = spec["themeId"]
    for family_id, family in spec["wallFamilies"].items():
        print(f"Baking {theme_id}/{family_id}")
        write_bundle(repo_root, spec_path, theme_id, family_id, family)


if __name__ == "__main__":
    main()
