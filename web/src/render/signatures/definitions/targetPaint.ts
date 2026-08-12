import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'target-paint',
  topology: 'owner-target',
  phases: ['tell', 'active'],
  canvasMarker: 'target-brackets',
  webglPainterId: 'target-paint',
  audio: [
    { trigger: 'acquired', cue: 'signature-target' },
    { trigger: 'maintained', cue: 'silent' },
    { trigger: 'cancelled', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'endpoints-and-outcome',
    travelSamples: 0,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 12,
    panels: 3,
    particles: 0,
    markers: 4,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '04-control-level-up.png#target-paint',
    dominantSilhouette: 'four blunt acquisition brackets and three tally bars',
  },
});
