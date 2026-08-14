import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'hardlight-block',
  topology: 'construct-tiles',
  phases: ['active'],
  canvasMarker: 'block',
  webglPainterId: 'hardlight-block',
  audio: [
    {
      trigger: 'started',
      cue: 'signature-mason',
      priority: 5,
      tickOffset: 0.28,
    },
    { trigger: 'maintained', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'tell-and-footprint',
    travelSamples: 0,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 11,
    panels: 5,
    particles: 0,
    markers: 4,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '01-high-impact-signatures.png#hardlight-block',
    dominantSilhouette: 'one grid-aligned block of five framed hardlight panes',
  },
});
