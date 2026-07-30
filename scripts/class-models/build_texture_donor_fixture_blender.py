#!/usr/bin/env python3
"""Build tiny offline fixtures for texture-donor transplant tests only."""

from __future__ import annotations

import argparse
import json
import sys
import traceback
from pathlib import Path

import bpy


EXPECTED_BLENDER = (5, 2)
MODEL_COLLECTION = "NB_TextureDonor_Test"
DONOR_FACTORS = {
    "baseColor": [0.55, 0.7, 0.9, 1.0],
    "metallic": 0.35,
    "roughness": 0.65,
    "emissive": [0.2, 0.35, 1.0],
    "emissiveStrength": 2.5,
    "normalScale": 0.75,
    "occlusionStrength": 0.37,
    "doubleSided": True,
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args(argv)


def material(
    name: str,
    *,
    color: tuple[float, float, float, float],
    team_accent: bool = False,
) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = 0.6
    result.diffuse_color = color
    if team_accent:
        result["nilbotsRole"] = "team-accent"
    return result


def triangle_object(
    name: str,
    *,
    vertices: list[tuple[float, float, float]],
    uvs: list[tuple[float, float]],
    owner: bpy.types.Collection,
    parent: bpy.types.Object,
    surface: bpy.types.Material,
) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(f"{name}.Mesh")
    mesh.from_pydata(vertices, [], [(0, 1, 2)])
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        uv_layer.data[loop.index].uv = uvs[loop.vertex_index]
    mesh.materials.append(surface)
    obj = bpy.data.objects.new(name, mesh)
    owner.objects.link(obj)
    obj.parent = parent
    return obj


def add_animation(pivot: bpy.types.Object) -> None:
    pivot.animation_data_create()
    action = bpy.data.actions.new("Shell_Deploy__FixturePivot")
    pivot.animation_data.action = action
    for frame, x, angle in (
        (1, 0.0, 0.0),
        (5, 0.2, 0.35),
        (10, 0.5, 0.7),
    ):
        pivot.location.x = x
        pivot.rotation_euler.z = angle
        pivot.keyframe_insert(data_path="location", frame=frame)
        pivot.keyframe_insert(data_path="rotation_euler", frame=frame)
    track = pivot.animation_data.nla_tracks.new()
    track.name = "Shell_Deploy"
    track.mute = True
    strip = track.strips.new("Shell_Deploy", 1, action)
    strip.action_frame_start = 1
    strip.action_frame_end = 10
    strip.extrapolation = "NOTHING"
    pivot.animation_data.action = None
    pivot.location = (0.0, 0.0, 0.0)
    pivot.rotation_euler = (0.0, 0.0, 0.0)


def save_image(
    path: Path,
    *,
    color: tuple[float, float, float, float],
) -> bpy.types.Image:
    image = bpy.data.images.new(path.stem, width=4, height=4, alpha=True)
    image.pixels = list(color) * 16
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    return image


def enabled_socket(sockets: object, name: str) -> bpy.types.NodeSocket:
    matches = [
        socket
        for socket in sockets
        if socket.name == name and bool(getattr(socket, "enabled", True))
    ]
    if len(matches) != 1:
        raise ValueError(f"Expected one enabled fixture socket {name!r}")
    return matches[0]


def color_multiply(
    tree: bpy.types.NodeTree,
    *,
    source: bpy.types.NodeSocket,
    factor: tuple[float, float, float, float],
) -> bpy.types.NodeSocket:
    mix = tree.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.clamp_factor = True
    enabled_socket(mix.inputs, "Factor").default_value = 1.0
    enabled_socket(mix.inputs, "B").default_value = factor
    tree.links.new(source, enabled_socket(mix.inputs, "A"))
    return enabled_socket(mix.outputs, "Result")


def connect_occlusion(
    tree: bpy.types.NodeTree,
    *,
    source: bpy.types.NodeSocket,
    strength: float,
) -> None:
    group = bpy.data.node_groups.get("glTF Material Output")
    if group is None:
        group = bpy.data.node_groups.new(
            "glTF Material Output",
            "ShaderNodeTree",
        )
        group.interface.new_socket(
            name="Occlusion",
            in_out="INPUT",
            socket_type="NodeSocketFloat",
        )
    output = tree.nodes.new("ShaderNodeGroup")
    output.node_tree = group
    separate = tree.nodes.new("ShaderNodeSeparateColor")
    separate.mode = "RGB"
    tree.links.new(source, separate.inputs["Color"])
    mix = tree.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MIX"
    mix.clamp_factor = True
    enabled_socket(mix.inputs, "Factor").default_value = strength
    enabled_socket(mix.inputs, "A").default_value = (1.0, 1.0, 1.0, 1.0)
    tree.links.new(separate.outputs["Red"], enabled_socket(mix.inputs, "B"))
    tree.links.new(
        enabled_socket(mix.outputs, "Result"),
        output.inputs["Occlusion"],
    )


def donor_material(
    *,
    output_dir: Path,
) -> bpy.types.Material:
    base = save_image(
        output_dir / "fixture-base.png",
        color=(0.19, 0.27, 0.36, 1.0),
    )
    metallic_roughness = save_image(
        output_dir / "fixture-metallic-roughness.png",
        color=(1.0, 0.62, 0.28, 1.0),
    )
    normal = save_image(
        output_dir / "fixture-normal.png",
        color=(0.5, 0.5, 1.0, 1.0),
    )
    emissive = save_image(
        output_dir / "fixture-emissive.png",
        color=(0.12, 0.16, 0.2, 1.0),
    )
    occlusion = save_image(
        output_dir / "fixture-occlusion.png",
        color=(0.7, 0.7, 0.7, 1.0),
    )
    for image in (metallic_roughness, normal, occlusion):
        image.colorspace_settings.name = "Non-Color"

    result = bpy.data.materials.new("DONOR_PBR")
    result.use_nodes = True
    tree = result.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    shader = tree.nodes.new("ShaderNodeBsdfPrincipled")
    tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    base_node = tree.nodes.new("ShaderNodeTexImage")
    base_node.image = base
    tree.links.new(
        color_multiply(
            tree,
            source=base_node.outputs["Color"],
            factor=tuple(DONOR_FACTORS["baseColor"]),
        ),
        shader.inputs["Base Color"],
    )

    mr_node = tree.nodes.new("ShaderNodeTexImage")
    mr_node.image = metallic_roughness
    separate = tree.nodes.new("ShaderNodeSeparateColor")
    separate.mode = "RGB"
    tree.links.new(mr_node.outputs["Color"], separate.inputs["Color"])
    roughness = tree.nodes.new("ShaderNodeMath")
    roughness.operation = "MULTIPLY"
    roughness.inputs[1].default_value = DONOR_FACTORS["roughness"]
    tree.links.new(separate.outputs["Green"], roughness.inputs[0])
    tree.links.new(roughness.outputs[0], shader.inputs["Roughness"])
    metallic = tree.nodes.new("ShaderNodeMath")
    metallic.operation = "MULTIPLY"
    metallic.inputs[1].default_value = DONOR_FACTORS["metallic"]
    tree.links.new(separate.outputs["Blue"], metallic.inputs[0])
    tree.links.new(metallic.outputs[0], shader.inputs["Metallic"])

    normal_node = tree.nodes.new("ShaderNodeTexImage")
    normal_node.image = normal
    normal_map = tree.nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = DONOR_FACTORS["normalScale"]
    tree.links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    tree.links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])

    emission_node = tree.nodes.new("ShaderNodeTexImage")
    emission_node.image = emissive
    emission_socket = shader.inputs.get("Emission Color")
    if emission_socket is None:
        emission_socket = shader.inputs["Emission"]
    tree.links.new(
        color_multiply(
            tree,
            source=emission_node.outputs["Color"],
            factor=(
                DONOR_FACTORS["emissive"][0],
                DONOR_FACTORS["emissive"][1],
                DONOR_FACTORS["emissive"][2],
                1.0,
            ),
        ),
        emission_socket,
    )
    shader.inputs["Emission Strength"].default_value = DONOR_FACTORS[
        "emissiveStrength"
    ]

    occlusion_node = tree.nodes.new("ShaderNodeTexImage")
    occlusion_node.image = occlusion
    connect_occlusion(
        tree,
        source=occlusion_node.outputs["Color"],
        strength=DONOR_FACTORS["occlusionStrength"],
    )
    result.use_backface_culling = not DONOR_FACTORS["doubleSided"]
    return result


