import * as THREE from 'three';
import type { ReplayModel } from '../replayModel';
import { unitAccent } from '../render/unitPresentation';
import {
  volleyArrowOutline,
  volleysAt,
  type VolleyMember,
} from '../render/volley';
import { PROJECTILE_HOVER } from './arenaActors';

/**
 * A volley as one swept ribbon in the air.
 *
 * The flat renderer fills a curved band between the blades' forward and rear points; this
 * is the same band as geometry — a triangle strip laid at bolt height, additively blended
 * so it reads as light rather than a plate sliding over the arena. The outline comes from
 * the shared derivation, so the two renderers cannot disagree about the shape a volley
 * makes, only about how it is put on screen.
 *
 * **The strip is rebuilt every frame, not transformed.** The blades diverge as they fly —
 * a symmetric fan is wider at eight tiles than at two — so there is no rigid body here to
 * move. Positions are written into a preallocated buffer whose size is fixed by the
 * volley's lane count, which the replay settles before playback starts.
 */

/** Forward reach and rearward tail of the glyph, in tiles. Matches the flat renderer. */
const REACH = 0.46;
const TAIL = 0.62;

/** How far a broken shard is thrown, in tiles, by the end of its life. */
const SHARD_REACH = 0.6;

export interface VolleyArrows {
  group: THREE.Group;
  update: (
    time: number,
    hidden: (projectileId: string, x: number, y: number) => boolean,
  ) => void;
  dispose: () => void;
}

