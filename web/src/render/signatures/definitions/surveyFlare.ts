import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'survey-flare',
  topology: 'target-area',
  phases: ['in-flight', 'active'],
  canvasMarker: 'calibration-field',
  webglPainterId: 'survey-flare',
  audio: [
    { trigger: 'landed', cue: 'signature-survey' },
    { trigger: 'maintained', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'tell-and-footprint',
    travelSamples: 2,
    particleScale: 0.25,
    decorativeDrift: false,
  },
  budget: {
    segments: 12,
    panels: 0,
    particles: 12,
    markers: 4,
    transparentLayers: 3,
    persistentProps: 0,
  },
  fidelity: {
    reference: '02-support-fields.png#survey-flare',
    dominantSilhouette: 'compact flight head, horizon ring, exact calibration footprint',
  },
});
