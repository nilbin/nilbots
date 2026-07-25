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


ATLAS_COLUMNS = 16
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
        "--check",
        action="store_true",
        help="Validate the manifest and current runtime budget without rebuilding.",
    )
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


def scaled_geometry(geometry: Dict[str, Any], scale: int) -> Dict[str, Any]:
    scaled = dict(geometry)
    for key in (
        "contentPixels",
        "gutterPixels",
        "insetPixels",
        "outsetPixels",
        "bevelPixels",
        "hardwareRadius",
    ):
        if key in scaled:
            scaled[key] = max(1, int(round(float(scaled[key]) * scale)))
    scaled["shadowBlur"] = float(scaled["shadowBlur"]) * scale
    scaled["shadowOffset"] = [
        int(round(float(offset) * scale))
        for offset in scaled["shadowOffset"]
    ]
    return scaled


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
    tile_pixels = int(geometry["contentPixels"]) + int(geometry["gutterPixels"]) * 2
    atlas_size = ATLAS_COLUMNS * tile_pixels
    edges = Image.new("RGBA", (atlas_size, atlas_size), (0, 0, 0, 0))
    shadows = Image.new("RGBA", (atlas_size, atlas_size), (0, 0, 0, 0))
    for mask in range(256):
        edge, shadow = bake_variant(mask, material, geometry)
        position = (
            (mask % ATLAS_COLUMNS) * tile_pixels,
            (mask // ATLAS_COLUMNS) * tile_pixels,
        )
        edges.paste(edge, position, edge)
        shadows.paste(shadow, position, shadow)
    return edges, shadows


def validate_floor_config(
    spec_path: Path,
    floor: Dict[str, Any],
) -> None:
    source_path = (spec_path.parent / floor["source"]).resolve()
    if not source_path.is_file():
        raise SystemExit(f"Floor source does not exist: {source_path}")
    runtime_name = str(floor["runtime"])
    if Path(runtime_name).name != runtime_name or not runtime_name.endswith(".webp"):
        raise SystemExit("floor.runtime must be one WebP filename.")
    if int(floor["size"]) < 1:
        raise SystemExit("floor.size must be positive.")
    if not 1 <= int(floor["quality"]) <= 100:
        raise SystemExit("floor.quality must be between 1 and 100.")


def runtime_package_root(
    repo_root: Path,
    theme_id: str,
    runtime: Dict[str, Any],
) -> Path:
    configured = runtime.get("packagePath")
    if configured is None:
        return repo_root / "web" / "src" / "assets" / "themes" / theme_id
    relative = Path(str(configured))
    if relative.is_absolute() or ".." in relative.parts:
        raise SystemExit("runtime.packagePath must stay inside the repository.")
    package_root = (repo_root / relative).resolve()
    if package_root == repo_root or repo_root not in package_root.parents:
        raise SystemExit(
            "runtime.packagePath must be a directory inside the repository.",
        )
    return package_root


def write_floor(
    runtime_root: Path,
    spec_path: Path,
    floor: Dict[str, Any],
) -> None:
    source_path = (spec_path.parent / floor["source"]).resolve()
    size = int(floor["size"])
    image = ImageOps.fit(
        Image.open(source_path).convert("RGB"),
        (size, size),
        method=Image.Resampling.LANCZOS,
    )
    runtime_root.mkdir(parents=True, exist_ok=True)
    image.save(
        runtime_root / floor["runtime"],
        "WEBP",
        quality=int(floor["quality"]),
        method=6,
    )


def write_bundle(
    runtime_root: Path,
    spec_path: Path,
    family_id: str,
    family: Dict[str, Any],
    atlas_scale: int,
    edge_quality: int,
) -> None:
    family_root = (spec_path.parent / family["source"]).resolve().parent
    source_path = (spec_path.parent / family["source"]).resolve()
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
    edges, shadows = bake_atlases(
        albedo,
        scaled_geometry(family["geometry"], atlas_scale),
    )
    edges.save(
        runtime_root / f"wall-{family_id}-edges.webp",
        "WEBP",
        quality=edge_quality,
        method=6,
        exact=True,
    )
    shadows.save(
        runtime_root / f"wall-{family_id}-shadows.webp",
        "WEBP",
        lossless=True,
        method=6,
    )


def validate_runtime_manifest(
    runtime_root: Path,
    floor: Dict[str, Any] | None,
    families: Dict[str, Any],
    atlas_scale: int,
) -> None:
    dimensions = {
        (
            int(family["geometry"]["contentPixels"]) * atlas_scale,
            int(family["geometry"]["gutterPixels"]) * atlas_scale,
        )
        for family in families.values()
    }
    if len(dimensions) != 1:
        raise SystemExit("Every wall family must use the same atlas dimensions.")
    content_pixels, gutter_pixels = dimensions.pop()
    manifest_path = runtime_root / "theme.json"
    manifest = json.loads(manifest_path.read_text())
    if floor is not None and manifest["textures"]["floor"] != floor["runtime"]:
        raise SystemExit(
            f"{manifest_path} textures.floor must be '{floor['runtime']}'.",
        )
    atlas = manifest["walls"]["atlas"]
    expected = {
        "columns": ATLAS_COLUMNS,
        "contentPixels": content_pixels,
        "gutterPixels": gutter_pixels,
    }
    if atlas != expected:
        raise SystemExit(
            f"{manifest_path} walls.atlas must be {expected}, got {atlas}.",
        )


def validate_floor_output(
    runtime_root: Path,
    floor: Dict[str, Any],
) -> None:
    runtime_path = runtime_root / floor["runtime"]
    if not runtime_path.is_file():
        raise SystemExit(f"Built floor does not exist: {runtime_path}")
    with Image.open(runtime_path) as image:
        expected_size = (int(floor["size"]), int(floor["size"]))
        if image.size != expected_size:
            raise SystemExit(
                f"{runtime_path} must be {expected_size}, got {image.size}.",
            )


def validate_runtime_budget(
    runtime_root: Path,
    theme_id: str,
    budget_bytes: int,
) -> None:
    total_bytes = sum(
        path.stat().st_size
        for path in runtime_root.iterdir()
        if path.is_file()
    )
    if total_bytes > budget_bytes:
        raise SystemExit(
            f"{theme_id} runtime assets use {total_bytes:,} bytes, "
            f"over the {budget_bytes:,}-byte budget.",
        )
    print(
        f"{theme_id} runtime assets: {total_bytes:,} / {budget_bytes:,} bytes",
    )


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    spec_path = args.spec.resolve()
    spec = json.loads(spec_path.read_text())
    if spec.get("formatVersion") != 2:
        raise SystemExit("Unsupported theme art formatVersion.")
    theme_id = spec["themeId"]
    runtime = spec["runtime"]
    atlas_scale = int(runtime["atlasScale"])
    edge_quality = int(runtime["edgeQuality"])
    budget_bytes = int(runtime["assetBudgetBytes"])
    package_root = runtime_package_root(repo_root, theme_id, runtime)
    if atlas_scale < 1:
        raise SystemExit("runtime.atlasScale must be at least 1.")
    if not 1 <= edge_quality <= 100:
        raise SystemExit("runtime.edgeQuality must be between 1 and 100.")
    floor = spec.get("floor")
    if floor is not None:
        validate_floor_config(spec_path, floor)
    families = spec["wallFamilies"]
    validate_runtime_manifest(package_root, floor, families, atlas_scale)
    if args.check:
        if floor is not None:
            validate_floor_output(package_root, floor)
        validate_runtime_budget(package_root, theme_id, budget_bytes)
        return
    if floor is not None:
        print(f"Baking {theme_id}/floor")
        write_floor(package_root, spec_path, floor)
    for family_id, family in families.items():
        print(f"Baking {theme_id}/{family_id}")
        write_bundle(
            package_root,
            spec_path,
            family_id,
            family,
            atlas_scale,
            edge_quality,
        )
    if floor is not None:
        validate_floor_output(package_root, floor)
    validate_runtime_budget(package_root, theme_id, budget_bytes)


if __name__ == "__main__":
    main()
