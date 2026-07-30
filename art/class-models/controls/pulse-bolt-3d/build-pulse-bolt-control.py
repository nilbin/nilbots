#!/usr/bin/env python3
"""Build the art-side procedural 3D Pulse Bolt comparison control.

Run through the pinned portable Blender:

  Blender --background --factory-startup --python \
    art/class-models/controls/pulse-bolt-3d/build-pulse-bolt-control.py -- \
    --repo-root "$PWD"

The runtime SVG remains canonical. This script asserts its exact hash, maps
the authored path coordinates to shallow genuine volumes, and keeps every
surface inside the canonical outer planform. It does not install a runtime
model or create a look/catalog ID.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


EXPECTED_SVG_SHA256 = (
    "21e821e5dda1c46ea666f46699c9a8311528c5db868f2a1d445ff08c43f0ef06"
)
SIZE = 1024

# Exact vertices from the four canonical SVG paths. Coordinates are mapped
# from the 256 viewBox with (128, 128) at the origin and +X still East.
OUTER = ((26, 128), (72, 88), (154, 88), (230, 128), (154, 168), (72, 168))
MID = ((48, 128), (86, 103), (158, 103), (213, 128), (158, 153), (86, 153))
CORE = ((84, 128), (122, 116), (164, 116), (202, 128), (164, 140), (122, 140))
UPPER_FACET = ((76, 96), (121, 116), (85, 119), (55, 106))
LOWER_FACET = ((76, 160), (121, 140), (85, 137), (55, 150))


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(collection):
            collection.remove(datablock)


def point(value: tuple[int, int]) -> tuple[float, float]:
    # The outer path spans 204 SVG units, so it becomes exactly one authored
    # gameplay tile without non-uniform scaling.
    return ((value[0] - 128) / 204, -(value[1] - 128) / 204)


def projectile_material(
    name: str,
    base: tuple[float, float, float, float],
    *,
    metallic: float,
    roughness: float,
    emission_strength: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material["nilbotsRole"] = "projectile-mask"
    material.diffuse_color = base
    nodes = material.node_tree.nodes
    principled = nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = base
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Emission Color"].default_value = base
    principled.inputs["Emission Strength"].default_value = emission_strength
    return material


def prism(
    name: str,
    points: tuple[tuple[int, int], ...],
    bottom: float,
    top: float,
    material: bpy.types.Material,
    *,
    bevel: float,
) -> bpy.types.Object:
    planar = [point(value) for value in points]
    count = len(planar)
    vertices = [(x, y, bottom) for x, y in planar] + [
        (x, y, top) for x, y in planar
    ]
    faces: list[tuple[int, ...]] = [
        tuple(reversed(range(count))),
        tuple(range(count, count * 2)),
    ]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append(
            (index, next_index, count + next_index, count + index)
        )
    mesh = bpy.data.meshes.new(f"{name}_MESH")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    if bevel > 0:
        modifier = obj.modifiers.new("AUTHORED_EDGE_FACET", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        modifier.affect = "EDGES"
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    return obj


def build_geometry() -> tuple[list[bpy.types.Object], list[bpy.types.Material]]:
    shell = projectile_material(
        "NB_PROJECTILE_MASK_SHELL",
        (0.72, 0.76, 0.80, 1.0),
        metallic=0.18,
        roughness=0.34,
        emission_strength=0.12,
    )
    energy = projectile_material(
        "NB_PROJECTILE_MASK_CORE",
        (0.96, 0.98, 1.0, 1.0),
        metallic=0.02,
        roughness=0.22,
        emission_strength=0.85,
    )
    objects = [
        # The canonical low-opacity envelope is the exact silhouette. Keep
        # its border unexpanded; all visible faceting comes from real side
        # depth and the contained raised layers.
        prism("PulseBolt_OuterVolume", OUTER, 0.0, 0.075, shell, bevel=0.0),
        prism("PulseBolt_MidFacet", MID, 0.055, 0.112, shell, bevel=0.008),
        prism("PulseBolt_Core", CORE, 0.085, 0.155, energy, bevel=0.007),
        prism(
            "PulseBolt_UpperBreakup",
            UPPER_FACET,
            0.078,
            0.128,
            energy,
            bevel=0.005,
        ),
        prism(
            "PulseBolt_LowerBreakup",
            LOWER_FACET,
            0.078,
            0.128,
            energy,
            bevel=0.005,
        ),
    ]
    return objects, [shell, energy]


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = SIZE
    scene.render.resolution_y = SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("AUDIT_WORLD")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.184, 0.184, 0.184, 1.0)
    background.inputs["Strength"].default_value = 0.7
    for name, location, energy, size in (
        ("Key", (2.5, -3.0, 4.5), 900.0, 4.0),
        ("Fill", (-3.0, -1.0, 2.0), 450.0, 5.0),
        ("Rim", (0.5, 3.5, 3.0), 600.0, 3.0),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        obj.location = location
        obj.rotation_euler = (
            Vector((0.0, 0.0, 0.08)) - Vector(location)
        ).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(obj)


def camera(
    name: str,
    location: tuple[float, float, float],
    target: tuple[float, float, float],
    *,
    orthographic_scale: float | None,
) -> bpy.types.Object:
    data = bpy.data.cameras.new(f"{name}_CAMERA")
    obj = bpy.data.objects.new(f"{name}_CAMERA", data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (
        Vector(target) - Vector(location)
    ).to_track_quat("-Z", "Y").to_euler()
    if orthographic_scale is None:
        data.type = "PERSP"
        data.angle = math.radians(42)
    else:
        data.type = "ORTHO"
        data.ortho_scale = orthographic_scale
    bpy.context.scene.camera = obj
    return obj


def render(
    output: Path,
    location: tuple[float, float, float],
    target: tuple[float, float, float],
    *,
    orthographic_scale: float | None,
    transparent: bool = False,
    override: bpy.types.Material | None = None,
) -> None:
    scene = bpy.context.scene
    scene.render.film_transparent = transparent
    scene.view_layers[0].material_override = override
    active = camera(
        output.stem,
        location,
        target,
        orthographic_scale=orthographic_scale,
    )
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(active, do_unlink=True)
    scene.view_layers[0].material_override = None


def tint(materials: list[bpy.types.Material], color: tuple[float, float, float, float]) -> None:
    for material in materials:
        principled = material.node_tree.nodes.get("Principled BSDF")
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Emission Color"].default_value = color
        material.diffuse_color = color


def mask_material() -> bpy.types.Material:
    material = bpy.data.materials.new("AUDIT_MASK")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return material


def main() -> None:
    args = parse_args()
    repo_root = args.repo_root.resolve()
    directory = repo_root / "art/class-models/controls/pulse-bolt-3d"
    review = directory / "review"
    review.mkdir(parents=True, exist_ok=True)
    svg = repo_root / "web/src/assets/projectile-looks/pulse-bolt/sprite.svg"
    svg_hash = sha256(svg)
    if svg_hash != EXPECTED_SVG_SHA256:
        raise ValueError(
            f"Pulse Bolt SVG changed: {svg_hash}; review the path mapping."
        )

    clear_scene()
    objects, materials = build_geometry()
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    blend_path = directory / "pulse-bolt-control.blend"
    glb_path = directory / "pulse-bolt-control.glb"
    bpy.context.preferences.filepaths.save_version = 0
    backup_path = directory / "pulse-bolt-control.blend1"
    if backup_path.exists():
        backup_path.unlink()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_extras=True,
        export_yup=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
    )

    configure_scene()
    center_z = 0.0775
    render(
        review / "normalized-top.png",
        (0.0, 0.0, 3.0),
        (0.0, 0.0, center_z),
        orthographic_scale=1.22,
    )
    render(
        review / "normalized-side.png",
        (0.0, -3.0, center_z),
        (0.0, 0.0, center_z),
        orthographic_scale=1.22,
    )
    render(
        review / "normalized-iso.png",
        (1.65, -1.75, 1.35 + center_z),
        (0.0, 0.0, center_z),
        orthographic_scale=1.32,
    )
    pitch = math.radians(58)
    distance = 1.8
    gameplay_location = (
        0.0,
        -math.cos(pitch) * distance,
        center_z + math.sin(pitch) * distance,
    )
    neutral_shell = (0.72, 0.76, 0.80, 1.0)
    neutral_core = (0.96, 0.98, 1.0, 1.0)
    render(
        review / "gameplay-neutral.png",
        gameplay_location,
        (0.0, 0.0, center_z),
        orthographic_scale=None,
        transparent=True,
    )
    tint(materials, (0.13, 0.83, 0.93, 1.0))
    render(
        review / "gameplay-cyan.png",
        gameplay_location,
        (0.0, 0.0, center_z),
        orthographic_scale=None,
        transparent=True,
    )
    tint(materials, (0.96, 0.25, 0.32, 1.0))
    render(
        review / "gameplay-red.png",
        gameplay_location,
        (0.0, 0.0, center_z),
        orthographic_scale=None,
        transparent=True,
    )
    tint([materials[0]], neutral_shell)
    tint([materials[1]], neutral_core)
    render(
        review / "top-mask.png",
        (0.0, 0.0, 3.0),
        (0.0, 0.0, center_z),
        orthographic_scale=1.22,
        transparent=True,
        override=mask_material(),
    )

    vertices = sum(len(obj.data.vertices) for obj in objects)
    triangles = sum(
        sum(max(0, len(face.vertices) - 2) for face in obj.data.polygons)
        for obj in objects
    )
    report = {
        "schemaVersion": 1,
        "id": "pulse-bolt",
        "status": "art-side-comparison-control-only",
        "canonicalSvg": {
            "path": "web/src/assets/projectile-looks/pulse-bolt/sprite.svg",
            "sha256": svg_hash,
        },
        "generator": (
            "art/class-models/controls/pulse-bolt-3d/"
            "build-pulse-bolt-control.py"
        ),
        "geometry": {
            "objects": len(objects),
            "vertices": vertices,
            "triangles": triangles,
            "authoredPlanformSpanTiles": 1.0,
            "maximumDepthTiles": 0.155,
            "facing": "+x",
            "up": "+y",
        },
        "materials": {
            "count": len(materials),
            "semanticRoles": ["projectile-mask", "projectile-mask"],
            "teamNeutral": True,
        },
        "runtime": {
            "installed": False,
            "catalogEntryAdded": False,
            "lookIdCreated": False,
            "canonicalLookScale": 0.54,
            "rendererProjectileFactor": 0.72,
            "effectivePlanformSpanTiles": 0.3888,
        },
        "exclusions": [
            "trail",
            "halo",
            "floor glow",
            "impact",
            "hitbox",
            "motion",
        ],
        "outputs": {
            "blend": {
                "file": "pulse-bolt-control.blend",
                "bytes": blend_path.stat().st_size,
                "sha256": sha256(blend_path),
            },
            "glb": {
                "file": "pulse-bolt-control.glb",
                "bytes": glb_path.stat().st_size,
                "sha256": sha256(glb_path),
            },
        },
    }
    (directory / "build-report.json").write_text(
        json.dumps(report, indent=2) + "\n",
        encoding="utf-8",
    )
    print(
        f"PULSE_BOLT_CONTROL {vertices} vertices {triangles} triangles "
        f"{report['outputs']['glb']['sha256']}"
    )


if __name__ == "__main__":
    main()
