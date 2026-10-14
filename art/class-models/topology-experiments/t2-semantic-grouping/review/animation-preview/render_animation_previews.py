#!/usr/bin/env python3
"""Render review loops from the animations embedded in the committed T2 GLB.

Run with Blender 5.2, not a system Python.  This script deliberately imports
the exported GLB instead of opening the authoring .blend so the review proves
the animation clips a runtime loader will receive.
"""

from __future__ import annotations

import argparse
import hashlib
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


EXPECTED_SOURCE_SHA256 = (
    "5fe60176625b562b0c8a34d7aa12962289ec209911df84f2b4c37d2c23786224"
)
FPS = 24
RENDER_SIZE = 512
PLAYBACK_SCALE = 2.0
PLAYBACK_REPEATS = 3.0
CLIPS = {
    "Turret_Deploy": (1.0, 25.0),
    "Turret_Recoil": (1.0, 13.0),
}


def arguments() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument(
        "--clip",
        action="append",
        choices=sorted(CLIPS),
        help="Render one clip; repeat for more. The default renders both.",
    )
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.actions,
        bpy.data.worlds,
    ):
        for datablock in list(datablocks):
            datablocks.remove(datablock)


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def plain_material(
    name: str,
    base_color: tuple[float, float, float, float],
    *,
    metallic: float,
    roughness: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = base_color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return material


def setup_review_scene() -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    # Select the movie output before creating the review-only floor. Blender
    # 5.2's contextual enum can temporarily show only still formats while the
    # active mesh is in a primitive-add operator context.
    scene.render.image_settings.file_format = "FFMPEG"
    scene.render.resolution_x = RENDER_SIZE
    scene.render.resolution_y = RENDER_SIZE
    scene.render.resolution_percentage = 100
    scene.render.fps = FPS
    scene.render.fps_base = 1.0
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.6
    scene.eevee.taa_render_samples = 16
    scene.eevee.taa_samples = 16
    scene.eevee.shadow_ray_count = 2

    world = bpy.data.worlds.new("Animation Preview World")
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (0.009, 0.013, 0.022, 1.0)
    background.inputs["Strength"].default_value = 0.22
    scene.world = world

    # A matte arena pad supplies contact shadows and makes the small vertical
    # component of the deploy/recoil clips readable.
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=96,
        radius=0.72,
        depth=0.018,
        location=(0.0, 0.0, -0.126),
    )
    pad = bpy.context.object
    pad.name = "Review_Arena_Pad"
    pad.data.materials.append(
        plain_material(
            "Review Arena Pad",
            (0.022, 0.032, 0.048, 1.0),
            metallic=0.28,
            roughness=0.62,
        )
    )

    camera_data = bpy.data.cameras.new("Gameplay 58 Camera")
    camera = bpy.data.objects.new("Gameplay 58 Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.34
    elevation = math.radians(58.0)
    azimuth = math.radians(-45.0)
    distance = 2.5
    camera.location = (
        distance * math.cos(elevation) * math.cos(azimuth),
        distance * math.cos(elevation) * math.sin(azimuth),
        distance * math.sin(elevation),
    )
    look_at(camera, Vector((0.0, 0.0, -0.005)))
    scene.camera = camera

    lights = (
        ("Preview Key", (1.8, -1.4, 2.4), 360.0, 2.2),
        ("Preview Fill", (-1.4, 1.0, 1.2), 145.0, 2.8),
        ("Preview Rim", (-0.4, -1.6, 1.3), 210.0, 1.7),
    )
    for name, location, energy, size in lights:
        data = bpy.data.lights.new(name, "AREA")
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        look_at(light, Vector((0.0, 0.0, 0.02)))

    return camera


def imported_rig(source: Path) -> bpy.types.Object:
    bpy.ops.import_scene.gltf(filepath=str(source))
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(rigs) != 1:
        raise ValueError(f"Expected one armature in exported GLB, found {len(rigs)}")
    return rigs[0]


def validate_embedded_clips() -> dict[str, bpy.types.Action]:
    actions = {action.name: action for action in bpy.data.actions}
    if set(CLIPS) - set(actions):
        raise ValueError(
            f"Missing embedded clips: {sorted(set(CLIPS) - set(actions))}"
        )
    for name, expected_range in CLIPS.items():
        actual = tuple(float(value) for value in actions[name].frame_range)
        if actual != expected_range:
            raise ValueError(
                f"{name} frame range changed: expected {expected_range}, got {actual}"
            )
    return {name: actions[name] for name in CLIPS}


def play_embedded_action(
    rig: bpy.types.Object,
    action: bpy.types.Action,
) -> tuple[int, int]:
    if rig.animation_data is None:
        rig.animation_data_create()
    rig.animation_data.action = None
    while rig.animation_data.nla_tracks:
        rig.animation_data.nla_tracks.remove(rig.animation_data.nla_tracks[0])
    track = rig.animation_data.nla_tracks.new()
    track.name = f"Review loop of embedded {action.name}"
    strip = track.strips.new(action.name, 1, action)
    strip.action_frame_start = action.frame_range[0]
    strip.action_frame_end = action.frame_range[1]
    strip.scale = PLAYBACK_SCALE
    strip.repeat = PLAYBACK_REPEATS
    frame_start = 1
    frame_end = int(round(strip.frame_end))
    return frame_start, frame_end


def render_mp4(
    output: Path,
    rig: bpy.types.Object,
    action: bpy.types.Action,
) -> None:
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = play_embedded_action(rig, action)
    scene.frame_set(scene.frame_start)
    scene.render.ffmpeg.format = "MPEG4"
    scene.render.ffmpeg.codec = "H264"
    scene.render.ffmpeg.constant_rate_factor = "MEDIUM"
    scene.render.ffmpeg.ffmpeg_preset = "GOOD"
    scene.render.ffmpeg.gopsize = FPS
    scene.render.filepath = str(output)
    bpy.ops.render.render(animation=True)
    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError(f"Blender did not create {output}")


def main() -> None:
    args = arguments()
    source = args.source.expanduser().resolve(strict=True)
    output_dir = args.output_dir.expanduser().resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    actual_hash = sha256(source)
    if actual_hash != EXPECTED_SOURCE_SHA256:
        raise ValueError(
            f"Source SHA-256 changed: expected {EXPECTED_SOURCE_SHA256}, "
            f"got {actual_hash}"
        )

    clear_scene()
    rig = imported_rig(source)
    actions = validate_embedded_clips()
    setup_review_scene()
    selected = args.clip or list(CLIPS)
    for name in selected:
        slug = name.removeprefix("Turret_").lower()
        render_mp4(
            output_dir / f"turret-{slug}-gameplay.mp4",
            rig,
            actions[name],
        )
        print(
            f"RENDERED {name} from embedded GLB at {FPS} fps "
            f"(review playback {PLAYBACK_SCALE:.1f}x slower, "
            f"{PLAYBACK_REPEATS:.0f} loops)"
        )


if __name__ == "__main__":
    main()
