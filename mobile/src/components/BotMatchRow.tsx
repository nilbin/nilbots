import { StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { Card } from '@/components/ui/Card';
import type { components } from '@/api/schema';
import { Arena, Mono, Space } from '@/theme/arena';

type MatchRow = components['schemas']['BotMatchHistoryRowResponse'];

/**
 * One game from a bot's history.
 *
 * `matchSetId` is the ranked/unranked discriminator: a row belonging to a set is one
 * game of a ranked best-of-six, and the set is the meaningful unit to open. A row
 * without one is a standalone unranked match.
 *
 * `setGame` qualifies the map, it does not replace it. A ranked set plays three map and
 * seed pairs, so "g4" alone hides which arena the game was fought on — the one thing on
 * the row that explains the result.
 *
 * `outcome` is null while `broadcasting` — outcomes are withheld until a match has
 * finished broadcasting, so never fall back to the status for a result. Show that it is
 * live instead; printing "unknown" here would leak that the match is over.
 */
export function BotMatchRow({ row, onPress }: { row: MatchRow; onPress?: () => void }) {
  const live = row.broadcasting;
  const label = live ? 'LIVE' : (row.outcome ?? row.status.toLowerCase());
  const opponent = row.opponent?.nameSnapshot ?? 'a removed bot';

  return (
    <Card
      onPress={onPress}
      accessibilityLabel={`${label} versus ${opponent} on ${row.mapId}${
        row.setGame ? `, game ${row.setGame} of a ranked set` : ''
      }`}>
      <View style={styles.row}>
        <Text
          style={[
            styles.outcome,
            live && styles.live,
            row.outcome === 'Win' && styles.win,
            row.outcome === 'Loss' && styles.loss,
          ]}>
          {label}
        </Text>
        <Text style={styles.vs}>vs</Text>
        <BotSprite
          lookId={row.opponent?.lookIdSnapshot}
          accent={row.opponent?.accentSnapshot}
          size="xs"
        />
        <Text style={styles.opponent} numberOfLines={1}>
          {opponent}
        </Text>
        <Text style={styles.meta} numberOfLines={1}>
          {row.setGame ? `g${row.setGame} · ${row.mapId}` : row.mapId}
        </Text>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.sm },
  // minWidth, not width: the outcomes that actually repeat down a list — Win, Loss,
  // Draw, LIVE — are all three or four monospace characters, so a floor is enough to
  // keep the sprites in a column. A fixed width has to cover the rare "running", which
  // then strands ~16px of dead space beside every real result.
  outcome: { ...Mono, color: Arena.dim, fontSize: 11, minWidth: 30 },
  live: { color: Arena.live, fontWeight: '700' },
  win: { color: Arena.ok, fontWeight: '700' },
  loss: { color: Arena.live },
  vs: { color: Arena.dim, fontSize: 12 },
  opponent: { color: Arena.text, fontSize: 14, flex: 1 },
  // The opponent's name gives way first; a truncated map id is a smaller loss than a
  // truncated name.
  meta: { ...Mono, color: Arena.dim, fontSize: 11, flexShrink: 0 },
});
