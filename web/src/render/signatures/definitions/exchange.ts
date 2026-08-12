import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'exchange',
  topology: 'paired-actors',
  phases: ['tell'],
  canvasMarker: 'paired-fold',
  webglPainterId: 'exchange',
  audio: [
    { trigger: 'completed', cue: 'signature-exchange' },
    { trigger: 'cancelled', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'endpoints-and-outcome',
    travelSamples: 1,
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
    reference: '04-control-level-up.png#exchange',
    dominantSilhouette: 'paired endpoint brackets and complementary folding ribbons',
  },
});
