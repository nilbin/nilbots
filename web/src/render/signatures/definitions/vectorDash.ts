import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'vector-dash',
  topology: 'self-path',
  phases: ['tell'],
  canvasMarker: 'arrow',
  webglPainterId: 'vector-dash',
  audio: [
    { trigger: 'completed', cue: 'signature-motion' },
    { trigger: 'cancelled', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'tell-and-footprint',
    travelSamples: 1,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 4,
    panels: 0,
    particles: 3,
    markers: 5,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '03-motion-objective.png#vector-dash',
    dominantSilhouette: 'broken floor promise and one short pressure wake',
  },
});
