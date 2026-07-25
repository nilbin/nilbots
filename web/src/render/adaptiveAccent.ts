const graphicalContrastTarget = 3;

/**
 * Keep the authored accent when it already reads. Otherwise make the smallest
 * one-percent blend toward black or white that reaches graphical contrast.
 */
export function adjustAccentForLuminance(
  accent: string,
  backgroundLuminance: number,
  target = graphicalContrastTarget,
): string {
  const source = parseHex(accent);
  if (!source) return accent;
  if (contrastRatio(relativeLuminance(source), backgroundLuminance) >= target)
    return accent;

  const towardDark = findAdjustment(source, [0, 0, 0], backgroundLuminance, target);
  const towardLight = findAdjustment(
    source,
    [255, 255, 255],
    backgroundLuminance,
    target,
  );
  if (!towardDark) return towardLight?.color ?? accent;
  if (!towardLight) return towardDark.color;
  if (towardDark.amount === towardLight.amount)
    return backgroundLuminance > 0.18 ? towardDark.color : towardLight.color;
  return towardDark.amount < towardLight.amount
    ? towardDark.color
    : towardLight.color;
}

export function adjustAccentForBackground(
  accent: string,
  background: string,
  target = graphicalContrastTarget,
): string {
  const parsed = parseHex(background);
  return parsed
    ? adjustAccentForLuminance(accent, relativeLuminance(parsed), target)
    : accent;
}

export function sampleCanvasLuminance(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  cssWidth: number,
  cssHeight: number,
  radius = 2,
): number | null {
  const scaleX = ctx.canvas.width / Math.max(1, cssWidth);
  const scaleY = ctx.canvas.height / Math.max(1, cssHeight);
  const left = Math.max(0, Math.floor((x - radius) * scaleX));
  const top = Math.max(0, Math.floor((y - radius) * scaleY));
  const right = Math.min(ctx.canvas.width, Math.ceil((x + radius) * scaleX));
  const bottom = Math.min(ctx.canvas.height, Math.ceil((y + radius) * scaleY));
  if (right <= left || bottom <= top) return null;

  try {
    const pixels = ctx.getImageData(left, top, right - left, bottom - top).data;
    let luminance = 0;
    let samples = 0;
    for (let index = 0; index < pixels.length; index += 4) {
      if (pixels[index + 3] === 0) continue;
      luminance += relativeLuminance([
        pixels[index],
        pixels[index + 1],
        pixels[index + 2],
      ]);
      samples++;
    }
    return samples > 0 ? luminance / samples : null;
  } catch {
    // A future cross-origin texture must not make replay rendering fail.
    return null;
  }
}

export function colorContrast(
  foreground: string,
  background: string,
): number | null {
  const first = parseHex(foreground);
  const second = parseHex(background);
  if (!first || !second) return null;
  return contrastRatio(relativeLuminance(first), relativeLuminance(second));
}

type Rgb = readonly [number, number, number];

function findAdjustment(
  source: Rgb,
  destination: Rgb,
  backgroundLuminance: number,
  target: number,
): { color: string; amount: number } | null {
  for (let amount = 1; amount <= 100; amount++) {
    const fraction = amount / 100;
    const candidate: Rgb = [
      blend(source[0], destination[0], fraction),
      blend(source[1], destination[1], fraction),
      blend(source[2], destination[2], fraction),
    ];
    if (
      contrastRatio(relativeLuminance(candidate), backgroundLuminance) >= target
    )
      return { color: toHex(candidate), amount };
  }
  return null;
}

function blend(start: number, end: number, fraction: number): number {
  return Math.round(start + (end - start) * fraction);
}

function parseHex(hex: string): Rgb | null {
  const match = /^#([0-9a-f]{6})$/i.exec(hex);
  if (!match) return null;
  const value = Number.parseInt(match[1], 16);
  return [(value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff];
}

function toHex([red, green, blue]: Rgb): string {
  return `#${[red, green, blue]
    .map((channel) => channel.toString(16).padStart(2, '0'))
    .join('')}`;
}

function relativeLuminance([red, green, blue]: Rgb): number {
  return (
    linearChannel(red) * 0.2126 +
    linearChannel(green) * 0.7152 +
    linearChannel(blue) * 0.0722
  );
}

function linearChannel(channel: number): number {
  const encoded = channel / 255;
  return encoded <= 0.04045
    ? encoded / 12.92
    : ((encoded + 0.055) / 1.055) ** 2.4;
}

function contrastRatio(first: number, second: number): number {
  return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
}
