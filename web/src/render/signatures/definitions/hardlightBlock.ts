import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'hardlight-block',
  topology: 'construct-tiles',
  phases: ['active'],
  canvasMarker: 'block',
  webglPainterId: 'hardlight-block',
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
    segments: 4,
    panels: 4,
    particles: 0,
    markers: 4,
    transparentLayers: 1,
    persistentProps: 0,
  },
  fidelity: {
    reference: '01-high-impact-signatures.png#hardlight-block',
    dominantSilhouette: 'full tile frame and four chunky matte slabs',
  },
});
