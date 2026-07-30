#!/usr/bin/env python3
"""Deterministically group and rigidly animate the owner-liked T2 turret.

Run through Blender 5.2:

  Blender --background --factory-startup --python build_t2_semantic_groups.py \
    -- --input /absolute/raw-model.glb --output-dir /absolute/new-directory

The source GLB is read-only. All output must be written beneath this script's
directory, and the output directory must not exist.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import struct
import sys
import zlib
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable

import bmesh
import bpy
from mathutils import Vector


EXPECTED_BLENDER = (5, 2)
EXPECTED_SOURCE_SHA256 = (
    "f9388c05e54bcf1603b7f901eefe0dedcaf7c636a384dcd039d4fe433e03dd1c"
)
EXPECTED_WELDED_COMPONENTS = 206
WELD_EPSILON = 1.0e-6

# The source is a square radial turret. The four long authored arm masses occupy
# diagonal sectors; the shorter cardinal pods remain part of the central hub.
# Constants are source-space fractions because the provider master is already
# normalized to a one-unit planform.
OUTER_SAMPLE_RADIUS = 0.205
ARM_MIN_OUTER_RADIUS = 0.275
ARM_MIN_CENTROID_RADIUS = 0.205
ARM_WEDGE_HALF_DEGREES = 25.0
ARM_CENTROID_WEDGE_HALF_DEGREES = 31.0
ARM_MIN_DOMINANT_FRACTION = 0.55
AMBIGUITY_RADIUS_MARGIN = 0.018
AMBIGUITY_ANGLE_MARGIN_DEGREES = 3.0
SIGNIFICANT_AMBIGUOUS_TRIANGLE_FRACTION = 0.02
ARM_BALANCE_MIN_RATIO = 0.72
BLOCKING_AMBIGUITY_REASONS = frozenset(
    (
        "outer-samples-touch-other-arm-sector",
        "centroid-and-outer-majority-disagree",
    )
)

ARM_SPECS = (
    ("NE", 45.0),
    ("NW", 135.0),
    ("SW", 225.0),
    ("SE", 315.0),
)
GROUP_NAMES = ("Hub", "Arm_NE", "Arm_NW", "Arm_SW", "Arm_SE")
RENDER_SIZE = 768
FPS = 24


class UnionFind:
    def __init__(self, size: int) -> None:
        self.parent = list(range(size))
        self.rank = [0] * size

    def find(self, item: int) -> int:
        root = item
        while root != self.parent[root]:
            root = self.parent[root]
        while item != root:
            next_item = self.parent[item]
            self.parent[item] = root
            item = next_item
        return root

    def union(self, left: int, right: int) -> None:
        left_root = self.find(left)
        right_root = self.find(right)
        if left_root == right_root:
            return
        if self.rank[left_root] < self.rank[right_root]:
            left_root, right_root = right_root, left_root
        self.parent[right_root] = left_root
        if self.rank[left_root] == self.rank[right_root]:
            self.rank[left_root] += 1


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def rounded(value: float, digits: int = 9) -> float:
    result = round(float(value), digits)
    return 0.0 if result == -0.0 else result


def vector_json(value: Iterable[float]) -> list[float]:
    return [rounded(component) for component in value]


def validate_paths(source: Path, output_dir: Path) -> tuple[Path, Path]:
    source = source.expanduser().resolve(strict=True)
    output_dir = output_dir.expanduser().resolve(strict=False)
    experiment_root = Path(__file__).resolve().parent
    if source.suffix.lower() != ".glb" or not source.is_file():
        raise ValueError("--input must be an existing GLB")
    if output_dir.exists():
        raise ValueError("--output-dir must not already exist")
    if output_dir == experiment_root or experiment_root not in output_dir.parents:
        raise ValueError("--output-dir must be a new child of the experiment directory")
    source_hash = sha256(source)
    if source_hash != EXPECTED_SOURCE_SHA256:
        raise ValueError(
            f"Unexpected source SHA-256 {source_hash}; refusing a different model"
        )
    output_dir.mkdir(parents=True)
    return source, output_dir


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
    ):
        # Imported assets are loaded after clearing; this removes factory data.
        for datablock in list(datablocks):
            datablocks.remove(datablock)


def imported_mesh(source: Path) -> bpy.types.Object:
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(source))
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    if len(meshes) != 1:
        raise ValueError(f"Expected one imported mesh, found {len(meshes)}")
    if any(obj.type in {"CAMERA", "LIGHT", "ARMATURE"} for obj in imported):
        raise ValueError("Provider source unexpectedly contains camera/light/armature")
    return meshes[0]


def quantized_key(point: Vector) -> tuple[int, int, int]:
    return tuple(int(round(float(value) / WELD_EPSILON)) for value in point)


def angle_degrees(x: float, y: float) -> float:
    return math.degrees(math.atan2(y, x)) % 360.0


def angular_distance(left: float, right: float) -> float:
    return abs((left - right + 180.0) % 360.0 - 180.0)


def triangle_area(a: Vector, b: Vector, c: Vector) -> float:
    return ((b - a).cross(c - a)).length * 0.5


def world_points(obj: bpy.types.Object) -> list[Vector]:
    matrix = obj.matrix_world
    return [matrix @ vertex.co for vertex in obj.data.vertices]


def component_partition(
    obj: bpy.types.Object,
) -> tuple[list[dict[str, Any]], dict[int, str], dict[str, list[int]], Vector]:
    mesh = obj.data
    points = world_points(obj)
    connectivity = UnionFind(len(mesh.vertices))

    for polygon in mesh.polygons:
        vertices = list(polygon.vertices)
        for left, right in zip(vertices, vertices[1:] + vertices[:1]):
            connectivity.union(left, right)

    buckets: dict[tuple[int, int, int], list[int]] = defaultdict(list)
    for index, point in enumerate(points):
        buckets[quantized_key(point)].append(index)
    for bucket in buckets.values():
        # Compare every candidate with the stable lowest-index anchor. The
        # provider's coincident seams are exact at this epsilon.
        anchor = min(bucket)
        for index in sorted(bucket):
            if index != anchor and (points[index] - points[anchor]).length <= WELD_EPSILON:
                connectivity.union(anchor, index)

    vertices_by_root: dict[int, list[int]] = defaultdict(list)
    for index in range(len(points)):
        vertices_by_root[connectivity.find(index)].append(index)
    polygons_by_root: dict[int, list[int]] = defaultdict(list)
    for polygon in mesh.polygons:
        if polygon.vertices:
            polygons_by_root[connectivity.find(polygon.vertices[0])].append(
                polygon.index
            )

    sorted_roots = sorted(
        vertices_by_root,
        key=lambda root: (
            -len(polygons_by_root.get(root, [])),
            -len(vertices_by_root[root]),
            root,
        ),
    )
    if len(sorted_roots) != EXPECTED_WELDED_COMPONENTS:
        raise ValueError(
            f"Expected {EXPECTED_WELDED_COMPONENTS} welded components, "
            f"found {len(sorted_roots)}"
        )

    minimum = Vector(
        (
            min(point.x for point in points),
            min(point.y for point in points),
            min(point.z for point in points),
        )
    )
    maximum = Vector(
        (
            max(point.x for point in points),
            max(point.y for point in points),
            max(point.z for point in points),
        )
    )
    center = (minimum + maximum) * 0.5

    mesh.calc_loop_triangles()
    triangles_by_polygon: dict[int, list[Any]] = defaultdict(list)
    for triangle in mesh.loop_triangles:
        triangles_by_polygon[triangle.polygon_index].append(triangle)

    components: list[dict[str, Any]] = []
    face_group: dict[int, str] = {}
    group_faces: dict[str, list[int]] = {name: [] for name in GROUP_NAMES}

    for component_index, root in enumerate(sorted_roots):
        vertex_indices = vertices_by_root[root]
        polygon_indices = sorted(polygons_by_root.get(root, []))
        component_points = [points[index] for index in vertex_indices]
        radii = [
            math.hypot(point.x - center.x, point.y - center.y)
            for point in component_points
        ]

        weighted_center = Vector((0.0, 0.0, 0.0))
        total_area = 0.0
        triangle_count = 0
        for polygon_index in polygon_indices:
            for triangle in triangles_by_polygon[polygon_index]:
                a, b, c = (points[index] for index in triangle.vertices)
                area = triangle_area(a, b, c)
                weighted_center += (a + b + c) / 3.0 * area
                total_area += area
                triangle_count += 1
        if total_area > 0.0:
            weighted_center /= total_area
        else:
            weighted_center = sum(component_points, Vector()) / len(component_points)

        outer_samples = [
            point
            for point, radius in zip(component_points, radii)
            if radius >= OUTER_SAMPLE_RADIUS
        ]
        sector_counts: Counter[str] = Counter()
        outside_wedges = 0
        outer_angle_deltas: list[float] = []
        for point in outer_samples:
            angle = angle_degrees(point.x - center.x, point.y - center.y)
            nearest_name, nearest_axis = min(
                ARM_SPECS,
                key=lambda item: (angular_distance(angle, item[1]), item[0]),
            )
            delta = angular_distance(angle, nearest_axis)
            outer_angle_deltas.append(delta)
            if delta <= ARM_WEDGE_HALF_DEGREES:
                sector_counts[nearest_name] += 1
            else:
                outside_wedges += 1

        dominant_name = None
        dominant_count = 0
        if sector_counts:
            dominant_name, dominant_count = sorted(
                sector_counts.items(), key=lambda item: (-item[1], item[0])
            )[0]
        dominant_fraction = (
            dominant_count / len(outer_samples) if outer_samples else 0.0
        )
        centroid_radius = math.hypot(
            weighted_center.x - center.x, weighted_center.y - center.y
        )
        centroid_angle = angle_degrees(
            weighted_center.x - center.x, weighted_center.y - center.y
        )
        centroid_sector, centroid_axis = min(
            ARM_SPECS,
            key=lambda item: (
                angular_distance(centroid_angle, item[1]),
                item[0],
            ),
        )
        centroid_axis_delta = angular_distance(centroid_angle, centroid_axis)
        max_radius = max(radii)
        min_radius = min(radii)

        qualifies_as_arm = (
            max_radius >= ARM_MIN_OUTER_RADIUS
            and centroid_radius >= ARM_MIN_CENTROID_RADIUS
            and centroid_axis_delta <= ARM_CENTROID_WEDGE_HALF_DEGREES
        )
        group = f"Arm_{centroid_sector}" if qualifies_as_arm else "Hub"

        ambiguity_reasons: list[str] = []
        if qualifies_as_arm:
            if (
                abs(
                    centroid_axis_delta
                    - ARM_CENTROID_WEDGE_HALF_DEGREES
                )
                <= AMBIGUITY_ANGLE_MARGIN_DEGREES
            ):
                ambiguity_reasons.append("centroid-near-wedge-boundary")
            if (
                abs(centroid_radius - ARM_MIN_CENTROID_RADIUS)
                <= AMBIGUITY_RADIUS_MARGIN
            ):
                ambiguity_reasons.append("centroid-near-radial-gate")
            other_sector_samples = sum(
                count
                for name, count in sector_counts.items()
                if name != centroid_sector
            )
            if outer_samples and other_sector_samples / len(outer_samples) >= 0.05:
                ambiguity_reasons.append("outer-samples-touch-other-arm-sector")
            if (
                dominant_name is not None
                and dominant_name != centroid_sector
                and dominant_fraction >= ARM_MIN_DOMINANT_FRACTION
            ):
                ambiguity_reasons.append("centroid-and-outer-majority-disagree")
        else:
            near_arm_gate = (
                max_radius
                >= ARM_MIN_OUTER_RADIUS - AMBIGUITY_RADIUS_MARGIN
                and centroid_radius
                >= ARM_MIN_CENTROID_RADIUS - AMBIGUITY_RADIUS_MARGIN
                and centroid_axis_delta
                <= ARM_CENTROID_WEDGE_HALF_DEGREES
                + AMBIGUITY_ANGLE_MARGIN_DEGREES
            )
            if near_arm_gate:
                ambiguity_reasons.append("hub-near-combined-arm-gate")

        for polygon_index in polygon_indices:
            if polygon_index in face_group:
                raise AssertionError("Polygon assigned more than once")
            face_group[polygon_index] = group
            group_faces[group].append(polygon_index)

        component_minimum = Vector(
            (
                min(point.x for point in component_points),
                min(point.y for point in component_points),
                min(point.z for point in component_points),
            )
        )
        component_maximum = Vector(
            (
                max(point.x for point in component_points),
                max(point.y for point in component_points),
                max(point.z for point in component_points),
            )
        )
        components.append(
            {
                "componentIndex": component_index,
                "originalRootVertex": root,
                "assignment": group,
                "ambiguous": bool(ambiguity_reasons),
                "ambiguityReasons": ambiguity_reasons,
                "vertexCount": len(vertex_indices),
                "faceCount": len(polygon_indices),
                "triangleCount": triangle_count,
                "surfaceArea": rounded(total_area),
                "boundsWorldDcc": {
                    "minimum": vector_json(component_minimum),
                    "maximum": vector_json(component_maximum),
                    "span": vector_json(component_maximum - component_minimum),
                },
                "areaWeightedCentroidWorldDcc": vector_json(weighted_center),
                "planar": {
                    "centroidRadius": rounded(centroid_radius),
                    "centroidAngleDegrees": rounded(centroid_angle),
                    "centroidArmSector": centroid_sector,
                    "centroidArmAxisDeltaDegrees": rounded(
                        centroid_axis_delta
                    ),
                    "minimumVertexRadius": rounded(min_radius),
                    "maximumVertexRadius": rounded(max_radius),
                    "outerSampleCount": len(outer_samples),
                    "dominantArmSector": dominant_name,
                    "dominantSectorFraction": rounded(dominant_fraction),
                    "outsideArmWedgeSamples": outside_wedges,
                    "sectorSampleCounts": {
                        name: sector_counts.get(name, 0)
                        for name, _axis in ARM_SPECS
                    },
                    "maximumOuterAngleDeltaDegrees": (
                        rounded(max(outer_angle_deltas))
                        if outer_angle_deltas
                        else None
                    ),
                },
            }
        )

    if len(face_group) != len(mesh.polygons):
        raise AssertionError("Not every source polygon received one assignment")
    for faces in group_faces.values():
        faces.sort()
    return components, face_group, group_faces, center


def group_pivots(center: Vector) -> dict[str, Vector]:
    # A single shared radial root distance keeps all four pivots symmetric.
    # It sits just inside OUTER_SAMPLE_RADIUS, so each rigid arm can translate
    # without changing the source-derived surface in the rest pose.
    pivot_radius = 0.195
    pivots = {"Hub": Vector((center.x, center.y, center.z))}
    for name, angle in ARM_SPECS:
        radians = math.radians(angle)
        pivots[f"Arm_{name}"] = Vector(
            (
                center.x + math.cos(radians) * pivot_radius,
                center.y + math.sin(radians) * pivot_radius,
                center.z,
            )
        )
    return pivots


def subset_object(
    source: bpy.types.Object,
    name: str,
    keep_polygons: set[int],
    pivot: Vector,
) -> bpy.types.Object:
    new_mesh = source.data.copy()
    new_mesh.name = f"{name}_Mesh"
    obj = source.copy()
    obj.data = new_mesh
    obj.name = name
    bpy.context.collection.objects.link(obj)

    bm = bmesh.new()
    bm.from_mesh(new_mesh)
    bm.faces.ensure_lookup_table()
    delete_faces = [face for face in bm.faces if face.index not in keep_polygons]
    bmesh.ops.delete(bm, geom=delete_faces, context="FACES")
    bm.to_mesh(new_mesh)
    bm.free()
    new_mesh.update()

    # Preserve the provider vertex/UV stream exactly. The named armature bone
    # supplies the actual mechanical pivot; duplicating that pivot through an
    # object-origin rewrite would introduce avoidable float roundoff.
    obj["nilbotsSemanticGroup"] = name
    obj["nilbotsArmRootPivotWorldDcc"] = vector_json(pivot)
    obj["nilbotsSourceSha256"] = EXPECTED_SOURCE_SHA256
    return obj


def mesh_counts(obj: bpy.types.Object) -> dict[str, int]:
    obj.data.calc_loop_triangles()
    return {
        "vertices": len(obj.data.vertices),
        "edges": len(obj.data.edges),
        "faces": len(obj.data.polygons),
        "triangles": len(obj.data.loop_triangles),
        "materialSlots": len(obj.material_slots),
        "uvLayers": len(obj.data.uv_layers),
    }


def canonical_triangle_records(obj: bpy.types.Object) -> list[bytes]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv_layer = mesh.uv_layers.active.data if mesh.uv_layers.active else None
    records: list[bytes] = []
    for triangle in mesh.loop_triangles:
        record = bytearray()
        record.extend(struct.pack("<I", triangle.material_index))
        for loop_index in triangle.loops:
            vertex_index = mesh.loops[loop_index].vertex_index
            world = obj.matrix_world @ mesh.vertices[vertex_index].co
            record.extend(struct.pack("<3f", world.x, world.y, world.z))
            if uv_layer:
                uv = uv_layer[loop_index].uv
                record.extend(struct.pack("<2f", uv.x, uv.y))
        records.append(bytes(record))
    return records


def canonical_scene_triangle_hash(
    objects: Iterable[bpy.types.Object],
) -> tuple[str, int]:
    records = [
        record
        for obj in objects
        for record in canonical_triangle_records(obj)
    ]
    digest = hashlib.sha256()
    for record in sorted(records):
        digest.update(struct.pack("<I", len(record)))
        digest.update(record)
    return digest.hexdigest(), len(records)


def make_principled_material(
    name: str,
    *,
    base_color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    emission_color: tuple[float, float, float, float],
    emission_strength: float,
    role: str,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = base_color
    material["nilbotsRole"] = role
    nodes = material.node_tree.nodes
    principled = nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = base_color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Emission Color"].default_value = emission_color
    principled.inputs["Emission Strength"].default_value = emission_strength
    return material


def create_beveled_box(
    name: str,
    *,
    center: Vector,
    radial_angle_degrees: float,
    length: float,
    width: float,
    height: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.data.name = f"{name}_Mesh"
    obj.scale = (length, width, height)
    obj.rotation_euler[2] = math.radians(radial_angle_degrees)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel = obj.modifiers.new("Restrained edge bevel", "BEVEL")
    bevel.width = 0.0018
    bevel.segments = 2
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    obj.data.materials.append(material)
    obj["nilbotsRole"] = "team-accent"
    return obj


def create_energy_ring(
    center: Vector, material: bpy.types.Material
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        align="WORLD",
        major_radius=0.040,
        minor_radius=0.003,
        major_segments=48,
        minor_segments=8,
        location=(center.x, center.y, 0.112),
    )
    obj = bpy.context.object
    obj.name = "NB_CLASS_ENERGY_CENTER"
    obj.data.name = "NB_CLASS_ENERGY_CENTER_Mesh"
    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    obj.data.materials.append(material)
    obj["nilbotsRole"] = "class-energy"
    return obj


def create_overlays(
    center: Vector,
) -> tuple[list[bpy.types.Object], bpy.types.Object, list[bpy.types.Material]]:
    accent = make_principled_material(
        "NB_TEAM_ACCENT",
        base_color=(0.85, 0.9, 0.92, 1.0),
        metallic=0.5,
        roughness=0.28,
        emission_color=(0.35, 0.4, 0.42, 1.0),
        emission_strength=0.7,
        role="team-accent",
    )
    energy = make_principled_material(
        "NB_CLASS_ENERGY",
        base_color=(0.12, 0.72, 0.82, 1.0),
        metallic=0.15,
        roughness=0.22,
        emission_color=(0.08, 0.82, 1.0, 1.0),
        emission_strength=4.0,
        role="class-energy",
    )
    strips: list[bpy.types.Object] = []
    for arm_name, angle in ARM_SPECS:
        radians = math.radians(angle)
        radial_center = 0.354
        strip = create_beveled_box(
            f"NB_TEAM_ACCENT_{arm_name}",
            center=Vector(
                (
                    center.x + math.cos(radians) * radial_center,
                    center.y + math.sin(radians) * radial_center,
                    0.109,
                )
            ),
            radial_angle_degrees=angle,
            length=0.086,
            width=0.009,
            height=0.0015,
            material=accent,
        )
        strips.append(strip)
    ring = create_energy_ring(center, energy)
    return strips, ring, [accent, energy]


def create_rigid_armature(
    groups: dict[str, bpy.types.Object],
    pivots: dict[str, Vector],
    overlays: list[bpy.types.Object],
    energy_ring: bpy.types.Object,
) -> tuple[bpy.types.Object, dict[str, str]]:
    armature_data = bpy.data.armatures.new("Aegis_T2_Turret_Rig")
    armature = bpy.data.objects.new("Aegis_T2_Turret_Rig", armature_data)
    bpy.context.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    root = armature_data.edit_bones.new("Hub")
    root.head = pivots["Hub"]
    root.tail = pivots["Hub"] + Vector((0.0, 0.0, 0.08))
    for arm_name, _angle in ARM_SPECS:
        group_name = f"Arm_{arm_name}"
        bone = armature_data.edit_bones.new(group_name)
        bone.head = pivots[group_name]
        bone.tail = pivots[group_name] + Vector((0.0, 0.0, 0.08))
        bone.parent = root
        bone.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    armature["nilbotsRigType"] = "rigid-single-weight"

    object_bones: dict[str, str] = {}
    for group_name, obj in groups.items():
        bone_name = group_name
        group = obj.vertex_groups.new(name=bone_name)
        group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
        modifier = obj.modifiers.new("Rigid armature", "ARMATURE")
        modifier.object = armature
        object_bones[obj.name] = bone_name

    for strip, (arm_name, _angle) in zip(overlays, ARM_SPECS):
        bone_name = f"Arm_{arm_name}"
        group = strip.vertex_groups.new(name=bone_name)
        group.add(list(range(len(strip.data.vertices))), 1.0, "REPLACE")
        modifier = strip.modifiers.new("Rigid armature", "ARMATURE")
        modifier.object = armature
        object_bones[strip.name] = bone_name

    energy_group = energy_ring.vertex_groups.new(name="Hub")
    energy_group.add(list(range(len(energy_ring.data.vertices))), 1.0, "REPLACE")
    energy_modifier = energy_ring.modifiers.new("Rigid armature", "ARMATURE")
    energy_modifier.object = armature
    object_bones[energy_ring.name] = "Hub"

    return armature, object_bones


def set_arm_pose(
    armature: bpy.types.Object,
    *,
    radial_delta: float,
    vertical_delta: float,
) -> None:
    for arm_name, angle in ARM_SPECS:
        radians = math.radians(angle)
        bone = armature.pose.bones[f"Arm_{arm_name}"]
        bone.location = Vector(
            (
                math.cos(radians) * radial_delta,
                math.sin(radians) * radial_delta,
                vertical_delta,
            )
        )
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def insert_arm_keys(
    armature: bpy.types.Object,
    frame: int,
    radial_delta: float,
    vertical_delta: float,
) -> None:
    set_arm_pose(
        armature,
        radial_delta=radial_delta,
        vertical_delta=vertical_delta,
    )
    for arm_name, _angle in ARM_SPECS:
        bone = armature.pose.bones[f"Arm_{arm_name}"]
        bone.keyframe_insert(data_path="location", frame=frame, group=bone.name)


def make_action(
    armature: bpy.types.Object,
    name: str,
    keyframes: list[tuple[int, float, float]],
) -> bpy.types.Action:
    if armature.animation_data is None:
        armature.animation_data_create()
    action = bpy.data.actions.new(name)
    armature.animation_data.action = action
    for frame, radial_delta, vertical_delta in keyframes:
        insert_arm_keys(armature, frame, radial_delta, vertical_delta)
    for fcurve in action_fcurves(action):
        for point in fcurve.keyframe_points:
            point.interpolation = "BEZIER"
            point.handle_left_type = "AUTO_CLAMPED"
            point.handle_right_type = "AUTO_CLAMPED"
    action.use_fake_user = True
    armature.animation_data.action = None
    return action


def action_fcurves(action: bpy.types.Action) -> list[bpy.types.FCurve]:
    """Return f-curves from Blender 5.2's layered Action representation."""
    curves: list[bpy.types.FCurve] = []
    for layer in action.layers:
        for strip in layer.strips:
            for channelbag in strip.channelbags:
                curves.extend(channelbag.fcurves)
    return curves


