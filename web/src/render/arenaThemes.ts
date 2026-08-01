import { trackDecode } from './assetReadiness';
import { preferredAtlasWidth } from './atlasResolution';

export type BotLookClassId =
  | 'striker'
  | 'bulwark'
  | 'fabricator'
  | 'kestrel'
  | 'palisade'
  | 'towline'
  | 'patchbay'
  | 'lantern'
  | 'mortar'
  | 'minesmith'
  | 'hush'
  | 'relay'
  | 'switchback'
  | 'longshot'
  | 'mason'
  | 'sunder'
  | 'repulsor'
  | 'veil'
  | 'nest';
export type BotLocomotionCue = 'low-hover' | 'wheels' | 'treads' | 'skids';

export interface BotLook {
  id: string;
  label: string;
  suggestedAccent: string;
  defaultProjectileLookId?: string;
  /**
   * Presentation-only compatibility metadata. The account/API contract does not enforce
   * this yet; class-owned defaults and future class cosmetics can still expose their
   * intended family to frontend consumers without parsing the look ID.
   */
  classId: BotLookClassId | null;
  /**
   * Presentation-only motion language. This moves the rendered body, never the
   * authoritative actor position or gameplay layer.
   */
  locomotionCue: BotLocomotionCue | null;
  image: HTMLImageElement | null;
  imageUrl: string;
  /** Raw SVG only when the asset carries semantic team-accent surfaces. */
  teamAccentSvg: string | null;
  /** Raster-exception semantic light mask; composited with renderer-owned colour. */
  teamMaskImage: HTMLImageElement | null;
  /** Class-owned signature stamp, authored beside the chassis and tinted the same way. */
  effectTeamAccentSvg: string | null;
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
  geometry3d: {
    height: number;
    cornerRadius: number;
    upperProfile: {
      height: number;
      inset: number;
      chamfer: number;
    } | null;
    details: {
      panelEvery: number;
      ventEvery: number;
      clampEvery: number;
      panelColor: string;
      clampColor: string;
      ventColor: string;
    } | null;
  };
  material3d: {
    normalMap: string | null;
    roughnessMap: string | null;
    normalScale: number;
    roughness: number;
    metalness: number;
  };
}

