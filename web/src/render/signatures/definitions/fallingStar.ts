import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'falling-star',
  topology: 'target-area',
  phases: ['tell'],
  canvasMarker: 'cardinal-target',
  webglPainterId: 'falling-star',
  audio: [
    { trigger: 'damage-fact', cue: 'impact' },
    { trigger: 'cancelled', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'tell-and-footprint',
    travelSamples: 1,
    particleScale: 0.25,
    decorativeDrift: false,
  },
  budget: {
    segments: 8,
    panels: 1,
    particles: 10,
    markers: 4,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '01-high-impact-signatures.png#falling-star',
    dominantSilhouette: 'four-arm reticle, descending dark capsule, low impact cross',
  },
});
