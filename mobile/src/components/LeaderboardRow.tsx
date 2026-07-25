import { StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { Card } from '@/components/ui/Card';
import type { LeaderboardEntry } from '@/api/client';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * A row of the standings. Rank leads, because that is what this screen is *for* — the
 * Bots tab is the roster and deliberately does not show rank; when both did, the two tabs
 * were the same screen.
 *
 * `rank` is competition rank from the server: ties share a number and the next rank skips.
 * Never substitute the array index — that silently renumbers tied bots, and stays wrong
 * once filtering is added, where #7 must still read as #7.
 */
export function LeaderboardRow({
  entry,
  onPress,
}: {
  entry: LeaderboardEntry;
  onPress?: () => void;
}) {
  const podium = entry.rank <= 3;

  return (
    <Card onPress={onPress} accessibilityLabel={`Rank ${entry.rank}, ${entry.name}`}>
      <View style={styles.row}>
        <Text style={[styles.rank, podium && styles.podium]}>{entry.rank}</Text>
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
          <Text style={styles.sets}>
            {entry.rankedSets} {entry.rankedSets === 1 ? 'set' : 'sets'}
          </Text>
        </View>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  rank: { ...Mono, color: Arena.dim, fontSize: 22, fontWeight: '800', minWidth: 34 },
  podium: { color: Arena.accent },
  identity: { flex: 1, gap: 1 },
  name: { color: Arena.text, fontSize: 15, fontWeight: '600' },
  owner: { color: Arena.dim, fontSize: 12 },
  stats: { alignItems: 'flex-end' },
  rating: { ...Mono, color: Arena.text, fontSize: 16, fontWeight: '700' },
  sets: { ...Mono, color: Arena.dim, fontSize: 11 },
});
