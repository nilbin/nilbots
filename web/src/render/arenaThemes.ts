export interface BotLook {
  id: string;
  label: string;
  accent: string;
  image: HTMLImageElement | null;
  scale: number;
}

export interface ArenaTheme {
  id: string;
  label: string;
  floorTexture: HTMLImageElement | null;
  wallTexture: HTMLImageElement | null;
  botLooks: readonly BotLook[];
  palette: {
    canvas: string;
    arena: string;
    floorTint: string;
    wallTint: string;
    grid: string;
    zone: string;
    frame: string;
  };
}

const floorTexture = loadImage(
  new URL('../assets/themes/control-room/floor-metal.png', import.meta.url).href,
);
const wallTexture = loadImage(
  new URL('../assets/themes/control-room/wall-bulkhead.png', import.meta.url).href,
);
const vanguard = loadImage(
  new URL('../assets/themes/control-room/bot-vanguard.png', import.meta.url).href,
);
const bulwark = loadImage(
  new URL('../assets/themes/control-room/bot-bulwark.png', import.meta.url).href,
);

const controlRoom: ArenaTheme = {
  id: 'control-room',
  label: 'Control Room',
  floorTexture,
  wallTexture,
  botLooks: [
    {
      id: 'vanguard',
      label: 'Vanguard',
      accent: '#38bdf8',
      image: vanguard,
      scale: 1.34,
    },
    {
      id: 'bulwark',
      label: 'Bulwark',
      accent: '#fb645b',
      image: bulwark,
      scale: 1.28,
    },
  ],
  palette: {
    canvas: '#05080d',
    arena: '#09101a',
    floorTint: 'rgba(5, 12, 20, 0.24)',
    wallTint: 'rgba(4, 8, 13, 0.20)',
    grid: 'rgba(91, 135, 175, 0.17)',
    zone: '#fbbf24',
    frame: '#26384b',
  },
};

/**
 * A map chooses its presentation; viewers never choose a skin for a replay.
 * Every shipped map currently belongs to the default Control Room world.
 * Future maps bind to another immutable theme here until map packages carry
 * the same association in their presentation manifest.
 */
const themeByMap: Readonly<Record<string, ArenaTheme>> = {
  'arena-01': controlRoom,
  'basic-01': controlRoom,
  'bastion-01': controlRoom,
  'causeway-01': controlRoom,
  'crossfire-01': controlRoom,
  'gallery-01': controlRoom,
};

export function arenaThemeForMap(mapId: string): ArenaTheme {
  return themeByMap[mapId] ?? controlRoom;
}

export function botLookForSlot(theme: ArenaTheme, slot: number): BotLook {
  return theme.botLooks[Math.abs(slot) % theme.botLooks.length];
}

export function presentationAccent(
  theme: ArenaTheme,
  slot: number,
  fallback: string,
): string {
  return botLookForSlot(theme, slot).accent || fallback;
}

function loadImage(source: string): HTMLImageElement | null {
  if (typeof Image === 'undefined') return null;
  const image = new Image();
  image.decoding = 'async';
  image.src = source;
  return image;
}
