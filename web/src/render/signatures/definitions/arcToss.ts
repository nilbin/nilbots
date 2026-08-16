import { defineArcSignature } from '../definition';

/** Arc Toss resolves after the accepted tick's combat phase. */
export const ARC_TOSS_RELEASE_TICK_OFFSET = 0.68;

export default defineArcSignature({
  signatureId: 'arc-toss',
  topology: 'objective-flight',
  phases: ['in-flight'],
  canvasMarker: 'objective-arc',
  webglPainterId: 'arc-toss',
  audio: [
    { trigger: 'core-facts', cue: 'core-family' },
    {
      trigger: 'launched',
      cue: 'signature-arc-toss',
      priority: 6,
      tickOffset: ARC_TOSS_RELEASE_TICK_OFFSET,
    },
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
    dominantSilhouette: 'destination marker and the one authoritative Core in flight',
  },
});
