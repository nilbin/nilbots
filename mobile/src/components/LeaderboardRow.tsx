import { StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { Card } from '@/components/ui/Card';
import type { LeaderboardEntry } from '@/api/client';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * `rank` is competition rank from the server, so ties share a number and the next rank
 * skips — do not substitute the array index, which would silently renumber tied bots.
 */
export function LeaderboardRow({
  entry,
  onPress,
}: {
  entry: LeaderboardEntry;
  onPress?: () => void;
}) {
  return (
    <Card onPress={onPress} accessibilityLabel={`${entry.name}, rank ${entry.rank}`}>
      <View style={styles.row}>
        <Text style={styles.rank}>{entry.rank}</Text>
        <BotSprite lookId={entry.lookId} accent={entry.accent} size="sm" />
        <View style={styles.identity}>
          <Text style={styles.name} numberOfLines={1}>
            {entry.name}
          </Text>
          <Text style={styles.owner} numberOfLines={1}>
            {entry.owner}
          </Text>
        </View>
        <View style={styles.stats}>
          <Text style={styles.rating}>{Math.round(entry.rating)}</Text>
          <Text style={styles.sets}>{entry.rankedSets} sets</Text>
        </View>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  rank: { ...Mono, color: Arena.dim, fontSize: 13, minWidth: 28 },
  identity: { flex: 1, gap: 1 },
  name: { color: Arena.text, fontSize: 14, fontWeight: '600' },
  owner: { color: Arena.dim, fontSize: 12 },
  stats: { alignItems: 'flex-end' },
  rating: { ...Mono, color: Arena.text, fontSize: 16, fontWeight: '700' },
  sets: { color: Arena.dim, fontSize: 11 },
});
