import { StyleSheet, Text, View } from 'react-native';

import { BotSprite, type BotSpriteSize } from '@/components/BotSprite';
import { Arena } from '@/theme/arena';

const NAME_SIZE: Record<BotSpriteSize, number> = { xs: 13, sm: 14, md: 16, lg: 24 };
const GAP: Record<BotSpriteSize, number> = { xs: 6, sm: 8, md: 10, lg: 12 };

/**
 * A bot's public identity: chassis, accent, name — the same three things the site shows,
 * so a bot is recognisable across surfaces.
 *
 * Match-history callers pass snapshot values so an old fight keeps the look it was fought
 * with. Name and accent are nullable because a deleted bot with no participant snapshot
 * resolves to null for both (MatchPublicProjection.ToSetBot).
 *
 * Needs a second line — an owner, a record — under the name? Compose `BotSprite` with
 * your own text stack instead; nesting this inside a column puts the sprite above that
 * text rather than beside it.
 */
export function BotIdentity({
  name,
  accent,
  lookId,
  size = 'sm',
  emphasized = false,
}: {
  name?: string | null;
  accent?: string | null;
  lookId?: string | null;
  size?: BotSpriteSize;
  emphasized?: boolean;
}) {
  return (
    <View style={[styles.row, { gap: GAP[size] }]}>
      <BotSprite lookId={lookId} accent={accent} size={size} />
      <Text
        style={[styles.name, { fontSize: NAME_SIZE[size] }, emphasized && styles.emphasized]}
        numberOfLines={1}>
        {name ?? 'a removed bot'}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', flexShrink: 1, minWidth: 0 },
  name: { color: Arena.text, flexShrink: 1 },
  emphasized: { fontWeight: '700' },
});
