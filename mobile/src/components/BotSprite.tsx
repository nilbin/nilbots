import { Image } from 'expo-image';
import { StyleSheet, View } from 'react-native';

import { botLookSprite } from '@/theme/botLooks';
import { Arena, Radius } from '@/theme/arena';

export type BotSpriteSize = 'xs' | 'sm' | 'md' | 'lg';

/** Mirrors the site's bot-identity frame scale. */
const SIZES: Record<BotSpriteSize, { frame: number; image: number; radius: number }> = {
  xs: { frame: 24, image: 20, radius: Radius.sm },
  sm: { frame: 32, image: 28, radius: Radius.sm },
  md: { frame: 40, image: 36, radius: Radius.md },
  lg: { frame: 48, image: 44, radius: Radius.md },
};

/**
 * A bot's chassis in its accent-edged frame, on its own so rows can put whatever they
 * like beside it — a name, a name over an owner, a name and a standing. A wrapper that
 * bundled the sprite with a name existed and was deleted: every real caller needed a
 * different text stack beside it, and nesting a fixed pairing inside a column stacks the
 * sprite above that text instead of beside it.
 *
 * The sprite always resolves (unknown looks fall back to the default chassis); the accent
 * may be null for a deleted bot, in which case the frame keeps its neutral edge.
 */
export function BotSprite({
  lookId,
  accent,
  size = 'sm',
}: {
  lookId?: string | null;
  accent?: string | null;
  size?: BotSpriteSize;
}) {
  const s = SIZES[size];
  return (
    <View
      style={[
        styles.frame,
        {
          width: s.frame,
          height: s.frame,
          borderRadius: s.radius,
          borderBottomColor: accent ?? Arena.edge,
        },
      ]}>
      <Image
        source={botLookSprite(lookId)}
        style={{ width: s.image, height: s.image }}
        contentFit="contain"
        transition={120}
        accessibilityIgnoresInvertColors
      />
    </View>
  );
}

const styles = StyleSheet.create({
  frame: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Arena.panel,
    borderWidth: 1,
    borderColor: Arena.edge,
    // The site draws the accent as an inset bottom edge, so the chassis reads as the
    // subject and the accent as a label on it.
    borderBottomWidth: 2,
    flexShrink: 0,
  },
});
