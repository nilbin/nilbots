import { trackDecode } from '../render/assetReadiness';

/**
 * Renderer-only material maps.
 *
 * Keep this glob inside the lazy WebGL tree: the self-contained CLI viewer
 * replaces that entry with Canvas2D and therefore never pays for normal or
 * roughness maps it cannot render.
 */
const materialUrls = import.meta.glob<string>(
  '../assets/themes/*/pbr/*.webp',
  { eager: true, import: 'default', query: '?url' },
);
const images = new Map<string, HTMLImageElement>();

export function themeMaterialImage(
  themeId: string,
  path: string | null,
): HTMLImageElement | null {
  if (path === null || typeof Image === 'undefined') return null;
  const key = `../assets/themes/${themeId}/${path}`;
  const source = materialUrls[key];
  if (!source) return null;

  const existing = images.get(key);
  if (existing) return existing;
  const image = new Image();
  image.decoding = 'async';
  trackDecode(image);
  image.src = source;
  images.set(key, image);
  return image;
}

export function hasThemeMaterialAsset(
  themeId: string,
  path: string,
): boolean {
  return `../assets/themes/${themeId}/${path}` in materialUrls;
}
