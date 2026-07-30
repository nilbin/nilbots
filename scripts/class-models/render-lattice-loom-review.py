"""Render deterministic Lattice Loom strict-view and gameplay-oblique evidence."""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--team", default="38bdf8")
    return parser.parse_args(raw)


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def point_camera(camera: bpy.types.Object, position: tuple[float, float, float]) -> None:
    camera.location = position
    target = Vector((0, 0, 0.11))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def colour_team_material(hex_value: str) -> None:
    value = hex_value.removeprefix("#")
    if len(value) != 6:
        raise RuntimeError("Team colour must be six hexadecimal digits.")
    srgb = tuple(int(value[index : index + 2], 16) / 255 for index in (0, 2, 4))
    colour = tuple(channel ** 2.2 for channel in srgb) + (1.0,)
    materials = [
        material
        for material in bpy.data.materials
        if material.get("nilbotsRole") == "team-accent"
    ]
    if len(materials) != 1:
        raise RuntimeError(f"Expected one semantic team material, found {len(materials)}.")
    material = materials[0]
    material.diffuse_color = colour
    principled = next(
        node
        for node in material.node_tree.nodes
        if node.bl_idname == "ShaderNodeBsdfPrincipled"
    )
    principled.inputs["Base Color"].default_value = colour
    principled.inputs["Emission Color"].default_value = colour
    principled.inputs["Emission Strength"].default_value = 1.2


def add_lighting() -> None:
    world = bpy.context.scene.world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (
        0.014,
        0.022,
        0.034,
        1,
    )
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.32

    bpy.ops.object.light_add(type="AREA", location=(-2.4, -2.2, 3.5))
    key = bpy.context.object
    key.name = "Key"
    key.data.energy = 700
    key.data.color = (0.82, 0.9, 1.0)
    key.data.shape = "DISK"
    key.data.size = 3.0

    bpy.ops.object.light_add(type="AREA", location=(2.1, 1.6, 2.2))
    fill = bpy.context.object
    fill.name = "Fill"
    fill.data.energy = 420
    fill.data.color = (1.0, 0.58, 0.25)
    fill.data.size = 2.4


def add_floor() -> bpy.types.Object:
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, -0.002))
    floor = bpy.context.object
    floor.name = "Review Floor"
    material = bpy.data.materials.new("Review Floor")
    material.diffuse_color = (0.025, 0.035, 0.05, 1)
    material.use_nodes = True
    principled = material.node_tree.nodes["Principled BSDF"]
    principled.inputs["Base Color"].default_value = (0.025, 0.035, 0.05, 1)
    principled.inputs["Roughness"].default_value = 0.78
    principled.inputs["Metallic"].default_value = 0.16
    floor.data.materials.append(material)
    return floor


def render(
    camera: bpy.types.Object,
    output: Path,
    position: tuple[float, float, float],
    *,
    orthographic: bool,
    floor: bpy.types.Object,
) -> None:
    camera.data.type = "ORTHO" if orthographic else "PERSP"
    if orthographic:
        camera.data.ortho_scale = 1.35
    else:
        camera.data.lens = 50
    point_camera(camera, position)
    floor.hide_render = orthographic
    bpy.context.scene.render.film_transparent = orthographic
    bpy.context.scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    args = parse_args()
    model = args.model.resolve()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    reset_scene()
    bpy.ops.import_scene.gltf(filepath=str(model))
    meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one GLB mesh node, found {len(meshes)}.")
    colour_team_material(args.team)
    add_lighting()
    floor = add_floor()

    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.name = "Review Camera"
    camera.data.lens = 50
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = True
    scene.render.image_settings.compression = 70

    views = [
        ("top", (0, 0, 3.0), True),
        ("side", (0, -3.0, 0.11), True),
        ("front", (3.0, 0, 0.11), True),
        ("rear", (-3.0, 0, 0.11), True),
        ("front-three-quarter", (2.2, -2.2, 1.5), True),
        ("rear-three-quarter", (-2.2, 2.2, 1.5), True),
    ]
    for label, position, orthographic in views:
        render(
            camera,
            output_dir / f"{label}-{args.team.removeprefix('#')}.png",
            position,
            orthographic=orthographic,
            floor=floor,
        )

    # Fable's runtime camera is 58 degrees above horizontal and sits on the
    # positive runtime-Z side. glTF import maps that to Blender -Y.
    pitch = math.radians(58)
    distance = 2.25
    render(
        camera,
        output_dir / f"gameplay-{args.team.removeprefix('#')}.png",
        (
            0,
            -math.cos(pitch) * distance,
            math.sin(pitch) * distance,
        ),
        orthographic=False,
        floor=floor,
    )


if __name__ == "__main__":
    main()
