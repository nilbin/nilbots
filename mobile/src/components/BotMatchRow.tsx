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
 * `outcome` is null while `broadcasting` — outcomes are withheld until a match has
 * finished broadcasting, so never fall back to the status for a result. Show that it is
 * live instead; printing "unknown" here would leak that the match is over.
 */
export function BotMatchRow({ row, onPress }: { row: MatchRow; onPress?: () => void }) {
  const live = row.broadcasting;
  const label = live ? 'LIVE' : (row.outcome ?? row.status.toLowerCase());

  return (
    <Card onPress={onPress} accessibilityLabel={`${label} versus ${row.opponent?.nameSnapshot ?? 'unknown'}`}>
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
          {row.opponent?.nameSnapshot ?? 'a removed bot'}
        </Text>
        <Text style={styles.meta} numberOfLines={1}>
          {row.setGame ? `set g${row.setGame}` : row.mapId}
        </Text>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.sm },
  outcome: { ...Mono, color: Arena.dim, fontSize: 11, width: 42 },
  live: { color: Arena.live, fontWeight: '700' },
  win: { color: Arena.ok, fontWeight: '700' },
  loss: { color: Arena.live },
  vs: { color: Arena.dim, fontSize: 12 },
  opponent: { color: Arena.text, fontSize: 14, flex: 1 },
  meta: { ...Mono, color: Arena.dim, fontSize: 11 },
});
