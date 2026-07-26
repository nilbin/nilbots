export interface BotLook {
  id: string;
  label: string;
  suggestedAccent: string;
  defaultProjectileLookId?: string;
  image: HTMLImageElement | null;
  imageUrl: string;
  scale: number;
}

export interface ProjectileLook {
  id: string;
  label: string;
  image: HTMLImageElement | null;
  imageUrl: string;
  scale: number;
}

export interface WallFamily {
  id: string;
  label: string;
  materialTexture: HTMLImageElement | null;
  edgeAtlasTexture: HTMLImageElement | null;
  shadowAtlasTexture: HTMLImageElement | null;
}

export interface ArenaTheme {
  id: string;
  label: string;
  floorTexture: HTMLImageElement | null;
  zoneTexture: HTMLImageElement | null;
  zoneTextureScale: number;
  walls: {
    defaults: {
      boundary: string;
      interior: string;
    };
    atlas: {
      columns: number;
      contentPixels: number;
      gutterPixels: number;
    };
    families: ReadonlyMap<string, WallFamily>;
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
    zone?: {
      file: string;
      scaleTiles: number;
    };
  };
  walls: {
    defaults: ArenaTheme['walls']['defaults'];
    atlas: ArenaTheme['walls']['atlas'];
    families: Record<
      string,
      {
        label: string;
        material: string;
        edgeAtlas: string;
        shadowAtlas: string;
      }
    >;
  };
  palette: ArenaTheme['palette'];
}

interface BotLookManifest {
  id: string;
  label: string;
  sprite: string;
  suggestedAccent: string;
  defaultProjectile?: string;
  scale: number;
}

interface ProjectileLookManifest {
  id: string;
  label: string;
  sprite: string;
  scale: number;
}

const themeManifests = import.meta.glob<ThemeManifest>(
  '../assets/themes/*/theme.json',
  { eager: true, import: 'default' },
);
const themeImages = import.meta.glob<string>(
  ['../assets/themes/*/*.png', '../assets/themes/*/*.webp'],
  { eager: true, import: 'default', query: '?url' },
);
const lookManifests = import.meta.glob<BotLookManifest>(
  '../assets/bot-looks/*/look.json',
  { eager: true, import: 'default' },
);
const lookImages = import.meta.glob<string>(
  ['../assets/bot-looks/*/*.png', '../assets/bot-looks/*/*.svg'],
  { eager: true, import: 'default', query: '?url' },
);
const projectileLookManifests = import.meta.glob<ProjectileLookManifest>(
  '../assets/projectile-looks/*/look.json',
  { eager: true, import: 'default' },
);
const projectileLookImages = import.meta.glob<string>(
  '../assets/projectile-looks/*/*.svg',
  { eager: true, import: 'default', query: '?url' },
);

const themes = buildThemes();
const looks = buildLooks();
const projectileLooks = buildProjectileLooks();
validateDefaultProjectiles();
const defaultThemeId = 'control-room';
const defaultLookId = 'vanguard';
const defaultProjectileLookId = 'pulse-bolt';
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

export function projectileLook(lookId?: string): ProjectileLook {
  return (
    (lookId ? projectileLooks.get(lookId) : undefined) ??
    requireEntry(projectileLooks, defaultProjectileLookId, 'projectile look')
  );
}

export function projectileLookOptions(): readonly ProjectileLook[] {
  return [...projectileLooks.values()].sort((a, b) =>
    a.label.localeCompare(b.label),
  );
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
    const wallFamilies = new Map<string, WallFamily>();
    for (const [familyId, family] of Object.entries(manifest.walls.families)) {
      const materialTexture = lazyImage(
        requireAsset(themeImages, `${directory}/${family.material}`, manifest.id),
      );
      const edgeAtlasTexture = lazyImage(
        requireAsset(themeImages, `${directory}/${family.edgeAtlas}`, manifest.id),
      );
      const shadowAtlasTexture = lazyImage(
        requireAsset(themeImages, `${directory}/${family.shadowAtlas}`, manifest.id),
      );
      wallFamilies.set(familyId, {
        id: familyId,
        label: family.label,
        get materialTexture() {
          return materialTexture();
        },
        get edgeAtlasTexture() {
          return edgeAtlasTexture();
        },
        get shadowAtlasTexture() {
          return shadowAtlasTexture();
        },
      });
    }
    for (const defaultFamily of [
      manifest.walls.defaults.boundary,
      manifest.walls.defaults.interior,
    ])
      if (!wallFamilies.has(defaultFamily))
        throw new Error(
          `Theme '${manifest.id}' references missing wall family '${defaultFamily}'.`,
        );
    const zoneUrl = manifest.textures.zone
      ? requireAsset(
          themeImages,
          `${directory}/${manifest.textures.zone.file}`,
          manifest.id,
        )
      : undefined;
    const floorTexture = lazyImage(floorUrl);
    const zoneTexture = zoneUrl ? lazyImage(zoneUrl) : () => null;
    if (result.has(manifest.id))
      throw new Error(`Duplicate arena theme ID '${manifest.id}'.`);
    result.set(manifest.id, {
      id: manifest.id,
      label: manifest.label,
      get floorTexture() {
        return floorTexture();
      },
      get zoneTexture() {
        return zoneTexture();
      },
      zoneTextureScale: Math.max(
        0.5,
        manifest.textures.zone?.scaleTiles ?? 4,
      ),
      walls: {
        defaults: manifest.walls.defaults,
        atlas: manifest.walls.atlas,
        families: wallFamilies,
      },
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
      defaultProjectileLookId: manifest.defaultProjectile,
      image: loadImage(imageUrl),
      imageUrl,
      scale: manifest.scale,
    });
  }
  return result;
}

function validateDefaultProjectiles(): void {
  for (const look of looks.values()) {
    if (
      look.defaultProjectileLookId &&
      !projectileLooks.has(look.defaultProjectileLookId)
    )
      throw new Error(
        `Bot look '${look.id}' references missing default projectile ` +
          `'${look.defaultProjectileLookId}'.`,
      );
  }
}

function buildProjectileLooks(): Map<string, ProjectileLook> {
  const result = new Map<string, ProjectileLook>();
  for (const [manifestPath, manifest] of Object.entries(projectileLookManifests)) {
    const directory = manifestPath.slice(0, manifestPath.lastIndexOf('/'));
    const imageUrl = requireAsset(
      projectileLookImages,
      `${directory}/${manifest.sprite}`,
      manifest.id,
    );
    if (result.has(manifest.id))
      throw new Error(`Duplicate projectile look ID '${manifest.id}'.`);
    result.set(manifest.id, {
      id: manifest.id,
      label: manifest.label,
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

/**
 * Atlas URLs are cheap; decoded 4096×4096 images are not. Keep every theme
 * discoverable, but only allocate its images when that theme is actually
 * rendered. A mobile replay should never decode the other maps' atlases.
 */
function lazyImage(source: string): () => HTMLImageElement | null {
  let initialized = false;
  let image: HTMLImageElement | null = null;
  return () => {
    if (!initialized) {
      initialized = true;
      image = loadImage(source);
    }
    return image;
  };
}
