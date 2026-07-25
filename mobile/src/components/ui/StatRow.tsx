import { StyleSheet, Text, View } from 'react-native';

import { Arena, Mono } from '@/theme/arena';

export type Stat = { label: string; value: string | number };

/**
 * A row of headline numbers — rating/rank/sets, wins/losses/draws — spread across the
 * full width of its card.
 *
 * Equal-width columns rather than fixed gaps between left-packed cells. Gaps chosen to
 * look right beside "1387" leave a card half empty once the numbers are "2" and "10",
 * and the block then reads as misaligned against the name above it. Columns divide the
 * width instead, so the row spans the card whatever the digits are.
 *
 * Values are pre-formatted by the caller: this decides layout, not rounding.
 */
export function StatRow({ stats }: { stats: readonly Stat[] }) {
  return (
    <View style={styles.row}>
      {stats.map((stat) => (
        <View key={stat.label} style={styles.stat}>
          <Text style={styles.value} numberOfLines={1}>
            {stat.value}
          </Text>
          <Text style={styles.label} numberOfLines={1}>
            {stat.label}
          </Text>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row' },
  stat: { flex: 1, minWidth: 0 },
  value: { ...Mono, color: Arena.text, fontSize: 20, fontWeight: '800' },
  label: { color: Arena.dim, fontSize: 11, letterSpacing: 1, textTransform: 'uppercase' },
});
