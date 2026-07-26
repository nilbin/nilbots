/**
 * Light thrown onto the arena by things that flash.
 *
 * The renderer already glows the *source* — projectiles and impacts composite with
 * `lighter`, and bots carry an accent bloom. What was missing is the light reaching
 * anything else: a shot lit itself but not the floor beside it, so the arena read as
 * stickers on a texture rather than objects in a place. Spill is most of what separates
 * flat from lit, and it needs no new art.
 *
 * Drawn under the entities and *before* the fog, so a flash in a tile the selected bot
 * cannot see is dimmed with everything else there. Light you can see through fog would
 * leak information the bot never had.
 */

export type LightKind = 'shot' | 'impact' | 'destroyed';

export interface LightSource {
  kind: LightKind;
  /** Tile coordinates of the flash. */
  x: number;
  y: number;
  /** 0 at the moment it fires, 1 when spent. */
  age: number;
  /** Accent of the bot responsible, so a flash carries its owner's colour. */
  color: string;
}

interface LightProfile {
  /** Radius as a multiple of tile size. */
  radius: number;
  /** Peak alpha at the centre. */
  intensity: number;
}

/**
 * Deliberately restrained. Additive light stacks, and a six-game set with two bots firing
 * every third tick will overlap constantly — values that look right alone wash the arena
 * out in combat.
 */
const PROFILES: Record<LightKind, LightProfile> = {
  shot: { radius: 1.6, intensity: 0.3 },
  impact: { radius: 2.2, intensity: 0.42 },
  destroyed: { radius: 4.5, intensity: 0.55 },
};

export function drawLightSpill(
  ctx: CanvasRenderingContext2D,
  sources: readonly LightSource[],
  geometry: { px: (x: number) => number; py: (y: number) => number; tile: number },
): void {
  if (sources.length === 0) return;
  const { px, py, tile } = geometry;

  ctx.save();
  ctx.globalCompositeOperation = 'lighter';

  for (const source of sources) {
    const profile = PROFILES[source.kind];
    // Fade out over the source's life; squared so the flash is bright then drops away
    // rather than lingering as a glow that never quite leaves.
    const fade = Math.max(0, 1 - source.age) ** 2;
    if (fade <= 0.01) continue;

    const radius = tile * profile.radius;
    const centreX = px(source.x) + tile / 2;
    const centreY = py(source.y) + tile / 2;

    const gradient = ctx.createRadialGradient(centreX, centreY, 0, centreX, centreY, radius);
    gradient.addColorStop(0, withAlpha(source.color, profile.intensity * fade));
    // A mid stop keeps the falloff from looking like a hard disc at low intensity.
    gradient.addColorStop(0.45, withAlpha(source.color, profile.intensity * fade * 0.35));
    gradient.addColorStop(1, withAlpha(source.color, 0));

    ctx.fillStyle = gradient;
    ctx.beginPath();
    ctx.arc(centreX, centreY, radius, 0, Math.PI * 2);
    ctx.fill();
  }

  ctx.restore();
}

/** `#rrggbb` to `rgba(...)`. Falls back to white, which is a plausible flash. */
function withAlpha(color: string, alpha: number): string {
  const match = /^#?([0-9a-f]{6})$/i.exec(color.trim());
  if (!match) return `rgba(255, 255, 255, ${alpha.toFixed(3)})`;
  const value = Number.parseInt(match[1], 16);
  const r = (value >> 16) & 0xff;
  const g = (value >> 8) & 0xff;
  const b = value & 0xff;
  return `rgba(${r}, ${g}, ${b}, ${alpha.toFixed(3)})`;
}
