import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'null-field',
  topology: 'field-footprint',
  phases: ['tell', 'active'],
  canvasMarker: 'dark-field',
  webglPainterId: 'null-field',
  audio: [
    { trigger: 'activated', cue: 'signature-null' },
    { trigger: 'maintained', cue: 'silent' },
    { trigger: 'cancelled', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'tell-and-footprint',
    travelSamples: 0,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 16,
    panels: 0,
    particles: 8,
    markers: 8,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '02-support-fields.png#null-field',
    dominantSilhouette: 'contracting boundary and shallow dark-violet floor lens',
  },
});