def create_animations(
    armature: bpy.types.Object,
) -> dict[str, bpy.types.Action]:
    actions = {
        "Turret_Deploy": make_action(
            armature,
            "Turret_Deploy",
            [
                (1, -0.022, 0.008),
                (14, 0.004, -0.001),
                (25, 0.0, 0.0),
            ],
        ),
        "Turret_Recoil": make_action(
            armature,
            "Turret_Recoil",
            [
                (1, 0.0, 0.0),
                (4, -0.010, 0.002),
                (9, 0.002, -0.0005),
                (13, 0.0, 0.0),
            ],
        ),
    }
    return actions


def set_active_action(
    armature: bpy.types.Object,
    action: bpy.types.Action | None,
) -> None:
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = action


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_render_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RENDER_SIZE
    scene.render.resolution_y = RENDER_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.render.image_settings.color_depth = "8"
    scene.render.fps = FPS
    scene.render.fps_base = 1.0
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -1.0
    scene.eevee.taa_render_samples = 1
    scene.eevee.taa_samples = 1
    scene.eevee.shadow_ray_count = 1

    world = bpy.data.worlds.new("Review World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (
        0.018,
        0.022,
        0.032,
        1.0,
    )
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.25
    scene.world = world

    camera_data = bpy.data.cameras.new("Review Camera")
    camera = bpy.data.objects.new("Review Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.23
    camera_data.lens = 55
    scene.camera = camera

    light_data = bpy.data.lights.new("Review Key", "AREA")
    light = bpy.data.objects.new("Review Key", light_data)
    bpy.context.collection.objects.link(light)
    light.location = (1.8, -1.4, 2.4)
    light_data.energy = 320
    light_data.shape = "DISK"
    light_data.size = 2.2
    look_at(light, Vector((0.0, 0.0, 0.0)))

    fill_data = bpy.data.lights.new("Review Fill", "AREA")
    fill = bpy.data.objects.new("Review Fill", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (-1.4, 1.0, 1.2)
    fill_data.energy = 135
    fill_data.size = 2.8
    look_at(fill, Vector((0.0, 0.0, 0.0)))

    rim_data = bpy.data.lights.new("Review Rim", "AREA")
    rim = bpy.data.objects.new("Review Rim", rim_data)
    bpy.context.collection.objects.link(rim)
    rim.location = (-0.4, -1.6, 1.3)
    rim_data.energy = 190
    rim_data.size = 1.7
    look_at(rim, Vector((0.0, 0.0, 0.02)))

    return camera, light


def png_chunk(kind: bytes, payload: bytes) -> bytes:
    return (
        struct.pack(">I", len(payload))
        + kind
        + payload
        + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
    )


def paeth(left: int, above: int, upper_left: int) -> int:
    estimate = left + above - upper_left
    distances = (
        (abs(estimate - left), left),
        (abs(estimate - above), above),
        (abs(estimate - upper_left), upper_left),
    )
    return min(distances, key=lambda item: item[0])[1]


def canonicalize_png(path: Path) -> None:
    source = path.read_bytes()
    signature = b"\x89PNG\r\n\x1a\n"
    if not source.startswith(signature):
        raise ValueError(f"Not a PNG: {path}")
    chunks: list[tuple[bytes, bytes]] = []
    offset = len(signature)
    while offset < len(source):
        length = struct.unpack(">I", source[offset : offset + 4])[0]
        kind = source[offset + 4 : offset + 8]
        payload = source[offset + 8 : offset + 8 + length]
        chunks.append((kind, payload))
        offset += 12 + length
        if kind == b"IEND":
            break
    ihdr = next(payload for kind, payload in chunks if kind == b"IHDR")
    width, height, bit_depth, color_type, compression, filtering, interlace = (
        struct.unpack(">IIBBBBB", ihdr)
    )
    if (bit_depth, color_type, compression, filtering, interlace) != (
        8,
        6,
        0,
        0,
        0,
    ):
        raise ValueError("Unexpected Blender PNG encoding")
    encoded = zlib.decompress(
        b"".join(payload for kind, payload in chunks if kind == b"IDAT")
    )
    row_bytes = width * 4
    previous = bytearray(row_bytes)
    output_rows: list[bytes] = []
    cursor = 0
    for _ in range(height):
        filter_type = encoded[cursor]
        cursor += 1
        filtered = encoded[cursor : cursor + row_bytes]
        cursor += row_bytes
        row = bytearray(row_bytes)
        for index, value in enumerate(filtered):
            left = row[index - 4] if index >= 4 else 0
            above = previous[index]
            upper_left = previous[index - 4] if index >= 4 else 0
            predictors = (0, left, above, (left + above) // 2, paeth(left, above, upper_left))
            if filter_type > 4:
                raise ValueError("Unsupported PNG filter")
            row[index] = (value + predictors[filter_type]) & 0xFF
        previous = row.copy()
        for index in range(0, row_bytes, 4):
            for channel in range(3):
                value = row[index + channel]
                row[index + channel] = value & (
                    0xF0 if value < 80 else 0xFC
                )
        output_rows.append(b"\x00" + bytes(row))
    canonical = bytearray(signature)
    canonical.extend(png_chunk(b"IHDR", ihdr))
    canonical.extend(png_chunk(b"IDAT", zlib.compress(b"".join(output_rows), 9)))
    canonical.extend(png_chunk(b"IEND", b""))
    path.write_bytes(canonical)


def render_view(
    camera: bpy.types.Object,
    output: Path,
    *,
    location: tuple[float, float, float],
    target: tuple[float, float, float] = (0.0, 0.0, 0.0),
    ortho_scale: float = 1.23,
) -> None:
    camera.location = location
    camera.data.ortho_scale = ortho_scale
    look_at(camera, Vector(target))
    bpy.context.scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    canonicalize_png(output)


VIEWS = {
    "top": ((0.0, 0.0, 2.4), (0.0, 0.0, 0.0), 1.18),
    "side": ((0.0, -2.4, 0.12), (0.0, 0.0, 0.0), 1.15),
    "front-three-quarter": ((1.65, -1.65, 1.05), (0.0, 0.0, 0.0), 1.22),
    "rear-three-quarter": ((-1.65, 1.65, 1.05), (0.0, 0.0, 0.0), 1.22),
}
GAMEPLAY_ELEVATION = math.radians(58.0)
GAMEPLAY_AZIMUTH = math.radians(-45.0)
GAMEPLAY_DISTANCE = 2.5
VIEWS["gameplay-58"] = (
    (
        GAMEPLAY_DISTANCE
        * math.cos(GAMEPLAY_ELEVATION)
        * math.cos(GAMEPLAY_AZIMUTH),
        GAMEPLAY_DISTANCE
        * math.cos(GAMEPLAY_ELEVATION)
        * math.sin(GAMEPLAY_AZIMUTH),
        GAMEPLAY_DISTANCE * math.sin(GAMEPLAY_ELEVATION),
    ),
    (0.0, 0.0, 0.0),
    1.20,
)


def render_standard_set(camera: bpy.types.Object, directory: Path) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    for name, (location, target, scale) in VIEWS.items():
        render_view(
            camera,
            directory / f"{name}.png",
            location=location,
            target=target,
            ortho_scale=scale,
        )


def render_animation_frames(
    camera: bpy.types.Object,
    armature: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
    directory: Path,
) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    locations = VIEWS["gameplay-58"]
    frame_sets = {
        "Turret_Deploy": (1, 14, 25),
        "Turret_Recoil": (1, 4, 13),
    }
    for action_name, frames in frame_sets.items():
        set_active_action(armature, actions[action_name])
        for label, frame in zip(("start", "mid", "end"), frames):
            bpy.context.scene.frame_set(frame)
            render_view(
                camera,
                directory / f"{action_name}-{label}-f{frame:02d}.png",
                location=locations[0],
                target=locations[1],
                ortho_scale=locations[2],
            )
    set_active_action(armature, None)
    bpy.context.scene.frame_set(1)
    set_arm_pose(armature, radial_delta=0.0, vertical_delta=0.0)


def debug_group_materials() -> dict[str, bpy.types.Material]:
    colors = {
        "Hub": (0.32, 0.34, 0.38, 1.0),
        "Arm_NE": (0.93, 0.23, 0.18, 1.0),
        "Arm_NW": (0.15, 0.66, 0.98, 1.0),
        "Arm_SW": (0.28, 0.82, 0.38, 1.0),
        "Arm_SE": (0.98, 0.72, 0.12, 1.0),
    }
    return {
        name: make_principled_material(
            f"DEBUG_{name}",
            base_color=color,
            metallic=0.0,
            roughness=0.72,
            emission_color=(0.0, 0.0, 0.0, 1.0),
            emission_strength=0.0,
            role="debug-only",
        )
        for name, color in colors.items()
    }


def render_group_debug(
    camera: bpy.types.Object,
    groups: dict[str, bpy.types.Object],
    output: Path,
) -> None:
    original_materials: dict[str, list[bpy.types.Material | None]] = {}
    debug = debug_group_materials()
    for name, obj in groups.items():
        original_materials[name] = [
            slot.material for slot in obj.material_slots
        ]
        for slot in obj.material_slots:
            slot.material = debug[name]
    render_view(
        camera,
        output,
        location=VIEWS["top"][0],
        target=VIEWS["top"][1],
        ortho_scale=VIEWS["top"][2],
    )
    for name, obj in groups.items():
        for slot, material in zip(obj.material_slots, original_materials[name]):
            slot.material = material


def select_only(objects: Iterable[bpy.types.Object]) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    selected = list(objects)
    for obj in selected:
        obj.hide_render = False
        obj.select_set(True)
    if selected:
        bpy.context.view_layer.objects.active = selected[0]


def export_glb(
    path: Path,
    objects: list[bpy.types.Object],
    *,
    animations: bool,
) -> None:
    select_only(objects)
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        check_existing=False,
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=False,
        export_materials="EXPORT",
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_cameras=False,
        export_lights=False,
        export_extras=True,
        export_animations=animations,
        export_animation_mode="ACTIONS" if animations else "ACTIVE_ACTIONS",
        export_force_sampling=False,
        export_frame_range=False,
        export_skins=animations,
        export_image_format="AUTO",
        export_image_quality=100,
        export_jpeg_quality=100,
    )


def parse_glb(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    if data[:4] != b"glTF":
        raise ValueError(f"Invalid GLB magic: {path}")
    _magic, version, declared_length = struct.unpack_from("<4sII", data, 0)
    if version != 2 or declared_length != len(data):
        raise ValueError(f"Invalid GLB header: {path}")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    if json_type != 0x4E4F534A:
        raise ValueError(f"GLB first chunk is not JSON: {path}")
    document = json.loads(data[20 : 20 + json_length].decode("utf-8"))

    accessors = document.get("accessors", [])
    animations: list[dict[str, Any]] = []
    for animation in document.get("animations", []):
        channels = []
        total_keyframes = 0
        for channel in animation.get("channels", []):
            sampler = animation["samplers"][channel["sampler"]]
            keyframe_count = accessors[sampler["input"]]["count"]
            total_keyframes += keyframe_count
            target_node = channel.get("target", {}).get("node")
            channels.append(
                {
                    "node": (
                        document["nodes"][target_node].get("name")
                        if target_node is not None
                        else None
                    ),
                    "path": channel.get("target", {}).get("path"),
                    "keyframes": keyframe_count,
                    "interpolation": sampler.get("interpolation", "LINEAR"),
                }
            )
        animations.append(
            {
                "name": animation.get("name"),
                "channelCount": len(channels),
                "totalKeyframesAcrossChannels": total_keyframes,
                "channels": channels,
            }
        )

    triangle_count = 0
    primitive_count = 0
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            primitive_count += 1
            indices = primitive.get("indices")
            if indices is not None:
                triangle_count += accessors[indices]["count"] // 3

    return {
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
        "asset": document.get("asset", {}),
        "counts": {
            "scenes": len(document.get("scenes", [])),
            "nodes": len(document.get("nodes", [])),
            "meshes": len(document.get("meshes", [])),
            "primitives": primitive_count,
            "triangles": triangle_count,
            "materials": len(document.get("materials", [])),
            "textures": len(document.get("textures", [])),
            "images": len(document.get("images", [])),
            "skins": len(document.get("skins", [])),
            "animations": len(document.get("animations", [])),
        },
        "nodeNames": [node.get("name") for node in document.get("nodes", [])],
        "materialRoles": {
            material.get("name", f"material-{index}"): (
                material.get("extras", {}).get("nilbotsRole")
            )
            for index, material in enumerate(document.get("materials", []))
        },
        "skins": [
            {
                "name": skin.get("name"),
                "jointCount": len(skin.get("joints", [])),
                "jointNames": [
                    document["nodes"][joint].get("name")
                    for joint in skin.get("joints", [])
                ],
            }
            for skin in document.get("skins", [])
        ],
        "animations": animations,
    }


def action_report(action: bpy.types.Action) -> dict[str, Any]:
    frame_start, frame_end = action.frame_range
    channels = []
    fcurves = action_fcurves(action)
    for fcurve in fcurves:
        channels.append(
            {
                "dataPath": fcurve.data_path,
                "arrayIndex": fcurve.array_index,
                "keyframes": [
                    {
                        "frame": rounded(point.co.x),
                        "value": rounded(point.co.y),
                        "interpolation": point.interpolation,
                    }
                    for point in fcurve.keyframe_points
                ],
            }
        )
    return {
        "name": action.name,
        "frameRange": [rounded(frame_start), rounded(frame_end)],
        "fps": FPS,
        "durationSeconds": rounded((frame_end - frame_start) / FPS),
        "fcurveCount": len(fcurves),
        "channels": channels,
    }


def main() -> None:
    if bpy.app.version[:2] != EXPECTED_BLENDER:
        raise RuntimeError(
            f"Expected Blender {EXPECTED_BLENDER[0]}.{EXPECTED_BLENDER[1]}, "
            f"found {bpy.app.version_string}"
        )
    args = parse_args()
    source, output_dir = validate_paths(args.input, args.output_dir)
    source_hash_before = sha256(source)

    clear_scene()
    source_obj = imported_mesh(source)
    source_obj.name = "Provider_Source_ReadOnly"
    source_obj.data.name = "Provider_Source_ReadOnly_Mesh"
    source_counts = mesh_counts(source_obj)
    source_triangle_hash, source_triangle_count = canonical_scene_triangle_hash(
        [source_obj]
    )
    source_materials = [slot.material.name for slot in source_obj.material_slots]
    source_images = [
        {
            "name": image.name,
            "width": image.size[0],
            "height": image.size[1],
            "packed": image.packed_file is not None,
            "packedBytes": (
                image.packed_file.size if image.packed_file is not None else None
            ),
        }
        for image in sorted(bpy.data.images, key=lambda item: item.name)
        if image.size[0] and image.size[1]
    ]

    components, _face_group, group_faces, center = component_partition(source_obj)
    pivots = group_pivots(center)
    groups = {
        name: subset_object(
            source_obj,
            name,
            set(group_faces[name]),
            pivots[name],
        )
        for name in GROUP_NAMES
    }
    bpy.data.objects.remove(source_obj, do_unlink=True)

    group_counts = {name: mesh_counts(obj) for name, obj in groups.items()}
    grouped_triangle_hashes = {
        name: canonical_scene_triangle_hash([obj])[0]
        for name, obj in groups.items()
    }
    grouped_scene_triangle_hash, grouped_scene_triangle_hash_count = (
        canonical_scene_triangle_hash(groups.values())
    )
    grouped_triangle_total = sum(
        counts["triangles"] for counts in group_counts.values()
    )

    arm_triangles = [
        group_counts[f"Arm_{name}"]["triangles"] for name, _angle in ARM_SPECS
    ]
    ambiguous_triangles = sum(
        component["triangleCount"]
        for component in components
        if component["ambiguous"]
    )
    significant_ambiguous_fraction = ambiguous_triangles / source_triangle_count
    blocking_ambiguous_components = [
        component
        for component in components
        if any(
            reason in BLOCKING_AMBIGUITY_REASONS
            for reason in component["ambiguityReasons"]
        )
    ]
    blocking_ambiguous_triangles = sum(
        component["triangleCount"]
        for component in blocking_ambiguous_components
    )
    blocking_ambiguous_fraction = (
        blocking_ambiguous_triangles / source_triangle_count
    )
    grouping_usable = (
        grouped_triangle_total == source_triangle_count
        and grouped_scene_triangle_hash_count == source_triangle_count
        and grouped_scene_triangle_hash == source_triangle_hash
        and min(arm_triangles) > 0
        and min(arm_triangles) / max(arm_triangles) >= ARM_BALANCE_MIN_RATIO
        and blocking_ambiguous_fraction
        <= SIGNIFICANT_AMBIGUOUS_TRIANGLE_FRACTION
    )

    camera, _key = setup_render_scene()
    review_dir = output_dir / "review"
    (review_dir / "base").mkdir(parents=True)
    render_standard_set(camera, review_dir / "base")
    render_group_debug(camera, groups, review_dir / "grouping-debug-top.png")

    base_glb = output_dir / "t2-turret-grouped.glb"
    export_glb(base_glb, list(groups.values()), animations=False)
    base_glb_report = parse_glb(base_glb)

    overlays: list[bpy.types.Object] = []
    energy_ring = None
    armature = None
    actions: dict[str, bpy.types.Action] = {}
    object_bones: dict[str, str] = {}
    animated_glb_report = None

    if grouping_usable:
        strips, energy_ring, _overlay_materials = create_overlays(center)
        overlays = strips
        armature, object_bones = create_rigid_armature(
            groups,
            pivots,
            overlays,
            energy_ring,
        )
        actions = create_animations(armature)
        set_active_action(armature, None)
        set_arm_pose(armature, radial_delta=0.0, vertical_delta=0.0)
        bpy.context.scene.frame_set(1)

        (review_dir / "overlay").mkdir(parents=True)
        render_standard_set(camera, review_dir / "overlay")
        render_animation_frames(
            camera,
            armature,
            actions,
            review_dir / "animation",
        )

        animated_glb = output_dir / "t2-turret-animated-overlay.glb"
        export_glb(
            animated_glb,
            list(groups.values()) + overlays + [energy_ring, armature],
            animations=True,
        )
        animated_glb_report = parse_glb(animated_glb)

        # Preserve an editable review scene in addition to the deterministic
        # source script. Cameras/lights stay review-only and are not in GLBs.
        bpy.ops.wm.save_as_mainfile(
            filepath=str(output_dir / "t2-turret-semantic-grouping.blend"),
            check_existing=False,
        )

    source_hash_after = sha256(source)
    if source_hash_after != source_hash_before:
        raise RuntimeError("Provider master changed during the experiment")

    report = {
        "schema": "nilbots-t2-semantic-grouping-1",
        "status": "usable-for-rigid-animation-review" if grouping_usable else "stop",
        "shippingStatus": "art-side-non-shipping-candidate",
        "source": {
            "path": os.path.relpath(source, Path.cwd()),
            "sha256Before": source_hash_before,
            "sha256After": source_hash_after,
            "unchanged": source_hash_before == source_hash_after,
            "counts": source_counts,
            "canonicalPositionUvTriangleMultisetSha256": source_triangle_hash,
            "materials": source_materials,
            "images": source_images,
        },
        "toolchain": {
            "blender": bpy.app.version_string,
            "background": bpy.app.background,
            "scriptSha256": sha256(Path(__file__).resolve()),
        },
        "classification": {
            "method": "coincident-weld-connectivity-plus-recorded-radial-sectors",
            "semanticAiInference": False,
            "sourceGeometryModified": False,
            "constants": {
                "weldEpsilon": WELD_EPSILON,
                "outerSampleRadius": OUTER_SAMPLE_RADIUS,
                "armMinimumOuterRadius": ARM_MIN_OUTER_RADIUS,
                "armMinimumCentroidRadius": ARM_MIN_CENTROID_RADIUS,
                "armWedgeHalfDegrees": ARM_WEDGE_HALF_DEGREES,
                "armCentroidWedgeHalfDegrees": (
                    ARM_CENTROID_WEDGE_HALF_DEGREES
                ),
                "armMinimumDominantFraction": ARM_MIN_DOMINANT_FRACTION,
                "armAxesDegrees": {
                    name: angle for name, angle in ARM_SPECS
                },
            },
            "weldedComponentCount": len(components),
            "componentAssignments": components,
            "ambiguousComponentCount": sum(
                component["ambiguous"] for component in components
            ),
            "ambiguousTriangleCount": ambiguous_triangles,
            "ambiguousTriangleFraction": rounded(
                significant_ambiguous_fraction
            ),
            "blockingAmbiguityReasons": sorted(
                BLOCKING_AMBIGUITY_REASONS
            ),
            "blockingAmbiguousComponentIndices": [
                component["componentIndex"]
                for component in blocking_ambiguous_components
            ],
            "blockingAmbiguousTriangleCount": (
                blocking_ambiguous_triangles
            ),
            "blockingAmbiguousTriangleFraction": rounded(
                blocking_ambiguous_fraction
            ),
        },
        "groups": {
            name: {
                "pivotWorldDcc": vector_json(pivots[name]),
                "componentIndices": [
                    component["componentIndex"]
                    for component in components
                    if component["assignment"] == name
                ],
                "counts": group_counts[name],
                "canonicalPositionUvTriangleMultisetSha256": grouped_triangle_hashes[
                    name
                ],
            }
            for name in GROUP_NAMES
        },
        "groupingGate": {
            "usable": grouping_usable,
            "sourceTriangleCount": source_triangle_count,
            "groupedTriangleTotal": grouped_triangle_total,
            "triangleConservationPassed": (
                grouped_triangle_total == source_triangle_count
            ),
            "groupedCanonicalPositionUvTriangleMultisetSha256": (
                grouped_scene_triangle_hash
            ),
            "sourceCanonicalPositionUvTriangleMultisetSha256": (
                source_triangle_hash
            ),
            "positionUvTriangleMultisetPreserved": (
                grouped_scene_triangle_hash_count == source_triangle_count
                and grouped_scene_triangle_hash == source_triangle_hash
            ),
            "armTriangleCounts": {
                name: group_counts[f"Arm_{name}"]["triangles"]
                for name, _angle in ARM_SPECS
            },
            "armBalanceRatio": rounded(
                min(arm_triangles) / max(arm_triangles)
            ),
            "minimumRequiredArmBalanceRatio": ARM_BALANCE_MIN_RATIO,
            "maximumAmbiguousTriangleFraction": (
                SIGNIFICANT_AMBIGUOUS_TRIANGLE_FRACTION
            ),
            "totalReportedBoundaryAmbiguousTriangleFraction": rounded(
                significant_ambiguous_fraction
            ),
            "blockingAmbiguousTriangleFraction": rounded(
                blocking_ambiguous_fraction
            ),
            "note": (
                "Animation is created only when every source triangle is "
                "conserved, four non-empty arms are balanced, and cross-arm "
                "ambiguity remains below the recorded significance gate. "
                "Hub/arm boundary components remain reported for review but "
                "are not treated as cross-arm conflicts."
            ),
        },
        "overlays": {
            "created": grouping_usable,
            "sourceAtlasRecolored": False,
            "teamAccentObjects": [obj.name for obj in overlays],
            "teamAccentMaterial": "NB_TEAM_ACCENT" if grouping_usable else None,
            "teamAccentRole": "team-accent" if grouping_usable else None,
            "fixedEnergyObject": (
                energy_ring.name if energy_ring is not None else None
            ),
            "fixedEnergyMaterial": (
                "NB_CLASS_ENERGY" if grouping_usable else None
            ),
            "fixedEnergyRole": "class-energy" if grouping_usable else None,
        },
        "rig": {
            "created": grouping_usable,
            "type": "five-bone-rigid-single-weight",
            "bones": (
                [bone.name for bone in armature.data.bones]
                if armature is not None
                else []
            ),
            "objectBoneAssignments": object_bones,
            "allSourceDerivedVerticesSingleWeight": grouping_usable,
            "surfaceDeformation": "none; rigid bone transforms only",
        },
        "animations": {
            "created": grouping_usable,
            "contract": (
                "form/deployment settle plus symmetric localized lock recoil; "
                "no scan, world rotation, or authoritative-facing change"
            ),
            "actions": {
                name: action_report(action) for name, action in actions.items()
            },
        },
        "outputs": {
            "baseGlb": {
                "path": base_glb.name,
                **base_glb_report,
            },
            "animatedGlb": (
                {
                    "path": "t2-turret-animated-overlay.glb",
                    **animated_glb_report,
                }
                if animated_glb_report is not None
                else None
            ),
            "blend": (
                "t2-turret-semantic-grouping.blend"
                if grouping_usable
                else None
            ),
            "reviewDirectory": "review",
        },
        "nextGates": [
            "human review of false-color grouping and base/overlay comparisons",
            "Khronos glTF validation",
            "byte-identical deterministic rerun",
            "renderer team-accent mutation with both team colors",
            "native replay form-event timing and gameplay-scale review",
            "on-demand transfer and runtime budget comparison",
        ],
    }
    report_path = output_dir / "report.json"
    report_path.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(
        json.dumps(
            {
                "output": str(output_dir),
                "report": report_path.name,
                "sourceUnchanged": source_hash_before == source_hash_after,
                "weldedComponents": len(components),
                "groupingUsable": grouping_usable,
                "baseGlbSha256": base_glb_report["sha256"],
                "animatedGlbSha256": (
                    animated_glb_report["sha256"]
                    if animated_glb_report is not None
                    else None
                ),
            },
            sort_keys=True,
        )
    )


if __name__ == "__main__":
    main()
