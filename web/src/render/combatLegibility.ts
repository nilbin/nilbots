/**
 * Combat information is not theme decoration. These renderer-owned neutrals
 * keep health and ordnance readable over every map material and bot accent.
 *
 * The dark/light pair has a worst-case contrast ratio above 4.5:1 against any
 * opaque background luminance. Important shapes use both tones, so one edge
 * always remains visible even when the other merges into the surface.
 */
export const combatLegibility = {
  dark: '#02040a',
  light: '#ffffff',
  minimumDualToneContrast: 4.5,
} as const;

export interface ProjectileStrokeWidths {
  glow: number;
  shadow: number;
  energy: number;
  core: number;
}

export function projectileStrokeWidths(tile: number): ProjectileStrokeWidths {
  return {
    glow: Math.max(7, tile * 0.22),
    shadow: Math.max(5, tile * 0.12),
    energy: Math.max(3, tile * 0.075),
    core: Math.max(1.5, tile * 0.035),
  };
}

export interface HealthIndicatorMetrics {
  width: number;
  shadowOffset: number;
  trackShadow: number;
  track: number;
  fillShadow: number;
  fill: number;
}

export function healthIndicatorMetrics(tile: number): HealthIndicatorMetrics {
  return {
    width: Math.max(12, tile * 0.52),
    shadowOffset: Math.max(1, tile * 0.025),
    trackShadow: Math.max(2.5, tile * 0.05),
    track: Math.max(1, tile * 0.018),
    fillShadow: Math.max(4, tile * 0.08),
    fill: Math.max(2.5, tile * 0.045),
  };
}

export function contrastRatio(first: string, second: string): number {
  const firstLuminance = relativeLuminance(first);
  const secondLuminance = relativeLuminance(second);
  return (
    (Math.max(firstLuminance, secondLuminance) + 0.05) /
    (Math.min(firstLuminance, secondLuminance) + 0.05)
  );
}

/**
 * Minimum, over every possible background luminance, of the stronger contrast
 * supplied by the dark or light keyline.
 */
export function dualToneContrastFloor(
  dark = combatLegibility.dark,
  light = combatLegibility.light,
): number {
  const lower = Math.min(relativeLuminance(dark), relativeLuminance(light));
  const upper = Math.max(relativeLuminance(dark), relativeLuminance(light));
  return Math.sqrt((upper + 0.05) / (lower + 0.05));
}

function relativeLuminance(hex: string): number {
  const match = /^#([0-9a-f]{6})$/i.exec(hex);
  if (!match) throw new Error(`Expected a six-digit hex color, received '${hex}'.`);
  const value = Number.parseInt(match[1], 16);
  const red = linearChannel((value >> 16) & 0xff);
  const green = linearChannel((value >> 8) & 0xff);
  const blue = linearChannel(value & 0xff);
  return red * 0.2126 + green * 0.7152 + blue * 0.0722;
}

function linearChannel(channel: number): number {
  const encoded = channel / 255;
  return encoded <= 0.04045
    ? encoded / 12.92
    : ((encoded + 0.055) / 1.055) ** 2.4;
}
