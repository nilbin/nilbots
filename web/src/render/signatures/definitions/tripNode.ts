import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'trip-node',
  topology: 'persistent-prop',
  phases: ['active'],
  canvasMarker: 'prop',
  webglPainterId: 'trip-node',
  audio: [
    {
      trigger: 'started',
      cue: 'signature-trip-deploy',
      priority: 4,
      tickOffset: 0.26,
    },
    {
      trigger: 'triggered',
      cue: 'signature-trip-trigger',
      priority: 7,
      tickOffset: 0.38,
    },
  ],
  reducedMotion: {
    retain: 'prop-and-capacity',
    travelSamples: 0,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 0,
    panels: 0,
    particles: 0,
    markers: 1,
    transparentLayers: 1,
    persistentProps: 1,
  },
  fidelity: {
    reference: 'approved-meshy7-trip-node',
    dominantSilhouette: 'matte low trap body with honest suppression and capacity',
  },
});
