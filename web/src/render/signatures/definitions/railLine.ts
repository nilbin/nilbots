import { defineArcSignature } from '../definition';

export default defineArcSignature({
  signatureId: 'rail-line',
  topology: 'frozen-line',
  phases: ['tell'],
  canvasMarker: 'pressure-lane',
  webglPainterId: 'rail-line',
  audio: [
    { trigger: 'started', cue: 'attack-windup' },
    { trigger: 'completed', cue: 'projectile' },
    { trigger: 'damage-fact', cue: 'impact' },
    { trigger: 'cancelled', cue: 'silent' },
  ],
  reducedMotion: {
    retain: 'tell-and-footprint',
    travelSamples: 1,
    particleScale: 0,
    decorativeDrift: false,
  },
  budget: {
    segments: 20,
    panels: 8,
    particles: 0,
    markers: 4,
    transparentLayers: 2,
    persistentProps: 0,
  },
  fidelity: {
    reference: '01-high-impact-signatures.png#rail-line',
    dominantSilhouette: 'notched wind-up and broad dim pressure slab',
  },
});
