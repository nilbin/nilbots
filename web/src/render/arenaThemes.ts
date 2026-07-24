export interface BotLook {
  id: string;
  label: string;
  suggestedAccent: string;
  image: HTMLImageElement | null;
  imageUrl: string;
  scale: number;
}

export interface ArenaTheme {
  id: string;
  label: string;
  floorTexture: HTMLImageElement | null;
  wallTexture: HTMLImageElement | null;
  wallTrimTexture: HTMLImageElement | null;
  wallShadowTexture: HTMLImageElement | null;
  zoneTexture: HTMLImageElement | null;
  zoneTextureScale: number;
  wall: {
    sourceInner: number;
    sourceCorner: number;
    inset: number;
    outset: number;
  };
  palette: {
    canvas: string;
    arena: string;
    floorTint: string;
    wallTint: string;
    zone: string;
    frame: string;
  };
}

interface ThemeManifest {
  id: string;
  label: string;
  textures: {
    floor: string;
    wall: string;
    wallTrim: string;
    wallShadow: string;
    zone?: {
      file: string;
      scaleTiles: number;
    };
  };
  wall: ArenaTheme['wall'];
  palette: ArenaTheme['palette'];
}

interface BotLookManifest {
  id: string;
  label: string;
  sprite: string;
  suggestedAccent: string;
  scale: number;
}

const themeManifests = import.meta.glob<ThemeManifest>(
  '../assets/themes/*/theme.json',
  { eager: true, import: 'default' },
);
const themeImages = import.meta.glob<string>(
  '../assets/themes/*/*.png',
  { eager: true, import: 'default', query: '?url' },
);
const lookManifests = import.meta.glob<BotLookManifest>(
  '../assets/bot-looks/*/look.json',
  { eager: true, import: 'default' },
);
const lookImages = import.meta.glob<string>(
  '../assets/bot-looks/*/*.png',
  { eager: true, import: 'default', query: '?url' },
);

const themes = buildThemes();
const looks = buildLooks();
const defaultThemeId = 'control-room';
const defaultLookId = 'vanguard';
const legacySlotLooks = ['vanguard', 'bulwark'] as const;

/**
 * A replay names the theme copied from its map JSON. There is intentionally no
 * map-ID lookup and no viewer preference: presentation ownership stays in data.
 */
export function arenaTheme(themeId?: string): ArenaTheme {
  return (
    (themeId ? themes.get(themeId) : undefined) ??
    requireEntry(themes, defaultThemeId, 'theme')
  );
}

/**
 * New replays carry the bot-owned look ID. The slot fallback exists only so
 * pre-look replays remain visually distinct when opened by a current viewer.
 */
export function botLook(lookId?: string, legacySlot = 0): BotLook {
  const legacyId = legacySlotLooks[Math.abs(legacySlot) % legacySlotLooks.length];
  return (
    (lookId ? looks.get(lookId) : undefined) ??
    looks.get(legacyId) ??
    requireEntry(looks, defaultLookId, 'bot look')
  );
}

export function botLookOptions(): readonly BotLook[] {
  return [...looks.values()].sort((a, b) => a.label.localeCompare(b.label));
}

export function presentationAccent(
  look: BotLook,
  participantAccent?: string,
): string {
  return participantAccent || look.suggestedAccent;
}

function buildThemes(): Map<string, ArenaTheme> {
  const result = new Map<string, ArenaTheme>();
  for (const [manifestPath, manifest] of Object.entries(themeManifests)) {
    const directory = manifestPath.slice(0, manifestPath.lastIndexOf('/'));
    const floorUrl = requireAsset(
      themeImages,
      `${directory}/${manifest.textures.floor}`,
      manifest.id,
    );
    const wallUrl = requireAsset(
      themeImages,
      `${directory}/${manifest.textures.wall}`,
      manifest.id,
    );
    const wallTrimUrl = requireAsset(
      themeImages,
      `${directory}/${manifest.textures.wallTrim}`,
      manifest.id,
    );
    const wallShadowUrl = requireAsset(
      themeImages,
      `${directory}/${manifest.textures.wallShadow}`,
      manifest.id,
    );
    const zoneUrl = manifest.textures.zone
      ? requireAsset(
          themeImages,
          `${directory}/${manifest.textures.zone.file}`,
          manifest.id,
        )
      : undefined;
    if (result.has(manifest.id))
      throw new Error(`Duplicate arena theme ID '${manifest.id}'.`);
    result.set(manifest.id, {
      id: manifest.id,
      label: manifest.label,
      floorTexture: loadImage(floorUrl),
      wallTexture: loadImage(wallUrl),
      wallTrimTexture: loadImage(wallTrimUrl),
      wallShadowTexture: loadImage(wallShadowUrl),
      zoneTexture: zoneUrl ? loadImage(zoneUrl) : null,
      zoneTextureScale: Math.max(
        0.5,
        manifest.textures.zone?.scaleTiles ?? 4,
      ),
      wall: manifest.wall,
      palette: manifest.palette,
    });
  }
  return result;
}

function buildLooks(): Map<string, BotLook> {
  const result = new Map<string, BotLook>();
  for (const [manifestPath, manifest] of Object.entries(lookManifests)) {
    const directory = manifestPath.slice(0, manifestPath.lastIndexOf('/'));
    const imageUrl = requireAsset(
      lookImages,
      `${directory}/${manifest.sprite}`,
      manifest.id,
    );
    if (result.has(manifest.id))
      throw new Error(`Duplicate bot look ID '${manifest.id}'.`);
    result.set(manifest.id, {
      id: manifest.id,
      label: manifest.label,
      suggestedAccent: manifest.suggestedAccent,
      image: loadImage(imageUrl),
      imageUrl,
      scale: manifest.scale,
    });
  }
  return result;
}

function requireAsset(
  assets: Record<string, string>,
  path: string,
  ownerId: string,
): string {
  const asset = assets[path];
  if (!asset)
    throw new Error(`Presentation '${ownerId}' references missing asset '${path}'.`);
  return asset;
}

function requireEntry<T>(entries: Map<string, T>, id: string, kind: string): T {
  const entry = entries.get(id);
  if (!entry) throw new Error(`Missing default ${kind} '${id}'.`);
  return entry;
}

function loadImage(source: string): HTMLImageElement | null {
  if (typeof Image === 'undefined') return null;
  const image = new Image();
  image.decoding = 'async';
  image.src = source;
  return image;
}
