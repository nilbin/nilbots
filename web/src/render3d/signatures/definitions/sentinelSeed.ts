import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { paintPropFallback } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'sentinel-seed',
  paint({ rig, points, time, persistentPropReady }) {
    paintPropFallback(
      rig,
      points,
      time,
      persistentPropReady,
      new THREE.Vector3(0.82, 1.15, 0.82),
    );
    rig.main.opacity *= 0.62;
  },
});
