from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])


def main() -> None:
    args = parse_args()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=False)

    scene = bpy.context.scene
    scene.camera = bpy.data.objects["PROOF.Camera.gameplay"]
    scene.render.resolution_x = 320
    scene.render.resolution_y = 320
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False

    # Team color stays semantic.  Blue is only the inspection color for this
    # neutral Blender proof; production emission, bloom, and floor glow remain
    # renderer-owned.
    for obj in bpy.data.objects:
        if obj.get("nilbotsRole") == "team-accent":
            obj.color = (0.05, 0.42, 1.0, 1.0)
        animation = obj.animation_data
        if animation is None:
            continue
        animation.action = None
        for track in animation.nla_tracks:
            track.mute = track.name != "Tower_Deploy"

    frames = list(range(1, 97, 2))
    if frames[-1] != 96:
        frames.append(96)
    for frame in frames:
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        scene.render.filepath = str(output_dir / f"f{frame:03d}.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
