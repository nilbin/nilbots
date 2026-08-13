import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, setEnergyOpacity, solidBarAt, targetBrackets, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'exchange',
  paint({ rig, owner, target, time, phaseProgress, reducedMotion }) {
    if (!owner || !target) return;
    const pulse = tellPulse(time);
    const dx = target.x - owner.x;
    const dz = target.z - owner.z;
    const length = Math.hypot(dx, dz) || 1;
    const normal = new THREE.Vector3(-dz / length, 0, dx / length);
    for (const [index, position] of [owner, target].entries()) {
      const spool = index === 0 ? rig.spool : rig.hub;
      spool.visible = true;
      spool.position.copy(position).setY(0.4);
      spool.scale.setScalar(index === 0 ? 1.55 : 0.88);
      spool.rotation.y = phaseProgress * Math.PI * (index === 0 ? 2 : -2);
    }
    const samples = reducedMotion ? 2 : 4;
    for (let index = 0; index < samples; index += 1) {
      const p0 = index / samples;
      const p1 = (index + 1) / samples;
      const wave0 = Math.sin((p0 + phaseProgress) * Math.PI * 2) * 0.26;
      const wave1 = Math.sin((p1 + phaseProgress) * Math.PI * 2) * 0.26;
      const from = new THREE.Vector3().lerpVectors(owner, target, p0).addScaledVector(normal, wave0);
      const to = new THREE.Vector3().lerpVectors(owner, target, p1).addScaledVector(normal, wave1);
      const ribbon = rig.segments[index]!;
      lineAt(ribbon, from, to, 0.24, 0.22);
      setEnergyOpacity(ribbon, 0.26);
      const mirror = rig.segments[index + samples]!;
      lineAt(
        mirror,
        new THREE.Vector3().lerpVectors(target, owner, p0).addScaledVector(normal, -wave0),
        new THREE.Vector3().lerpVectors(target, owner, p1).addScaledVector(normal, -wave1),
        0.18,
        0.23,
      );
      setEnergyOpacity(mirror, 0.2);
      const solid = rig.bars[index]!;
      solidBarAt(solid, from, to, 0.14, 0.13);
    }
    targetBrackets(rig, owner, pulse * 1.42, 8);
    targetBrackets(rig, target, pulse * 1.42, 16);
    for (const [index, position] of [owner, target].entries()) {
      const endpoint = rig.toruses[index]!;
      endpoint.visible = true;
      endpoint.position.copy(position).setY(0.1);
      endpoint.scale.setScalar(1.65 - phaseProgress * 0.54);
      setEnergyOpacity(endpoint, 0.25);
      const fin = rig.fins[index]!;
      fin.visible = true;
      fin.position.copy(position).setY(0.52 + Math.sin(phaseProgress * Math.PI) * 0.32);
      fin.rotation.z = Math.PI;
      fin.scale.set(2.15, 2.6, 2.15);
      rig.solid.opacity = 0.78;
      const plate = rig.slabs[index]!;
      plate.visible = true;
      plate.position.copy(position).setY(0.1);
      plate.scale.set(0.9, 0.11, 0.9);
    }
  },
  paintOutcome({ rig, facts, fraction }) {
    const relocations = facts.filter((fact) => fact.kind === 'body-relocated');
    if (relocations.length === 0) return;
    const life = 1 - fraction;
    for (const [index, fact] of relocations.entries()) {
      if (fact.kind !== 'body-relocated') continue;
      const from = new THREE.Vector3(fact.from.x + 0.5, 0.14, fact.from.y + 0.5);
      const to = new THREE.Vector3(fact.to.x + 0.5, 0.14, fact.to.y + 0.5);
      const dx = to.x - from.x;
      const dz = to.z - from.z;
      const length = Math.hypot(dx, dz) || 1;
      const normal = new THREE.Vector3(-dz / length, 0, dx / length);
      const samples = 4;
      for (let sample = 0; sample < samples; sample += 1) {
        const p0 = sample / samples;
        const p1 = (sample + 1) / samples;
        const bow = (index === 0 ? 1 : -1) * Math.sin(p0 * Math.PI) * 0.34;
        const bowNext = (index === 0 ? 1 : -1) * Math.sin(p1 * Math.PI) * 0.34;
        const start = new THREE.Vector3().lerpVectors(from, to, p0).addScaledVector(normal, bow);
        const end = new THREE.Vector3().lerpVectors(from, to, p1).addScaledVector(normal, bowNext);
        const trail = rig.segments[index * samples + sample]!;
        lineAt(trail, start, end, 0.18 * life + 0.06, 0.18);
        setEnergyOpacity(trail, life * 0.25);
        solidBarAt(rig.bars[index * samples + sample]!, start, end, 0.12, 0.13);
      }
      const shuttle = index === 0 ? rig.payload : rig.pod;
      shuttle.visible = true;
      shuttle.position.lerpVectors(from, to, fraction).setY(0.28 + Math.sin(fraction * Math.PI) * 0.35);
      shuttle.scale.set(1.15, 0.72, 1.15);
      shuttle.rotation.z = Math.PI / 2;
      const endpoint = rig.toruses[index]!;
      endpoint.visible = true;
      endpoint.position.copy(to).setY(0.09);
      endpoint.scale.setScalar(1.25 - fraction * 0.35);
      setEnergyOpacity(endpoint, life * 0.24);
    }
    rig.solid.color.set('#526267');
    rig.solid.emissive.set('#173037');
    rig.solid.emissiveIntensity = 0.06;
    rig.solid.opacity = 0.9 * life;
  },
});