export interface ArenaTheme {
  id: string;
  label: string;
  floorTexture: HTMLImageElement | null;
  zoneTexture: HTMLImageElement | null;
  zoneTextureScale: number;
  environment3d: {
    lighting: {
      keyColor: string;
      keyIntensity: number;
      ambientColor: string;
      ambientIntensity: number;
      fillColor: string;
      fillIntensity: number;
    };
    floor: {
      bumpScale: number;
      roughness: number;
      metalness: number;
    };
  };
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
  environment3d?: {
    lighting?: Partial<ArenaTheme['environment3d']['lighting']>;
    floor?: Partial<ArenaTheme['environment3d']['floor']>;
  };
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
        geometry3d?: {
          height: number;
          cornerRadius: number;
          upperProfile?: {
            height: number;
            inset: number;
            chamfer: number;
          };
          details?: {
            panelEvery: number;
            ventEvery: number;
            clampEvery: number;
            panelColor: string;
            clampColor: string;
            ventColor: string;
          };
        };
        material3d?: {
          normalMap?: string;
          roughnessMap?: string;
          normalScale?: number;
          roughness?: number;
          metalness?: number;
        };
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
  classId?: BotLookClassId;
  locomotionCue?: BotLocomotionCue;
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
/**
 * Baked-down atlases from `scripts/generate-atlas-variants.mjs`, keyed
 * `.../variants/<name>@<width>.webp`. Generated and gitignored, so an absent variant is
 * normal — `atlasSource` falls back to the master rather than failing.
 */
const themeAtlasVariants = import.meta.glob<string>(
  '../assets/themes/*/variants/*.webp',
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
const lookSvgSources = import.meta.glob<string>(
  '../assets/bot-looks/*/*.svg',
  { eager: true, import: 'default', query: '?raw' },
);
const classLookManifests = import.meta.glob<BotLookManifest>(
  '../assets/class-looks/*/look.json',
  { eager: true, import: 'default' },
);
const classLookImages = import.meta.glob<string>(
  [
    '../assets/class-looks/*/sprite.svg',
    '../assets/class-looks/*/sprite.png',
    '../assets/class-looks/*/team-mask.png',
    '../assets/class-looks/*/effect.svg',
  ],
  { eager: true, import: 'default', query: '?url' },
);
const classLookSvgSources = import.meta.glob<string>(
  [
    '../assets/class-looks/*/sprite.svg',
    '../assets/class-looks/*/effect.svg',
  ],
  { eager: true, import: 'default', query: '?raw' },
);
const projectileLookManifests = import.meta.glob<ProjectileLookManifest>(
  '../assets/projectile-looks/*/look.json',
  { eager: true, import: 'default' },
);
const projectileLookImages = import.meta.glob<string>(
  '../assets/projectile-looks/*/*.svg',
  { eager: true, import: 'default', query: '?url' },
);
const classProjectileLookManifests = import.meta.glob<ProjectileLookManifest>(
  '../assets/class-projectile-looks/*/look.json',
  { eager: true, import: 'default' },
);
const classProjectileLookImages = import.meta.glob<string>(
  '../assets/class-projectile-looks/*/*.svg',
  { eager: true, import: 'default', query: '?url' },
);

const classIds = new Set<BotLookClassId>([
  'striker',
  'bulwark',
  'fabricator',
  'kestrel',
  'palisade',
  'towline',
  'patchbay',
  'lantern',
  'mortar',
  'minesmith',
  'hush',
  'relay',
  'switchback',
  'longshot',
  'mason',
  'sunder',
  'repulsor',
  'veil',
  'nest',
]);
const themes = buildThemes();
const looks = buildLooks(lookManifests, lookImages, lookSvgSources);
const classLooks = buildLooks(
  classLookManifests,
  classLookImages,
  classLookSvgSources,
);
const projectileLooks = buildProjectileLooks(
  projectileLookManifests,
  projectileLookImages,
);
const classProjectileLooks = buildProjectileLooks(
  classProjectileLookManifests,
  classProjectileLookImages,
);
validateDefaultProjectiles();
/**
 * Which theme stands in when a replay names one this build does not have.
 *
 * Normally `control-room`. The CLI builds one artifact per theme (see
 * `vite.cli.config.ts`), and in those there is no `control-room` to fall back *to* — so
 * the build substitutes the theme it was scoped to, and this stays the only place that
 * decides.
 */
const defaultThemeId =
  typeof __BOTARENA_DEFAULT_THEME__ === 'string' ? __BOTARENA_DEFAULT_THEME__ : 'control-room';
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

/**
 * A form-authored look may name a player cosmetic or a class-owned presentation asset.
 * Class defaults are kept out of `botLookOptions`, so they render without becoming
 * globally equippable cosmetics while the account contract has no class compatibility.
 */
export function presentationBotLook(
  lookId?: string,
  legacySlot = 0,
): BotLook {
  return (
    (lookId ? classLooks.get(lookId) : undefined) ??
    botLook(lookId, legacySlot)
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

/** Class-owned projectile masks are renderable but are not appearance-editor options. */
export function presentationProjectileLook(
  lookId?: string,
): ProjectileLook {
  return (
    (lookId ? classProjectileLooks.get(lookId) : undefined) ??
    projectileLook(lookId)
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
        atlasSource(directory, family.material),
      );
      const edgeAtlasTexture = lazyImage(
        atlasSource(directory, family.edgeAtlas),
      );
      const shadowAtlasTexture = lazyImage(
        atlasSource(directory, family.shadowAtlas),
      );
      wallFamilies.set(familyId, {
        id: familyId,
        label: family.label,
        geometry3d: wallGeometry3d(
          family.geometry3d,
          manifest.id,
          familyId,
        ),
        material3d: wallMaterial3d(
          family.material3d,
          manifest.id,
          familyId,
        ),
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
      environment3d: environment3d(
        manifest.environment3d,
        manifest.id,
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

function wallGeometry3d(
  candidate:
    | {
        height: number;
        cornerRadius: number;
        upperProfile?: {
          height: number;
          inset: number;
          chamfer: number;
        };
        details?: {
          panelEvery: number;
          ventEvery: number;
          clampEvery: number;
          panelColor: string;
          clampColor: string;
          ventColor: string;
        };
      }
    | undefined,
  themeId: string,
  familyId: string,
): WallFamily['geometry3d'] {
  const height = candidate?.height ?? 0.62;
  const cornerRadius = candidate?.cornerRadius ?? 0.1;
  if (
    !Number.isFinite(height) ||
    height < 0.25 ||
    height > 0.9 ||
    !Number.isFinite(cornerRadius) ||
    cornerRadius < 0 ||
    cornerRadius > 0.4
  )
    throw new Error(
      `Theme '${themeId}' wall family '${familyId}' has invalid 3D geometry.`,
    );
  const upperProfile = candidate?.upperProfile ?? null;
  if (
    upperProfile !== null &&
    (!finiteRange(upperProfile.height, 0.04, height - 0.05) ||
      !finiteRange(upperProfile.inset, 0, 0.12) ||
      !finiteRange(upperProfile.chamfer, 0.005, 0.08) ||
      upperProfile.inset + upperProfile.chamfer > 0.16)
  )
    throw new Error(
      `Theme '${themeId}' wall family '${familyId}' has invalid upper profile.`,
    );
  const details = candidate?.details ?? null;
  if (
    details !== null &&
    (![details.panelEvery, details.ventEvery, details.clampEvery].every(
      (value) => Number.isInteger(value) && value >= 1 && value <= 64,
    ) ||
      !validHex(details.panelColor) ||
      !validHex(details.clampColor) ||
      !validHex(details.ventColor))
  )
    throw new Error(
      `Theme '${themeId}' wall family '${familyId}' has invalid 3D details.`,
    );
  return { height, cornerRadius, upperProfile, details };
}

function wallMaterial3d(
  candidate: ThemeManifest['walls']['families'][string]['material3d'],
  themeId: string,
  familyId: string,
): WallFamily['material3d'] {
  const result = {
    normalMap: candidate?.normalMap ?? null,
    roughnessMap: candidate?.roughnessMap ?? null,
    normalScale: candidate?.normalScale ?? 1,
    roughness: candidate?.roughness ?? 0.88,
    metalness: candidate?.metalness ?? 0.2,
  };
  if (
    (result.normalMap !== null && !validAssetPath(result.normalMap)) ||
    (result.roughnessMap !== null &&
      !validAssetPath(result.roughnessMap)) ||
    !finiteRange(result.normalScale, 0, 4) ||
    !finiteRange(result.roughness, 0, 1) ||
    !finiteRange(result.metalness, 0, 1)
  )
    throw new Error(
      `Theme '${themeId}' wall family '${familyId}' has invalid 3D material.`,
    );
  return result;
}

function environment3d(
  candidate: ThemeManifest['environment3d'],
  themeId: string,
): ArenaTheme['environment3d'] {
  const lighting = {
    keyColor: candidate?.lighting?.keyColor ?? '#e8f1ff',
    keyIntensity: candidate?.lighting?.keyIntensity ?? 4.4,
    ambientColor: candidate?.lighting?.ambientColor ?? '#6f8bb0',
    ambientIntensity: candidate?.lighting?.ambientIntensity ?? 2.4,
    fillColor: candidate?.lighting?.fillColor ?? '#4d7099',
    fillIntensity: candidate?.lighting?.fillIntensity ?? 1.5,
  };
  const floor = {
    bumpScale: candidate?.floor?.bumpScale ?? 1.6,
    roughness: candidate?.floor?.roughness ?? 0.86,
    metalness: candidate?.floor?.metalness ?? 0.12,
  };
  if (
    !validHex(lighting.keyColor) ||
    !validHex(lighting.ambientColor) ||
    !validHex(lighting.fillColor) ||
    !finiteRange(lighting.keyIntensity, 0, 12) ||
    !finiteRange(lighting.ambientIntensity, 0, 12) ||
    !finiteRange(lighting.fillIntensity, 0, 12) ||
    !finiteRange(floor.bumpScale, 0, 4) ||
    !finiteRange(floor.roughness, 0, 1) ||
    !finiteRange(floor.metalness, 0, 1)
  )
    throw new Error(`Theme '${themeId}' has invalid 3D environment values.`);
  return { lighting, floor };
}

function finiteRange(value: number, minimum: number, maximum: number): boolean {
  return Number.isFinite(value) && value >= minimum && value <= maximum;
}

function validHex(value: string): boolean {
  return /^#[0-9a-f]{6}$/i.test(value);
}

function validAssetPath(value: string): boolean {
  return (
    value.length > 0 &&
    !value.startsWith('/') &&
    !value.includes('\\') &&
    value.split('/').every((segment) => segment !== '' && segment !== '..')
  );
}

function buildLooks(
  manifests: Record<string, BotLookManifest>,
  images: Record<string, string>,
  svgSources: Record<string, string> = {},
): Map<string, BotLook> {
  const result = new Map<string, BotLook>();
  for (const [manifestPath, manifest] of Object.entries(manifests)) {
    const directory = manifestPath.slice(0, manifestPath.lastIndexOf('/'));
    const imageUrl = requireAsset(
      images,
      `${directory}/${manifest.sprite}`,
      manifest.id,
    );
    const source = svgSources[`${directory}/${manifest.sprite}`] ?? null;
    const teamMaskUrl = images[`${directory}/team-mask.png`] ?? null;
    const teamAccentSvg =
      source?.includes('data-team-accent="true"') === true
        ? source
        : null;
    if (manifest.classId !== undefined && !classIds.has(manifest.classId))
      throw new Error(
        `Bot look '${manifest.id}' has unknown class '${manifest.classId}'.`,
      );
    if (
      manifest.locomotionCue !== undefined &&
      !['low-hover', 'wheels', 'treads', 'skids'].includes(
        manifest.locomotionCue,
      )
    )
      throw new Error(
        `Bot look '${manifest.id}' has unknown locomotion cue ` +
          `'${String(manifest.locomotionCue)}'.`,
      );
    if (result.has(manifest.id))
      throw new Error(`Duplicate bot look ID '${manifest.id}'.`);
    result.set(manifest.id, {
      id: manifest.id,
      label: manifest.label,
      suggestedAccent: manifest.suggestedAccent,
      defaultProjectileLookId: manifest.defaultProjectile,
      classId: manifest.classId ?? null,
      locomotionCue: manifest.locomotionCue ?? null,
      image: loadImage(imageUrl),
      imageUrl,
      teamAccentSvg,
      teamMaskImage: teamMaskUrl ? loadImage(teamMaskUrl) : null,
      effectTeamAccentSvg:
        svgSources[`${directory}/effect.svg`] ?? null,
      scale: manifest.scale,
    });
  }
  return result;
}

function validateDefaultProjectiles(): void {
  for (const [collection, projectiles] of [
    [looks, projectileLooks],
    [classLooks, classProjectileLooks],
  ] as const) {
    for (const look of collection.values()) {
      if (
        look.defaultProjectileLookId &&
        !projectiles.has(look.defaultProjectileLookId)
      )
        throw new Error(
          `Bot look '${look.id}' references missing default projectile ` +
            `'${look.defaultProjectileLookId}'.`,
        );
    }
  }
}

function buildProjectileLooks(
  manifests: Record<string, ProjectileLookManifest>,
  images: Record<string, string>,
): Map<string, ProjectileLook> {
  const result = new Map<string, ProjectileLook>();
  for (const [manifestPath, manifest] of Object.entries(manifests)) {
    const directory = manifestPath.slice(0, manifestPath.lastIndexOf('/'));
    const imageUrl = requireAsset(
      images,
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

const accentedBotImages = new Map<string, HTMLImageElement>();
const maxAccentedBotImages = 48;
const accentedEffectImages = new Map<string, HTMLImageElement>();

/**
 * The sprite source for one bot/team pairing.
 *
 * Only elements explicitly tagged `data-team-accent="true"` are substituted. Authored
 * armor, energy and material colours remain untouched, so team identity is a small
 * semantic surface rather than a chassis-wide wash.
 */
export function teamAccentedBotImage(
  look: BotLook,
  accent: string,
): HTMLImageElement | null {
  if (look.teamMaskImage) return teamAccentedRasterImage(look, accent);
  if (!look.teamAccentSvg || typeof Image === 'undefined')
    return look.image;
  const source = applyTeamAccentToSvg(look.teamAccentSvg, accent);
  if (source === look.teamAccentSvg) return look.image;

  const key = `${look.id}:${accent.toLowerCase()}`;
  const cached = accentedBotImages.get(key);
  if (cached) {
    accentedBotImages.delete(key);
    accentedBotImages.set(key, cached);
    return cached;
  }

  const image = new Image();
  image.decoding = 'async';
  trackDecode(image);
  image.src =
    `data:image/svg+xml;charset=utf-8,${encodeURIComponent(source)}`;
  accentedBotImages.set(key, image);
  if (accentedBotImages.size > maxAccentedBotImages) {
    const oldest = accentedBotImages.keys().next().value;
    if (oldest !== undefined) accentedBotImages.delete(oldest);
  }
  return image;
}

function teamAccentedRasterImage(
  look: BotLook,
  accent: string,
): HTMLImageElement | null {
  if (
    typeof document === 'undefined' ||
    typeof Image === 'undefined' ||
    !/^#[0-9a-f]{6}$/i.test(accent) ||
    !look.image ||
    !look.teamMaskImage
  )
    return look.image;

  const key = `${look.id}:${accent.toLowerCase()}`;
  const cached = accentedBotImages.get(key);
  if (cached) {
    accentedBotImages.delete(key);
    accentedBotImages.set(key, cached);
    return cached;
  }
  if (
    !look.image.complete ||
    look.image.naturalWidth <= 0 ||
    !look.teamMaskImage.complete ||
    look.teamMaskImage.naturalWidth <= 0
  )
    return look.image;

  const width = look.image.naturalWidth;
  const height = look.image.naturalHeight;
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d');
  const lightCanvas = document.createElement('canvas');
  lightCanvas.width = width;
  lightCanvas.height = height;
  const light = lightCanvas.getContext('2d');
  if (!context || !light) return look.image;

  context.drawImage(look.image, 0, 0, width, height);
  light.drawImage(look.teamMaskImage, 0, 0, width, height);
  light.globalCompositeOperation = 'source-in';
  light.fillStyle = accent;
  light.fillRect(0, 0, width, height);
  context.save();
  context.globalAlpha = 0.62;
  context.filter = `blur(${Math.max(3, Math.round(width / 55))}px)`;
  context.drawImage(lightCanvas, 0, 0);
  context.restore();
  context.drawImage(lightCanvas, 0, 0);

  const image = new Image();
  image.decoding = 'async';
  trackDecode(image);
  image.src = canvas.toDataURL('image/png');
  accentedBotImages.set(key, image);
  if (accentedBotImages.size > maxAccentedBotImages) {
    const oldest = accentedBotImages.keys().next().value;
    if (oldest !== undefined) accentedBotImages.delete(oldest);
  }
  return image;
}

/** The class-owned signature stamp for one bot/team pairing. */
export function teamAccentedEffectImage(
  look: BotLook,
  accent: string,
): HTMLImageElement | null {
  if (!look.effectTeamAccentSvg || typeof Image === 'undefined') return null;
  const source = applyTeamAccentToSvg(look.effectTeamAccentSvg, accent);
  const key = `${look.id}:effect:${accent.toLowerCase()}`;
  const cached = accentedEffectImages.get(key);
  if (cached) return cached;
  const image = new Image();
  image.decoding = 'async';
  trackDecode(image);
  image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(source)}`;
  accentedEffectImages.set(key, image);
  if (accentedEffectImages.size > maxAccentedBotImages) {
    const oldest = accentedEffectImages.keys().next().value;
    if (oldest !== undefined) accentedEffectImages.delete(oldest);
  }
  return image;
}

/**
 * Pure substitution used by the image loader and pinned independently in tests.
 * A strict colour grammar prevents replay-authored strings from becoming SVG markup.
 */
export function applyTeamAccentToSvg(
  source: string,
  accent: string,
): string {
  if (!/^#[0-9a-f]{6}$/i.test(accent)) return source;
  return source.replace(
    /<[^>]+\bdata-team-accent="true"[^>]*>/gi,
    (element) =>
      element
        .replace(/\bfill="(?!none\b)[^"]*"/gi, `fill="${accent}"`)
        .replace(/\bstroke="(?!none\b)[^"]*"/gi, `stroke="${accent}"`),
  );
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
  trackDecode(image);
  image.src = source;
  return image;
}

/**
 * Atlas URLs are cheap; decoded 4096×4096 images are not. Keep every theme
 * discoverable, but only allocate its images when that theme is actually
 * rendered. A mobile replay should never decode the other maps' atlases.
 */
/**
 * The URL to actually download for an atlas: the baked variant this device needs, or the
 * master when no smaller bake is big enough (or none was generated).
 *
 * Resolved per atlas rather than once globally so a missing variant degrades to the
 * master for that file alone.
 */
function atlasSource(directory: string, filename: string): string {
  const master = requireAsset(themeImages, `${directory}/${filename}`, directory);
  const base = filename.replace(/\.webp$/, '');
  const width = preferredAtlasWidth();
  if (width >= 4096) return master;
  return themeAtlasVariants[`${directory}/variants/${base}@${width}.webp`] ?? master;
}

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
