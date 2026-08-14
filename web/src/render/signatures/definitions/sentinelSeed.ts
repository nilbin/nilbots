import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'sentinel-seed',
  topology: 'persistent-prop',
  phases: ['tell', 'active'],
  canvasMarker: 'prop',
  webglPainterId: 'sentinel-seed',
  projectileAudioProfileIds: ['sentinel-bolt'],
  audio: [
    {
      trigger: 'started',
      cue: 'silent',
      replacesGenericWindup: true,
    },
    {
      trigger: 'deployed',
      cue: 'signature-sentinel-deploy',
      priority: 6,
      tickOffset: 0.3,
    },
    {
      trigger: 'projectile-launched',
      cue: 'signature-sentinel-fire',
      priority: 3,
      tickOffset: 0.08,
    },
    { trigger: 'damage-fact', cue: 'impact' },
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
