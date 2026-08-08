/**
 * Shared renderer contract for an unpossessed Arc Relay Core.
 *
 * Team ownership is deliberately absent here. Loose and in-flight Cores use a
 * pale, low-chroma white/lilac energy palette so neither launch team's cyan or
 * amber reads as possession. Both renderers consume these exact tokens.
 */
export const ARC_CORE_NEUTRAL_PALETTE = {
  body: '#686572',
  emissive: '#f4f0ff',
  glow: '#ddd5ff',
  light: '#faf7ff',
  canvasGlowInner: 'rgba(244, 240, 255, 0.46)',
  canvasGlowMiddle: 'rgba(221, 213, 255, 0.24)',
  canvasGlowOuter: 'rgba(221, 213, 255, 0)',
  canvasCentre: '#fffdfd',
  canvasInner: '#f1edff',
  canvasMiddle: '#c7c0da',
  canvasEdge: '#686572',
} as const;
