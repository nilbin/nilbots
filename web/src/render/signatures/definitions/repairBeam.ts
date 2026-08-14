import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'repair-beam',
  topology: 'owner-target',
  phases: ['channel'],
  canvasMarker: 'braid',
  webglPainterId: 'repair-beam',
  audio: [
    {
      trigger: 'repair-fact',
      cue: 'signature-repair',
      priority: 6,
      tickOffset: 0.28,
    },
    { trigger: 'maintained', cue: 'silent' },
    { trigger: 'cancelled', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'endpoints-and-outcome',
    travelSamples: 2,
    particleScale: 0.35,
    decorativeDrift: false,
  },
  budget: {
    segments: 18,
    panels: 0,
    particles: 5,
    markers: 1,
    transparentLayers: 3,
    persistentProps: 0,
  },
  fidelity: {
    reference: '02-support-fields.png#repair-beam',
    dominantSilhouette: 'three braided bands and travelling repair packets',
  },
});
