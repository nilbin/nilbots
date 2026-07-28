import { adjustAccentForBackground } from '../render/adaptiveAccent';

/**
 * CSS tokens cannot be read while rendering an SVG or binding a server-provided colour,
 * so keep the two dark grounds here beside the helper that needs their concrete values.
 * These values mirror `index.css`; changing either surface means changing this pair too.
 */
const surfaces = {
  background: '#0b0705',
  panel: '#191210',
} as const;

export type PlayerAccentSurface = keyof typeof surfaces;

/**
 * Player colours are arbitrary server data. Preserve the authored colour when it reads
 * against the target ground and make the smallest contrast correction when it does not.
 */
export function playerAccent(
  accent: string,
  surface: PlayerAccentSurface = 'background',
): string {
  return adjustAccentForBackground(accent, surfaces[surface]);
}
