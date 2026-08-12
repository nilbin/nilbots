import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { paintPropFallback } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'trip-node',
  paint({ rig, points, time, persistentPropReady }) {
    paintPropFallback(
      rig,
      points,
      time,
      persistentPropReady,
      new THREE.Vector3(0.75, 0.42, 0.75),
    );
    rig.main.opacity *= 0.62;
  },
});
