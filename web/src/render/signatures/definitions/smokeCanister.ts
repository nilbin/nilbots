import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'smoke-canister',
  topology: 'target-area',
  phases: ['in-flight', 'active'],
  canvasMarker: 'smoke-field',
  webglPainterId: 'smoke-canister',
  audio: [
    { trigger: 'landed', cue: 'signature-smoke' },
    { trigger: 'maintained', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'tell-and-footprint',
    travelSamples: 2,
    particleScale: 0.3,
    decorativeDrift: false,
  },
  budget: {
    segments: 6,
    panels: 0,
    particles: 12,
    markers: 2,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '02-support-fields.png#smoke-canister',
    dominantSilhouette: 'muted canister flight and low rolling matte lobes',
  },
});
