import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'kinetic-burst',
  topology: 'self-radius',
  phases: ['tell'],
  canvasMarker: 'pressure-ring',
  webglPainterId: 'kinetic-burst',
  audio: [
    { trigger: 'completed', cue: 'signature-kinetic' },
    { trigger: 'cancelled', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'tell-and-footprint',
    travelSamples: 0,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 8,
    panels: 0,
    particles: 4,
    markers: 4,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '04-control-level-up.png#kinetic-burst',
    dominantSilhouette: 'contracting local ring and low outward pressure torus',
  },
});
