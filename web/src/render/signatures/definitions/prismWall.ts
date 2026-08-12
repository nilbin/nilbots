import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'prism-wall',
  topology: 'construct-tiles',
  phases: ['active'],
  canvasMarker: 'panels',
  webglPainterId: 'prism-wall',
  audio: [
    { trigger: 'started', cue: 'signature-construct' },
    { trigger: 'maintained', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'tell-and-footprint',
    travelSamples: 0,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 6,
    panels: 3,
    particles: 0,
    markers: 6,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '03-motion-objective.png#prism-wall',
    dominantSilhouette: 'contact posts and three low rough panes',
  },
});
