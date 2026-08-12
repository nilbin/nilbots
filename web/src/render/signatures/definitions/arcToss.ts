import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'arc-toss',
  topology: 'objective-flight',
  phases: ['tell', 'in-flight'],
  canvasMarker: 'objective-arc',
  webglPainterId: 'arc-toss',
  audio: [
    { trigger: 'core-facts', cue: 'core-family' },
    { trigger: 'started', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'endpoints-and-outcome',
    travelSamples: 2,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 8,
    panels: 0,
    particles: 0,
    markers: 2,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '03-motion-objective.png#arc-toss',
    dominantSilhouette: 'destination tell and the one authoritative Core in flight',
  },
});
