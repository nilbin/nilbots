import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  clamp01,
  hasConstructRemoval,
  risingSettle,
  setEnergyColour,
  setEnergyOpacity,
} from '../primitives';

const PRISM_GLASS = '#79aeb2';
const PRISM_FRAME = '#566468';
const SCREEN_GLASS = '#73d8ff';
const SCREEN_FRAME = '#b79352';
const SCREEN_FRAME_EMISSIVE = '#4b3515';

function isMovingScreen(signature: {
  maximumCapacity?: number | null;
}): boolean {
  return (signature.maximumCapacity ?? 0) >= 10;
}

export default defineArcSignatureWebGLPainter({
  signatureId: 'prism-wall',
  paint({
    rig,
    signature,
    points,
    owner,
    ownerAngle,
    time,
    reducedMotion,
    phaseStartedTick,
  }) {
    if (points.length === 0) return;
    const deploy = reducedMotion ? 1 : risingSettle(time, phaseStartedTick);
    if (isMovingScreen(signature) && owner && ownerAngle !== undefined) {
      const forward = new THREE.Vector3(
        Math.cos(ownerAngle),
        0,
        Math.sin(ownerAngle),
      );
      const tangent = new THREE.Vector3(-forward.z, 0, forward.x);
      const yaw = -Math.atan2(tangent.z, tangent.x);
      const health = Math.max(
        0,
        Math.min(
          1,
          signature.remainingCapacity / (signature.maximumCapacity ?? 1),
        ),
      );
      const centres: THREE.Vector3[] = [];
      for (let index = 0; index < 3; index += 1) {
        const lateral = index - 1;
        const point = owner.clone()
          .addScaledVector(forward, 0.92 - Math.abs(lateral) * 0.035)
          .addScaledVector(tangent, lateral * 0.91)
          .setY(0.05);
        centres.push(point);
        const delay = reducedMotion
          ? 1
          : clamp01(deploy * 1.4 - Math.abs(lateral) * 0.1);
        const frame = rig.frames[index]!;
        frame.visible = true;
        frame.position.copy(point).setY(0.07 + delay * 0.43);
        frame.rotation.set(0, yaw, lateral * 0.018);
        frame.scale.set(1.06, Math.max(0.07, delay * 2.06), 0.76);

        const panel = rig.panels[index]!;
        panel.visible = true;
        panel.position.copy(point).setY(0.08 + delay * 0.43);
        panel.rotation.set(0, yaw, lateral * 0.018);
        panel.scale.set(0.97, Math.max(0.08, delay * 1.9), 0.6);
        setEnergyColour(panel, SCREEN_GLASS);
        setEnergyOpacity(
          panel,
          (0.16 + delay * 0.16) * (signature.suppressed ? 0.38 : 1),
        );

        for (const [post, side] of [
          [rig.posts[index * 2]!, -1],
          [rig.posts[index * 2 + 1]!, 1],
        ] as const) {
          post.visible = true;
          post.position.copy(point)
            .addScaledVector(tangent, side * 0.43)
            .setY(0.05 + delay * 0.46);
          post.rotation.set(0, yaw, 0);
          post.scale.set(0.72, Math.max(0.08, delay * 2.08), 0.72);
        }

        const capacity = rig.rings[index]!;
        capacity.visible = index < Math.ceil(health * 3);
        capacity.position.copy(point).setY(0.055);
        capacity.scale.setScalar(
          0.66 + 0.035 * Math.sin(time * Math.PI * 1.5 + index),
        );
        setEnergyColour(capacity, SCREEN_GLASS);
        setEnergyOpacity(capacity, 0.2 + health * 0.16);
      }
      for (let index = 0; index < 2; index += 1) {
        const connector = rig.bars[index]!;
        connector.visible = true;
        connector.position.lerpVectors(centres[index]!, centres[index + 1]!, 0.5)
          .setY(0.82 * deploy);
        connector.rotation.set(0, yaw, 0);
        connector.scale.set(0.52, Math.max(0.035, deploy * 0.055), 0.07);
      }
      rig.solid.color.set(signature.suppressed ? '#6d7479' : SCREEN_FRAME);
      rig.solid.emissive.set(
        signature.suppressed ? '#22282b' : SCREEN_FRAME_EMISSIVE,
      );
      rig.solid.emissiveIntensity = signature.suppressed ? 0.02 : 0.13;
      rig.solid.opacity = signature.suppressed ? 0.56 : 0.98;
      rig.main.color.set(SCREEN_FRAME);
      rig.main.opacity = signature.suppressed ? 0.3 : 0.8;
      return;
    }
    rig.fill.color.set(PRISM_GLASS);
    rig.fill.opacity = 0.14;
    const direction = points.length > 1
      ? new THREE.Vector3().subVectors(points.at(-1)!, points[0]!)
      : new THREE.Vector3(1, 0, 0);
    const yaw = -Math.atan2(direction.z, direction.x);
    for (const [index, point] of points.slice(0, 3).entries()) {
      const panel = rig.panels[index]!;
      const frame = rig.frames[index]!;
      const delay = reducedMotion ? 1 : clamp01(deploy * 1.35 - index * 0.14);
      frame.visible = true;
      frame.position.copy(point).setY(0.05 + delay * 0.37);
      // Extruded frame geometry is authored in XY; keep Y vertical and rotate only around
      // the arena up-axis. Rotating it onto the floor made the old "wall" read as two
      // orange hoops in normal replays.
      frame.rotation.set(0, yaw, 0);
      frame.scale.set(1.2, Math.max(0.07, delay * 1.82), 0.9);
      panel.visible = true;
      panel.position.copy(point).setY(0.06 + delay * 0.37);
      panel.rotation.set(0, yaw, 0);
      panel.scale.set(1.12, Math.max(0.08, delay * 1.68), 0.68);
      setEnergyColour(panel, PRISM_GLASS);
      setEnergyOpacity(panel, 0.1 + delay * 0.08);
      const frontPost = rig.posts[index * 2]!;
      const backPost = rig.posts[index * 2 + 1]!;
      for (const [post, side] of [[frontPost, -1], [backPost, 1]] as const) {
        post.visible = true;
        post.position.copy(point).add(
          new THREE.Vector3(
            Math.cos(yaw) * side * 0.43,
            0.04 + delay * 0.42,
            -Math.sin(yaw) * side * 0.43,
          ),
        );
        post.scale.set(0.82, Math.max(0.08, delay * 1.82), 0.82);
      }
      const capacity = rig.rings[index]!;
      capacity.visible = index < signature.remainingCapacity;
      capacity.position.copy(point).setY(0.045);
      capacity.scale.setScalar(0.72 + 0.04 * Math.sin(time * Math.PI * 1.2));
      setEnergyColour(capacity, PRISM_GLASS);
      setEnergyOpacity(capacity, 0.2);
    }
    rig.solid.color.set(PRISM_FRAME);
    rig.solid.emissive.set('#162a2d');
    rig.solid.emissiveIntensity = 0.04;
    rig.solid.opacity = 0.96;
  },
  paintOutcome({
    rig,
    priorSignature,
    facts,
    owner,
    ownerAngle,
    fraction,
    reducedMotion,
  }) {
    if (!hasConstructRemoval(facts) || !priorSignature) return;
    if (isMovingScreen(priorSignature) && owner && ownerAngle !== undefined) {
      const forward = new THREE.Vector3(
        Math.cos(ownerAngle),
        0,
        Math.sin(ownerAngle),
      );
      const tangent = new THREE.Vector3(-forward.z, 0, forward.x);
      const yaw = -Math.atan2(tangent.z, tangent.x);
      const life = 1 - fraction;
      const height = reducedMotion ? life : 1 - fraction ** 2;
      const centres: THREE.Vector3[] = [];
      for (let index = 0; index < 3; index += 1) {
        const lateral = index - 1;
        const point = owner.clone()
          .addScaledVector(forward, 0.92 - Math.abs(lateral) * 0.035)
          .addScaledVector(tangent, lateral * 0.91)
          .setY(0.05);
        centres.push(point);
        const frame = rig.frames[index]!;
        frame.visible = true;
        frame.position.copy(point).setY(0.07 + height * 0.43);
        frame.rotation.set(0, yaw, lateral * 0.018);
        frame.scale.set(1.06 * life, Math.max(0.045, height * 2.06), 0.76);
        const panel = rig.panels[index]!;
        panel.visible = true;
        panel.position.copy(point).setY(0.08 + height * 0.43);
        panel.rotation.set(0, yaw, lateral * 0.018);
        panel.scale.set(0.97 * life, Math.max(0.045, height * 1.9), 0.6);
        setEnergyColour(panel, SCREEN_GLASS);
        setEnergyOpacity(panel, life * 0.28);
        for (const [post, side] of [
          [rig.posts[index * 2]!, -1],
          [rig.posts[index * 2 + 1]!, 1],
        ] as const) {
          post.visible = true;
          post.position.copy(point)
            .addScaledVector(tangent, side * 0.43)
            .setY(0.05 + height * 0.46);
          post.rotation.set(0, yaw, 0);
          post.scale.set(0.72 * life, Math.max(0.045, height * 2.08), 0.72);
        }
        const mote = rig.particles[index]!;
        mote.visible = !reducedMotion;
        mote.position.copy(point).setY(0.22 + fraction * (0.5 + index * 0.08));
        mote.scale.setScalar(0.4 * life);
        setEnergyColour(mote, SCREEN_GLASS);
        setEnergyOpacity(mote, life * 0.18);
      }
      for (let index = 0; index < 2; index += 1) {
        const connector = rig.bars[index]!;
        connector.visible = true;
        connector.position.lerpVectors(centres[index]!, centres[index + 1]!, 0.5)
          .setY(0.82 * height);
        connector.rotation.set(0, yaw, 0);
        connector.scale.set(0.52 * life, 0.055, 0.07);
      }
      rig.solid.color.set(SCREEN_FRAME);
      rig.solid.emissive.set(SCREEN_FRAME_EMISSIVE);
      rig.solid.emissiveIntensity = 0.08;
      rig.solid.opacity = life * 0.9;
      return;
    }
    const points = priorSignature.positions.map(
      (point) => new THREE.Vector3(point.x + 0.5, 0.065, point.y + 0.5),
    );
    if (points.length === 0) return;
    const life = 1 - fraction;
    const collapse = reducedMotion ? 1 : 1 - (1 - fraction) ** 3;
    const direction = points.length > 1
      ? new THREE.Vector3().subVectors(points.at(-1)!, points[0]!)
      : new THREE.Vector3(1, 0, 0);
    const yaw = -Math.atan2(direction.z, direction.x);
    for (const [index, point] of points.slice(0, 3).entries()) {
      const delay = clamp01(collapse * 1.3 - index * 0.12);
      const height = Math.max(0.04, 1 - delay);
      const frame = rig.frames[index]!;
      frame.visible = true;
      frame.position.copy(point).setY(0.05 + height * 0.37);
      frame.rotation.set(0, yaw, 0);
      frame.scale.set(1.2, Math.max(0.045, height * 1.82), 0.9);
      const panel = rig.panels[index]!;
      panel.visible = true;
      panel.position.copy(point).setY(0.06 + height * 0.37);
      panel.rotation.set(0, yaw, 0);
      panel.scale.set(
        Math.max(0.08, 1.12 * life),
        Math.max(0.045, height * 1.68),
        0.68,
      );
      setEnergyColour(panel, PRISM_GLASS);
      setEnergyOpacity(panel, life * 0.14);
      for (const [post, side] of [
        [rig.posts[index * 2]!, -1],
        [rig.posts[index * 2 + 1]!, 1],
      ] as const) {
        post.visible = true;
        post.position.copy(point).add(new THREE.Vector3(
          Math.cos(yaw) * side * 0.43,
          0.04 + height * 0.42,
          -Math.sin(yaw) * side * 0.43,
        ));
        post.scale.set(0.82, Math.max(0.045, height * 1.82), 0.82);
      }
      const mote = rig.particles[index]!;
      mote.visible = !reducedMotion;
      mote.position.copy(point).setY(0.18 + fraction * (0.54 + index * 0.1));
      mote.scale.setScalar(0.46 * life);
      setEnergyColour(mote, PRISM_GLASS);
      setEnergyOpacity(mote, life * 0.12);
    }
    rig.solid.color.set(PRISM_FRAME);
    rig.solid.emissive.set('#162a2d');
    rig.solid.emissiveIntensity = 0.025;
    rig.solid.opacity = life * 0.82;
  },
});