def export_glb(path: Path, collection: bpy.types.Collection) -> None:
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=False,
        collection=collection.name,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_cameras=False,
        export_lights=False,
        export_extras=True,
        export_animations=False,
        export_normals=True,
        export_apply=True,
        export_yup=True,
    )


def main() -> None:
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise ValueError(
            f"Expected Blender 5.2.x, got {bpy.app.version_string}"
        )
    args = parse_args()
    repo_root = args.repo_root.resolve(strict=True)
    output_dir = args.output_dir.resolve()
    try:
        output_dir.relative_to(repo_root)
    except ValueError as error:
        raise ValueError("Fixture output escaped repository") from error
    if output_dir.exists():
        raise ValueError("Fixture output already exists")
    output_dir.mkdir(parents=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    collection = bpy.data.collections.new(MODEL_COLLECTION)
    bpy.context.scene.collection.children.link(collection)
    root = bpy.data.objects.new("AUTH_FixtureRoot", None)
    collection.objects.link(root)
    pivot = bpy.data.objects.new("AUTH_FixturePivot", None)
    collection.objects.link(pivot)
    pivot.parent = root

    body = material(
        "AUTH_Body",
        color=(0.22, 0.29, 0.37, 1.0),
    )
    accent = material(
        "AUTH_TeamAccent",
        color=(0.86, 0.92, 0.98, 1.0),
        team_accent=True,
    )
    body_object = triangle_object(
        "AUTH_BodyMesh",
        vertices=[(-0.9, -0.4, 0.0), (-0.1, -0.4, 0.0), (-0.5, 0.4, 0.0)],
        uvs=[(0.05, 0.05), (0.45, 0.05), (0.25, 0.45)],
        owner=collection,
        parent=pivot,
        surface=body,
    )
    accent_object = triangle_object(
        "AUTH_AccentMesh",
        vertices=[(0.1, -0.2, 0.02), (0.5, -0.2, 0.02), (0.3, 0.2, 0.02)],
        uvs=[(0.55, 0.05), (0.95, 0.05), (0.75, 0.45)],
        owner=collection,
        parent=pivot,
        surface=accent,
    )
    add_animation(pivot)
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 10
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()

    blend_path = output_dir / "authored.blend"
    uploaded_path = output_dir / "uploaded-source.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    export_glb(uploaded_path, collection)

    shared_material_blend_path = output_dir / "shared-material-authored.blend"
    outside_mesh = body_object.data.copy()
    outside_mesh.name = "OUTSIDE_SharedBody.Mesh"
    outside_object = bpy.data.objects.new(
        "OUTSIDE_SharedBody",
        outside_mesh,
    )
    bpy.context.scene.collection.objects.link(outside_object)
    bpy.ops.wm.save_as_mainfile(
        filepath=str(shared_material_blend_path),
        check_existing=False,
    )
    bpy.data.objects.remove(outside_object, do_unlink=True)
    bpy.data.meshes.remove(outside_mesh)

    donor = donor_material(output_dir=output_dir)
    for obj in (body_object, accent_object):
        obj.name = obj.name.replace("AUTH_", "DONOR_")
        obj.data.materials.clear()
        obj.data.materials.append(donor)
    donor_path = output_dir / "donor.glb"
    export_glb(donor_path, collection)

    root.location = (0.27, -0.19, 0.41)
    root.scale = (1.7, 1.7, 1.7)
    similarity_donor_path = output_dir / "similarity-donor.glb"
    export_glb(similarity_donor_path, collection)
    root.location = (0.0, 0.0, 0.0)
    root.scale = (1.0, 1.0, 1.0)

    root.location = (0.27, -0.19, 0.41)
    root.scale = (-1.7, 1.7, 1.7)
    reflection_donor_path = output_dir / "reflection-donor.glb"
    export_glb(reflection_donor_path, collection)
    root.location = (0.0, 0.0, 0.0)
    root.scale = (1.0, 1.0, 1.0)

    uv_loop = body_object.data.uv_layers.active.data[0]
    original_uv = uv_loop.uv.copy()
    uv_loop.uv.x += 0.08
    bad_uv_donor_path = output_dir / "bad-uv-donor.glb"
    export_glb(bad_uv_donor_path, collection)
    uv_loop.uv = original_uv

    body_object.data.vertices[0].co.x -= 0.08
    body_object.data.update()
    bad_donor_path = output_dir / "bad-donor.glb"
    export_glb(bad_donor_path, collection)

    manifest = {
        "modelCollection": MODEL_COLLECTION,
        "authoredBlend": blend_path.relative_to(repo_root).as_posix(),
        "sharedMaterialAuthoredBlend": (
            shared_material_blend_path.relative_to(repo_root).as_posix()
        ),
        "uploadedSourceGlb": uploaded_path.relative_to(repo_root).as_posix(),
        "donorGlb": donor_path.relative_to(repo_root).as_posix(),
        "similarityDonorGlb": (
            similarity_donor_path.relative_to(repo_root).as_posix()
        ),
        "reflectionDonorGlb": (
            reflection_donor_path.relative_to(repo_root).as_posix()
        ),
        "badUvDonorGlb": bad_uv_donor_path.relative_to(repo_root).as_posix(),
        "badDonorGlb": bad_donor_path.relative_to(repo_root).as_posix(),
        "donorFactors": DONOR_FACTORS,
    }
    (output_dir / "fixture.json").write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        raise SystemExit(1)