export function buildVolleyArrows(replay: ReplayModel): VolleyArrows {
  const group = new THREE.Group();
  const disposables: { dispose: () => void }[] = [];

  // One ribbon per run on screen, borrowed from a pool. A volley can be cut into as many
  // runs as it has lanes, and several volleys can be in the air at once, so the pool is
  // grown on demand rather than sized from the lane count.
  const ribbons: {
    mesh: THREE.Mesh;
    geometry: THREE.BufferGeometry;
    positions: Float32Array;
    material: THREE.MeshBasicMaterial;
    lanes: number;
  }[] = [];

  const borrowRibbon = (index: number, lanes: number) => {
    const wanted = Math.max(lanes, 2);
    const existing = ribbons[index];
    if (existing && existing.lanes >= wanted) return existing;
    if (existing) {
      existing.geometry.dispose();
      group.remove(existing.mesh);
    }
    const geometry = new THREE.BufferGeometry();
    const positions = new Float32Array(wanted * 2 * 3);
    geometry.setAttribute(
      'position',
      new THREE.BufferAttribute(positions, 3).setUsage(THREE.DynamicDrawUsage),
    );
    const indices: number[] = [];
    for (let lane = 0; lane < wanted - 1; lane++) {
      const a = lane * 2;
      indices.push(a, a + 1, a + 2, a + 2, a + 1, a + 3);
    }
    geometry.setIndex(indices);
    const material =
      existing?.material ??
      new THREE.MeshBasicMaterial({
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
    if (!existing) disposables.push(material);
    const mesh = new THREE.Mesh(geometry, material);
    mesh.userData.cue = 'volley-arrow';
    mesh.frustumCulled = false;
    mesh.visible = false;
    group.add(mesh);
    const entry = { mesh, geometry, positions, material, lanes: wanted };
    ribbons[index] = entry;
    return entry;
  };

  // A bright bead per surviving blade. Without them a cut volley and a narrow one are the
  // same ribbon, and which lane was lost is the thing worth reading.
  const nodeGeometry = new THREE.SphereGeometry(0.13, 10, 8);
  disposables.push(nodeGeometry);
  const nodes: THREE.Mesh[] = [];
  const borrowNode = (index: number) => {
    while (nodes.length <= index) {
      const material = new THREE.MeshBasicMaterial({
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      });
      const mesh = new THREE.Mesh(nodeGeometry, material);
      mesh.userData.cue = 'volley-node';
      mesh.visible = false;
      group.add(mesh);
      nodes.push(mesh);
      disposables.push(material);
    }
    return nodes[index];
  };

  // And the segments that broke off: a ring that collapses when a shell ate the bolt, and
  // one that expands when a wall or a body shattered it.
  // Thin: a thick ring on the floor reads as a marker painted there, which is what the
  // first attempt looked like — two orange pads where two blades had just shattered.
  const shardGeometry = new THREE.RingGeometry(0.24, 0.32, 24);
  shardGeometry.rotateX(-Math.PI / 2);
  disposables.push(shardGeometry);
  const shards: THREE.Mesh[] = [];
  const borrowShard = (index: number) => {
    while (shards.length <= index) {
      const material = new THREE.MeshBasicMaterial({
        transparent: true,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const mesh = new THREE.Mesh(shardGeometry, material);
      mesh.userData.cue = 'volley-break';
      mesh.visible = false;
      group.add(mesh);
      shards.push(mesh);
      disposables.push(material);
    }
    return shards[index];
  };

  const accents = new Map<string, THREE.Color>();
  const accentFor = (unitKey: string) => {
    const cached = accents.get(unitKey);
    if (cached) return cached;
    const colour = new THREE.Color(
      unitAccent(replay, unitKey as Parameters<typeof unitAccent>[1]),
    );
    accents.set(unitKey, colour);
    return colour;
  };

  const update: VolleyArrows['update'] = (time, hidden) => {
    let usedRibbons = 0;
    let usedNodes = 0;
    let usedShards = 0;
    for (const volley of volleysAt(replay, time)) {
      const accent = accentFor(volley.ownerActor.unitKey);
      for (const run of volley.runs) {
        if (
          run.some((member) => hidden(member.id, member.x, member.y))
        )
          continue;
        writeRibbon(run, usedRibbons++, accent);
        for (const member of run) {
          const node = borrowNode(usedNodes++);
          node.visible = true;
          node.position.set(
            member.x + 0.5,
            PROJECTILE_HOVER,
            member.y + 0.5,
          );
          const material = node.material as THREE.MeshBasicMaterial;
          material.color.copy(accent).lerp(WHITE, 0.45);
          material.opacity = 0.95;
        }
      }
      for (const member of volley.broken) {
        if (hidden(member.id, member.x, member.y)) continue;
        const age = member.breakAge ?? 0;
        const shard = borrowShard(usedShards++);
        shard.visible = true;
        shard.position.set(member.x + 0.5, PROJECTILE_HOVER, member.y + 0.5);
        // Turned collapses inward (the return bolt carries the energy on);
        // shattered flies apart. Same ring, opposite direction, so which of
        // the two happened is legible without a second effect.
        shard.scale.setScalar(
          member.breakKind === 'deflected'
            ? 1.1 * (1 - age * 0.8)
            : 0.8 + age * SHARD_REACH * 3.2,
        );
        const material = shard.material as THREE.MeshBasicMaterial;
        material.color.copy(accent);
        material.opacity = (1 - age) ** 2 * 0.7;
      }
    }
    for (let index = usedRibbons; index < ribbons.length; index++)
      ribbons[index].mesh.visible = false;
    for (let index = usedNodes; index < nodes.length; index++)
      nodes[index].visible = false;
    for (let index = usedShards; index < shards.length; index++)
      shards[index].visible = false;
  };

  function writeRibbon(
    run: readonly VolleyMember[],
    index: number,
    accent: THREE.Color,
  ): void {
    const outline = volleyArrowOutline(run, REACH, TAIL);
    const lanes = outline.leading.length;
    const entry = borrowRibbon(index, lanes);
    for (let lane = 0; lane < lanes; lane++) {
      const leading = outline.leading[lane];
      const trailing = outline.trailing[lane];
      const base = lane * 6;
      entry.positions[base] = leading.x + 0.5;
      entry.positions[base + 1] = PROJECTILE_HOVER;
      entry.positions[base + 2] = leading.y + 0.5;
      entry.positions[base + 3] = trailing.x + 0.5;
      entry.positions[base + 4] = PROJECTILE_HOVER;
      entry.positions[base + 5] = trailing.y + 0.5;
    }
    // Unused pairs are collapsed onto the last real one rather than left stale: the index
    // buffer is sized for the pool slot, not for this run.
    for (let lane = lanes; lane < entry.lanes; lane++) {
      const base = lane * 6;
      entry.positions.copyWithin(base, base - 6, base);
    }
    entry.geometry.attributes.position.needsUpdate = true;
    entry.geometry.computeBoundingSphere();
    entry.mesh.visible = true;
    entry.material.color.copy(accent);
    entry.material.opacity = 0.38;
  }

  return {
    group,
    update,
    dispose: () => {
      for (const ribbon of ribbons) ribbon.geometry.dispose();
      for (const item of disposables) item.dispose();
    },
  };
}

const WHITE = new THREE.Color(0xffffff);
