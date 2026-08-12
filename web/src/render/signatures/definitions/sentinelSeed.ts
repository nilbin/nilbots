import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'sentinel-seed',
  topology: 'persistent-prop',
  phases: ['tell', 'active'],
  canvasMarker: 'prop',
  webglPainterId: 'sentinel-seed',
  audio: [
    { trigger: 'started', cue: 'silent' },
    { trigger: 'damage-fact', cue: 'projectile' },
    { trigger: 'maintained', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'prop-and-capacity',
    travelSamples: 1,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 2,
    panels: 0,
    particles: 0,
    markers: 2,
    transparentLayers: 1,
    persistentProps: 1,
  },
  fidelity: {
    reference: 'approved-meshy7-sentinel-seed',
    dominantSilhouette: 'matte stationary base, authored yaw head, honest capacity',
  },
});
