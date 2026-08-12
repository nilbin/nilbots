import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'tractor-hook',
  topology: 'owner-target',
  phases: ['tell', 'in-flight'],
  canvasMarker: 'tether',
  webglPainterId: 'tractor-hook',
  audio: [
    { trigger: 'started', cue: 'attack-windup' },
    {
      trigger: 'launched',
      cue: 'signature-tether',
      replacesGenericRelease: true,
    },
    { trigger: 'cancelled', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'endpoints-and-outcome',
    travelSamples: 2,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 12,
    panels: 0,
    particles: 2,
    markers: 2,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '01-high-impact-signatures.png#tractor-hook',
    dominantSilhouette: 'narrowing line, compact grapple head, taut dark cable',
  },
});
