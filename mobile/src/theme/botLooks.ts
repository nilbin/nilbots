import type { ImageSourcePropType } from 'react-native';

/**
 * The bot chassis sprites, shared with the site rather than copied — these `require`s
 * reach into `web/src/assets/bot-looks/`, which Metro can see because metro.config.js
 * watches the repo root.
 *
 * Metro needs static require paths, so this map is written out by hand and must list
 * every directory under web/src/assets/bot-looks. Nothing enforces that yet: a look added
 * to the catalogue but missed here falls back to Vanguard silently, on the app only. If
 * looks start changing often, generate this file in scripts/generate-api-clients.sh and
 * let the contract-drift job catch it.
 */
const LOOKS = {
  'aureate-warden': require('../../../web/src/assets/bot-looks/aureate-warden/sprite.svg'),
  bulwark: require('../../../web/src/assets/bot-looks/bulwark/sprite.svg'),
  'glass-manta': require('../../../web/src/assets/bot-looks/glass-manta/sprite.svg'),
  'helio-kite': require('../../../web/src/assets/bot-looks/helio-kite/sprite.svg'),
  lancer: require('../../../web/src/assets/bot-looks/lancer/sprite.svg'),
  mantis: require('../../../web/src/assets/bot-looks/mantis/sprite.svg'),
  mossback: require('../../../web/src/assets/bot-looks/mossback/sprite.svg'),
  needle: require('../../../web/src/assets/bot-looks/needle/sprite.svg'),
  orbiter: require('../../../web/src/assets/bot-looks/orbiter/sprite.svg'),
  'rift-runner': require('../../../web/src/assets/bot-looks/rift-runner/sprite.svg'),
  'scrap-jackal': require('../../../web/src/assets/bot-looks/scrap-jackal/sprite.svg'),
  vanguard: require('../../../web/src/assets/bot-looks/vanguard/sprite.svg'),
} as const satisfies Record<string, ImageSourcePropType>;

export type BotLookId = keyof typeof LOOKS;

/** Same default the site falls back to (arenaThemes.ts `defaultLookId`). */
export const DEFAULT_LOOK_ID: BotLookId = 'vanguard';

export const BOT_LOOK_IDS = Object.keys(LOOKS) as BotLookId[];

/**
 * A bot's sprite, falling back to the default for an unknown or absent look — replays
 * predating bot-owned looks carry no lookId, and the site resolves them the same way.
 */
export function botLookSprite(lookId: string | null | undefined): ImageSourcePropType {
  if (lookId && lookId in LOOKS) return LOOKS[lookId as BotLookId];
  return LOOKS[DEFAULT_LOOK_ID];
}
